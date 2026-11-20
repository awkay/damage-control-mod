using System;
using DamageControl.Core.Common;

namespace DamageControl.Core.Repair
{
	/// <summary>
	/// R5 (04 §2.2, 00 §4): engineers turn <see cref="DefaultScrapPerPart"/> scrap into one replacement part
	/// instead of vanilla's 2. The game keeps its own salvage action; a transpiler swaps each of its
	/// <c>2f</c> scrap constants for a call to the effective ratio. The swap is all-or-nothing: unless every
	/// patched method had exactly the expected number of constants, the effective ratio stays vanilla's, so
	/// a changed IL shape can never leave the threshold and the consumed amount disagreeing.
	/// </summary>
	public static class SalvageRatio
	{
		/// <summary>The mod's default ratio: the game's own 2 (maintainer decision 2026-11-20); tunable via a data row.</summary>
		public const float DefaultScrapPerPart = 2f;

		/// <summary>The 2026.1 vanilla ratio, which is also the constant the transpiler looks for.</summary>
		public const float VanillaScrapPerPart = 2f;

		/// <summary>Seconds of work per part at 1x (0.01 skill progress per second, 04 §2.2; vanilla, unchanged).</summary>
		public const float SecondsPerPart = 100f;

		/// <summary>A configured ratio: finite and at least 1, else the default.</summary>
		public static float Sanitize(float scrapPerPart)
		{
			if (!DcMath.IsFinite(scrapPerPart) || scrapPerPart < 1f)
				return DefaultScrapPerPart;
			return scrapPerPart;
		}

		/// <summary>The ratio the patched game code uses: the configured one only when the whole swap applied.</summary>
		public static float Effective(bool swapApplied, float configured)
		{
			return swapApplied ? Sanitize(configured) : VanillaScrapPerPart;
		}

		/// <summary>The salvage order is offered (not disabled for scrap) with at least one ratio's worth in the workshop.</summary>
		public static bool HasEnoughScrap(float scrapInWorkshop, float scrapPerPart)
		{
			return scrapInWorkshop >= scrapPerPart;
		}

		/// <summary>
		/// One completed 100 s cycle as the patched vanilla loop runs it: needs room for a part, consumes one
		/// ratio of scrap, makes one part, and continues only while at least one more ratio remains.
		/// Returns true when the order goes on.
		/// </summary>
		public static bool Cycle(ref float scrap, ref float parts, bool roomForPart, float scrapPerPart)
		{
			if (!roomForPart || scrap < scrapPerPart)
				return false;
			scrap -= scrapPerPart;
			parts += 1f;
			return scrap >= scrapPerPart;
		}
	}

	/// <summary>
	/// Bookkeeping for the R5 transpilers: which game methods were rewritten, and whether the swap as a
	/// whole applied. Pure, so the fail-closed rule is unit-tested.
	/// </summary>
	public sealed class SalvagePatchState
	{
		/// <summary>Method name -> number of <c>2f</c> scrap constants expected in the 2026.1 IL (00 §4).</summary>
		public static readonly string[] Methods = { "GetState", "GetActionProfit", "UpdateAction" };

		private static readonly int[] Expected = { 1, 1, 2 };

		private readonly bool[] applied = new bool[Methods.Length];

		/// <summary>The expected constant count for a method, or -1 for a method that is not ours to patch.</summary>
		public static int ExpectedMatches(string method)
		{
			int i = Array.IndexOf(Methods, method);
			return i < 0 ? -1 : Expected[i];
		}

		/// <summary>
		/// Records what a transpiler found. Returns true when the method should be rewritten (the count is
		/// exactly the expected one); otherwise it must be left as it is.
		/// </summary>
		public bool Record(string method, int found)
		{
			int i = Array.IndexOf(Methods, method);
			if (i < 0)
				return false;
			bool ok = found == Expected[i];
			applied[i] = ok;
			return ok;
		}

		/// <summary>True when every method was rewritten.</summary>
		public bool AllApplied
		{
			get
			{
				for (int i = 0; i < applied.Length; i++)
				{
					if (!applied[i])
						return false;
				}
				return true;
			}
		}
	}
}
