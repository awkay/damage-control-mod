using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Game.Infrastructure;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// P11: "equalize pressure" order on a closed interior hatch (01 §2.11). The crewman works the hatch's
	/// default slot and, while he is interacting, bleeds air and water through it with the shared kernel
	/// (equalize profile, vent at highY, valve at lowY; C3). As soon as the hatch can be opened he opens it;
	/// once the hatch is open the order is cancelled with the game's standard delay. It never completes on
	/// its own.
	/// </summary>
	[Serializable]
	public class EqualizePressureAction : DeviceUseAction
	{
		public EqualizePressureAction(GameDataRow data)
			: base(data)
		{
		}

		public EqualizePressureAction(EqualizePressureAction source, GameActivator activator)
			: base(source, activator)
		{
		}

		public override string GetLocalizationKeyword(GameActivator activator, PlayableCharacter character)
		{
			return HatchText.EqualizeKeyword;
		}

		public override int GetSortingPriority(GameActivator activator, PlayableCharacter character)
		{
			return EqualizeRules.SortingPriority;
		}

		public override ActionStateData GetState(GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
		{
			try
			{
				Hatch hatch = activator != null ? activator.AssociatedEquipment as Hatch : null;
				if (hatch == null || !EqualizeRules.IsOffered(true, hatch.IsExteriorHatch, hatch.Locked,
					hatch.LeftCompartment != null, hatch.RightCompartment != null, hatch.PressureBlockadeCompartment != null))
					return ActionState.Hidden;
				ActionStateData result = CheckActivatorUseConditions(activator, character,
					ActivatorUseFlags.DisallowDuplicateActions | ActivatorUseFlags.AlwaysAllowSailorWithoutOfficer, fillDetails);
				if (result.ActionState <= ActionState.Disabled)
					return result;
				Interactable interactable = hatch.GetComponent<Interactable>();
				if (interactable == null)
					return ActionState.Hidden;
				return CheckInteractableUseConditions(interactable, "Default", character, SlotUsageFlags.AllowUnderWaterWithRebreather, fillDetails, isPlayerOrder);
			}
			catch (Exception e)
			{
				DcLog.Exception("EqualizePressureAction.GetState", e);
				return ActionState.Hidden;
			}
		}

		protected override void StartMainCharacter(PlayableCharacter character)
		{
			UseDevice(character, "Default", "Repair", int.MinValue);
		}

		public override void UpdateAction()
		{
			try
			{
				PlayableCharacter character = Character;
				Hatch hatch = Activator != null ? Activator.AssociatedEquipment as Hatch : null;
				if (character != null && hatch != null)
				{
					if (!hatch.Locked)
						character.CancelOrders();
					else if (character.IsInteracting)
						Work(hatch);
					// The order lasts until the hatch is open: keep the use animation from completing it.
					if (character.SkillProgress > 0.99f)
						character.SkillProgress = 0.99f;
				}
			}
			catch (Exception e)
			{
				DcLog.Exception("EqualizePressureAction.UpdateAction", e);
			}
			base.UpdateAction();
		}

		protected override void OnDeviceUsed()
		{
		}

		private static void Work(Hatch hatch)
		{
			Compartment left = hatch.LeftCompartment;
			Compartment right = hatch.RightCompartment;
			HatchGeometry geometry;
			if (left == null || right == null || !BoatLayout.TryGetGeometry(hatch, out geometry))
				return;
			if (!HatchRuntime.SameWaterBody(left, right))
			{
				float k = HatchExchange.EqualizeStep(GameClock.Now(), HatchExchange.DefaultRate);
				HatchFlow flow = HatchExchange.Equalize(HatchRuntime.ReadSide(left), HatchRuntime.ReadSide(right), geometry, k);
				HatchRuntime.Apply(left, right, flow);
			}
			if (hatch.CanBeOpened)
			{
				hatch.Locked = false;
				hatch.IsPreferredToBeLocked = false;
				DcLog.Debug("equalize: hatch \"" + hatch.name + "\" opened");
			}
		}
	}
}
