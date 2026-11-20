namespace DamageControl.Core.Automation
{
	/// <summary>
	/// H12 and H14: the two small automation rules the mod puts on vanilla 2026.1 background tasks.
	/// </summary>
	public static class AutomationGates
	{
		/// <summary>H12 (02 §2.10, §4): vanilla's sailors-block-leaks routine runs only while the setting is on.</summary>
		public static bool LeakBlockingRuns(bool automaticLeakBlocking)
		{
			return automaticLeakBlocking;
		}

		/// <summary>
		/// H14 (02 §2.11, 00 C7): the trim pump is never switched off automatically while any equipment on
		/// the boat leaks. Vanilla's controller switches the pump off once its "turn-off" condition has held
		/// for a sustained delay, tracked as a deadline in game ticks (0 = not running). While a leak exists
		/// the deadline is held at 0, so the delay restarts from scratch once the last leak is gone.
		/// Returns the deadline to store.
		/// </summary>
		public static long PumpTurnOffDeadline(long deadline, bool anyLeak)
		{
			return anyLeak ? 0L : deadline;
		}
	}
}
