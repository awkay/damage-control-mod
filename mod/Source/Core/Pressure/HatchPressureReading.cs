using System;

namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P4 (01 §2.4, 00 §4): the pressure a sensing point reports. A point on an interior hatch measures
	/// the air-pressure difference across the hatch; every other point keeps the game's own reading.
	/// </summary>
	public static class HatchPressureReading
	{
		/// <summary>
		/// |pLeft - pRight| when the point's target is an interior hatch with both compartments known,
		/// otherwise <paramref name="vanillaReading"/>. A non-finite pressure keeps the vanilla reading.
		/// </summary>
		public static float Reported(bool isInteriorHatchWithBothSides, double pLeft, double pRight, float vanillaReading)
		{
			if (!isInteriorHatchWithBothSides)
				return vanillaReading;
			double difference = Math.Abs(pLeft - pRight);
			if (double.IsNaN(difference) || double.IsInfinity(difference))
				return vanillaReading;
			return (float)difference;
		}
	}
}
