using System;
using System.Globalization;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Game.Infrastructure;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// H6: rewrites the interior hatch panel (02 §2.4): lock warning, both side pressures (water head per
	/// D1), durability or a badly-damaged warning, one "partially sealed" line while any seal source is
	/// active, debug lines behind R14, and vanilla's evacuation note for the first-person viewer (C22).
	/// The exterior hatch and hatches lacking a side keep vanilla's text.
	/// </summary>
	[HarmonyPatch(typeof(Hatch), "GetStateText")]
	internal static class HatchStateTextPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("HatchGetStateText", ex);
		}

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static Locale locale;
		#pragma warning restore 0649

		private static void Postfix(Hatch __instance, PlayableCharacter character, ref string __result)
		{
			try
			{
				if (locale == null || __instance.IsExteriorHatch)
					return;
				Compartment left = __instance.LeftCompartment;
				Compartment right = __instance.RightCompartment;
				Compartment seat = __instance.PressureBlockadeCompartment;
				Compartment far = HatchRuntime.FarSide(__instance, seat);
				HatchGeometry geometry;
				if (left == null || right == null || far == null || !BoatLayout.TryGetGeometry(__instance, out geometry))
					return;

				var input = new HatchPanelInput
				{
					ShowLockedWarning = __instance.Locked && !__instance.CanBeOpened,
					OpeningSideAtm = WaterHead.SidePressure(seat.AirPressure, seat.WaterElevation, geometry.LowY),
					ClosingSideAtm = WaterHead.SidePressure(far.AirPressure, far.WaterElevation, geometry.LowY),
					Intact = __instance.Health > 0f,
					DesignPressure = __instance.InteriorDesignPressure,
					PartiallySealed = HatchSeals.Shared.IsSealed(__instance),
					ShowEvacuated = ViewerSeesEvacuation(character, left, right),
					Debug = DcLog.DebugEnabled ? DebugLines(__instance, left, right, geometry) : null,
				};
				__result = HatchPanel.Build(input, Texts());
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchGetStateText", e);
			}
		}

		private static bool ViewerSeesEvacuation(PlayableCharacter character, Compartment left, Compartment right)
		{
			if (character == null || !character.IsImpersonated)
				return false;
			Compartment current = character.CurrentCompartment;
			bool rightSide = HatchPanel.ViewerFarSideIsRight(current != null, current != null ? current.HorizontalIndex : 0, left.HorizontalIndex);
			Compartment beyond = rightSide ? right : left;
			return beyond != null && beyond.Evacuation;
		}

		private static HatchPanelText Texts()
		{
			return new HatchPanelText
			{
				LockedWarning = locale[HatchText.LocaleKey(HatchText.PanelLocked)],
				OpeningSideFormat = locale[HatchText.LocaleKey(HatchText.PanelOpeningSide)],
				ClosingSideFormat = locale[HatchText.LocaleKey(HatchText.PanelClosingSide)],
				DurabilityFormat = locale[HatchText.VanillaDurability],
				BadlyDamaged = locale[HatchText.LocaleKey(HatchText.PanelBadlyDamaged)],
				PartiallySealed = locale[HatchText.LocaleKey(HatchText.PanelPartiallySealed)],
				Evacuated = locale[HatchText.VanillaEvacuated],
				Culture = CultureInfo.CurrentCulture,
			};
		}

		private static string DebugLines(Hatch hatch, Compartment left, Compartment right, HatchGeometry g)
		{
			return "[DC] lowY " + g.LowY.ToString("0.00") + " highY " + g.HighY.ToString("0.00")
				+ " seals " + HatchSeals.Shared.Count(hatch)
				+ "\n[DC] L " + Describe(left) + "\n[DC] R " + Describe(right);
		}

		private static string Describe(Compartment c)
		{
			return c.name + ": cap " + c.Capacity.ToString("0.0") + " water " + c.Water.ToString("0.00")
				+ " air " + c.Air.ToString("0.00") + " p " + c.AirPressure.ToString("0.00") + " we " + c.WaterElevation.ToString("0.00");
		}
	}
}
