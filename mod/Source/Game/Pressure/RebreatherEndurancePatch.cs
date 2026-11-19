using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P3 (01 §2.3, 00 C23): a 2026.1 rebreather counts its wear time against its own "Durability"
	/// parameter, and only while it is worn. So when a rebreather is put on, the mod makes sure that
	/// parameter carries one x2.5 scale modifier (<see cref="RebreatherEndurance"/>) instead of patching
	/// the per-frame update. The modifier is looked up by name before it is added, so it never stacks;
	/// it is saved with the item like the game's own modifiers.
	///
	/// Rebreather.Awake is not patched: it is 19 bytes of IL and at risk of inlining, and durability is
	/// consumed only after OnEquipped anyway.
	/// </summary>
	[HarmonyPatch(typeof(Rebreather), "OnEquipped")]
	internal static class RebreatherEndurancePatch
	{
		private static void Postfix(Parameter ___durability)
		{
			try
			{
				if (___durability == null)
					return;
				Modifier modifier = ___durability.GetScaleModifier(RebreatherEndurance.ModifierName, true);
				if (modifier != null && !DcMath.Approximately(modifier.Value, RebreatherEndurance.DurabilityScale))
					modifier.Value = RebreatherEndurance.DurabilityScale;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("RebreatherEndurance", e);
			}
		}
	}
}
