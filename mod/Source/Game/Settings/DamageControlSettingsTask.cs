using System;
using DamageControl.Core.Common;
using DamageControl.Core.Settings;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Core.Serialization;

namespace DamageControl.Game.Settings
{
	/// <summary>
	/// R9: the per-campaign settings store. Declared in our General sheet, worksheet "Background Tasks",
	/// with start event CampaignStart, so the game creates one per new campaign and writes its fields into
	/// the save. Saves made before the mod was installed get one after loading (the game re-runs missing
	/// CampaignStart tasks; <see cref="DcSettings"/> creates one if it did not). Extra instances are finished.
	///
	/// This class only stores the values (plain bool fields, so a renamed or removed field degrades to its
	/// default instead of breaking the save) and publishes itself as the active store. Logic lives in Core
	/// (<see cref="SettingsModel"/>); access goes through <see cref="DcSettings"/>.
	/// </summary>
	[Serializable]
	[FormerlySerializedName("DamageControl.Game.Settings.DamageControlSettingsTask")]
	public sealed class DamageControlSettingsTask : BackgroundTaskBase
	{
		/// <summary>Full type name as written in the data sheet.</summary>
		public const string TypeName = "DamageControl.Game.Settings.DamageControlSettingsTask";

		/// <summary>Row key in General/Background Tasks.</summary>
		public const string RowKey = "DamageControlSettings";

		// Saved fields. Initialisers give new instances the D3 defaults; on load the game overwrites them
		// with the saved values, and a field missing from an older save keeps its initialiser value.
		// The [FormerlySerializedName] alias above lets the save's type name resolve to this mod type.
		private bool hatchPressureBlocking = SettingsCatalog.DefaultValue(SettingId.HatchPressureBlocking);
		private bool autoLeakBlocking = SettingsCatalog.DefaultValue(SettingId.AutoLeakBlocking);
		private bool autoCarryWounded = SettingsCatalog.DefaultValue(SettingId.AutoCarryWounded);
		private bool diagnostics = SettingsCatalog.DefaultValue(SettingId.Diagnostics); // added after 0.1.0 saves: older saves keep this default (off)

		public override void Load(GameDataRow data)
		{
			// Nothing to read: the row only declares the task.
		}

		/// <summary>Fresh instance (new campaign, or created for a pre-mod save).</summary>
		public override void Start()
		{
			DcSettings.OnTaskStarted(this, SettingsTaskSource.Fresh);
		}

		/// <summary>Instance restored from a save.</summary>
		public override void Resume()
		{
			DcSettings.OnTaskStarted(this, SettingsTaskSource.Restored);
		}

		protected override void OnFinished()
		{
			DcSettings.OnTaskFinished(this);
		}

		internal SettingsModel ToModel()
		{
			return new SettingsModel(hatchPressureBlocking, autoLeakBlocking, autoCarryWounded, diagnostics);
		}

		internal void Store(SettingsModel model)
		{
			hatchPressureBlocking = model.HatchPressureBlocking;
			autoLeakBlocking = model.AutoLeakBlocking;
			autoCarryWounded = model.AutoCarryWounded;
			diagnostics = model.Diagnostics;
		}
	}
}
