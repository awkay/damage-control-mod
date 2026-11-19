using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P7 (01 §2.7): when a slot registers with its compartment, override its water tolerance (and for
	/// hatch slots its interaction type) per <see cref="SlotTuning"/>. The prefix runs before the
	/// game's insert, which keeps the compartment's slot list sorted by tolerance.
	///
	/// Owners are resolved by component type: <c>DivingPlanesStation</c>, the ship's electric
	/// <c>PlayerShipEngine</c>, interior <c>Hatch</c>es, and the ship's <c>HatchToConningTower</c>.
	/// "Second default slot" means the second of the hatch's slots carrying the game's "Default" slot
	/// tag, in the hatch's own slot order. The interaction-type part can be turned off with the data
	/// row General/Settings/Damage Control/Hatch Slot Interaction Types.
	/// </summary>
	[HarmonyPatch(typeof(Compartment), "AddInteractableSlot")]
	internal static class SlotTuningPatch
	{
		private const string DefaultSlotTag = "Default";
		private const string InteractionTypesPath = Tunables.Root + "Hatch Slot Interaction Types";

		private static AccessTools.FieldRef<InteractableSlot, float> toleranceField;
		private static AccessTools.FieldRef<InteractableSlot, int> interactionTypeField;

		private static void Prefix(InteractableSlot slot)
		{
			try
			{
				if (slot == null)
					return;
				SlotOverride tuning;
				if (!SlotTuning.TryGetOverride(Classify(slot), out tuning))
					return;
				if (toleranceField == null)
				{
					toleranceField = AccessTools.FieldRefAccess<InteractableSlot, float>("waterElevationToleration");
					interactionTypeField = AccessTools.FieldRefAccess<InteractableSlot, int>("interactionType");
				}
				toleranceField(slot) = tuning.WaterTolerance;
				if (tuning.OverridesInteractionType && Tunables.GetBool(InteractionTypesPath, true))
					interactionTypeField(slot) = tuning.InteractionType;
				slot.IsBelowWater = false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("SlotTuning", e);
			}
		}

		private static SlotOwner Classify(InteractableSlot slot)
		{
			Interactable interactable = slot.Interactable;
			Device device = interactable != null ? interactable.Device : null;
			if (device == null)
				device = slot.GetComponentInParent<Device>();
			if (device == null)
				return SlotOwner.Other;

			if (device is DivingPlanesStation)
				return SlotOwner.DivingPlanesStation;

			PlayerShipEngine engine = device as PlayerShipEngine;
			if (engine != null)
			{
				PlayerShip ship = engine.GetComponentInParent<PlayerShip>();
				return ship != null && ship.ElectricEngine == engine ? SlotOwner.ElectricEngine : SlotOwner.Other;
			}

			Hatch hatch = device as Hatch;
			if (hatch == null)
				return SlotOwner.Other;
			PlayerShip hatchShip = hatch.GetComponentInParent<PlayerShip>();
			bool isConningTowerHatch = hatchShip != null && hatchShip.HatchToConningTower == hatch;
			return SlotTuning.ClassifyHatchSlot(hatch.IsExteriorHatch, isConningTowerHatch, DefaultSlotOrdinal(interactable, slot));
		}

		/// <summary>Position of <paramref name="slot"/> among the interactable's "Default"-tagged slots, or -1.</summary>
		private static int DefaultSlotOrdinal(Interactable interactable, InteractableSlot slot)
		{
			if (interactable == null || interactable.Slots == null)
				return -1;
			int ordinal = 0;
			foreach (InteractableSlot candidate in interactable.Slots)
			{
				if (candidate == null || candidate.name.IndexOf(DefaultSlotTag, StringComparison.Ordinal) < 0)
					continue;
				if (candidate == slot)
					return ordinal;
				ordinal++;
			}
			return -1;
		}
	}
}
