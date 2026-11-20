using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Interactions;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using UnityEngine;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P6 (01 §2.6, 00 §4, C24, D7): whole-method replacement of the compartment's low-frequency
	/// update, which now runs every second. It covers everything vanilla 2026.1 does there (pressure
	/// damage and slot underwater flags) with the mod's rules from <see cref="CompartmentTick"/>:
	/// <list type="number">
	/// <item>light bulbs under pressure take water damage;</item>
	/// <item>pressure points are flagged "being damaged" and their equipment takes water damage while
	/// overstressed; hatches feel the pressure difference across them whatever the water level;</item>
	/// <item>interaction slots are underwater from their own compartment's water, from any containing
	/// neighbour's water (D7 "any side"), or always while the air is unbreathable (C24).</item>
	/// </list>
	/// Damage chances are rolled over the game time since the compartment's previous tick, which exceeds
	/// 1 s at high compression. Outside gameplay, and on an exception, vanilla runs for that tick.
	/// </summary>
	[HarmonyPatch(typeof(Compartment), "LowFrequencyUpdate")]
	internal static class CompartmentTickPatch
	{
		private static float[] neighbourWater = new float[4];
		private static float[] containingWater = new float[4];

		// Game clock (ticks) of each compartment's previous tick; session-only.
		private static readonly Dictionary<Compartment, long> lastTicks = new Dictionary<Compartment, long>();
		private static bool sessionHooked;

		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("CompartmentTick", ex);
		}

		/// <summary>Game seconds since this compartment's previous tick (<see cref="CompartmentTick.ElapsedSeconds"/>).</summary>
		private static float Elapsed(Compartment compartment)
		{
			if (!sessionHooked)
			{
				sessionHooked = true;
				DcSettings.SessionStarted += lastTicks.Clear;
			}
			long now = GameClock.Ticks;
			if (now < 0)
				return CompartmentTick.TickSeconds;
			long previous;
			if (!lastTicks.TryGetValue(compartment, out previous))
				previous = -1L;
			lastTicks[compartment] = now;
			return CompartmentTick.ElapsedSeconds(previous, now);
		}

		private static bool Prefix(
			Compartment __instance,
			List<CompartmentPressure> ___pressurizedEquipment,
			List<InteractableSlot> ___interactableSlots,
			Device[] ___devices,
			ref float __result)
		{
			try
			{
				if (!GameplayState.IsActive)
					return true;
				float elapsed = Elapsed(__instance);
				IRandom random = UnityRandomSource.Instance;
				double pressure = __instance.AirPressure;
				float waterElevation = __instance.WaterElevation;
				float waterAboveFloor = __instance.WaterElevationAboveFloor;

				TickBulbs(___devices, pressure, waterElevation, random, elapsed);
				TickPressurePoints(___pressurizedEquipment, waterAboveFloor, random, elapsed);
				TickSlots(__instance, ___interactableSlots, waterAboveFloor);

				__result = CompartmentTick.TickSeconds;
				return false;
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("CompartmentTick", e);
				return true;
			}
		}

		private static void TickBulbs(Device[] devices, double pressure, float waterElevation, IRandom random, float elapsed)
		{
			if (devices == null || !(pressure > CompartmentTick.BulbSubmergedRiskPressure))
				return; // no bulb can be at risk at or below 4 atm
			for (int i = 0; i < devices.Length; i++)
			{
				LightBulb bulb = devices[i] as LightBulb;
				if (bulb == null)
					continue;
				bool submerged = waterElevation > bulb.transform.position.y;
				float chance = CompartmentTick.BulbDamageChance(submerged, pressure, bulb.Health);
				if (chance > 0f && Probability.Roll(random, chance, elapsed))
					bulb.AddWaterDamage(1f, true);
			}
		}

		private static void TickPressurePoints(List<CompartmentPressure> points, float waterAboveFloor, IRandom random, float elapsed)
		{
			if (points == null)
				return;
			for (int i = 0; i < points.Count; i++)
			{
				CompartmentPressure point = points[i];
				if (point == null)
					continue;
				Equipment target = point.Target;
				if (target == null)
					continue;
				bool overstressed = CompartmentTick.IsOverstressed(
					target is Hatch, waterAboveFloor, point.Elevation, point.Pressure, target.InteriorDesignPressure.Value);
				PressureDamageStatus.Set(point, overstressed);
				if (!overstressed || !(target.Health > 0f))
					continue;
				float chance = CompartmentTick.OverstressDamageChance(target.InteriorPressureDurability.Value);
				if (chance > 0f && Probability.Roll(random, chance, elapsed))
					target.AddWaterDamage(1f, true);
			}
		}

		private static void TickSlots(Compartment compartment, List<InteractableSlot> slots, float waterAboveFloor)
		{
			if (slots == null || slots.Count == 0)
				return;
			bool unbreathable = compartment.IsAirToxic;
			Compartment[] neighbours = compartment.NeighbouringCompartments;
			int neighbourCount = neighbours != null ? neighbours.Length : 0;
			if (neighbourWater.Length < neighbourCount)
			{
				neighbourWater = new float[neighbourCount];
				containingWater = new float[neighbourCount];
			}
			float maxNeighbourWater = 0f;
			for (int j = 0; j < neighbourCount; j++)
			{
				Compartment neighbour = neighbours[j];
				float water = neighbour != null ? neighbour.WaterElevationAboveFloor : 0f;
				neighbourWater[j] = water;
				if (water > maxNeighbourWater)
					maxNeighbourWater = water;
			}

			for (int i = 0; i < slots.Count; i++)
			{
				InteractableSlot slot = slots[i];
				if (slot == null)
					continue;
				float tolerance = slot.WaterElevationToleration;
				int containing = 0;
				if (!unbreathable && !(waterAboveFloor > tolerance) && maxNeighbourWater > tolerance)
				{
					// Only a neighbour flooded past this slot's tolerance can change the answer; test containment for those.
					Vector3 position = slot.transform.position;
					for (int j = 0; j < neighbourCount; j++)
					{
						if (neighbourWater[j] > tolerance && neighbours[j] != null && neighbours[j].Contains(position))
							containingWater[containing++] = neighbourWater[j];
					}
				}
				slot.IsBelowWater = CompartmentTick.IsSlotUnderwater(unbreathable, tolerance, waterAboveFloor, containingWater, containing);
			}
		}
	}

	/// <summary>
	/// The pressure-damage status of one pressure point, following <see cref="PressureDamageLatch"/>.
	/// Writes the point's private status field so that a detached status is forgotten and a later
	/// overstress attaches a new one (vanilla 2026.1's setter keeps the old one and never re-attaches).
	/// </summary>
	internal static class PressureDamageStatus
	{
		private static AccessTools.FieldRef<CompartmentPressure, PressureDamage> statusField;

		public static void Set(CompartmentPressure point, bool beingDamaged)
		{
			if (statusField == null)
				statusField = AccessTools.FieldRefAccess<CompartmentPressure, PressureDamage>("pressureDamage");
			Equipment target = point.Target;
			PressureDamage current = statusField(point);
			switch (PressureDamageLatch.Decide(current != null, beingDamaged))
			{
				case StatusChange.Attach:
					current = new PressureDamage();
					statusField(point) = current;
					target.AddStatus(current);
					break;
				case StatusChange.Detach:
					target.RemoveStatus(current);
					statusField(point) = null;
					break;
				default:
					if (beingDamaged)
						target.AddStatus(current); // no-op when attached; repairs a status vanilla detached but kept
					break;
			}
		}
	}
}
