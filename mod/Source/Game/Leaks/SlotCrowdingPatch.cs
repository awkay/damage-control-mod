using System;
using DamageControl.Core.Common;
using DamageControl.Core.Leaks;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// P8 (01 §2.8): when a crewman starts or stops using a slot of a one-man hole, pipe or hatch, the
	/// interactable is widened to two characters (initial and current maximum), so a blocker and a
	/// repairer can share a leak. On hull holes the slots also stop disabling one another. Idempotent:
	/// after the first change the maximum is 2 and the postfix returns at its first check.
	/// </summary>
	[HarmonyPatch(typeof(Interactable), "OnSlotInteractionChanged")]
	internal static class SlotCrowdingPatch
	{
		private static void Postfix(Interactable __instance, ref int ___maxCharacters, ref int ___maxCharactersCurrent)
		{
			try
			{
				if (___maxCharacters != 1 || __instance == null)
					return;
				CrowdingKind kind = SlotCrowding.Classify(
					__instance.GetComponent<Hatch>() != null,
					__instance.GetComponent<PressureHullHole>() != null,
					__instance.GetComponent<LeakableEquipment>() != null);
				int widened = SlotCrowding.NewMaximum(kind, ___maxCharacters);
				if (widened == ___maxCharacters)
					return;
				if (___maxCharactersCurrent == ___maxCharacters)
					___maxCharactersCurrent = widened;
				___maxCharacters = widened;
				if (SlotCrowding.ClearsSiblingDisables(kind))
					ClearSiblingDisables(__instance.Slots);
				DcLog.Debug("slot crowding: \"" + __instance.name + "\" (" + kind + ") now takes " + widened + " characters");
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("SlotCrowding", e);
			}
		}

		/// <summary>Stops the slots of one interactable from disabling each other, and lifts any such disable now in force.</summary>
		private static void ClearSiblingDisables(InteractableSlot[] slots)
		{
			if (slots == null)
				return;
			for (int i = 0; i < slots.Length; i++)
			{
				InteractableSlot slot = slots[i];
				if (slot == null)
					continue;
				InteractableSlot[] disabled = slot.DisabledSlots;
				if (disabled != null && disabled.Length > 0)
				{
					int kept = 0;
					InteractableSlot[] remaining = new InteractableSlot[disabled.Length];
					for (int j = 0; j < disabled.Length; j++)
					{
						if (disabled[j] != null && Array.IndexOf(slots, disabled[j]) < 0)
							remaining[kept++] = disabled[j];
					}
					if (kept != disabled.Length)
					{
						Array.Resize(ref remaining, kept);
						slot.DisabledSlots = remaining;
					}
				}
				for (int j = 0; j < slots.Length; j++)
				{
					if (j != i && slots[j] != null)
						slot.RemoveDisableReason(slots[j]);
				}
			}
		}
	}
}
