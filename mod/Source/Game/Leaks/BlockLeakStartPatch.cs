using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Leaks;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// P8: a block-leak order joins a partner instead of replacing him. Vanilla's start stops every other
	/// crewman blocking the same equipment before it walks the new one to a slot, and stops a repairer there
	/// when the equipment takes one character. So with one man already blocking, ordering a second only
	/// swapped them.
	///
	/// The prefix first widens the target (<see cref="SlotCrowdingRuntime.Widen"/>), before vanilla's slot
	/// reservation and its one-character rule. On a shared target it then takes the blockers that may stay
	/// (<see cref="SlotCrowding.BlockersToKeep"/>) out of the activator's user list for the duration of
	/// vanilla's start, so vanilla's replacement loop passes them by; the finalizer puts them back whatever
	/// happens. Everything else in vanilla's start runs unchanged.
	/// </summary>
	[HarmonyPatch(typeof(BlockLeakAction), "StartMainCharacter")]
	internal static class BlockLeakStartPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("BlockLeakStart", ex);
		}

		private static void Prefix(BlockLeakAction __instance, PlayableCharacter character, out List<PlayableCharacter> __state)
		{
			__state = null;
			try
			{
				GameActivator activator = __instance.Activator;
				Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
				Interactable interactable = equipment != null ? equipment.GetComponent<Interactable>() : null;
				if (interactable == null || character == null)
					return;
				SlotCrowdingRuntime.Widen(interactable);
				if (!SlotCrowdingRuntime.IsShared(interactable))
					return;
				List<PlayableCharacter> users = activator.Users;
				int blockers;
				int repairers;
				SlotCrowdingRuntime.CountWorkers(users, character, out blockers, out repairers);
				bool freeSlot = SlotCrowdingRuntime.HasFreeLeakSlot(interactable, character.HasRebreather);
				int keep = SlotCrowding.BlockersToKeep(blockers, repairers, freeSlot);
				if (keep <= 0)
					return;
				__state = new List<PlayableCharacter>(keep);
				for (int i = users.Count - 1; i >= 0 && __state.Count < keep; i--)
				{
					PlayableCharacter user = users[i];
					if (user != null && user != character && user.Action is BlockLeakAction)
					{
						__state.Add(user);
						users.RemoveAt(i);
					}
				}
				DcLog.Debug("block leak: " + character.name + " joins " + __state[0].name + " on \"" + equipment.name + "\"");
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakStart", e);
			}
		}

		private static Exception Finalizer(Exception __exception, BlockLeakAction __instance, List<PlayableCharacter> __state)
		{
			if (__state == null || __state.Count == 0)
				return __exception;
			try
			{
				GameActivator activator = __instance.Activator;
				List<PlayableCharacter> users = activator != null ? activator.Users : null;
				if (users != null)
				{
					for (int i = __state.Count - 1; i >= 0; i--)
					{
						PlayableCharacter kept = __state[i];
						if (kept != null && kept.Action != null && kept.Action.Activator == activator && !users.Contains(kept))
							users.Add(kept);
					}
				}
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakStart", e);
			}
			return __exception;
		}
	}
}
