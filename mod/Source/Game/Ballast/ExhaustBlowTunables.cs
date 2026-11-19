using DamageControl.Core.Ballast;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;

namespace DamageControl.Game.Ballast
{
	/// <summary>
	/// Exhaust Blow numbers from our General sheet (rows under <see cref="Tunables.Root"/>), read once per
	/// campaign session. A missing row keeps the spec value from <see cref="ExhaustBlowSettings.Default"/>.
	/// </summary>
	public static class ExhaustBlowTunables
	{
		public const string FlowPerRevolution = Tunables.Root + "Exhaust Blow Flow Per Revolution";
		public const string Stage2WaterThreshold = Tunables.Root + "Exhaust Blow Stage 2 Water Threshold";
		public const string CompletionWater = Tunables.Root + "Exhaust Blow Completion Water";
		public const string MinimumRpm = Tunables.Root + "Exhaust Blow Minimum Diesel RPM";
		public const string DeckClearance = Tunables.Root + "Exhaust Blow Deck Clearance";
		public const string CancelGraceSeconds = Tunables.Root + "Exhaust Blow Cancel Grace Seconds";

		private static bool loaded;
		private static ExhaustBlowSettings current;

		static ExhaustBlowTunables()
		{
			DcSettings.SessionStarted += () => loaded = false;
		}

		/// <summary>The settings for this session (main thread).</summary>
		public static ExhaustBlowSettings Current
		{
			get
			{
				if (!loaded)
				{
					current = Read();
					loaded = true;
				}
				return current;
			}
		}

		private static ExhaustBlowSettings Read()
		{
			ExhaustBlowSettings s = ExhaustBlowSettings.Default;
			s.FlowPerRevolution = Tunables.GetFloat(FlowPerRevolution, s.FlowPerRevolution);
			s.Stage2WaterThreshold = Tunables.GetFloat(Stage2WaterThreshold, s.Stage2WaterThreshold);
			s.CompletionWater = Tunables.GetFloat(CompletionWater, (float)s.CompletionWater);
			s.MinimumRpm = Tunables.GetFloat(MinimumRpm, s.MinimumRpm);
			s.DeckClearance = Tunables.GetFloat(DeckClearance, s.DeckClearance);
			s.CancelGraceSeconds = Tunables.GetFloat(CancelGraceSeconds, s.CancelGraceSeconds);
			return s;
		}
	}
}
