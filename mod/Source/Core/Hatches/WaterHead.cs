using System;

namespace DamageControl.Core.Hatches
{
	/// <summary>
	/// The one water-head unit of the mod (00 §2.5, decision D1): metres of water above a reference
	/// height convert to atmospheres at a physical 0.1 atm per metre, which matches the game's own
	/// FluidsPressure. Every hatch rule, the hatch panel and the equalize action go through here, so the
	/// logic and what the player reads always agree.
	/// </summary>
	public static class WaterHead
	{
		/// <summary>D1 (b): 1 m of water = 0.1 atm.</summary>
		public const float AtmPerMetreOfWater = 0.1f;

		/// <summary>Atmospheres of head for <paramref name="metres"/> of water; negative or NaN depth counts as none.</summary>
		public static float Atm(float metres)
		{
			return metres > 0f ? metres * AtmPerMetreOfWater : 0f;
		}

		/// <summary>
		/// Pressure acting on one side of an opening whose lowest point is at <paramref name="referenceY"/>:
		/// the side's air pressure plus the head of water standing above that point.
		/// </summary>
		public static double SidePressure(double airPressure, float waterElevation, float referenceY)
		{
			return airPressure + Atm(waterElevation - referenceY);
		}
	}
}
