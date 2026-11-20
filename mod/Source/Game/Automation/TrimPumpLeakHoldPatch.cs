using System;
using System.Collections.Generic;
using DamageControl.Core.Automation;
using DamageControl.Core.Common;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using UBOAT.Game.Scene.Tasks;

namespace DamageControl.Game.Automation
{
	/// <summary>
	/// H14 (02 §2.11, 00 C7): vanilla's automatic-actions controller turns the trim pump off once it has
	/// had no water to pump for a sustained delay. While any equipment on the boat leaks, this patch holds
	/// the controller's private pump deadline at 0 before and after each controller tick, so that deadline
	/// never matures and the delay starts afresh after the last leak is stopped. Nothing else in the
	/// controller is touched (no replacement).
	/// </summary>
	[HarmonyPatch(typeof(AutomaticActionsController), "DoUpdate")]
	internal static class TrimPumpLeakHoldPatch
	{
		private static void Prefix(ref long ___pumpDelay, out bool __state)
		{
			__state = false;
			try
			{
				__state = AnyLeak(CrewAutomation.Ship);
				___pumpDelay = AutomationGates.PumpTurnOffDeadline(___pumpDelay, __state);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("TrimPumpLeakHold", e);
			}
		}

		private static void Postfix(ref long ___pumpDelay, bool __state)
		{
			try
			{
				___pumpDelay = AutomationGates.PumpTurnOffDeadline(___pumpDelay, __state);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("TrimPumpLeakHold", e);
			}
		}

		private static bool AnyLeak(PlayerShip ship)
		{
			if (ship == null)
				return false;
			List<Equipment> equipment = ship.Equipment;
			if (equipment == null)
				return false;
			for (int i = equipment.Count - 1; i >= 0; i--)
			{
				Equipment item = equipment[i];
				if (item != null && item.HasStatus<Leaking>())
					return true;
			}
			return false;
		}
	}
}
