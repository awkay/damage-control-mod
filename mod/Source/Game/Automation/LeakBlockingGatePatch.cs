using System;
using DamageControl.Core.Automation;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Tasks;

namespace DamageControl.Game.Automation
{
	/// <summary>
	/// H12 (02 §2.10, §4): vanilla's per-frame "sailors block leaks on their own" routine runs only while
	/// the automatic leak blocking setting is on (default on, D3). Blockers already at work keep working
	/// when it is switched off; only new self-dispatching stops.
	/// </summary>
	[HarmonyPatch(typeof(LeakBlockingBySailors), "DoUpdate")]
	internal static class LeakBlockingGatePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("LeakBlockingGate", ex);
		}

		private static bool Prefix()
		{
			try
			{
				return AutomationGates.LeakBlockingRuns(DcSettings.AutoLeakBlocking);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("LeakBlockingGate", e);
				return true;
			}
		}
	}
}
