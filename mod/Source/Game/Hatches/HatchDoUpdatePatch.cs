using System;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Game.Infrastructure;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Scene.Audio;
using UBOAT.Game.Scene.Camera;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using UnityEngine;

namespace DamageControl.Game.Hatches
{
	/// <summary>
	/// H4 and H5: replaces Hatch.DoUpdate (00 §4, one of the two kept whole-method replacements). Vanilla's
	/// per-frame hatch update drives the occlusion portal, moves air and water only while open, and plays
	/// the water-jet particles. This replacement keeps the portal and the particles and swaps in the mod's
	/// physical seal (02 §2.3):
	/// <list type="number">
	/// <item>a. occlusion portal, reimplemented from the behavioural description;</item>
	/// <item>b. the frame's game dt x damped rate (at most 8, vanilla's clamp), integrated in substeps of at
	/// most 0.5 game seconds each (<see cref="HatchExchange.Substeps"/>);</item>
	/// <item>c. destroyed-hatch blow-open as a per-second hazard (D7);</item>
	/// <item>d. Leaking status upkeep;</item>
	/// <item>e. flow gate: open, or closed and leaking at 2 % x seal factor;</item>
	/// <item>f/g. vertical or horizontal exchange through the Core kernel.</item>
	/// </list>
	/// When the hatch has no usable geometry, or on any exception, vanilla runs instead.
	/// </summary>
	[HarmonyPatch(typeof(Hatch), "DoUpdate")]
	internal static class HatchDoUpdatePatch
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static MainCamera mainCamera;

		[Inject]
		private static ISoundEffectsDatabase soundEffectsDatabase;
		#pragma warning restore 0649

		private static bool Prefix(
			Hatch __instance,
			OcclusionPortal ___occlusionPortal,
			bool ___verticalPassage,
			bool ___isAnimating,
			Compartment ___leftCompartment,
			Compartment ___rightCompartment,
			ParticleSystem ___flowEffectLeft,
			ParticleSystem ___flowEffectRight)
		{
			try
			{
				Compartment left = ___leftCompartment;
				Compartment right = ___rightCompartment;
				HatchGeometry geometry;
				if (left == null || right == null || !BoatLayout.TryGetGeometry(__instance, out geometry))
					return true;
				HatchRuntime.EnsureInitialized();

				UpdatePortal(__instance, ___occlusionPortal, ___isAnimating, left, right);

				SimClock clock = GameClock.Now();
				FrameSubsteps substeps = HatchExchange.Substeps(clock, HatchRuntime.FlowRate);
				UpdateDamage(__instance, ___isAnimating, clock);

				bool open = !__instance.Locked;
				bool leaking = !open && __instance.HasStatus<Leaking>();
				float gated = HatchExchange.GatedStep(substeps.Step, open, leaking, HatchSeals.Shared.SealFactor(__instance));
				if (!open && !leaking)
				{
					SetJets(___flowEffectLeft, ___flowEffectRight, JetEffect.None);
					return false;
				}
				if (gated <= 0f || HatchRuntime.SameWaterBody(left, right))
					return false;

				// Each substep re-reads both compartments, so the kernel's guards (air floor, no
				// equalization overshoot, no overfill) hold per substep against the current state.
				float verticalOpening = HatchExchange.VerticalOpeningHeight(geometry.LowY, left.FloorElevation);
				JetEffect jet = JetEffect.None;
				for (int i = 0; i < substeps.Count; i++)
				{
					HatchSide l = HatchRuntime.ReadSide(left);
					HatchSide r = HatchRuntime.ReadSide(right);
					HatchFlow flow = ___verticalPassage
						? HatchExchange.Vertical(l, r, verticalOpening, gated)
						: HatchExchange.Horizontal(l, r, geometry, gated);
					HatchRuntime.Apply(left, right, flow);
					jet = ___verticalPassage ? JetEffect.None : flow.Jet;
					if (flow.Air == 0.0 && flow.Water == 0.0)
						break; // at rest: later substeps would see the same state
				}
				SetJets(___flowEffectLeft, ___flowEffectRight, jet);
				return false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("HatchDoUpdate", e);
				return true;
			}
		}

		/// <summary>
		/// 02 §2.3a. With the camera inside a compartment the portal is open when the hatch is unlocked and
		/// within N compartments of the camera (N = 3 when the camera is in compartment 1, or at most 3 with
		/// the hatch's left side below 3; otherwise 2), or while the hatch animates. Outside, open when
		/// unlocked or animating.
		/// </summary>
		private static void UpdatePortal(Hatch hatch, OcclusionPortal portal, bool animating, Compartment left, Compartment right)
		{
			if (portal == null)
				return;
			bool unlocked = !hatch.Locked;
			Compartment cameraCompartment = mainCamera != null ? mainCamera.Compartment : null;
			bool open;
			if (cameraCompartment != null)
			{
				int cam = cameraCompartment.HorizontalIndex;
				int distance = Math.Min(Math.Abs(cam - left.HorizontalIndex), Math.Abs(cam - right.HorizontalIndex));
				int reach = cam == 1 || (cam <= 3 && left.HorizontalIndex < 3) ? 3 : 2;
				open = (unlocked && distance < reach) || animating;
			}
			else
			{
				open = unlocked || animating;
			}
			if (portal.open != open)
				portal.open = open;
		}

		/// <summary>02 §2.3 c and d.</summary>
		private static void UpdateDamage(Hatch hatch, bool animating, SimClock clock)
		{
			float health = hatch.Health;
			bool locked = hatch.Locked;
			Compartment seat = hatch.PressureBlockadeCompartment;
			Compartment far = HatchRuntime.FarSide(hatch, seat);
			if (seat != null && far != null && health <= 0f && !clock.IsPaused)
			{
				bool beingRepaired = hatch.HasStatus<IsBeingRepaired>();
				if (HatchDamageRules.CanBlowOpen(health, beingRepaired, locked, animating, seat.AirPressure, far.AirPressure, hatch.InteriorDesignPressure)
					&& HatchDamageRules.RollBlowOpen(UnityRandomSource.Instance, HatchRuntime.BlowOpenChance, clock.CappedGameDt(HatchExchange.MaxFrameSeconds)))
				{
					HatchRuntime.SetLockedUnguarded(hatch, false);
					locked = hatch.Locked;
					if (soundEffectsDatabase != null)
					{
						ISoundEffect clip = soundEffectsDatabase.GetClip("Pressure Damage", hatch.transform);
						if (clip != null)
							clip.Play();
					}
					DcLog.Debug("hatch \"" + hatch.name + "\" blown open by the pressure behind it");
				}
			}

			LeakStatusChange change = HatchDamageRules.UpkeepLeak(health, locked, animating, hatch.HasStatus<Leaking>());
			if (change == LeakStatusChange.Add)
				hatch.AddStatus<Leaking>();
			else if (change == LeakStatusChange.Remove && hatch.isActiveAndEnabled)
				hatch.RemoveStatuses<Leaking>(); // a disabled hatch keeps the leak H3 gave it
		}

		private static void SetJets(ParticleSystem leftJet, ParticleSystem rightJet, JetEffect jet)
		{
			SetJet(leftJet, jet == JetEffect.Left);
			SetJet(rightJet, jet == JetEffect.Right);
		}

		private static void SetJet(ParticleSystem jet, bool play)
		{
			if (jet == null)
				return;
			if (play && !jet.isPlaying)
				jet.Play(true);
			else if (!play && jet.isPlaying)
				jet.Stop(true);
		}
	}
}
