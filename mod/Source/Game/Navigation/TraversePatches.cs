using System;
using DamageControl.Core.Common;
using DamageControl.Core.Navigation;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Navigation;
using UBOAT.Game.Scene.Entities.NavMesh;
using UBOAT.Game.Scene.Items;

namespace DamageControl.Game.Navigation
{
	/// <summary>
	/// B10 (03 §2.F): a character who starts through a closed hatch that cannot be opened has his active
	/// orders cancelled, so he stops at the hatch instead of passing through. The vanilla traverse still runs;
	/// keeping the hatch itself shut is the hatch lock guard's job (02 H2).
	/// </summary>
	internal static class TraverseGuard
	{
		/// <summary>Profit band of player and queued orders, as the game's own order cancelling uses it.</summary>
		private const float OrderProfitMin = 0f;
		private const float OrderProfitMax = 5f;

		public static void CancelIfBlocked(NavMeshNavigator navigator, Hatch hatch, string patchId)
		{
			try
			{
				if (navigator == null || !GameplayState.IsActive)
					return;
				bool hasHatch = hatch != null;
				if (!HatchCrossing.ShouldCancelTraverse(hasHatch, hasHatch && hatch.IsExteriorHatch, hasHatch && hatch.Locked, !hasHatch || hatch.CanBeOpened))
					return;
				InteractiveCharacter character = navigator.Character;
				if (character == null)
					return;
				DcLog.Debug(character.name + " stopped at blocked hatch " + hatch.name);
				character.CancelActiveOrders(OrderProfitMin, OrderProfitMax);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed(patchId, e);
			}
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
			TraverseGuard.CancelIfBlocked(navigator, ___hatch, "DoorTraverse");
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

		private static void Prefix(NavMeshLadder __instance, NavMeshNavigator navigator)
		{
			TraverseGuard.CancelIfBlocked(navigator, __instance != null ? __instance.Hatch : null, "LadderTraverse");
		}
	}
}
