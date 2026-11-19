using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// H3: vanilla opens a hatch when it is disabled during play. If the hatch was closed and the seat
	/// side still pushes harder than the far side, the mod keeps it shut and makes it leak instead, so the
	/// physical seal (H4) keeps working; otherwise vanilla's opening stands. Only during gameplay (not on
	/// quit or while leaving to the menu).
	/// </summary>
	[HarmonyPatch(typeof(Hatch), "OnDisable")]
	internal static class HatchOnDisablePatch
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameState gameState;
		#pragma warning restore 0649

		private static void Prefix(Hatch __instance, out bool __state)
		{
			__state = false;
			try
			{
				__state = __instance.Locked;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchOnDisable.Prefix", e);
			}
		}

		private static void Postfix(Hatch __instance, bool __state)
		{
			try
			{
				if (gameState == null || gameState.CurrentState != GameState.State.Gameplay)
					return;
				Compartment seat = __instance.PressureBlockadeCompartment;
				Compartment far = HatchRuntime.FarSide(__instance, seat);
				if (seat == null || far == null)
					return;
				if (!HatchDamageRules.StaysShutWhenDisabled(__state, seat.AirPressure, far.AirPressure))
					return;
				if (!__instance.Locked)
					HatchRuntime.SetLockedUnguarded(__instance, true);
				if (!__instance.HasStatus<Leaking>())
					__instance.AddStatus<Leaking>();
				DcLog.Debug("hatch \"" + __instance.name + "\" disabled under pressure: kept shut and leaking");
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchOnDisable", e);
			}
		}
	}
}
