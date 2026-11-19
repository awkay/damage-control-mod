using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P13 (01 §2.13, 00 §1.1): the bilge ("water storage") panel of each compartment always shows that
	/// compartment's air pressure; the 2022.1 "mass &gt; 100" proxy is dropped. Bilges are recognised the
	/// way vanilla Storage.GetStateText itself recognises them (a plain Storage whose object name
	/// contains "Water"), so the line appears exactly on the panels that show the vanilla bilge text.
	/// </summary>
	[HarmonyPatch(typeof(Storage), "GetStateText")]
	internal static class BilgePressurePatch
	{
		private const string BilgeMarker = "Water";

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static Locale locale;
		#pragma warning restore 0649

		private static void Postfix(Storage __instance, PlayableCharacter character, ref string __result)
		{
			try
			{
				if (locale == null || __instance.GetType() != typeof(Storage))
					return;
				if (__instance.name.IndexOf(BilgeMarker, StringComparison.Ordinal) < 0)
					return;
				Compartment room = __instance.GetComponentInParent<Compartment>();
				if (room == null)
					return;
				string line = locale.Format(PressureText.CompartmentAirPressure, room.AirPressure);
				__result = string.IsNullOrEmpty(__result) ? line : __result + "\n" + line;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BilgePressure", e);
			}
		}
	}
}
