using System;
using DamageControl.Core.Common;
using DamageControl.Core.Leaks;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// H8, 02 §6 #16: vanilla already hides "Block leak" when there is no visible leak. This postfix also
	/// hides it on an open hatch (blocking needs the leaf shut) and on a hatch with no interaction slots,
	/// where vanilla's start code would throw. Other equipment keeps vanilla's answer.
	/// </summary>
	[HarmonyPatch(typeof(BlockLeakAction), "GetState", new[] { typeof(GameActivator), typeof(PlayableCharacter) })]
	internal static class BlockLeakStatePatch
	{
		private static void Postfix(GameActivator activator, ref ActionState __result)
		{
			try
			{
				if (__result == ActionState.Hidden || activator == null)
					return;
				Hatch hatch = activator.AssociatedEquipment as Hatch;
				if (hatch == null)
					return;
				Leaking leak = hatch.GetStatus<Leaking>();
				bool offered = LeakBlockRules.IsOffered(leak != null && !leak.Hidden, true, !hatch.Locked, hatch.GetComponent<Interactable>() != null);
				if (!offered)
					__result = ActionState.Hidden;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakState", e);
			}
		}
	}
}
