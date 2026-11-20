using DamageControl.Core.Repair;

namespace DamageControl.Core.Leaks
{
	/// <summary>Which interactables the crowding rule applies to (01 §2.8).</summary>
	public enum CrowdingKind
	{
		None,

		/// <summary>A hull hole (pressure-hull hole equipment).</summary>
		Hole,

		/// <summary>Any other leakable fitting: pipes, valves, tanks.</summary>
		Pipe,

		/// <summary>A hatch.</summary>
		Hatch
	}

	/// <summary>
	/// P8 (01 §2.8, owner 02): a leaking hole, pipe or hatch built for one crewman takes two, so a blocker
	/// and a repairer, or two blockers, can work the same leak side by side. Holes also stop their slots
	/// from disabling each other, so both slots can be used at once.
	///
	/// In 2026.1 one slot holds one crewman, and the interactable's maximum only feeds vanilla's
	/// "more interacting than allowed" cancel and the <c>MaxCharacters == 1</c> rules of the repair and
	/// block-leak orders. So the two-seat rule needs three things, all decided here: the wider maximum
	/// (before the first crewman reserves a slot), a block-leak start that keeps a partner instead of
	/// replacing every earlier blocker, and a slot for the second man when the block-leak slot is taken.
	/// </summary>
	public static class SlotCrowding
	{
		/// <summary>How many characters a crowdable interactable allows.</summary>
		public const int CrowdedMaximum = 2;

		/// <summary>
		/// Whether the interactable is widened at all: a hole, pipe or hatch that is leaking now. One with
		/// crew work slots is a manned device whose performance is shared out by its maximum, so it keeps
		/// vanilla's maximum.
		/// </summary>
		public static bool AppliesTo(CrowdingKind kind, bool isLeaking, bool hasCrewWorkSlots)
		{
			return kind != CrowdingKind.None && isLeaking && !hasCrewWorkSlots;
		}

		/// <summary>
		/// The maximum to apply: <see cref="CrowdedMaximum"/> for a crowdable kind that allows exactly one
		/// character, otherwise <paramref name="currentMaximum"/> unchanged. Idempotent.
		/// </summary>
		public static int NewMaximum(CrowdingKind kind, int currentMaximum)
		{
			if (kind == CrowdingKind.None || currentMaximum != 1)
				return currentMaximum;
			return CrowdedMaximum;
		}

		/// <summary>Whether sibling-slot disabling is removed (holes only).</summary>
		public static bool ClearsSiblingDisables(CrowdingKind kind)
		{
			return kind == CrowdingKind.Hole;
		}

		/// <summary>Classifies equipment by the component flags the Game layer reads.</summary>
		public static CrowdingKind Classify(bool isHatch, bool isHullHole, bool isLeakableEquipment)
		{
			if (isHatch)
				return CrowdingKind.Hatch;
			if (isHullHole)
				return CrowdingKind.Hole;
			if (isLeakableEquipment)
				return CrowdingKind.Pipe;
			return CrowdingKind.None;
		}

		/// <summary>
		/// Whether the two-seat order rules below apply to a target: a crowdable kind, leaking, and holding
		/// exactly <see cref="CrowdedMaximum"/> characters (so a widening that failed leaves vanilla's rules).
		/// </summary>
		public static bool IsShared(CrowdingKind kind, bool isLeaking, int maximum)
		{
			return kind != CrowdingKind.None && isLeaking && maximum == CrowdedMaximum;
		}

		/// <summary>
		/// A new block-leak order starts on a shared target. Vanilla stops every other blocker there, so a
		/// second blocker only ever replaced the first. Returns how many of the
		/// <paramref name="otherBlockers"/> stay: as many as fit beside the newcomer and the
		/// <paramref name="otherRepairers"/> within <see cref="CrowdedMaximum"/>, less one more when no slot
		/// is free for the newcomer and nobody else is leaving (his order would otherwise find no slot).
		/// 0 means vanilla's replacement.
		/// </summary>
		public static int BlockersToKeep(int otherBlockers, int otherRepairers, bool freeSlotForNewcomer)
		{
			if (otherBlockers <= 0)
				return 0;
			int room = CrowdedMaximum - 1 - (otherRepairers > 0 ? otherRepairers : 0);
			if (room <= 0)
				return 0;
			int keep = otherBlockers < room ? otherBlockers : room;
			if (!freeSlotForNewcomer && keep == otherBlockers)
				keep--;
			return keep;
		}

		/// <summary>
		/// The slot tag a blocker on a shared target walks to. Vanilla asks for <c>Block Leak</c> with
		/// <c>Default</c> as the fallback; when both are taken (by his partner), the <c>Repair</c> slot is used,
		/// mirroring the repair's own fallback to the block-leak slot (R1). With nothing free the answer is
		/// vanilla's <c>Block Leak</c>.
		/// </summary>
		public static string BlockerSlotTag(bool blockLeakFree, bool defaultFree, bool repairFree)
		{
			if (blockLeakFree)
				return RepairText.SlotBlockLeak;
			if (defaultFree)
				return RepairText.SlotDefault;
			if (repairFree)
				return RepairText.SlotRepair;
			return RepairText.SlotBlockLeak;
		}

		/// <summary>
		/// Whether a repair may join a shared target where <paramref name="othersAtWork"/> other crewmen
		/// already block or repair. Never more than <see cref="CrowdedMaximum"/>.
		/// </summary>
		public static bool AdmitsAnother(int othersAtWork)
		{
			return othersAtWork < CrowdedMaximum;
		}
	}
}
