using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P4 (01 §2.4, 00 §4): a pressure point on an interior hatch reports the air-pressure difference
	/// across the hatch. Points on the exterior hatch and on other equipment keep the vanilla value.
	/// </summary>
	[HarmonyPatch(typeof(CompartmentPressure), "Pressure", MethodType.Getter)]
	internal static class HatchPressureReadingPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("HatchPressureReading", ex);
		}

		private static void Postfix(CompartmentPressure __instance, ref float __result)
		{
			try
			{
				Hatch hatch = __instance.Target as Hatch;
				if (hatch == null || hatch.IsExteriorHatch)
					return;
				Compartment left = hatch.LeftCompartment;
				Compartment right = hatch.RightCompartment;
				if (left == null || right == null)
					return;
				__result = HatchPressureReading.Reported(true, left.AirPressure, right.AirPressure, __result);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchPressureReading", e);
			}
		}
	}
}
