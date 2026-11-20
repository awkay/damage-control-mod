using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Core.Repair;
using DamageControl.Game.Hatches;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// Game side of R2 and R3: per-repair memory and the effects a vanilla <see cref="RepairAction"/> applies
	/// to its target while the repairer interacts (hatch seal source, leak-intensity throttle, one close of
	/// an open interior hatch). Nothing is registered with the game's execution queue: the vanilla action's
	/// own update drives it through <see cref="RepairUpdatePatch"/>, and <see cref="RepairStopPatch"/>
	/// releases everything. Main thread only.
	/// </summary>
	public static class RepairRuntime
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static UserSettings userSettings;
		#pragma warning restore 0649

		private static readonly Dictionary<RepairAction, RepairTrack> tracks = new Dictionary<RepairAction, RepairTrack>();
		private static bool initialized;

		private static void EnsureInitialized()
		{
			if (initialized)
				return;
			initialized = true;
			DcSettings.SessionStarted += OnSessionStarted;
		}

		private static void OnSessionStarted()
		{
			// HatchSeals.Shared is cleared by WS2 at the same moment.
			tracks.Clear();
		}

		/// <summary>One frame of a running repair, after vanilla's update (R2, R3).</summary>
		public static void Update(RepairAction action)
		{
			EnsureInitialized();
			GameActivator activator = action.Activator;
			Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
			if (equipment == null)
			{
				Stop(action);
				return;
			}
			PlayableCharacter character = action.Character;
			bool interacting = character != null && character.IsInteracting && action.Stage != CharacterActionStage.Completed;

			RepairTrack track;
			if (!tracks.TryGetValue(action, out track))
			{
				if (!interacting)
					return; // nothing applied yet, nothing to release
				track = new RepairTrack();
				tracks.Add(action, track);
			}

			Hatch hatch = equipment as Hatch;
			LeakableEquipment leakable = equipment as LeakableEquipment;
			Leaking leak = leakable != null ? equipment.GetStatus<Leaking>() : null;
			bool firstFrame = !track.Started;
			RepairFrame frame = RepairEffects.Step(track, interacting,
				hatch != null,
				hatch != null && hatch.IsExteriorHatch,
				hatch != null && !HatchRuntime.HasNoCompartments(hatch),
				hatch != null && hatch.Locked,
				leakable != null,
				leak != null && !leak.Hidden,
				action.Progress);

			if (frame.Release)
			{
				ReleaseEffects(action, track, hatch, leakable);
				return;
			}
			if (frame.CloseHatch)
			{
				// A close is never blocked by the H2 guard; after this H1 step 1 and H2 hold the hatch while
				// the repair status shows (C10). Preferring it closed keeps the crew from reopening it later.
				hatch.Locked = true;
				hatch.IsPreferredToBeLocked = true;
				DcLog.Debug("repair: closed hatch \"" + hatch.name + "\" for the repair");
			}
			if (frame.SealHatch)
				HatchSeals.Shared.Add(hatch, SealSource.Repair, action);
			if (frame.ThrottleLeak && leakable != null)
			{
				Parameter intensity = leakable.LeakIntensity;
				if (firstFrame)
					intensity.RemoveScaleModifiers(RepairText.LeakThrottleModifier); // left over from a save, say
				intensity.GetScaleModifier(RepairText.LeakThrottleModifier, true).Value = frame.ThrottleValue;
			}
		}

		/// <summary>The repair stopped: drop its seal and throttle and forget it.</summary>
		public static void Stop(RepairAction action)
		{
			EnsureInitialized();
			RepairTrack track;
			if (tracks.TryGetValue(action, out track))
			{
				GameActivator activator = action.Activator;
				Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
				ReleaseEffects(action, track, equipment as Hatch, equipment as LeakableEquipment);
				tracks.Remove(action);
			}
			HatchSeals.Shared.RemoveOwner(action);
		}

		private static void ReleaseEffects(RepairAction action, RepairTrack track, Hatch hatch, LeakableEquipment leakable)
		{
			if (hatch != null)
				HatchSeals.Shared.Remove(hatch, action);
			if (track.Throttles && leakable != null)
				leakable.LeakIntensity.RemoveScaleModifiers(RepairText.LeakThrottleModifier);
		}

		/// <summary>Vanilla's part rule (04 §2.1.6), read from the game.</summary>
		public static bool PartRequiredButMissing(Equipment equipment, PlayableCharacter character)
		{
			if (equipment == null || equipment.Data == null)
				return false;
			bool isNpc = character != null && character.Data.CharacterType == PlayableCharacterData.Type.NPC;
			bool inBackpack = character != null && character.Backpack.Item != null && character.Backpack.Item.name == RepairText.ReplacementParts;
			bool manualDelivery = userSettings != null && userSettings.GameplaySettings.manualItemDelivery;
			bool inStorage = false;
			if (!manualDelivery && BoatLayout.Ship != null && BoatLayout.Ship.MainStorage != null)
				inStorage = (BoatLayout.Ship.MainStorage.GetItem(RepairText.ReplacementParts) as UnityEngine.Object) != null;
			return RepairSlots.PartRequiredButMissing(equipment.Health, equipment.Data.RepairConsumesParts, isNpc, inBackpack, manualDelivery, inStorage);
		}
	}
}
