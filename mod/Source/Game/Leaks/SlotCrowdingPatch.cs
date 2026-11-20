using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Interactions;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// P8 (01 §2.8) backstop: when a crewman starts or stops using a slot of a leaking one-man hole, pipe or
	/// hatch, the interactable is widened to two characters (initial and current maximum); on hull holes the
	/// slots also stop disabling one another (<see cref="SlotCrowdingRuntime.Apply"/>). The main widening
	/// happens earlier, when a block-leak order starts (<see cref="BlockLeakStartPatch"/>); this covers
	/// targets a repairer reaches first. Idempotent.
	/// </summary>
	[HarmonyPatch(typeof(Interactable), "OnSlotInteractionChanged")]
	internal static class SlotCrowdingPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("SlotCrowding", ex);
		}

		private static void Postfix(Interactable __instance, ref int ___maxCharacters, ref int ___maxCharactersCurrent)
		{
			try
			{
				if (___maxCharacters != 1 || __instance == null)
					return;
				SlotCrowdingRuntime.Apply(__instance, ref ___maxCharacters, ref ___maxCharactersCurrent);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("SlotCrowding", e);
			}
		}
	}
}
