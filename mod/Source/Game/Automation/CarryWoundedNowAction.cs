using System;
using Cysharp.Threading.Tasks;
using DamageControl.Core.Automation;
using DamageControl.Core.Common;
using DamageControl.Game.Settings;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Automation
{
	/// <summary>
	/// H13 trigger (02 §2.10): "carry the wounded now", on the vanilla light switch. Instant: pre-start arms
	/// the one-shot of <see cref="CarryWoundedGate"/> and declines to queue, so no crewman walks for the
	/// order itself; vanilla's carry routine then runs until nobody is left lying injured. Offered only
	/// while automatic carrying is off; enabled while at least one crewman lies injured. Not listed in the
	/// orders (tab) menu.
	/// </summary>
	[Serializable]
	public class CarryWoundedNowAction : SingleCharacterAction
	{
		public CarryWoundedNowAction(GameDataRow data)
			: base(data)
		{
		}

		public CarryWoundedNowAction(CarryWoundedNowAction source, GameActivator activator)
			: base(source, activator)
		{
		}

		public override ActionStateData GetState(GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
		{
			try
			{
				switch (CarryWoundedGate.Availability(DcSettings.IsCampaignActive, DcSettings.AutoCarryWounded, CrewAutomation.InjuredLying))
				{
					case TriggerAvailability.Enabled:
						return ActionState.Enabled;
					case TriggerAvailability.Disabled:
						return ActionState.Disabled;
					default:
						return ActionState.Hidden;
				}
			}
			catch (Exception e)
			{
				DcLog.Exception("CarryWoundedNowAction.GetState", e);
				return ActionState.Hidden;
			}
		}

		public override string GetLocalizationKeyword(GameActivator activator, PlayableCharacter character)
		{
			return CarryWoundedGate.TriggerKeyword;
		}

		/// <summary>After the light switch's own orders.</summary>
		public override int GetSortingPriority(GameActivator activator, PlayableCharacter character)
		{
			return -100;
		}

		public override bool ShowInTabMenuForActivator(GameActivator activator)
		{
			return false;
		}

		public override UniTask<bool> OnPreStart(PlayableCharacter character)
		{
			try
			{
				CrewAutomation.Gate.Arm();
				DcLog.Info("carry wounded: order given; the crew will carry the injured until none are left lying");
			}
			catch (Exception e)
			{
				DcLog.Exception("CarryWoundedNowAction.OnPreStart", e);
			}
			// Never queue: the order has already taken effect.
			return UniTask.FromResult(false);
		}
	}
}
