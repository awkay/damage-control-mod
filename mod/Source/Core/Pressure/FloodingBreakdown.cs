using DamageControl.Core.Common;

namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P12b (01 §2.12 items 1 to 4): numbers for the lines the mod appends to the vanilla buoyancy
	/// tooltip: water in the ballast tanks (total and per tank, with % of capacity) and flooding per
	/// compartment, highlighted when alarming. Tanks and compartments come from the boat by identity;
	/// this class only does the arithmetic.
	/// </summary>
	public static class FloodingBreakdown
	{
		/// <summary>Flooding (m3) from which a compartment's value is highlighted.</summary>
		public const double AlarmWater = 5.0;

		/// <summary>Lower threshold for the galley, a small compartment.</summary>
		public const double GalleyAlarmWater = 2.0;

		/// <summary>True when <paramref name="water"/> m3 is alarming for this compartment (&gt;= threshold, with an epsilon, D7).</summary>
		public static bool IsAlarming(double water, bool isGalley)
		{
			double threshold = isGalley ? GalleyAlarmWater : AlarmWater;
			return water >= threshold - DcMath.Epsilon;
		}

		/// <summary>100 x water / capacity; 0 when the capacity is not positive (no division by zero).</summary>
		public static float Percent(double water, double capacity)
		{
			if (!(capacity > 0.0) || double.IsNaN(water))
				return 0f;
			return (float)(100.0 * water / capacity);
		}
	}
}
