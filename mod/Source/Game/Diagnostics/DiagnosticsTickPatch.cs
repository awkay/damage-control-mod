using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Entities;

namespace DamageControl.Game.Diagnostics
{
	/// <summary>
	/// Diagnostics driver: after each Update of the player ship, let <see cref="DiagnosticsRuntime"/> poll for
	/// events and write its report when due. Read-only; does nothing unless the diagnostics setting is on,
	/// and only in the Gameplay state (no save loading) with the player boat present. Update also runs while
	/// the game is paused, so the report keys off real time and skips windows in which no game time passed.
	/// </summary>
	[HarmonyPatch(typeof(PlayerShip), "Update")]
	internal static class DiagnosticsTickPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("DiagnosticsTick", ex);
		}

		private static void Postfix(PlayerShip __instance)
		{
			try
			{
				if (__instance == null || !GameplayState.IsActive || __instance != BoatLayout.Ship)
					return;
				DiagnosticsRuntime.Tick(__instance);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("DiagnosticsTick", e);
			}
		}
	}
}
