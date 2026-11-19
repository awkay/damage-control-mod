namespace DamageControl.Core.Pressure
{
	/// <summary>What an interaction slot belongs to, as far as P7 cares. Resolved by component type, never by name.</summary>
	public enum SlotOwner
	{
		Other,
		DivingPlanesStation,
		ElectricEngine,
		InteriorHatch,
		ConningTowerHatchSecondDefaultSlot,
		ConningTowerHatchOtherSlot,
	}

	/// <summary>The override for one slot.</summary>
	public struct SlotOverride
	{
		/// <summary>Metres of water above the floor at which the slot counts as underwater.</summary>
		public readonly float WaterTolerance;

		/// <summary>The slot's animator interaction type, or -1 to keep the game's value.</summary>
		public readonly int InteractionType;

		public SlotOverride(float waterTolerance, int interactionType)
		{
			WaterTolerance = waterTolerance;
			InteractionType = interactionType;
		}

		public bool OverridesInteractionType
		{
			get { return InteractionType >= 0; }
		}
	}

	/// <summary>
	/// P7 (01 §2.7): water tolerance and interaction type overrides applied when a slot registers with
	/// its compartment, before the game's sorted insert. The 2026.1 slot interaction type is a plain
	/// int fed to the animator's "interaction_type" parameter, so the spec's raw values 4, 5 and 6 are
	/// used as they are.
	/// </summary>
	public static class SlotTuning
	{
		public const int KeepInteractionType = -1;

		/// <summary>The override for <paramref name="owner"/>; false for <see cref="SlotOwner.Other"/>.</summary>
		public static bool TryGetOverride(SlotOwner owner, out SlotOverride result)
		{
			switch (owner)
			{
				case SlotOwner.DivingPlanesStation:
					result = new SlotOverride(0.7f, KeepInteractionType);
					return true;
				case SlotOwner.ElectricEngine:
					result = new SlotOverride(0.6f, KeepInteractionType);
					return true;
				case SlotOwner.InteriorHatch:
					result = new SlotOverride(1.4f, 5);
					return true;
				case SlotOwner.ConningTowerHatchSecondDefaultSlot:
					result = new SlotOverride(1.4f, 6);
					return true;
				case SlotOwner.ConningTowerHatchOtherSlot:
					result = new SlotOverride(0.7f, 4);
					return true;
				default:
					result = new SlotOverride(0f, KeepInteractionType);
					return false;
			}
		}

		/// <summary>
		/// Classifies a hatch slot. <paramref name="defaultSlotOrdinal"/> is the slot's position among the
		/// hatch's slots whose name marks them as "Default" slots (0-based), or -1 if it is not one.
		/// </summary>
		public static SlotOwner ClassifyHatchSlot(bool isExteriorHatch, bool isConningTowerHatch, int defaultSlotOrdinal)
		{
			if (isExteriorHatch)
				return SlotOwner.Other;
			if (!isConningTowerHatch)
				return SlotOwner.InteriorHatch;
			return defaultSlotOrdinal == 1 ? SlotOwner.ConningTowerHatchSecondDefaultSlot : SlotOwner.ConningTowerHatchOtherSlot;
		}
	}
}
