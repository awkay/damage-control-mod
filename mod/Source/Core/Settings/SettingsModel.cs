using System;
using System.Collections.Generic;

namespace DamageControl.Core.Settings
{
	/// <summary>
	/// The per-campaign player settings (R9, P14, H12, H13). Values are persisted by the Game layer's
	/// settings task under field names derived from these; append new members, never renumber.
	/// </summary>
	public enum SettingId
	{
		/// <summary>P14: pressure difference can hold an interior hatch shut (WS2, H1).</summary>
		HatchPressureBlocking = 0,

		/// <summary>H12: the vanilla routine that sends sailors to block leaks may run (WS3).</summary>
		AutoLeakBlocking = 1,

		/// <summary>H13: the vanilla routine that carries wounded crew to safety may run (WS3).</summary>
		AutoCarryWounded = 2,
	}

	/// <summary>Static facts about each setting: defaults (D3), data-sheet names and the wheel label keywords.</summary>
	public static class SettingsCatalog
	{
		/// <summary>Every setting, in display order.</summary>
		public static readonly SettingId[] All = { SettingId.HatchPressureBlocking, SettingId.AutoLeakBlocking, SettingId.AutoCarryWounded };

		/// <summary>Name of the Crew/Actions row parameter that tells a toggle row which setting it flips.</summary>
		public const string ToggleRowParameter = "Setting";

		/// <summary>
		/// D3: vanilla automatic leak blocking and automatic carrying of wounded stay on (the mod only adds
		/// an off switch); hatch pressure blocking is opt-in. Pre-mod saves and new campaigns both get these.
		/// </summary>
		public static bool DefaultValue(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return false;
				case SettingId.AutoLeakBlocking:
					return true;
				case SettingId.AutoCarryWounded:
					return true;
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}

		/// <summary>True for a defined <see cref="SettingId"/>.</summary>
		public static bool IsDefined(SettingId id)
		{
			return Array.IndexOf(All, id) >= 0;
		}

		/// <summary>The name used in data rows ("Setting = HatchPressureBlocking").</summary>
		public static string ParameterName(SettingId id)
		{
			return id.ToString();
		}

		/// <summary>Parses a data-row parameter value: the enum member name, case-insensitive, surrounding spaces ignored. Numbers are rejected.</summary>
		public static bool TryParse(string text, out SettingId id)
		{
			id = SettingId.HatchPressureBlocking;
			if (string.IsNullOrEmpty(text))
				return false;
			string name = text.Trim();
			foreach (SettingId candidate in All)
			{
				if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
				{
					id = candidate;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Localization keyword (and icon name) of the toggle action, which names what choosing it will do:
		/// the "Enable" keyword while the setting is off, the "Disable" keyword while it is on.
		/// The game looks the label up as "Activators/&lt;keyword&gt;" and the description as "Activators/&lt;keyword&gt; Description".
		/// </summary>
		public static string ToggleKeyword(SettingId id, bool currentValue)
		{
			return "DC " + (currentValue ? "Disable " : "Enable ") + DisplayStem(id);
		}

		/// <summary>Both keywords of every setting (for data and icon completeness checks).</summary>
		public static IEnumerable<string> AllToggleKeywords()
		{
			foreach (SettingId id in All)
			{
				yield return ToggleKeyword(id, false);
				yield return ToggleKeyword(id, true);
			}
		}

		private static string DisplayStem(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return "Hatch Pressure Blocking";
				case SettingId.AutoLeakBlocking:
					return "Automatic Leak Blocking";
				case SettingId.AutoCarryWounded:
					return "Automatic Wounded Carrying";
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}
	}

	/// <summary>
	/// The values of all settings plus the toggle logic. The Game layer's settings task owns the stored
	/// fields and copies them in and out of this model; nothing else stores settings.
	/// </summary>
	public sealed class SettingsModel
	{
		private bool hatchPressureBlocking;
		private bool autoLeakBlocking;
		private bool autoCarryWounded;

		public SettingsModel(bool hatchPressureBlocking, bool autoLeakBlocking, bool autoCarryWounded)
		{
			this.hatchPressureBlocking = hatchPressureBlocking;
			this.autoLeakBlocking = autoLeakBlocking;
			this.autoCarryWounded = autoCarryWounded;
		}

		/// <summary>A model holding the D3 defaults.</summary>
		public static SettingsModel CreateDefaults()
		{
			return new SettingsModel(
				SettingsCatalog.DefaultValue(SettingId.HatchPressureBlocking),
				SettingsCatalog.DefaultValue(SettingId.AutoLeakBlocking),
				SettingsCatalog.DefaultValue(SettingId.AutoCarryWounded));
		}

		public bool HatchPressureBlocking
		{
			get { return hatchPressureBlocking; }
		}

		public bool AutoLeakBlocking
		{
			get { return autoLeakBlocking; }
		}

		public bool AutoCarryWounded
		{
			get { return autoCarryWounded; }
		}

		public bool Get(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return hatchPressureBlocking;
				case SettingId.AutoLeakBlocking:
					return autoLeakBlocking;
				case SettingId.AutoCarryWounded:
					return autoCarryWounded;
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}

		/// <summary>Sets a value. Returns true if it changed.</summary>
		public bool Set(SettingId id, bool value)
		{
			bool old = Get(id);
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					hatchPressureBlocking = value;
					break;
				case SettingId.AutoLeakBlocking:
					autoLeakBlocking = value;
					break;
				case SettingId.AutoCarryWounded:
					autoCarryWounded = value;
					break;
			}
			return old != value;
		}

		/// <summary>Flips a setting and returns its new value.</summary>
		public bool Toggle(SettingId id)
		{
			bool value = !Get(id);
			Set(id, value);
			return value;
		}

		/// <summary>The keyword of the toggle action for <paramref name="id"/> in the current state.</summary>
		public string ToggleKeyword(SettingId id)
		{
			return SettingsCatalog.ToggleKeyword(id, Get(id));
		}

		public SettingsModel Clone()
		{
			return new SettingsModel(hatchPressureBlocking, autoLeakBlocking, autoCarryWounded);
		}

		public override string ToString()
		{
			return "HatchPressureBlocking=" + hatchPressureBlocking + ", AutoLeakBlocking=" + autoLeakBlocking + ", AutoCarryWounded=" + autoCarryWounded;
		}
	}
}
