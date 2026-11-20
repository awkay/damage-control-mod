using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Repair;
using DamageControl.Game.Infrastructure;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core;
using UBOAT.Game.Core.AI.Behaviours;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Utilities;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// R6: "dismantle for scrap" (04 §2.3 with the Q3 checks). An engineer takes a working device apart one
	/// health point at a time at repair speed and recovers scrap into the main storage when it reaches 0.
	/// The device is left broken. Unlike the original it is not offered while someone repairs the device,
	/// while the same order is already running there, while the device is out of action, or when no slot
	/// can be reached (flooded slots need a rebreather), and a non-integer remainder always ends the order.
	/// It grants no XP, adds no repair status and does not touch leaks.
	/// </summary>
	[Serializable]
	public class DismantleAction : DeviceUseAction
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static PlayerShip playerShip;
		#pragma warning restore 0649

		// Saved with the order (the game serializes every instance field).
		private float pointProgress;
		private float totalProgress;
		private float totalWork;
		private float performanceMultiplier = 1f;
		private bool finished;

		public DismantleAction(GameDataRow data)
			: base(data)
		{
		}

		public DismantleAction(DismantleAction source, GameActivator activator)
			: base(source, activator)
		{
		}

		public override string GetLocalizationKeyword(GameActivator activator, PlayableCharacter character)
		{
			return RepairText.DismantleKeyword;
		}

		public override int GetSortingPriority(GameActivator activator, PlayableCharacter character)
		{
			return Dismantle.SortingPriority;
		}

		public override ActionStateData GetState(GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
		{
			try
			{
				Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
				Interactable interactable = equipment != null ? equipment.GetComponent<Interactable>() : null;
				if (!Dismantle.IsOffered(equipment != null, interactable != null, equipment != null ? equipment.Health : 0f))
					return ActionState.Hidden;
				List<PlayableCharacter> users = activator.Users;
				for (int i = users.Count - 1; i >= 0; i--)
				{
					if (users[i] != character && users[i].Action is RepairAction)
						return new ActionStateData(ActionState.Disabled, fillDetails ? new LocalizedString(RepairText.AlreadyBeingRepaired, users[i].GetRichTextName()) : null);
				}
				ActivatorUseFlags flags = ActivatorUseFlags.CheckForBlockingItems | ActivatorUseFlags.DisallowDuplicateActions;
				if (equipment is Device)
					flags |= ActivatorUseFlags.RequireWorkingDevice;
				ActionStateData result = CheckActivatorUseConditions(activator, character, flags, fillDetails);
				if (result.ActionState <= ActionState.Disabled)
					return result;
				if (equipment is Hatch)
					return CheckInteractableUseConditions(interactable, RepairText.SlotDefault, character, SlotUsageFlags.AllowUnderWaterWithRebreather, fillDetails, isPlayerOrder);
				return CheckInteractableUseConditions(interactable, RepairText.SlotRepair, RepairText.SlotDefault, character, SlotUsageFlags.AllowUnderWaterWithRebreather, fillDetails, isPlayerOrder);
			}
			catch (Exception e)
			{
				DcLog.Exception("DismantleAction.GetState", e);
				return ActionState.Hidden;
			}
		}

		protected override void StartMainCharacter(PlayableCharacter character)
		{
			Equipment equipment = Activator.AssociatedEquipment;
			performanceMultiplier = Dismantle.PerformanceMultiplier(character.Data.HasStatus(RepairText.PerkHandyman));
			AllowTransitionToStageView = false;
			if (totalWork <= 0f)
			{
				DismantleState begin = Dismantle.Begin(equipment != null ? equipment.Health : 0f);
				pointProgress = begin.PointProgress;
				totalProgress = begin.TotalProgress;
				totalWork = begin.TotalWork;
			}
			string slot = equipment is Hatch ? RepairText.SlotDefault : RepairText.SlotRepair;
			CompoundBehaviour behaviour = UseDevice(character, slot, RepairText.RepairAnimation, int.MinValue);
			InteractionBehaviour interaction = behaviour != null ? behaviour.GetBehaviour<InteractionBehaviour>() : null;
			if (interaction != null)
				interaction.InteractionType = RepairSlots.ClampInteractionType(interaction.InteractionType);
		}

		public override void UpdateAction()
		{
			try
			{
				PlayableCharacter character = Character;
				Equipment equipment = Activator != null ? Activator.AssociatedEquipment : null;
				if (character != null && equipment != null && character.IsInteracting && !finished)
				{
					float repairTime = equipment.Data != null ? equipment.Data.RepairTime : 1f;
					float rate = Dismantle.RatePerSecond(character.Performance, performanceMultiplier, repairTime);
					DismantleState state = new DismantleState { PointProgress = pointProgress, TotalProgress = totalProgress, TotalWork = totalWork };
					DismantleStep step = Dismantle.Advance(state, equipment.Health, rate, GameClock.Now().GameDt);
					pointProgress = step.State.PointProgress;
					totalProgress = step.State.TotalProgress;
					totalWork = step.State.TotalWork;
					if (!DcMath.Approximately(step.Health, equipment.Health))
						equipment.Health = step.Health;
					if (step.Finished)
					{
						finished = true;
						if (step.YieldsScrap)
							YieldScrap(character, equipment);
						character.CancelOrders();
						return;
					}
					Progress = step.Progress;
					character.SkillProgress = step.Progress;
				}
			}
			catch (Exception e)
			{
				DcLog.Exception("DismantleAction.UpdateAction", e);
			}
			base.UpdateAction();
		}

		protected override void OnDeviceUsed()
		{
		}

		private static void YieldScrap(PlayableCharacter character, Equipment equipment)
		{
			int count = Dismantle.ScrapYield(character.Data.HasStatus(RepairText.PerkThrifty));
			if (playerShip == null || playerShip.MainStorage == null)
			{
				DcLog.WarnOnce("DismantleNoStorage", "dismantle: no main storage; the scrap from \"" + equipment.name + "\" is lost");
				return;
			}
			for (int i = 0; i < count; i++)
			{
				// The game falls back to the crewman's hands when the main storage is full (Q4).
				if (!StorageUtility.GenerateItem(character, playerShip.MainStorage, RepairText.Scrap))
				{
					DcLog.Info("dismantle: no room for scrap from \"" + equipment.name + "\" in the main storage or the crewman's hands");
					return;
				}
			}
			DcLog.Debug("dismantle: \"" + equipment.name + "\" taken apart for " + count + " scrap");
		}
	}
}
