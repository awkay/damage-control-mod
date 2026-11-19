using System;
using System.Collections.Generic;
using DamageControl.Core.Ballast;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Ballast
{
	/// <summary>
	/// B2 Exhaust Blow (03 §2.A, 00 D5): a manual order at the flooding-and-blowing valve station. The
	/// crewman takes the valve over (manual blowing, turned off, so vanilla stops driving the tanks) and leads
	/// diesel exhaust into the main ballast until it is dry. No bottle air is used.
	///
	/// Available only while not docked, the diesel runs and the deck is above water; a running blow is
	/// cancelled when one of those fails for longer than a short grace (03 §7.4, D7). No automation in v1:
	/// nothing queues this action by itself.
	/// </summary>
	[Serializable]
	[FormerlySerializedName("DamageControl.Game.Ballast.ExhaustBlowAction")]
	public class ExhaustBlowAction : DeviceUseAction
	{
		public const string Keyword = "DC Exhaust Blow";
		public const string WarningDieselNotRunning = "Actions/DC Warning Diesel Not Running";
		public const string WarningDeckUnderWater = "Actions/DC Warning Deck Under Water";
		public const string WarningBallastEmpty = "Actions/DC Warning Ballast Empty";

		private const string SlotTag = "Use";
		private const string FallbackSlotTag = "Default";

		// Main-thread scratch buffers shared by every instance (no per-frame allocation).
		private static readonly List<double> water = new List<double>();
		private static readonly List<float> capacity = new List<float>();
		private static float[] air = new float[8];

		// Not saved (03 §4): a load mid-blow takes the valve over again on the first interacting frame.
		[NonSerialized]
		private bool tookOverValve;

		[NonSerialized]
		private ExhaustBlowGateWatch gateWatch;

		public ExhaustBlowAction(GameDataRow data)
			: base(data)
		{
		}

		public ExhaustBlowAction(ExhaustBlowAction source, GameActivator activator)
			: base(source, activator)
		{
		}

		public override string GetLocalizationKeyword(GameActivator activator, PlayableCharacter character)
		{
			return Keyword;
		}

		/// <summary>03 §2.A: sorting priority 1 among the valve orders.</summary>
		public override int GetSortingPriority(GameActivator activator, PlayableCharacter character)
		{
			return 1;
		}

		public override ActionStateData GetState(GameActivator activator, PlayableCharacter character, bool fillDetails, bool isPlayerOrder, ActionContext actionContext)
		{
			try
			{
				FloodingAndBlowingValve valve = activator != null ? activator.AssociatedEquipment as FloodingAndBlowingValve : null;
				if (valve == null || !BoatLayout.Require("Exhaust Blow") || BoatLayout.BallastTanks.Count == 0)
					return ActionState.Hidden;

				ExhaustBlowConditions conditions;
				if (!TryReadConditions(out conditions))
					return ActionState.Hidden;
				ExhaustBlowBlock block = ExhaustBlow.Availability(conditions, ExhaustBlowTunables.Current);
				if (block != ExhaustBlowBlock.None)
					return new ActionStateData(ActionState.Disabled, fillDetails ? WarningFor(block) : null);

				ActionStateData result = CheckActivatorUseConditions(activator, character,
					ActivatorUseFlags.RequireWorkingDevice | ActivatorUseFlags.DisallowDuplicateActions | ActivatorUseFlags.CheckForBlockingItems, fillDetails);
				if (result.ActionState <= ActionState.Disabled)
					return result;
				// 03 §2.A: the valve wheel can be worked from a flooded slot.
				return CheckInteractableUseConditions(valve.GetComponent<Interactable>(), SlotTag, FallbackSlotTag, character, SlotUsageFlags.AllowUnderWaterAlways, fillDetails, isPlayerOrder);
			}
			catch (Exception e)
			{
				DcLog.Exception("ExhaustBlowAction.GetState", e);
				return ActionState.Hidden;
			}
		}

		protected override void StartMainCharacter(PlayableCharacter character)
		{
			tookOverValve = false;
			GateWatch.Reset();
			UseDevice(character, SlotTag, null, int.MinValue, null, false, SlotUsageFlags.AllowUnderWaterAlways);
		}

		protected override void StopMainCharacter(PlayableCharacter character)
		{
			try
			{
				// Leave the valve in its resting post-blow state, still turned off (it was off for the whole blow).
				if (tookOverValve)
				{
					FloodingAndBlowingValve valve = Valve;
					if (valve != null)
					{
						valve.Mode = FloodingAndBlowingValve.OperatingMode.OptimalBlowing;
						valve.SetDisableFlag(DeviceDisableFlag.TurnedOff, true);
					}
				}
			}
			catch (Exception e)
			{
				DcLog.Exception("ExhaustBlowAction.StopMainCharacter", e);
			}
			tookOverValve = false;
			base.StopMainCharacter(character);
		}

		public override void UpdateAction()
		{
			PlayableCharacter character = Character;
			if (Stage != CharacterActionStage.Completed && character != null)
			{
				try
				{
					Tick(character);
				}
				catch (Exception e)
				{
					DcLog.Exception("ExhaustBlowAction.UpdateAction", e);
				}
			}
			// Our progress stays below 1, so the base never completes the action on its own; it only does
			// its bookkeeping (listener removal once completed).
			base.UpdateAction();
		}

		protected override void OnDeviceUsed()
		{
		}

		private ExhaustBlowGateWatch GateWatch
		{
			get
			{
				if (gateWatch == null)
					gateWatch = new ExhaustBlowGateWatch();
				return gateWatch;
			}
		}

		private FloodingAndBlowingValve Valve
		{
			get
			{
				GameActivator activator = Activator;
				return activator != null ? activator.AssociatedEquipment as FloodingAndBlowingValve : null;
			}
		}

		private void Tick(PlayableCharacter character)
		{
			ExhaustBlowSettings settings = ExhaustBlowTunables.Current;
			IReadOnlyList<Tank> tanks = BoatLayout.BallastTanks;
			ExhaustBlowConditions conditions;
			if (!BoatLayout.IsKnown || tanks.Count == 0 || !TryReadConditions(out conditions))
			{
				character.CancelOrders();
				return;
			}
			ReadTanks(tanks);

			// 03 §2.A: progress runs every frame, also while walking to the valve.
			float progress = ExhaustBlow.Progress(water, capacity);
			Progress = progress;
			character.SkillProgress = progress;

			float dt = DcMath.Clamp(GameClock.Now().GameDt, 0f, settings.MaxStepSeconds);
			if (GateWatch.Update(ExhaustBlow.GatesHold(conditions, settings), dt, settings.CancelGraceSeconds))
			{
				DcLog.Debug("exhaust blow cancelled: " + ExhaustBlow.Availability(conditions, settings));
				character.CancelOrders();
				return;
			}

			if (!character.IsInteracting)
				return;

			FloodingAndBlowingValve valve = Valve;
			if (valve == null)
			{
				character.CancelOrders();
				return;
			}
			if (!tookOverValve)
			{
				valve.Mode = FloodingAndBlowingValve.OperatingMode.ManualBlowing;
				valve.SetDisableFlag(DeviceDisableFlag.TurnedOff, true);
				tookOverValve = true;
			}

			if (ExhaustBlow.IsComplete(ExhaustBlow.TotalWater(water), settings.CompletionWater))
			{
				Finish(valve);
				return;
			}

			float flow = ExhaustBlow.Flow(conditions.DieselEnabled, conditions.DieselRpm, dt, settings.FlowPerRevolution);
			if (air.Length < water.Count)
				air = new float[water.Count];
			ExhaustBlow.Distribute(flow, water, air, settings.Stage2WaterThreshold);
			for (int i = 0; i < tanks.Count; i++)
			{
				if (air[i] > 0f && tanks[i] != null)
					tanks[i].Air += air[i];
			}
		}

		/// <summary>03 §2.A completion: valve to optimal blowing and off, depth preset Surface, action complete.</summary>
		private void Finish(FloodingAndBlowingValve valve)
		{
			valve.Mode = FloodingAndBlowingValve.OperatingMode.OptimalBlowing;
			valve.SetDisableFlag(DeviceDisableFlag.TurnedOff, true);
			tookOverValve = false;
			PlayerShip ship = BoatLayout.Ship;
			if (ship != null && ship.DepthPreset != DepthPreset.Surface)
			{
				if (IsDirectPlayerOrder)
					ship.FollowDiveSchedule = false;
				ship.SetDepthPreset(DepthPreset.Surface, true);
			}
			DcLog.Debug("exhaust blow complete");
			CompleteAction();
		}

		private static void ReadTanks(IReadOnlyList<Tank> tanks)
		{
			water.Clear();
			capacity.Clear();
			for (int i = 0; i < tanks.Count; i++)
			{
				Tank tank = tanks[i];
				bool alive = tank != null;
				water.Add(alive ? tank.Water : 0.0);
				capacity.Add(alive ? tank.Capacity : 0f);
			}
		}

		private static bool TryReadConditions(out ExhaustBlowConditions conditions)
		{
			conditions = new ExhaustBlowConditions();
			PlayerShip ship = BoatLayout.Ship;
			if (ship == null)
				return false;
			conditions.Docked = ship.Docked;
			PlayerShipEngine diesel = ship.DieselEngine;
			bool dieselAlive = diesel != null;
			conditions.DieselEnabled = dieselAlive && diesel.enabled;
			conditions.DieselRpm = dieselAlive ? diesel.CurrentSignedRPM : 0f;
			conditions.DeckDepth = ship.DeckDepthUnclamped;
			double total = 0;
			IReadOnlyList<Tank> tanks = BoatLayout.BallastTanks;
			for (int i = 0; i < tanks.Count; i++)
			{
				if (tanks[i] != null && tanks[i].Water > 0)
					total += tanks[i].Water;
			}
			conditions.BallastWater = total;
			return true;
		}

		private static LocalizedString WarningFor(ExhaustBlowBlock block)
		{
			switch (block)
			{
				case ExhaustBlowBlock.DieselNotRunning:
					return new LocalizedString(WarningDieselNotRunning);
				case ExhaustBlowBlock.DeckUnderWater:
					return new LocalizedString(WarningDeckUnderWater);
				case ExhaustBlowBlock.BallastEmpty:
					return new LocalizedString(WarningBallastEmpty);
				default:
					return null;
			}
		}
	}
}
