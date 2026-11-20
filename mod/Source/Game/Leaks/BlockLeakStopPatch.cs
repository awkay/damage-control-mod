using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// H8: when a block-leak order stops for any reason, drop every hatch seal it holds (02 §2.6). Vanilla's
	/// own stop code still runs first: it removes the "is being repaired" status and the leak-intensity
	/// modifier (or our detached placeholder, see <see cref="BlockLeakUpdatePatch"/>).
	/// </summary>
	[HarmonyPatch(typeof(BlockLeakAction), "StopMainCharacter")]
	internal static class BlockLeakStopPatch
	{
		private static void Postfix(BlockLeakAction __instance, PlayableCharacter character)
		{
			try
			{
				if (HatchSeals.Shared.RemoveOwner(__instance) > 0)
					DcLog.Debug("block leak: " + (character != null ? character.name : "crewman") + " released a hatch seal");
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakStop", e);
			}
		}
	}
}
