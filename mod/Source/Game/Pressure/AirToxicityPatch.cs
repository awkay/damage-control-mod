using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using HarmonyLib;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P2 / C18: the toxicity value the game's choking status adds to its injury check is -0.1 while
	/// the air is unbreathable (about a 5 % hit chance per check for a sailor, 20 % for an officer)
	/// and -1.0 otherwise (never).
	/// </summary>
	[HarmonyPatch(typeof(Compartment), "AirToxicity", MethodType.Getter)]
	internal static class AirToxicityPatch
	{
		private static void Postfix(Compartment __instance, ref float __result)
		{
			try
			{
				__result = AirQuality.Toxicity(__instance.IsAirToxic);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("AirToxicity", e);
			}
		}
	}
}
