using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// H1: the pressure-lock rule (02 §2.1) as a postfix on Hatch.CanBeOpened.
	/// <list type="bullet">
	/// <item>A visible "is being repaired" status always means "cannot be opened" (C10, the R3 repair hold).</item>
	/// <item>With P14 off, vanilla's own rule stands (C25).</item>
	/// <item>With P14 on, hatches with compartments get the Core rule; compartment-less hatches (exterior,
	/// doors) keep vanilla, whose deck-depth rule is already at least as strict as the spec's step 6.</item>
	/// </list>
	/// </summary>
	[HarmonyPatch(typeof(Hatch), "CanBeOpened", MethodType.Getter)]
	internal static class HatchCanBeOpenedPatch
	{
		private static void Postfix(Hatch __instance, ref bool __result, bool ___pressureBlockade)
		{
			try
			{
				if (HatchRuntime.IsRepairHeld(__instance))
				{
					__result = false;
					return;
				}
				if (!DcSettings.HatchPressureBlocking || HatchRuntime.HasNoCompartments(__instance))
					return;
				HatchGeometry geometry;
				if (!BoatLayout.TryGetGeometry(__instance, out geometry))
				{
					DcLog.WarnOnce("hatch-geometry:" + __instance.name, "hatch \"" + __instance.name + "\" has no usable geometry; its pressure lock follows vanilla");
					return;
				}
				__result = HatchLockRule.CanBeOpened(BuildInput(__instance, ___pressureBlockade, geometry.LowY));
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchCanBeOpened", e);
			}
		}

		internal static HatchLockInput BuildInput(Hatch hatch, bool pressureBlockade, float lowY)
		{
			Compartment seat = hatch.PressureBlockadeCompartment;
			Compartment far = HatchRuntime.FarSide(hatch, seat);
			var input = new HatchLockInput
			{
				BeingRepaired = false, // handled before the rule; see Postfix
				IsPressureBlockade = pressureBlockade,
				BlockingEnabled = true,
				HasLeft = hatch.LeftCompartment != null,
				HasRight = hatch.RightCompartment != null,
				HasSeat = seat != null,
				HasFar = far != null,
				LowY = lowY,
				Threshold = HatchRuntime.LockThreshold,
			};
			if (seat != null)
			{
				input.SeatAirPressure = seat.AirPressure;
				input.SeatWaterElevation = seat.WaterElevation;
			}
			if (far != null)
			{
				input.FarAirPressure = far.AirPressure;
				input.FarWaterElevation = far.WaterElevation;
			}
			Ship ship = hatch.ParentEntity as Ship;
			input.OnShip = ship != null;
			if (ship != null)
				input.DeckDepth = ship.DeckDepth;
			return input;
		}
	}
}
