namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P2 (01 §2.2, 00 C18): when a compartment's air is unbreathable, and the toxicity value the game's
	/// choking status reads. Replaces vanilla's criteria entirely for unmerged compartments.
	/// </summary>
	public static class AirQuality
	{
		/// <summary>Below this ship-wide oxygen reserve (0..1) the air is unbreathable.</summary>
		public const double MinOxygen = 0.05;

		/// <summary>Below this compartment air pressure (atm) the air is unbreathable.</summary>
		public const double MinPressure = 0.15;

		/// <summary>Above this compartment air pressure (atm) the air is unbreathable.</summary>
		public const double MaxPressure = 8.5;

		/// <summary>Above this much water over the floor (m) the compartment is unbreathable.</summary>
		public const float MaxWaterAboveFloor = 1.5f;

		/// <summary>Toxicity while unbreathable. Choking injures when Random &gt; (0.85 or 0.7) - toxicity (C18).</summary>
		public const float ToxicityUnbreathable = -0.1f;

		/// <summary>Toxicity while breathable: the choking check never injures.</summary>
		public const float ToxicityBreathable = -1.0f;

		/// <summary>
		/// True when any of: oxygen &lt; 0.05, pressure &lt; 0.15 atm, pressure &gt; 8.5 atm, water above
		/// floor &gt; 1.5 m. Bounds are strict, so exactly 0.15 atm, 8.5 atm and 1.5 m are breathable.
		/// NaN inputs never make air unbreathable on their own.
		/// </summary>
		public static bool IsUnbreathable(double oxygen, double airPressure, float waterAboveFloor)
		{
			return oxygen < MinOxygen
				|| airPressure < MinPressure
				|| airPressure > MaxPressure
				|| waterAboveFloor > MaxWaterAboveFloor;
		}

		/// <summary>The value reported as the compartment's air toxicity.</summary>
		public static float Toxicity(bool unbreathable)
		{
			return unbreathable ? ToxicityUnbreathable : ToxicityBreathable;
		}
	}
}
