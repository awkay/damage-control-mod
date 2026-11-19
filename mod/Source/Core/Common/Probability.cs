using System;

namespace DamageControl.Core.Common
{
	/// <summary>
	/// D7: probabilities are specified per second and converted to a per-step chance, so outcomes do
	/// not depend on frame rate or tick interval.
	/// </summary>
	public static class Probability
	{
		/// <summary>
		/// Chance that an event with per-second probability <paramref name="perSecond"/> happens at least
		/// once during <paramref name="dt"/> seconds: 1 - (1 - p)^dt. Inputs are clamped (p to [0, 1],
		/// dt to &gt;= 0), so the result is always in [0, 1].
		/// </summary>
		public static float PerStep(float perSecond, float dt)
		{
			float p = DcMath.Clamp01(perSecond);
			if (!(dt > 0f) || p <= 0f)
				return 0f;
			if (p >= 1f)
				return 1f;
			return DcMath.Clamp01((float)(1.0 - Math.Pow(1.0 - p, dt)));
		}

		/// <summary>Rolls <see cref="PerStep"/> against <paramref name="random"/>.</summary>
		public static bool Roll(IRandom random, float perSecond, float dt)
		{
			return random.Chance(PerStep(perSecond, dt));
		}
	}
}
