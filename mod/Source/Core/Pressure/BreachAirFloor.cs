namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P1 (01 §2.1): a breached compartment always keeps a small amount of air, so the vanilla breach
	/// simulation never runs a compartment down to zero air (which lets water fill the last of the
	/// volume and makes the pressure spike).
	/// </summary>
	public static class BreachAirFloor
	{
		/// <summary>The air floor, in the game's tank air units.</summary>
		public const double Floor = 0.1;

		/// <summary>
		/// Air amount to use before the breach is simulated: <see cref="Floor"/> when the tank is a
		/// compartment, the breach is open (openness &gt; 0) and the air is below the floor; otherwise
		/// <paramref name="air"/> unchanged. Ballast, fuel, trim and air tanks are never touched.
		/// </summary>
		public static double Apply(bool isCompartment, float openness, double air)
		{
			if (!isCompartment || !(openness > 0f))
				return air;
			if (double.IsNaN(air) || air < Floor)
				return Floor;
			return air;
		}
	}
}
