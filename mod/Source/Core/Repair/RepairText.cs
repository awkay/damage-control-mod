namespace DamageControl.Core.Repair
{
	/// <summary>
	/// Names used by the WS5 repair and parts area: slot tags, item names, locale keys, the dismantle
	/// keyword, activator tags and tunable rows. Game facts (slot, item, perk and tag names, vanilla
	/// locale keys) are quoted exactly as the 2026.1 game spells them.
	/// </summary>
	public static class RepairText
	{
		// ---- interaction slot tags (game facts)
		public const string SlotRepair = "Repair";
		public const string SlotBlockLeak = "Block Leak";
		public const string SlotDefault = "Default";

		/// <summary>Animator parameter of the repair pose (game fact).</summary>
		public const string RepairAnimation = "Repair";

		// ---- items and perks (game facts)
		public const string Scrap = "Scrap";
		public const string ReplacementParts = "Replacement Parts";
		public const string PerkHandyman = "Handyman";
		public const string PerkThrifty = "Thrifty";

		// ---- locale
		public const string ActivatorsSheet = "Activators";
		public const string ItemsSheet = "Items";

		/// <summary>R6 wheel label keyword (ours); also the icon name.</summary>
		public const string DismantleKeyword = "DC Dismantle";
		public const string DismantleDescription = DismantleKeyword + " Description";
		public const string DismantleProgressing = DismantleKeyword + " Progressing Description";

		/// <summary>
		/// Vanilla repair label keywords. Not overridden: their 2026.1 texts are swapped in most languages
		/// (04 §4.2), but an English-only override would replace every translation with English (known issue).
		/// </summary>
		public const string RepairKeyword = "Repair";
		public const string RepairRequiresPartsKeyword = "Repair (Requires Parts)";

		/// <summary>Vanilla scrap tooltip, overridden because vanilla states 2:1. Names no ratio: the ratio is a tunable.</summary>
		public const string ScrapDescription = "Scrap Description";

		/// <summary>Vanilla warning reused when someone else is repairing the device ({0} = name).</summary>
		public const string AlreadyBeingRepaired = "Actions/RepairAction/Already Being Repaired";

		/// <summary>Vanilla info line attached to a repair that will consume a part.</summary>
		public const string ReplacementPartsInfo = "Actions/RepairAction/Replacement Parts Info";

		// ---- data rows
		public const string DismantleRowKey = "DCDismantleForParts";
		public const string DismantleType = "DamageControl.Game.Repair.DismantleAction";

		/// <summary>Tunable row under General/Settings/Damage Control (Q9: one named setting, default 4).</summary>
		public const string TunableSalvageScrapPerPart = "Salvage Scrap Per Part";

		/// <summary>Name of the R2 leak-intensity scale modifier on leakable equipment.</summary>
		public const string LeakThrottleModifier = "DC Repair Throttle";

		/// <summary>Activator tags the dismantle order is offered on (04 §2.3, game spelling, "Ventillation" included).</summary>
		public static readonly string[] DismantleActivatorTags =
		{
			"Compartment Hatch", "Compressor", "Deck Gun", "Depth Steers Station", "Engines", "Flak",
			"Gyrocompass", "Listening Room", "Periscope", "Pill Thrower", "Power Distribution Board Bow",
			"Power Distribution Board Stern", "Radio Room", "Targeting Sight", "Torpedo Launcher",
			"Trim Pump", "Valves", "Ventillation",
		};
	}
}
