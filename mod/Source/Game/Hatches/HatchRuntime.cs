using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// Shared WS2 game-side state and readers: tunables, the session reset of <see cref="HatchSeals.Shared"/>,
	/// the lock-guard bypass, and conversion of game compartments into Core <see cref="HatchSide"/>s.
	/// Main thread only.
	/// </summary>
	public static class HatchRuntime
	{
		private static bool initialized;
		private static bool tunablesLoaded;
		private static float lockThreshold = HatchLockRule.DefaultThreshold;
		private static float flowRate = HatchExchange.DefaultRate;
		private static float blowOpenChance = HatchDamageRules.DefaultBlowOpenChancePerSecond;
		private static int bypassDepth;

		/// <summary>Lock threshold in atm (data row "Hatch Lock Threshold", default 0.9; D1).</summary>
		public static float LockThreshold
		{
			get
			{
				EnsureInitialized();
				return lockThreshold;
			}
		}

		/// <summary>Base exchange rate k (data row "Hatch Flow Rate", default 2).</summary>
		public static float FlowRate
		{
			get
			{
				EnsureInitialized();
				return flowRate;
			}
		}

		/// <summary>Per-second blow-open hazard of a destroyed hatch (data row "Hatch Blow Open Chance", default 0.75).</summary>
		public static float BlowOpenChance
		{
			get
			{
				EnsureInitialized();
				return blowOpenChance;
			}
		}

		/// <summary>True while the mod itself opens or restores a hatch; the lock guard (H2) lets it through.</summary>
		public static bool GuardBypassed
		{
			get { return bypassDepth > 0; }
		}

		/// <summary>Sets <see cref="Hatch.Locked"/> past the H2 lock guard (blow-open, H3 restore).</summary>
		public static void SetLockedUnguarded(Hatch hatch, bool locked)
		{
			bypassDepth++;
			try
			{
				hatch.Locked = locked;
			}
			finally
			{
				bypassDepth--;
			}
		}

		/// <summary>Subscribes the session reset once. Called from every WS2 entry point; cheap after the first call.</summary>
		public static void EnsureInitialized()
		{
			if (!initialized)
			{
				initialized = true;
				DcSettings.SessionStarted += OnSessionStarted;
			}
			if (!tunablesLoaded)
				LoadTunables();
		}

		private static void OnSessionStarted()
		{
			HatchSeals.Shared.Clear();
			tunablesLoaded = false;
		}

		private static void LoadTunables()
		{
			tunablesLoaded = true;
			lockThreshold = HatchLockRule.SanitizeThreshold(Tunables.GetFloat(Tunables.Root + HatchText.TunableLockThreshold, HatchLockRule.DefaultThreshold));
			float rate = Tunables.GetFloat(Tunables.Root + HatchText.TunableFlowRate, HatchExchange.DefaultRate);
			flowRate = rate > 0f && DcMath.IsFinite(rate) ? rate : HatchExchange.DefaultRate;
			blowOpenChance = DcMath.Clamp01(Tunables.GetFloat(Tunables.Root + HatchText.TunableBlowOpenChance, HatchDamageRules.DefaultBlowOpenChancePerSecond));
			DcLog.Debug("hatch tunables: lock threshold " + lockThreshold + " atm, flow rate " + flowRate + ", blow-open chance " + blowOpenChance + "/s");
		}

		/// <summary>The compartment as Core sees it.</summary>
		public static HatchSide ReadSide(Compartment c)
		{
			return HatchSide.Create(c.AirPressure, c.Air, c.Water, c.Capacity, c.WaterElevation, c.FloorElevation);
		}

		/// <summary>The side opposite the pressure-blockade (seat) compartment, or null when the seat is neither side.</summary>
		public static Compartment FarSide(Hatch hatch, Compartment seat)
		{
			if (seat == null)
				return null;
			Compartment left = hatch.LeftCompartment;
			Compartment right = hatch.RightCompartment;
			if (left != null && ReferenceEquals(seat, left))
				return right;
			if (right != null && ReferenceEquals(seat, right))
				return left;
			return null;
		}

		/// <summary>True when the hatch carries a visible "is being repaired" status (H1 step 1, C10).</summary>
		public static bool IsRepairHeld(Hatch hatch)
		{
			IsBeingRepaired status = hatch.GetStatus<IsBeingRepaired>();
			return status != null && !status.Hidden;
		}

		/// <summary>A hatch with no compartment at all (doors, cargo hatch, exterior): the pressure rules leave it to vanilla.</summary>
		public static bool HasNoCompartments(Hatch hatch)
		{
			return hatch.LeftCompartment == null && hatch.RightCompartment == null && hatch.PressureBlockadeCompartment == null;
		}

		/// <summary>Moves a Core flow (positive left to right) between the two compartments.</summary>
		public static void Apply(Compartment left, Compartment right, HatchFlow flow)
		{
			if (flow.Air != 0.0)
			{
				left.Air -= flow.Air;
				right.Air += flow.Air;
			}
			if (flow.Water != 0.0)
			{
				left.Water -= flow.Water;
				right.Water += flow.Water;
			}
		}

		/// <summary>
		/// Both sides resolve to the same water body (merged decks): no exchange makes sense between them.
		/// </summary>
		public static bool SameWaterBody(Compartment left, Compartment right)
		{
			Compartment l = left.MergedCompartment != null ? left.MergedCompartment : left;
			Compartment r = right.MergedCompartment != null ? right.MergedCompartment : right;
			return ReferenceEquals(l, r);
		}
	}
}
