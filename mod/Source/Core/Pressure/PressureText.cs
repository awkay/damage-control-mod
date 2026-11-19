namespace DamageControl.Core.Pressure
{
	/// <summary>Locale keys of the pressure area's player-facing text. Every key has an English row in data/pressure.</summary>
	public static class PressureText
	{
		/// <summary>Tooltip line: total ballast water and % of total ballast capacity. Args: m3, %.</summary>
		public const string BallastTotal = "Tooltips/DC Ballast Total";

		/// <summary>Tooltip line per ballast tank. Args: tank name, m3, %.</summary>
		public const string BallastTank = "Tooltips/DC Ballast Tank";

		/// <summary>Tooltip line: total compartment flooding. Args: m3.</summary>
		public const string FloodingTotal = "Tooltips/DC Flooding Total";

		/// <summary>Tooltip line per compartment. Args: compartment name, m3.</summary>
		public const string FloodingCompartment = "Tooltips/DC Flooding Compartment";

		/// <summary>Same, highlighted as alarming. Args: compartment name, m3.</summary>
		public const string FloodingCompartmentAlarm = "Tooltips/DC Flooding Compartment Alarm";

		/// <summary>Bilge state line (P13): the compartment's air pressure. Args: atm.</summary>
		public const string CompartmentAirPressure = "Activators/DC Compartment Air Pressure";

		public static readonly string[] All =
		{
			BallastTotal, BallastTank, FloodingTotal, FloodingCompartment, FloodingCompartmentAlarm, CompartmentAirPressure,
		};
	}
}
