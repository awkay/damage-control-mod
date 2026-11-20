using System;
using DamageControl.Core.Common;
using DamageControl.Core.Leaks;
using DamageControl.Core.Repair;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Leaks;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// R1 (04 §2.1.5, 00 §4): when the vanilla repair walks to its device, choose the station: a hatch's free
	/// <c>Default</c> slot, else the first free of <c>Repair</c>, <c>Block Leak</c>, <c>Default</c>. Only the
	/// slot argument changes; the game still picks the closest matching slot, falls back to <c>Default</c>
	/// and takes the pose from the chosen slot (vanilla then lifts an interaction type of 0 or less to 1).
	/// For <see cref="BlockLeakAction"/> on a shared leak target it also picks the second blocker's slot (P8, WS3).
	/// </summary>
	[HarmonyPatch(typeof(DeviceUseAction), "UseDevice")]
	internal static class RepairSlotPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("RepairSlot", ex);
		}

		private static void Prefix(DeviceUseAction __instance, PlayableCharacter character, ref string slotTag, Device device)
		{
			try
			{
				if (__instance is BlockLeakAction)
				{
					BlockLeakSlot(__instance, character, ref slotTag, device);
					return;
				}
				if (!(__instance is RepairAction) || slotTag != RepairText.SlotRepair || character == null)
					return;
				GameActivator activator = __instance.Activator;
				if (activator == null)
					return;
				Device target = device != null ? device : activator.GetComponent<Device>();
				Interactable interactable = (target != null ? target.gameObject : activator.gameObject).GetComponent<Interactable>();
				if (interactable == null)
					return;
				bool underWater = character.HasRebreather;
				bool isHatch = activator.AssociatedEquipment is Hatch || target is Hatch;
				slotTag = RepairSlots.Choose(isHatch,
					interactable.HasFreeSlot(RepairText.SlotRepair, underWater),
					interactable.HasFreeSlot(RepairText.SlotBlockLeak, underWater),
					interactable.HasFreeSlot(RepairText.SlotDefault, underWater));
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("RepairSlot", e);
			}
		}

		/// <summary>
		/// P8 (WS3): on a shared leak target a second blocker finds the block-leak slot (and any default slot)
		/// taken by his partner, and vanilla would end his order for want of a slot. He takes the free
		/// <c>Repair</c> slot instead (<see cref="SlotCrowding.BlockerSlotTag"/>). Any other target keeps
		/// vanilla's slot.
		/// </summary>
		private static void BlockLeakSlot(DeviceUseAction action, PlayableCharacter character, ref string slotTag, Device device)
		{
			if (slotTag != RepairText.SlotBlockLeak || character == null)
				return;
			GameActivator activator = action.Activator;
			if (activator == null)
				return;
			Device target = device != null ? device : activator.GetComponent<Device>();
			Interactable interactable = (target != null ? target.gameObject : activator.gameObject).GetComponent<Interactable>();
			if (interactable == null || !SlotCrowdingRuntime.IsShared(interactable))
				return;
			bool underWater = character.HasRebreather;
			slotTag = SlotCrowding.BlockerSlotTag(
				interactable.HasFreeSlot(RepairText.SlotBlockLeak, underWater),
				interactable.HasFreeSlot(RepairText.SlotDefault, underWater),
				interactable.HasFreeSlot(RepairText.SlotRepair, underWater));
		}
	}
}
