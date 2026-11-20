using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Tasks;

namespace DamageControl.Game.Automation
{
	/// <summary>
	/// H13 (02 §2.10, 00 C27): gate on vanilla's timed "carry the wounded to safety" routine. It is skipped
	/// (re-checked after the vanilla interval) when automatic carrying is off, the one-shot is not armed and
	/// no diver is waiting. The same vanilla routine pulls divers back aboard, so it always runs while a
	/// diver waits; that run may also send a carrier, which is accepted (C27).
	/// </summary>
	[HarmonyPatch(typeof(AutoCarryWoundedToSafety), "DoUpdate")]
	internal static class AutoCarryWoundedGatePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("AutoCarryWoundedGate", ex);
		}

		/// <summary>Vanilla's own idle interval, in seconds.</summary>
		private const float SkippedInterval = 2.5f;

		private static bool Prefix(ref float __result, DiverSpot ___diverSpot)
		{
			try
			{
				int divers = ___diverSpot != null && ___diverSpot.Divers != null ? ___diverSpot.Divers.Count : 0;
				bool wasArmed = CrewAutomation.Gate.OneShotArmed;
				bool run = CrewAutomation.Gate.BeforeTick(DcSettings.AutoCarryWounded, CrewAutomation.IsLoading, divers, CrewAutomation.InjuredLying);
				if (wasArmed && !CrewAutomation.Gate.OneShotArmed)
					DcLog.Debug("carry wounded: nobody left lying injured; the one-shot order is done");
				if (run)
					return true;
				__result = SkippedInterval;
				return false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("AutoCarryWoundedGate", e);
				return true;
			}
		}
	}
}
