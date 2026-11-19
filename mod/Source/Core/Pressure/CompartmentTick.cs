using DamageControl.Core.Common;

namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P6 (01 §2.6, 00 §4, C24, D7): the rules of the 1 s compartment tick that replaces vanilla's
	/// low-frequency update. Light bulbs and pressure-sensitive equipment take water damage under
	/// pressure; interaction slots are flagged underwater from the compartment's own water and from
	/// any neighbouring compartment whose volume contains the slot.
	///
	/// Damage chances are stated per second (D7). The tick runs every <see cref="TickSeconds"/> of game
	/// time, so the Game layer rolls them with <c>Probability.Roll(random, chance, TickSeconds)</c>.
	/// </summary>
	public static class CompartmentTick
	{
		/// <summary>Tick interval returned to the game's timed queue (vanilla uses 4 s).</summary>
		public const float TickSeconds = 1f;

		/// <summary>A submerged bulb is at risk above this pressure (atm).</summary>
		public const double BulbSubmergedRiskPressure = 4.0;

		/// <summary>Any bulb is at risk above this pressure (atm).</summary>
		public const double BulbAnyRiskPressure = 16.0;

		/// <summary>Cap of the bulb damage chance per second.</summary>
		public const float BulbMaxChance = 0.1f;

		/// <summary>Overstressed equipment damage chance per second at zero durability.</summary>
		public const float OverstressMaxChance = 0.05f;

		/// <summary>
		/// A bulb is at risk when it is under water (compartment water elevation above the bulb) and
		/// the pressure exceeds 4 atm, or whenever the pressure exceeds 16 atm.
		/// </summary>
		public static bool IsBulbAtRisk(bool submerged, double pressure)
		{
			return (submerged && pressure > BulbSubmergedRiskPressure) || pressure > BulbAnyRiskPressure;
		}

		/// <summary>0.1 x clamp(p / 40 - 0.1, 0, 1): the damage chance of an at-risk bulb.</summary>
		public static float BulbChanceAtPressure(double pressure)
		{
			return BulbMaxChance * DcMath.Clamp01((float)(pressure / 40.0 - 0.1));
		}

		/// <summary>
		/// Per-second chance that a bulb takes 1 point of water damage: 0 for a broken bulb (health
		/// &lt;= 0) or one not at risk, otherwise <see cref="BulbChanceAtPressure"/>.
		/// </summary>
		public static float BulbDamageChance(bool submerged, double pressure, float health)
		{
			if (!(health > 0f) || !IsBulbAtRisk(submerged, pressure))
				return 0f;
			return BulbChanceAtPressure(pressure);
		}

		/// <summary>
		/// A pressure point is overstressed when its target is a hatch, or the water above the floor has
		/// risen past the point's elevation, and the reported pressure exceeds the target's interior
		/// design pressure. Hatches are stressed by the difference across them whatever the water.
		/// </summary>
		public static bool IsOverstressed(bool targetIsHatch, float waterAboveFloor, float pointElevation, float reportedPressure, float designPressure)
		{
			return (targetIsHatch || waterAboveFloor > pointElevation) && reportedPressure > designPressure;
		}

		/// <summary>Per-second chance an overstressed target takes 1 point of water damage: 0.05 x clamp(1 - durability, 0, 1).</summary>
		public static float OverstressDamageChance(float pressureDurability)
		{
			return OverstressMaxChance * DcMath.Clamp01(1f - pressureDurability);
		}

		/// <summary>
		/// Whether an interaction slot counts as underwater (C24, D7). While the compartment's air is
		/// unbreathable every slot is underwater (vanilla 2026.1 keeps rebreathers mandatory). Otherwise
		/// the slot is underwater when the water above the floor exceeds its tolerance on <em>any</em>
		/// side: its own compartment or any neighbour whose volume contains the slot.
		/// </summary>
		/// <param name="neighbourWater">Water above floor of each neighbouring compartment containing the slot; may be null.</param>
		/// <param name="neighbourCount">How many entries of <paramref name="neighbourWater"/> to read.</param>
		public static bool IsSlotUnderwater(bool airUnbreathable, float tolerance, float ownWaterAboveFloor, float[] neighbourWater, int neighbourCount)
		{
			if (airUnbreathable || ownWaterAboveFloor > tolerance)
				return true;
			if (neighbourWater == null)
				return false;
			int count = neighbourCount < neighbourWater.Length ? neighbourCount : neighbourWater.Length;
			for (int i = 0; i < count; i++)
			{
				if (neighbourWater[i] > tolerance)
					return true;
			}
			return false;
		}
	}

	/// <summary>What to do with a pressure point's "being damaged" status.</summary>
	public enum StatusChange
	{
		None,
		Attach,
		Detach,
	}

	/// <summary>
	/// Pressure-damage status bookkeeping for one pressure point (01 §2.5): attach one status when the
	/// point becomes overstressed, detach it when it stops, and nothing on repeats in either direction.
	/// The Game layer mirrors this with the real status object; vanilla 2026.1's setter never forgets
	/// a detached status, so after the first detach it could never re-attach one.
	/// </summary>
	public sealed class PressureDamageLatch
	{
		/// <summary>True while a status is attached.</summary>
		public bool Attached { get; private set; }

		public PressureDamageLatch(bool attached)
		{
			Attached = attached;
		}

		/// <summary>Records the wanted state and returns the change that achieves it.</summary>
		public StatusChange Set(bool beingDamaged)
		{
			StatusChange change = Decide(Attached, beingDamaged);
			Attached = beingDamaged;
			return change;
		}

		/// <summary>The change from <paramref name="attached"/> to <paramref name="wanted"/>.</summary>
		public static StatusChange Decide(bool attached, bool wanted)
		{
			if (wanted == attached)
				return StatusChange.None;
			return wanted ? StatusChange.Attach : StatusChange.Detach;
		}
	}
}
