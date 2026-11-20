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
	/// P8 (01 §2.8, owner 02): holes, pipes and hatches built for one crewman take two, so a blocker and
	/// a repairer can work the same leak side by side. Holes also stop their slots from disabling each
	/// other, so both slots can be used at once.
	/// </summary>
	public static class SlotCrowding
	{
		/// <summary>How many characters a crowdable interactable allows.</summary>
		public const int CrowdedMaximum = 2;

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
	}
}
