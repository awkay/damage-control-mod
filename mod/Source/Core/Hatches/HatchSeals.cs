using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DamageControl.Core.Hatches
{
	/// <summary>What is holding a hatch seal (00 C9).</summary>
	public enum SealSource
	{
		/// <summary>A crewman blocking the hatch leak while interacting (H8, WS3).</summary>
		Blocker,

		/// <summary>A crewman repairing the hatch while interacting (R2, WS5).</summary>
		Repair
	}

	/// <summary>
	/// Registry of active seals on hatches (00 §2.5, C9, C28; owner of the hatch half of the leak-blocking
	/// mechanism). It replaces the original's "modifier on the hatch Performance stat": no game stat is
	/// touched. Each source (an owner object, normally the action instance) seals a hatch at most once;
	/// every source cuts a closed, leaking hatch's flow to <see cref="FactorPerSource"/> of what it was,
	/// so the closed-hatch leak is 0.02 x 0.1^n of the open rate.
	///
	/// Keys are compared by reference, so Unity objects and plain test objects both work. State is
	/// session-only: the Game layer clears <see cref="Shared"/> at every campaign session start, and
	/// owners re-register (Add is idempotent) while they interact. Main thread only.
	/// </summary>
	public sealed class HatchSeals
	{
		/// <summary>Factor applied once per active source (presence, not value, matters; C9).</summary>
		public const float FactorPerSource = 0.1f;

		/// <summary>The registry the game uses. WS3 (blocker) and WS5 (repair) add and remove their sources here.</summary>
		public static readonly HatchSeals Shared = new HatchSeals();

		private readonly Dictionary<object, List<Entry>> byHatch = new Dictionary<object, List<Entry>>(ReferenceComparer.Instance);

		private struct Entry
		{
			public readonly object Owner;
			public readonly SealSource Source;

			public Entry(object owner, SealSource source)
			{
				Owner = owner;
				Source = source;
			}
		}

		/// <summary>
		/// Registers <paramref name="owner"/> as sealing <paramref name="hatch"/>. Idempotent: calling it
		/// again for the same pair (even with another <paramref name="source"/>) keeps one entry and updates
		/// its source. Returns true when the entry is new. Null arguments are ignored.
		/// </summary>
		public bool Add(object hatch, SealSource source, object owner)
		{
			if (hatch == null || owner == null)
				return false;
			List<Entry> list;
			if (!byHatch.TryGetValue(hatch, out list))
			{
				list = new List<Entry>(2);
				byHatch[hatch] = list;
			}
			for (int i = 0; i < list.Count; i++)
			{
				if (ReferenceEquals(list[i].Owner, owner))
				{
					list[i] = new Entry(owner, source);
					return false;
				}
			}
			list.Add(new Entry(owner, source));
			return true;
		}

		/// <summary>Removes <paramref name="owner"/>'s seal on <paramref name="hatch"/>. Returns true if one was removed.</summary>
		public bool Remove(object hatch, object owner)
		{
			List<Entry> list;
			if (hatch == null || owner == null || !byHatch.TryGetValue(hatch, out list))
				return false;
			for (int i = list.Count - 1; i >= 0; i--)
			{
				if (ReferenceEquals(list[i].Owner, owner))
				{
					list.RemoveAt(i);
					if (list.Count == 0)
						byHatch.Remove(hatch);
					return true;
				}
			}
			return false;
		}

		/// <summary>Removes every seal <paramref name="owner"/> holds on any hatch (for cleanup paths that lost the hatch). Returns the count.</summary>
		public int RemoveOwner(object owner)
		{
			if (owner == null)
				return 0;
			int removed = 0;
			var empty = new List<object>();
			foreach (KeyValuePair<object, List<Entry>> pair in byHatch)
			{
				removed += pair.Value.RemoveAll(e => ReferenceEquals(e.Owner, owner));
				if (pair.Value.Count == 0)
					empty.Add(pair.Key);
			}
			foreach (object hatch in empty)
				byHatch.Remove(hatch);
			return removed;
		}

		/// <summary>Removes all seals on <paramref name="hatch"/> (e.g. the hatch was destroyed).</summary>
		public void ClearHatch(object hatch)
		{
			if (hatch != null)
				byHatch.Remove(hatch);
		}

		/// <summary>Forgets everything (campaign session start).</summary>
		public void Clear()
		{
			byHatch.Clear();
		}

		/// <summary>Number of active sources on <paramref name="hatch"/>.</summary>
		public int Count(object hatch)
		{
			List<Entry> list;
			return hatch != null && byHatch.TryGetValue(hatch, out list) ? list.Count : 0;
		}

		/// <summary>Number of active sources of one kind on <paramref name="hatch"/>.</summary>
		public int Count(object hatch, SealSource source)
		{
			List<Entry> list;
			if (hatch == null || !byHatch.TryGetValue(hatch, out list))
				return 0;
			int n = 0;
			for (int i = 0; i < list.Count; i++)
			{
				if (list[i].Source == source)
					n++;
			}
			return n;
		}

		/// <summary>True when at least one source seals <paramref name="hatch"/> (drives the panel's "partially sealed" line, shown once).</summary>
		public bool IsSealed(object hatch)
		{
			return Count(hatch) > 0;
		}

		/// <summary>True when <paramref name="owner"/> currently seals <paramref name="hatch"/>.</summary>
		public bool Has(object hatch, object owner)
		{
			List<Entry> list;
			if (hatch == null || owner == null || !byHatch.TryGetValue(hatch, out list))
				return false;
			for (int i = 0; i < list.Count; i++)
			{
				if (ReferenceEquals(list[i].Owner, owner))
					return true;
			}
			return false;
		}

		/// <summary>Product of the per-source factors: 1 unsealed, 0.1 with one source, 0.01 with two, ...</summary>
		public float SealFactor(object hatch)
		{
			return FactorFor(Count(hatch));
		}

		/// <summary>Fraction of the open-hatch rate that a closed, leaking hatch passes: 0.02 x <see cref="SealFactor"/>.</summary>
		public float ClosedLeakMultiplier(object hatch)
		{
			return HatchExchange.ClosedLeakFactor * SealFactor(hatch);
		}

		/// <summary>0.1^n.</summary>
		public static float FactorFor(int sources)
		{
			float factor = 1f;
			for (int i = 0; i < sources; i++)
				factor *= FactorPerSource;
			return factor;
		}

		private sealed class ReferenceComparer : IEqualityComparer<object>
		{
			public static readonly ReferenceComparer Instance = new ReferenceComparer();

			public new bool Equals(object x, object y)
			{
				return ReferenceEquals(x, y);
			}

			public int GetHashCode(object obj)
			{
				return RuntimeHelpers.GetHashCode(obj);
			}
		}
	}
}
