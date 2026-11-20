using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Core.Leaks;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// H8: when a block-leak order stops for any reason, drop every hatch seal it holds (02 §2.6). Vanilla's
	/// own stop code still runs: it removes the "is being repaired" status and the leak-intensity modifier
	/// (or our detached placeholder, see <see cref="BlockLeakUpdatePatch"/>).
	///
	/// Vanilla removes that status whoever added it. Since P8 lets a blocker and a repairer share a target,
	/// the prefix remembers a status that belongs to another crewman's running repair and the postfix puts
	/// the same status object back, so the repair keeps its status and a hatch keeps its repair hold (C10).
	/// </summary>
	[HarmonyPatch(typeof(BlockLeakAction), "StopMainCharacter")]
	internal static class BlockLeakStopPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("BlockLeakStop", ex);
		}

		private static void Prefix(BlockLeakAction __instance, PlayableCharacter character, out IsBeingRepaired __state)
		{
			__state = null;
			try
			{
				GameActivator activator = __instance.Activator;
				Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
				if (equipment == null)
					return;
				IsBeingRepaired status = equipment.GetStatus<IsBeingRepaired>();
				if (status != null && OtherRepairRunning(activator, character))
					__state = status;
			}
			catch (Exception e)
			{
				__state = null;
				DcLog.PatchFailed("BlockLeakStop", e);
			}
		}

		private static void Postfix(BlockLeakAction __instance, PlayableCharacter character, IsBeingRepaired __state)
		{
			try
			{
				if (HatchSeals.Shared.RemoveOwner(__instance) > 0)
					DcLog.Debug("block leak: " + (character != null ? character.name : "crewman") + " released a hatch seal");
				if (__state == null)
					return;
				GameActivator activator = __instance.Activator;
				Equipment equipment = activator != null ? activator.AssociatedEquipment : null;
				if (equipment == null)
					return;
				if (LeakBlockRules.RestoresRepairStatus(true, OtherRepairRunning(activator, character), equipment.GetStatus<IsBeingRepaired>() != null))
				{
					equipment.AddStatus(__state);
					DcLog.Debug("block leak: kept the running repair's status on \"" + equipment.name + "\"");
				}
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakStop", e);
			}
		}

		/// <summary>Another crewman on this activator is running a repair order.</summary>
		private static bool OtherRepairRunning(GameActivator activator, PlayableCharacter character)
		{
			List<PlayableCharacter> users = activator.Users;
			if (users == null)
				return false;
			for (int i = users.Count - 1; i >= 0; i--)
			{
				PlayableCharacter user = users[i];
				if (user != null && user != character && user.Action is RepairAction)
					return true;
			}
			return false;
		}
	}
}
