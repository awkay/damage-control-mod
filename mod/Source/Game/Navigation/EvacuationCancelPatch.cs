using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters.Actions;

namespace DamageControl.Game.Navigation
{
	/// <summary>
	/// B11 (03 §2.F): the evacuate-to-safety action marks itself unstoppable when a character starts it; this
	/// prefix makes it stoppable again before each escape leg, so the player can cancel an evacuation order.
	/// </summary>
	[HarmonyPatch(typeof(EvacuateToSafetyAction), "Escape")]
	internal static class EvacuationCancelPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("EvacuationCancel", ex);
		}

		private static void Prefix(EvacuateToSafetyAction __instance)
		{
			try
			{
				if (__instance != null)
					__instance.CanBeStopped = true;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("EvacuationCancel", e);
			}
		}
	}
}
