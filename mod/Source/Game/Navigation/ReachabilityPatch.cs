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
	/// B9 (03 §2.F, 00 C26, C33): a destination behind a hatch Damage Control holds shut (repair hold, or
	/// the pressure lock with P14 on) is unreachable, for player orders too. Hatches closed only by vanilla's
	/// rules are left to vanilla. Vanilla keeps its own evacuation walk (and returns true early for player orders);
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

		// Debug-log throttle: one line per hatch and reason every few seconds (reachability is asked often).
		private const float LogInterval = 5f;
		private static Hatch lastLoggedHatch;
		private static HatchHold lastLoggedHold;
		private static float lastLoggedAt = float.NegativeInfinity;

		private static void Postfix(NavMeshNavigator __instance, Vector3 pos, ref bool __result)
		{
			if (!__result)
				return;
			try
			{
				Hatch hatch;
				HatchHold hold = FindHold(__instance, pos, out hatch);
				if (hold != HatchHold.None)
				{
					__result = HatchCrossing.Combine(__result, true);
					LogBlocked(__instance, hatch, hold);
				}
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("Reachability", e);
			}
		}

		/// <summary>The first hatch on the way that the mod holds shut, and why; <see cref="HatchHold.None"/> when none.</summary>
		private static HatchHold FindHold(NavMeshNavigator navigator, Vector3 pos, out Hatch blocking)
		{
			blocking = null;
			if (navigator == null || !GameplayState.IsActive || !BoatLayout.Require(Feature))
				return HatchHold.None;
			float conningTowerLevel;
			if (!BoatLayout.TryGetConningTowerLevel(out conningTowerLevel))
				return HatchHold.None;
			NavMeshAgent agent = navigator.Agent;
			if (agent == null)
				return HatchHold.None;

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
				// The hold is evaluated lazily below, only for the hatches the walk crosses.
				hatches.Add(new CrossingHatch(hatch.ObstaclePositionZ, IdOf(hatch.LeftCompartment), IdOf(hatch.RightCompartment), false));
			}

			int first, last;
			if (HatchCrossing.TryGetCrossedRange(hatches, query, out first, out last))
			{
				for (int i = first; i <= last; i++)
				{
					HatchHold hold = TraverseGuard.HoldOf(ordered[i]);
					if (hold != HatchHold.None)
					{
						blocking = ordered[i];
						return hold;
					}
				}
			}
			if (HatchCrossing.CrossesConningTowerLevel(query))
			{
				Hatch tower = BoatLayout.ConningTowerHatch;
				HatchHold hold = TraverseGuard.HoldOf(tower);
				if (hold != HatchHold.None)
				{
					blocking = tower;
					return hold;
				}
			}
			return HatchHold.None;
		}

		private static void LogBlocked(NavMeshNavigator navigator, Hatch hatch, HatchHold hold)
		{
			if (!DcLog.DebugEnabled)
				return;
			float now = Time.realtimeSinceStartup;
			if (ReferenceEquals(hatch, lastLoggedHatch) && hold == lastLoggedHold && now - lastLoggedAt < LogInterval)
				return;
			lastLoggedHatch = hatch;
			lastLoggedHold = hold;
			lastLoggedAt = now;
			InteractiveCharacter character = navigator.Character;
			DcLog.Debug("B9: destination unreachable for " + (character != null ? character.name : navigator.name) + " behind hatch \""
				+ (hatch != null ? hatch.name : "?") + "\": " + HatchCrossing.Describe(hold));
		}

		private static int IdOf(Compartment compartment)
		{
			return compartment != null ? compartment.GetInstanceID() : HatchCrossing.NoCompartment;
		}
	}
}
