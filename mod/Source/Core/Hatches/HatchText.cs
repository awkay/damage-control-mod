using System;

namespace DamageControl.Core.Hatches
{
	/// <summary>
	/// WS2 locale keys, action keyword and tunable names. Locale keys live in the game's "Activators"
	/// locale sheet, so the game looks them up as "Activators/&lt;key&gt;".
	/// </summary>
	public static class HatchText
	{
		public const string Sheet = "Activators";

		/// <summary>Keyword of the equalize order: its label, "&lt;keyword&gt; Description" and its wheel icon name.</summary>
		public const string EqualizeKeyword = "DC Equalize Pressure";

		public const string PanelLocked = "DC Hatch Pressure Locked";
		public const string PanelOpeningSide = "DC Hatch Opening Side Pressure";
		public const string PanelClosingSide = "DC Hatch Closing Side Pressure";
		public const string PanelBadlyDamaged = "DC Hatch Badly Damaged";
		public const string PanelPartiallySealed = "DC Hatch Partially Sealed";

		/// <summary>Every panel key (each needs an English row).</summary>
		public static readonly string[] PanelKeys = { PanelLocked, PanelOpeningSide, PanelClosingSide, PanelBadlyDamaged, PanelPartiallySealed };

		/// <summary>Vanilla keys reused by the panel.</summary>
		public const string VanillaDurability = "Activators/Hatch Pressure Durability";
		public const string VanillaEvacuated = "Activators/Compartment Evacuated";

		/// <summary>Tunable rows under General/Settings/Damage Control/.</summary>
		public const string TunableLockThreshold = "Hatch Lock Threshold";
		public const string TunableFlowRate = "Hatch Flow Rate";
		public const string TunableBlowOpenChance = "Hatch Blow Open Chance";

		/// <summary>"Activators/&lt;key&gt;".</summary>
		public static string LocaleKey(string key)
		{
			return Sheet + "/" + key;
		}
	}
}
