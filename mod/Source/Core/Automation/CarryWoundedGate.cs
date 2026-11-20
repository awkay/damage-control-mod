namespace DamageControl.Core.Automation
{
	/// <summary>How the "carry the wounded now" order shows on the light switch.</summary>
	public enum TriggerAvailability
	{
		Hidden,
		Disabled,
		Enabled
	}

	/// <summary>
	/// H13 (02 §2.10, 00 C27, D7): gate on vanilla's automatic carrying of the wounded, plus the one-shot
	/// "carry the wounded now" order.
	///
	/// The vanilla routine runs when automatic carrying is on, or while the one-shot is armed (and no save
	/// is loading). It always runs while a diver waits to be pulled back aboard, because the same vanilla
	/// routine recovers divers (C27). The one-shot disarms on the first run that finds nobody left lying
	/// injured. It is session-only and re-armed at every campaign session start (02 §7.12 fixed, D7), so a
	/// freshly loaded boat gets its wounded seen to once even with automatic carrying off.
	/// Main thread only.
	/// </summary>
	public sealed class CarryWoundedGate
	{
		/// <summary>Label, description and wheel icon name of the trigger order.</summary>
		public const string TriggerKeyword = "DC Carry Wounded Now";

		/// <summary>The gate the game uses.</summary>
		public static readonly CarryWoundedGate Shared = new CarryWoundedGate();

		private bool oneShot = true;

		/// <summary>True while the one-shot is armed.</summary>
		public bool OneShotArmed
		{
			get { return oneShot; }
		}

		/// <summary>Arms the one-shot: the trigger order, and every campaign session start.</summary>
		public void Arm()
		{
			oneShot = true;
		}

		/// <summary>
		/// One tick of the vanilla routine is about to run. Returns true when vanilla should run.
		/// When it runs with nobody lying injured, the one-shot disarms (that tick still runs).
		/// </summary>
		public bool BeforeTick(bool automaticCarrying, bool loading, int diversWaiting, int injuredLying)
		{
			bool run = diversWaiting > 0 || automaticCarrying || (oneShot && !loading);
			if (run && injuredLying <= 0)
				oneShot = false;
			return run;
		}

		/// <summary>
		/// 02 §6 #19 with D3: hidden outside a campaign and while automatic carrying is on (the order would
		/// do nothing); otherwise enabled iff at least one crewman lies injured.
		/// </summary>
		public static TriggerAvailability Availability(bool campaignActive, bool automaticCarrying, int injuredLying)
		{
			if (!campaignActive || automaticCarrying)
				return TriggerAvailability.Hidden;
			return injuredLying >= 1 ? TriggerAvailability.Enabled : TriggerAvailability.Disabled;
		}
	}
}
