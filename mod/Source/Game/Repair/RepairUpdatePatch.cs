using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters.Actions;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// R2, R3 (04 §2.1.7, 00 C9, C10): after vanilla's repair update, apply the repair's effects on its target
	/// while the repairer interacts: a hatch seal source, the leak throttle of 1 - P on leakable equipment
	/// that was leaking when the work began, and one close of an open interior hatch. Vanilla's progress,
	/// parts, XP, flaw handling and voice lines are untouched (D4).
	/// </summary>
	[HarmonyPatch(typeof(RepairAction), "UpdateAction")]
	internal static class RepairUpdatePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("RepairUpdate", ex);
		}

		private static void Postfix(RepairAction __instance)
		{
			try
			{
				RepairRuntime.Update(__instance);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("RepairUpdate", e);
			}
		}
	}

	/// <summary>R2, R3 cleanup: when the repair stops, its seal and throttle go (04 §2.1.8).</summary>
	[HarmonyPatch(typeof(RepairAction), "StopMainCharacter")]
	internal static class RepairStopPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("RepairStop", ex);
		}

		private static void Postfix(RepairAction __instance)
		{
			try
			{
				RepairRuntime.Stop(__instance);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("RepairStop", e);
			}
		}
	}
}
