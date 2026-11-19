using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P1 (01 §2.1, 00 §4): before the game simulates a breach into a compartment, raise the
	/// compartment's air to the floor in <see cref="BreachAirFloor"/>. Prefix only; vanilla always runs.
	/// Runs per hole per physics step, so the common case is one type check.
	/// </summary>
	[HarmonyPatch(typeof(Tank), "SimulateBreach")]
	internal static class BreachAirFloorPatch
	{
		private static void Prefix(Tank __instance, ExternalPressure breach)
		{
			try
			{
				if (breach == null || __instance.TankType != TankType.Compartment)
					return;
				double air = __instance.Air;
				double floored = BreachAirFloor.Apply(true, breach.Openess, air);
				if (floored > air || double.IsNaN(air))
					__instance.Air = floored;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BreachAirFloor", e);
			}
		}
	}
}
