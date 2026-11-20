namespace DamageControl.Core.Repair
{
	/// <summary>
	/// R1: where a repair is carried out (04 §1 item 1, §2.1.2, §2.1.5). The vanilla 2026.1 repair walks to
	/// a <c>Repair</c> slot, falling back to <c>Default</c>. The mod widens that: a hatch is repaired from its
	/// <c>Default</c> slot when one is free; anything else uses the first free slot of <c>Repair</c>,
	/// <c>Block Leak</c>, <c>Default</c>. The pose then comes from the chosen slot.
	/// </summary>
	public static class RepairSlots
	{
		/// <summary>
		/// The slot tag to walk to. When no slot is free the answer is <c>Default</c> (the game then picks the
		/// closest matching slot or cancels, as vanilla does).
		/// </summary>
		public static string Choose(bool isHatch, bool repairFree, bool blockLeakFree, bool defaultFree)
		{
			if (isHatch && defaultFree)
				return RepairText.SlotDefault;
			if (repairFree)
				return RepairText.SlotRepair;
			if (blockLeakFree)
				return RepairText.SlotBlockLeak;
			return RepairText.SlotDefault;
		}

		/// <summary>Interaction types of 0 or less become 1 (04 §2.1.5 item 4; vanilla does the same after start).</summary>
		public static int ClampInteractionType(int interactionType)
		{
			return interactionType <= 0 ? 1 : interactionType;
		}

		/// <summary>
		/// Availability fallback (04 §2.1.2): vanilla offers the repair only when a <c>Repair</c> or
		/// <c>Default</c> slot is usable. When vanilla said Disabled, this decides whether the cause was only
		/// the slot check and a <c>Block Leak</c> slot is usable instead, so the order may be offered after all.
		/// Every non-slot reason vanilla can disable for (a blocking item carried, another crewman already
		/// repairing, a part required and none obtainable) keeps it disabled.
		/// </summary>
		public static bool OfferThroughBlockLeakSlot(bool vanillaDisabled, bool carriesBlockingItem, bool otherCrewmanRepairing,
			bool partRequiredButMissing, bool repairOrDefaultSlotUsable, bool blockLeakSlotUsable)
		{
			if (!vanillaDisabled || carriesBlockingItem || otherCrewmanRepairing || partRequiredButMissing)
				return false;
			return !repairOrDefaultSlotUsable && blockLeakSlotUsable;
		}

		/// <summary>
		/// The part rule of 04 §2.1.6 and vanilla 2026.1: a part is needed only from 0 health on equipment
		/// whose repairs consume parts, NPCs are exempt, and it may come from the backpack or, with manual
		/// item delivery off, from the main storage.
		/// </summary>
		public static bool PartRequiredButMissing(float health, bool repairConsumesParts, bool isNpc,
			bool partInBackpack, bool manualItemDelivery, bool partInMainStorage)
		{
			if (health > 0f || !repairConsumesParts || isNpc)
				return false;
			if (partInBackpack)
				return false;
			return manualItemDelivery || !partInMainStorage;
		}
	}
}
