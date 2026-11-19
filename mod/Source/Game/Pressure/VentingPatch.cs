using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P9 (01 §2.9, 00 §4): after each physics step of the player ship, vent the conning tower and the
	/// diesel compartment toward 1 atm while they connect to the atmosphere, and let a running diesel
	/// draw air from its compartment (<see cref="Venting"/>). Uses the step's game time
	/// (GameClock.FixedNow). Compartments come from <see cref="PressureLayout"/>; when the layout is
	/// unknown the feature stays off.
	/// </summary>
	[HarmonyPatch(typeof(PlayerShip), "FixedUpdate")]
	internal static class VentingPatch
	{
		private static void Postfix(PlayerShip __instance)
		{
			try
			{
				if (__instance == null || __instance != BoatLayout.Ship)
					return;
				if (!BoatLayout.Require("Atmospheric venting"))
					return;
				Compartment tower = PressureLayout.ConningTower;
				Compartment diesel = PressureLayout.DieselCompartment;
				if (tower == null && diesel == null)
					return;
				SimClock clock = GameClock.FixedNow();
				if (clock.IsPaused)
					return;

				var input = new VentingInput();
				input.Dt = clock.GameDt;
				input.DeckDepth = __instance.DeckDepth;
				input.TowerPressure = tower != null ? tower.AirPressure : double.NaN;
				input.TowerFreeVolume = PressureLayout.FreeVolume(tower);
				Hatch exterior = BoatLayout.ExteriorHatch;
				input.ExteriorHatchOpen = exterior != null && !exterior.Locked;
				input.DieselPressure = diesel != null ? diesel.AirPressure : double.NaN;
				input.DieselFreeVolume = PressureLayout.FreeVolume(diesel);

				ShipEquipmentSlot snorkelSlot = __instance.SnorkelSlot;
				Snorkel snorkel = snorkelSlot != null ? snorkelSlot.AssignedDevice as Snorkel : null;
				input.HasSnorkel = snorkel != null;
				input.SnorkelPerformance = snorkel != null ? snorkel.Performance.Value : 0f;

				PlayerShipEngine dieselEngine = __instance.DieselEngine;
				input.DieselActive = diesel != null && dieselEngine != null && __instance.ActiveEngine == dieselEngine;
				input.DieselFuelUsage = dieselEngine != null ? dieselEngine.FuelUsage.Value : 0f;

				VentingResult result = Venting.Step(input);
				Apply(tower, result.TowerAirDelta);
				Apply(diesel, result.DieselAirDelta);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("Venting", e);
			}
		}

		private static void Apply(Compartment compartment, double delta)
		{
			if (compartment == null || !(delta > 0.0 || delta < 0.0))
				return; // nothing to do (or NaN)
			double air = compartment.Air + delta;
			compartment.Air = air > 0.0 ? air : 0.0;
		}
	}
}
