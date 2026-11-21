using System;
using Cysharp.Threading.Tasks;
using DamageControl.Core.Common;
using DamageControl.Core.Settings;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Settings
{
	/// <summary>
	/// One of the settings toggles (P14, H12, H13 and diagnostics toggle actions). Rows in Crew/Actions name this
	/// type and pick the setting with the row parameter "Setting = &lt;SettingId&gt;". Hosted on the vanilla
	/// Workshop activator (D2), contexts Normal, Queue and NoCharacter (C6).
	///
	/// Always enabled while a campaign is active; instant: pre-start flips the setting and declines to
	/// queue, so no crewman walks anywhere. The label names what choosing it will do.
	/// </summary>
	[Serializable]
	public class SettingToggleAction : SingleCharacterAction
	{
		private readonly SettingId setting;
		private readonly bool configured;

		public SettingToggleAction(GameDataRow data)
			: base(data)
		{
			string value = data != null ? data.GetString(SettingsCatalog.ToggleRowParameter) : null;
			configured = SettingsCatalog.TryParse(value, out setting);
			if (!configured)
			{
				DcLog.Error("Crew/Actions row " + (data != null ? data.Path : "(null)") + " needs \""
					+ SettingsCatalog.ToggleRowParameter + " = <" + string.Join("|", Array.ConvertAll(SettingsCatalog.All, s => s.ToString())) + ">\"; got \"" + value + "\"");
			}
		}

		public SettingToggleAction(SettingToggleAction source, GameActivator activator)
			: base(source, activator)
		{
			setting = source.setting;
			configured = source.configured;
		}

		/// <summary>The setting this row toggles.</summary>
		public SettingId Setting
		{
			get { return setting; }
		}

		public override ActionStateData GetState(GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
		{
			if (!configured || !DcSettings.IsCampaignActive)
				return ActionState.Hidden;
			return ActionState.Enabled;
		}

		public override string GetLocalizationKeyword(GameActivator activator, PlayableCharacter character)
		{
			return DcSettings.ToggleKeyword(setting);
		}

		/// <summary>Listed after the workshop's own orders.</summary>
		public override int GetSortingPriority(GameActivator activator, PlayableCharacter character)
		{
			return -100;
		}

		public override UniTask<bool> OnPreStart(PlayableCharacter character)
		{
			try
			{
				if (configured)
					DcSettings.Toggle(setting);
			}
			catch (Exception e)
			{
				DcLog.Exception("SettingToggleAction " + setting, e);
			}
			// Never queue: the toggle has already happened.
			return UniTask.FromResult(false);
		}
	}
}
