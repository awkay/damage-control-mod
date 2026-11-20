using System;
using DamageControl.Core.Common;

namespace DamageControl.Core.Hatches
{
	/// <summary>What to do with a hatch's Leaking status this frame.</summary>
	public enum LeakStatusChange
	{
		None,
		Add,
		Remove
	}

	/// <summary>H5: destroyed-hatch blow-open and leak-status upkeep (02 §2.3 c, d).</summary>
	public static class HatchDamageRules
	{
		/// <summary>
		/// D7: the original's 5 % per frame becomes a per-second hazard. 0.75/s is about what 5 % per frame
		/// gave at 30 fps. Tunable through the data row "Hatch Blow Open Chance".
		/// </summary>
		public const float DefaultBlowOpenChancePerSecond = 0.75f;

		/// <summary>Blow-open needs far-side air pressure above seat side + design pressure / this.</summary>
		public const float DesignPressureDivisor = 3f;

		/// <summary>
		/// 02 §2.3c conditions: destroyed (health 0), not being repaired, closed, not animating, and the far
		/// side's air pressure exceeds the seat side's by more than a third of the hatch's interior design
		/// pressure. Water head is not included.
		/// </summary>
		public static bool CanBlowOpen(float health, bool beingRepaired, bool locked, bool animating, double seatAirPressure, double farAirPressure, float designPressure)
		{
			if (health > 0f || beingRepaired || !locked || animating)
				return false;
			return farAirPressure > seatAirPressure + designPressure / DesignPressureDivisor;
		}

		/// <summary>Rolls the per-second blow-open hazard for <paramref name="dt"/> game seconds (D7).</summary>
		public static bool RollBlowOpen(IRandom random, float chancePerSecond, float dt)
		{
			return Probability.Roll(random, chancePerSecond, dt);
		}

		/// <summary>
		/// Chance the hatch blows open during this frame: the per-second hazard integrated over the frame's
		/// whole game time (<see cref="SimClock.GameDt"/>, uncapped), so the hazard per game second is the
		/// same at 1x and at 64x. Zero while paused.
		/// </summary>
		public static float BlowOpenChanceThisFrame(float chancePerSecond, SimClock clock)
		{
			return Probability.PerStep(chancePerSecond, clock.GameDt);
		}

		/// <summary>Rolls <see cref="BlowOpenChanceThisFrame"/>.</summary>
		public static bool RollBlowOpen(IRandom random, float chancePerSecond, SimClock clock)
		{
			return random.Chance(BlowOpenChanceThisFrame(chancePerSecond, clock));
		}

		/// <summary>
		/// 02 §2.3d: a destroyed, closed, idle hatch leaks; any health stops the leak. Adds only when no
		/// Leaking status is present, so exactly one is ever added.
		/// </summary>
		public static LeakStatusChange UpkeepLeak(float health, bool locked, bool animating, bool hasLeakingStatus)
		{
			if (health <= 0f)
				return locked && !animating && !hasLeakingStatus ? LeakStatusChange.Add : LeakStatusChange.None;
			return hasLeakingStatus ? LeakStatusChange.Remove : LeakStatusChange.None;
		}

		/// <summary>H3: a disabled hatch stays shut (and leaks) only when the seat side still pushes harder than the far side.</summary>
		public static bool StaysShutWhenDisabled(bool wasLocked, double seatAirPressure, double farAirPressure)
		{
			return wasLocked && seatAirPressure > farAirPressure;
		}
	}
}
