using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Repair;
using HarmonyLib;
using UBOAT.Game.Core;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// R1 availability (04 §2.1.2): vanilla offers the repair only while a <c>Repair</c> or <c>Default</c> slot
	/// is usable. When vanilla disabled it for that reason alone and a <c>Block Leak</c> slot is usable, the
	/// repair is offered after all, with vanilla's "a part will be used" note when one will. Every other
	/// vanilla verdict stands.
	/// </summary>
	[HarmonyPatch(typeof(RepairAction), "GetState", new Type[] { typeof(GameActivator), typeof(PlayableCharacter), typeof(bool), typeof(bool), typeof(ActionContext) })]
	internal static class RepairStatePatch
	{
		private static void Postfix(RepairAction __instance, GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ref ActionStateData __result)
		{
			try
			{
				if (__result.ActionState != ActionState.Disabled || activator == null)
					return;
				Equipment equipment = activator.AssociatedEquipment;
				Interactable interactable = equipment != null ? equipment.GetComponent<Interactable>() : null;
				if (interactable == null || !interactable.HasSlots(RepairText.SlotBlockLeak))
					return;
				if (!MeetsSoftRequirements(__instance, character))
					return;
				bool blocking = character != null && (character.Item is IInteractionsBlockingItem || character.Backpack.Item is IInteractionsBlockingItem);
				bool otherRepairing = OtherCrewmanRepairing(activator, character);
				bool partMissing = RepairRuntime.PartRequiredButMissing(equipment, character);
				bool repairSlot = RepairSlotProbe.Check(interactable, RepairText.SlotRepair, RepairText.SlotDefault, character, false, isPlayerOrder).ActionState > ActionState.Disabled;
				ActionStateData leakSlot = RepairSlotProbe.Check(interactable, RepairText.SlotBlockLeak, character, fillDetails, isPlayerOrder);
				if (!RepairSlots.OfferThroughBlockLeakSlot(true, blocking, otherRepairing, partMissing, repairSlot, leakSlot.ActionState > ActionState.Disabled))
					return;
				if (fillDetails && leakSlot.Warning == null && equipment.Health == 0f && equipment.Data != null && equipment.Data.RepairConsumesParts)
					leakSlot.Warning = new LocalizedString(RepairText.ReplacementPartsInfo);
				__result = leakSlot;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("RepairState", e);
			}
		}

		private static bool OtherCrewmanRepairing(GameActivator activator, PlayableCharacter character)
		{
			List<PlayableCharacter> users = activator.Users;
			for (int i = users.Count - 1; i >= 0; i--)
			{
				if (users[i] != character && users[i].Action is RepairAction)
					return true;
			}
			return false;
		}

		private static bool MeetsSoftRequirements(CharacterAction action, PlayableCharacter character)
		{
			string[] soft = action.SoftRequirements;
			if (soft == null || character == null)
				return true;
			for (int i = soft.Length - 1; i >= 0; i--)
			{
				if (character.Data.HasStatus(soft[i]))
					return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Gives the repair patches the game's own slot-usability test, which the game keeps protected on
	/// <see cref="CharacterAction"/>. Never instantiated and never registered as an action.
	/// </summary>
	internal abstract class RepairSlotProbe : CharacterAction
	{
		private RepairSlotProbe()
		{
		}

		/// <summary>The game's test for slots tagged <paramref name="tag"/> or <paramref name="fallbackTag"/>.</summary>
		internal static ActionStateData Check(Interactable interactable, string tag, string fallbackTag, PlayableCharacter character, bool fillDetails, bool isPlayerOrder)
		{
			return CheckInteractableUseConditions(interactable, tag, fallbackTag, character, SlotUsageFlags.AllowUnderWaterWithRebreather, fillDetails, isPlayerOrder);
		}

		/// <summary>The game's test for slots tagged <paramref name="tag"/>.</summary>
		internal static ActionStateData Check(Interactable interactable, string tag, PlayableCharacter character, bool fillDetails, bool isPlayerOrder)
		{
			return CheckInteractableUseConditions(interactable, tag, character, SlotUsageFlags.AllowUnderWaterWithRebreather, fillDetails, isPlayerOrder);
		}
	}
}
