using System;
using DamageControl.Core.Common;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// H2: lock-state guard on the Hatch.Locked setter (02 §2.2). Opening a closed interior hatch that
	/// cannot be opened is suppressed, whichever code path asks (player, crew traversal, scripts).
	/// Exterior hatches are excluded by Hatch.IsExteriorHatch (C11), compartment-less hatches (doors) are
	/// left alone, and nothing changes while neither P14 nor a repair hold is active, so vanilla
	/// behaviour is untouched with the setting off. The mod's own forced openings bypass it.
	/// </summary>
	[HarmonyPatch(typeof(Hatch), "Locked", MethodType.Setter)]
	internal static class HatchLockedSetterPatch
	{
		private static bool Prefix(Hatch __instance, bool value)
		{
			try
			{
				if (value || HatchRuntime.GuardBypassed || !__instance.Locked || __instance.IsExteriorHatch)
					return true;
				if (HatchRuntime.HasNoCompartments(__instance))
					return true;
				if (!DcSettings.HatchPressureBlocking && !HatchRuntime.IsRepairHeld(__instance))
					return true;
				if (__instance.CanBeOpened)
					return true;
				DcLog.Debug("kept hatch \"" + __instance.name + "\" shut: it cannot be opened now");
				return false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchLockedSetter", e);
				return true;
			}
		}
	}
}
