using HarmonyLib;
using UBOAT.Game.Scene.Items;
using UnityEngine;

namespace DamageControl.Patches
{
	/// <summary>
	/// Pipeline smoke-test patch: logs once when the first compartment runs its low-frequency
	/// update, proving that Harmony patching of game code works in this install.
	/// Target: private float Compartment.LowFrequencyUpdate() (scheduled via IExecutionQueue).
	/// </summary>
	[HarmonyPatch(typeof(Compartment), "LowFrequencyUpdate")]
	internal static class CompartmentHeartbeatPatch
	{
		private static bool logged;

		private static void Postfix(Compartment __instance, float __result, bool ___evacuation)
		{
			if (logged)
				return;
			logged = true;
			Debug.Log("[Damage Control] Compartment heartbeat: " + __instance.name
				+ " next update in " + __result + " s, evacuation=" + ___evacuation);
		}
	}
}
