using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Navigation;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Navigation;
using UBOAT.Game.Scene.Items;
using UnityEngine;
using UnityEngine.AI;

namespace DamageControl.Game.Navigation
{
	/// <summary>
	/// B9 (03 §2.F, 00 C26): a destination behind a closed hatch that cannot be opened is unreachable, for
	/// player orders too. Vanilla keeps its own evacuation walk (and returns true early for player orders);
	/// this postfix only ever turns "reachable" into "unreachable". The conning-tower level comes from the
	/// boat layout (C12), not from the 2022.1 constants.
	/// </summary>
	[HarmonyPatch(typeof(NavMeshNavigator), "IsDestinationReachable")]
	internal static class ReachabilityPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("Reachability", ex);
		}

		private const string Feature = "Crew navigation through blocked hatches";

		// Main-thread scratch list, rebuilt per call (a handful of hatches).
		private static readonly List<CrossingHatch> hatches = new List<CrossingHatch>();

		private static void Postfix(NavMeshNavigator __instance, Vector3 pos, ref bool __result)
		{
			if (!__result)
				return;
			try
			{
				if (IsBlocked(__instance, pos))
					__result = HatchCrossing.Combine(__result, true);
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("Reachability", e);
			}
		}

		private static bool IsBlocked(NavMeshNavigator navigator, Vector3 pos)
		{
			if (navigator == null || !GameplayState.IsActive || !BoatLayout.Require(Feature))
				return false;
			float conningTowerLevel;
			if (!BoatLayout.TryGetConningTowerLevel(out conningTowerLevel))
				return false;
			NavMeshAgent agent = navigator.Agent;
			if (agent == null)
				return false;

			bool interior = navigator.Layer == NavMeshLayer.UboatInterior;
			Vector3 origin = agent.nextPosition;
			float originY = origin.y;
			if (!interior)
			{
				// Off the interior layer the agent is on deck (or outside): above every interior level.
				float exteriorLevel;
				if (BoatLayout.TryGetExteriorLevel(out exteriorLevel))
					originY = Mathf.Max(originY, exteriorLevel);
			}

			PlayableCharacter playable = navigator.Character as PlayableCharacter;
			Compartment current = playable != null ? playable.CurrentCompartment : null;

			var query = new CrossingQuery
			{
				OriginZ = interior ? origin.z : 0f,
				OriginY = originY,
				DestinationZ = pos.z,
				DestinationY = pos.y,
				CurrentCompartment = IdOf(current),
				ConningTowerLevel = conningTowerLevel,
			};

			IReadOnlyList<Hatch> ordered = BoatLayout.HatchesLeftToRight;
			hatches.Clear();
			for (int i = 0; i < ordered.Count; i++)
			{
				Hatch hatch = ordered[i];
				// Blocked is evaluated lazily below, only for the hatches the walk crosses.
				hatches.Add(new CrossingHatch(hatch.ObstaclePositionZ, IdOf(hatch.LeftCompartment), IdOf(hatch.RightCompartment), false));
			}

			int first, last;
			if (HatchCrossing.TryGetCrossedRange(hatches, query, out first, out last))
			{
				for (int i = first; i <= last; i++)
				{
					if (IsBlocked(ordered[i]))
						return true;
				}
			}
			return HatchCrossing.CrossesConningTowerLevel(query) && IsBlocked(BoatLayout.ConningTowerHatch);
		}

		private static bool IsBlocked(Hatch hatch)
		{
			return hatch != null && HatchCrossing.IsBlocked(hatch.Locked, hatch.CanBeOpened);
		}

		private static int IdOf(Compartment compartment)
		{
			return compartment != null ? compartment.GetInstanceID() : HatchCrossing.NoCompartment;
		}
	}
}
