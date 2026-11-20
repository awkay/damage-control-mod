using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Core.Leaks;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;

namespace DamageControl.Game.Leaks
{
	/// <summary>
	/// H8, 00 C28: vanilla BlockLeakAction on a leaking hatch. Vanilla's per-frame update casts the leaking
	/// equipment to LeakableEquipment to scale its leak intensity; a hatch is a Device, so the cast throws
	/// every frame once WS2 gives a destroyed hatch the Leaking status.
	///
	/// For any leaking equipment that is not leakable equipment this prefix stores a detached placeholder
	/// in the action's private modifier field, so vanilla's "add the modifier" branch is skipped and the rest
	/// of vanilla's update (crowding check, leak-gone check, device use) still runs. Vanilla's stop code
	/// calls Remove() on it, which is harmless for a modifier attached to nothing. For a hatch it also holds
	/// a <see cref="SealSource.Blocker"/> seal while the crewman interacts and cancels the order once the
	/// hatch is open (02 §2.6). Leakable equipment is left entirely to vanilla.
	/// </summary>
	[HarmonyPatch(typeof(BlockLeakAction), "UpdateAction")]
	internal static class BlockLeakUpdatePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("BlockLeakUpdate", ex);
		}

		/// <summary>Name of the placeholder modifier (never attached to a stat).</summary>
		internal const string PlaceholderName = "DC Hatch Seal";

		private static bool Prefix(BlockLeakAction __instance, Leaking ___leak, ref Modifier ___leakIntensityModifier)
		{
			try
			{
				PlayableCharacter character = __instance.Character;
				if (___leak == null)
				{
					// D7 null guard: vanilla would throw every frame. End the order instead.
					if (character != null)
						character.CancelOrders();
					return false;
				}
				Equipment equipment = ___leak.Equipment;
				if (!LeakBlockRules.TakesNonLeakablePath(equipment != null, equipment is LeakableEquipment))
					return true;

				if (___leakIntensityModifier == null)
					___leakIntensityModifier = new Modifier(null, PlaceholderName);

				Hatch hatch = equipment as Hatch;
				if (hatch == null || character == null)
					return true;
				HatchBlockerStep step = LeakBlockRules.HatchStep(!hatch.Locked, character.IsInteracting);
				if (step.Seal)
					HatchSeals.Shared.Add(hatch, SealSource.Blocker, __instance);
				else
					HatchSeals.Shared.Remove(hatch, __instance);
				if (step.Cancel)
				{
					DcLog.Debug("block leak: hatch \"" + hatch.name + "\" is open; " + character.name + " stops blocking it");
					character.CancelOrders();
				}
				return true;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BlockLeakUpdate", e);
				return true;
			}
		}
	}
}
