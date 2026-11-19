using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P2 (01 §2.2, 00 §4): replaces the unbreathable-air test of an unmerged compartment with
	/// <see cref="AirQuality.IsUnbreathable"/>. A merged compartment keeps vanilla's delegation to the
	/// compartment it is merged with, whose getter comes back through this prefix.
	/// </summary>
	[HarmonyPatch(typeof(Compartment), "IsAirToxic", MethodType.Getter)]
	internal static class AirToxicPatch
	{
		private static bool Prefix(Compartment __instance, Compartment ___mergedCompartment, Resource ___oxygen, ref bool __result)
		{
			try
			{
				if (___mergedCompartment != null)
					return true;
				double oxygen = ___oxygen != null ? ___oxygen.Amount : 1.0;
				__result = AirQuality.IsUnbreathable(oxygen, __instance.AirPressure, __instance.WaterElevationAboveFloor);
				return false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("AirToxic", e);
				return true;
			}
		}
	}
}
