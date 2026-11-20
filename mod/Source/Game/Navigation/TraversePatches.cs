using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Navigation;
using DamageControl.Game.Hatches;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Core.AI;
using UBOAT.Game.Core.AI.Behaviours;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Navigation;
using UBOAT.Game.Scene.Entities.NavMesh;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Navigation
{
	/// <summary>
	/// B10 (03 §2.F, 00 C33): a character who starts through a hatch that Damage Control holds shut (repair
	/// hold, or the pressure lock with P14 on) has his active orders cancelled, so he stops at the hatch
	/// instead of passing through. Hatches closed only by vanilla's rules are left to vanilla, which opens
	/// them on traverse. Evacuating crew, crew leaving the deck on a dive and crew already on the link are
	/// never cancelled. The vanilla traverse still runs; keeping the hatch itself shut is the lock guard's
	/// job (02 H2).
	/// </summary>
	internal static class TraverseGuard
	{
		/// <summary>Profit band of player and queued orders, as the game's own order cancelling uses it.</summary>
		private const float OrderProfitMin = 0f;
		private const float OrderProfitMax = 5f;

		/// <summary>Whether, and why, the mod holds this hatch shut (B9 and B10 share it).</summary>
		public static HatchHold HoldOf(Hatch hatch)
		{
			if (hatch == null || !hatch.Locked)
				return HatchHold.None;
			var state = new HatchHoldState
			{
				Locked = true,
				IsExterior = hatch.IsExteriorHatch,
				HasCompartments = !HatchRuntime.HasNoCompartments(hatch),
				RepairHeld = HatchRuntime.IsRepairHeld(hatch),
				PressureBlockingEnabled = DcSettings.HatchPressureBlocking,
			};
			// Read the (patched) rule only when it can matter.
			if (!state.IsExterior && state.HasCompartments && !state.RepairHeld && state.PressureBlockingEnabled)
				state.CanBeOpened = hatch.CanBeOpened;
			else
				state.CanBeOpened = true;
			return HatchCrossing.HoldOf(state);
		}

		public static void CancelIfBlocked(NavMeshNavigator navigator, Hatch hatch, bool alreadyTraversing, string patchId)
		{
			try
			{
				if (navigator == null || hatch == null || !GameplayState.IsActive)
					return;
				HatchHold hold = HoldOf(hatch);
				if (hold == HatchHold.None)
					return;
				InteractiveCharacter character = navigator.Character;
				if (character == null)
					return;
				var who = new TraverserState
				{
					Evacuating = IsEvacuating(character),
					LeavingDeckForDive = IsLeavingDeck(character),
					AlreadyTraversing = alreadyTraversing,
				};
				if (!HatchCrossing.ShouldCancelTraverse(hold, who))
				{
					DcLog.Debug("B10: let " + character.name + " through hatch \"" + hatch.name + "\" (" + HatchCrossing.Describe(hold)
						+ "): " + (who.Evacuating ? "evacuating" : who.LeavingDeckForDive ? "leaving the deck on a dive" : "already on the link"));
					return;
				}
				DcLog.Debug("B10: " + character.name + " stopped at blocked hatch \"" + hatch.name + "\": " + HatchCrossing.Describe(hold));
				character.CancelActiveOrders(OrderProfitMin, OrderProfitMax);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed(patchId, e);
			}
		}

		private static bool IsEvacuating(InteractiveCharacter character)
		{
			PlayableCharacter playable = character as PlayableCharacter;
			return playable != null && playable.Action is EvacuateToSafetyAction;
		}

		/// <summary>The game's own "get below" behaviours: escaping the deck's danger zone, or hiding in the boat when she dives.</summary>
		private static bool IsLeavingDeck(InteractiveCharacter character)
		{
			CharacterAI ai = character.Ai;
			if (ai == null)
				return false;
			return ai.GetActiveBehaviour<EscapeDangerBehaviour>() != null || ai.GetActiveBehaviour<HideToShipBehaviour>() != null;
		}
	}

	/// <summary>B10 for horizontal hatches (bulkhead doors).</summary>
	[HarmonyPatch(typeof(NavMeshDoors), "TraverseStart")]
	internal static class DoorTraversePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("DoorTraverse", ex);
		}

		private static void Prefix(NavMeshNavigator navigator, Hatch ___hatch)
		{
			TraverseGuard.CancelIfBlocked(navigator, ___hatch, false, "DoorTraverse");
		}
	}

	/// <summary>B10 for ladder hatches; the exterior hatch is exempt (Hatch.IsExteriorHatch, C11).</summary>
	[HarmonyPatch(typeof(NavMeshLadder), "TraverseStart")]
	internal static class LadderTraversePatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("LadderTraverse", ex);
		}

		private static void Prefix(NavMeshLadder __instance, NavMeshNavigator navigator, List<NavMeshLadder.NavigatorData> ___navigators)
		{
			TraverseGuard.CancelIfBlocked(navigator, __instance != null ? __instance.Hatch : null, IsQueued(___navigators, navigator), "LadderTraverse");
		}

		/// <summary>The navigator is already on this ladder's shared queue (both sides share it), i.e. part way through.</summary>
		private static bool IsQueued(List<NavMeshLadder.NavigatorData> queue, NavMeshNavigator navigator)
		{
			if (queue == null || navigator == null)
				return false;
			for (int i = 0; i < queue.Count; i++)
			{
				if (queue[i] != null && ReferenceEquals(queue[i].navigator, navigator))
					return true;
			}
			return false;
		}
	}
}
