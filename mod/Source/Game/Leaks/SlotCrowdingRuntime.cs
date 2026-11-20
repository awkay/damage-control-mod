using System;
using System.Collections.Generic;
using System.Text;
using DamageControl.Core.Common;
using DamageControl.Core.Leaks;
using DamageControl.Core.Repair;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// P8 game-side helpers shared by the crowding patches: classify a leak target, widen it to two
	/// characters (<see cref="SlotCrowding"/>), and count who works it. Widening happens when a block-leak
	/// order starts (before vanilla reserves a slot or applies its <c>MaxCharacters == 1</c> rule) and,
	/// as a backstop, whenever a slot's occupant changes (<see cref="SlotCrowdingPatch"/>).
	/// </summary>
	internal static class SlotCrowdingRuntime
	{
		private const string WorkSlotTag = "Work";

		private static AccessTools.FieldRef<Interactable, int> maxField;
		private static AccessTools.FieldRef<Interactable, int> currentField;
		private static bool fieldsResolved;

		/// <summary>The crowding kind of the equipment that carries <paramref name="interactable"/>.</summary>
		internal static CrowdingKind KindOf(Interactable interactable)
		{
			if (interactable == null)
				return CrowdingKind.None;
			return SlotCrowding.Classify(
				interactable.GetComponent<Hatch>() != null,
				interactable.GetComponent<PressureHullHole>() != null,
				interactable.GetComponent<LeakableEquipment>() != null);
		}

		/// <summary>Whether the equipment that carries <paramref name="interactable"/> has a Leaking status.</summary>
		internal static bool IsLeaking(Interactable interactable)
		{
			Equipment equipment = interactable != null ? interactable.GetComponent<Equipment>() : null;
			return equipment != null && equipment.HasStatus<Leaking>();
		}

		/// <summary>The two-seat order rules apply to this target now.</summary>
		internal static bool IsShared(Interactable interactable)
		{
			return interactable != null && SlotCrowding.IsShared(KindOf(interactable), IsLeaking(interactable), interactable.MaxCharacters);
		}

		/// <summary>
		/// Widens <paramref name="interactable"/> through its private fields when the rule applies. Used where
		/// no patch injection of those fields is available. Never throws.
		/// </summary>
		internal static void Widen(Interactable interactable)
		{
			if (interactable == null || !ResolveFields())
				return;
			Apply(interactable, ref maxField(interactable), ref currentField(interactable));
		}

		/// <summary>
		/// The widening itself, on the interactable's initial and current maximum: a leaking, unmanned hole,
		/// pipe or hatch built for one goes to two; on holes the slots stop disabling each other. Idempotent:
		/// once the maximum is 2 it returns at the first check.
		/// </summary>
		internal static void Apply(Interactable interactable, ref int maxCharacters, ref int maxCharactersCurrent)
		{
			if (maxCharacters != 1 || interactable == null)
				return;
			CrowdingKind kind = KindOf(interactable);
			if (!SlotCrowding.AppliesTo(kind, IsLeaking(interactable), interactable.HasSlots(WorkSlotTag)))
				return;
			int widened = SlotCrowding.NewMaximum(kind, maxCharacters);
			if (widened == maxCharacters)
				return;
			if (maxCharactersCurrent == maxCharacters)
				maxCharactersCurrent = widened;
			maxCharacters = widened;
			if (SlotCrowding.ClearsSiblingDisables(kind))
				ClearSiblingDisables(interactable.Slots);
			DcLog.Debug("slot crowding: \"" + interactable.name + "\" (" + kind + ") now takes " + widened + " characters; slots: " + DescribeSlots(interactable.Slots));
		}

		/// <summary>Whether a slot tagged for leak work (block leak, default or repair) is free for this crewman.</summary>
		internal static bool HasFreeLeakSlot(Interactable interactable, bool allowUnderWater)
		{
			return interactable.HasFreeSlot(RepairText.SlotBlockLeak, allowUnderWater)
				|| interactable.HasFreeSlot(RepairText.SlotDefault, allowUnderWater)
				|| interactable.HasFreeSlot(RepairText.SlotRepair, allowUnderWater);
		}

		/// <summary>Counts the crewmen other than <paramref name="except"/> on these users who block or repair.</summary>
		internal static void CountWorkers(List<PlayableCharacter> users, PlayableCharacter except, out int blockers, out int repairers)
		{
			blockers = 0;
			repairers = 0;
			if (users == null)
				return;
			for (int i = users.Count - 1; i >= 0; i--)
			{
				PlayableCharacter user = users[i];
				if (user == null || user == except)
					continue;
				if (user.Action is BlockLeakAction)
					blockers++;
				else if (user.Action is RepairAction)
					repairers++;
			}
		}

		private static bool ResolveFields()
		{
			if (fieldsResolved)
				return maxField != null && currentField != null;
			fieldsResolved = true;
			try
			{
				maxField = AccessTools.FieldRefAccess<Interactable, int>("maxCharacters");
				currentField = AccessTools.FieldRefAccess<Interactable, int>("maxCharactersCurrent");
			}
			catch (Exception e)
			{
				maxField = null;
				currentField = null;
				DcLog.PatchFailed("SlotCrowding (fields)", e);
			}
			return maxField != null && currentField != null;
		}

		/// <summary>"Default, Default [under water]": the slot names, for the debug log that confirms a target's layout in game.</summary>
		private static string DescribeSlots(InteractableSlot[] slots)
		{
			if (slots == null || slots.Length == 0)
				return "none";
			StringBuilder text = new StringBuilder();
			for (int i = 0; i < slots.Length; i++)
			{
				InteractableSlot slot = slots[i];
				if (slot == null)
					continue;
				if (text.Length > 0)
					text.Append(", ");
				text.Append(slot.name);
				if (slot.IsBelowWater)
					text.Append(" [under water]");
			}
			return text.ToString();
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
