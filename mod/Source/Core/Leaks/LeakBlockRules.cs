namespace DamageControl.Core.Leaks
{
	/// <summary>What a block-leak order on a hatch does this frame (02 §2.6, 00 C28).</summary>
	public struct HatchBlockerStep
	{
		/// <summary>Hold a <c>SealSource.Blocker</c> seal on the hatch for this action.</summary>
		public readonly bool Seal;

		/// <summary>Cancel the order (with the game's standard order-cancel delay).</summary>
		public readonly bool Cancel;

		public HatchBlockerStep(bool seal, bool cancel)
		{
			Seal = seal;
			Cancel = cancel;
		}
	}

	/// <summary>
	/// H8: the block-leak rules the mod adds to vanilla 2026.1 <c>BlockLeakAction</c> (00 C9, C28).
	///
	/// Leakable equipment (pipes, hull holes) keeps the vanilla path: while the crewman interacts, vanilla
	/// scales the leak intensity by 0.2. Any other equipment that carries a Leaking status (in practice a
	/// destroyed hatch, which WS2 marks as leaking) takes the mod's path instead: no intensity modifier
	/// (there is no leak-intensity stat to scale), a hatch seal while interacting, and the order ends as
	/// soon as the hatch is opened.
	/// </summary>
	public static class LeakBlockRules
	{
		/// <summary>
		/// True when the target must not go through vanilla's leakable-equipment code: the leak still
		/// refers to equipment and that equipment is not leakable equipment.
		/// </summary>
		public static bool TakesNonLeakablePath(bool leakHasEquipment, bool isLeakableEquipment)
		{
			return leakHasEquipment && !isLeakableEquipment;
		}

		/// <summary>
		/// Per-frame decision for a hatch target. An open hatch ends the order (02 §2.6 termination) and is
		/// never sealed; a closed hatch is sealed only while the crewman is actually interacting.
		/// </summary>
		public static HatchBlockerStep HatchStep(bool hatchOpen, bool interacting)
		{
			if (hatchOpen)
				return new HatchBlockerStep(false, true);
			return new HatchBlockerStep(interacting, false);
		}

		/// <summary>
		/// Vanilla's block-leak stop removes the target's "is being repaired" status whoever added it. With
		/// P8 a blocker and a repairer share one target, so a blocker leaving would strip the status of a
		/// repair still in progress, which also ends the hatch repair hold (C10). True when the status must be
		/// put back after vanilla's stop: it was present before, another crewman's repair is still running,
		/// and it is gone afterwards.
		/// </summary>
		public static bool RestoresRepairStatus(bool statusBefore, bool otherRepairRunning, bool statusAfter)
		{
			return statusBefore && otherRepairRunning && !statusAfter;
		}

		/// <summary>
		/// 02 §6 #16: whether the block-leak order may be offered on this target. Hidden when there is no
		/// visible leak, on an open hatch, and on a hatch with no interaction slots (vanilla would throw
		/// looking for one).
		/// </summary>
		public static bool IsOffered(bool hasVisibleLeak, bool isHatch, bool hatchOpen, bool hasInteractable)
		{
			if (!hasVisibleLeak)
				return false;
			if (isHatch && (hatchOpen || !hasInteractable))
				return false;
			return true;
		}
	}
}
