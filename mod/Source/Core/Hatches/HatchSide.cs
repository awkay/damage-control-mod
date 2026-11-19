using System;

namespace DamageControl.Core.Hatches
{
	/// <summary>
	/// One compartment as seen from a hatch, as plain values (00 §2.1 rule 1). Units are the game's:
	/// pressure in atm, air/water/capacity in the tank's volume units, elevations in interior-space metres.
	/// </summary>
	public struct HatchSide
	{
		public readonly double AirPressure;
		public readonly double Air;
		public readonly double Water;
		public readonly float Capacity;
		public readonly float WaterElevation;
		public readonly float FloorElevation;

		/// <summary>Volume the air occupies (pressure = Air / FreeVolume). Zero when unknown.</summary>
		public readonly double FreeVolume;

		public HatchSide(double airPressure, double air, double water, float capacity, float waterElevation, float floorElevation, double freeVolume)
		{
			AirPressure = airPressure;
			Air = air;
			Water = water;
			Capacity = capacity;
			WaterElevation = waterElevation;
			FloorElevation = floorElevation;
			FreeVolume = freeVolume > 0.0 ? freeVolume : 0.0;
		}

		/// <summary>Free volume derived the way the game derives pressure: Air / AirPressure, or Capacity - Water.</summary>
		public static HatchSide Create(double airPressure, double air, double water, float capacity, float waterElevation, float floorElevation)
		{
			double free = airPressure > 1e-9 && air > 0.0 ? air / airPressure : capacity - water;
			return new HatchSide(airPressure, air, water, capacity, waterElevation, floorElevation, free);
		}

		/// <summary>Depth of water standing on the floor (m).</summary>
		public float WaterAboveFloor
		{
			get { return WaterElevation - FloorElevation; }
		}

		/// <summary>Water / Capacity; 1 for a compartment without capacity.</summary>
		public double Fill
		{
			get { return Capacity > 0f ? Water / Capacity : 1.0; }
		}

		/// <summary>Room left for water.</summary>
		public double Room
		{
			get { return Math.Max(0.0, Capacity - Water); }
		}

		/// <summary>The same side after <paramref name="airDelta"/> air was added (negative: removed), pressure moved accordingly.</summary>
		public HatchSide WithAirAdded(double airDelta)
		{
			double air = Math.Max(0.0, Air + airDelta);
			double pressure;
			if (FreeVolume > 0.0)
				pressure = air / FreeVolume;
			else
				pressure = Air > 0.0 ? AirPressure * (air / Air) : AirPressure;
			return new HatchSide(pressure, air, Water, Capacity, WaterElevation, FloorElevation, FreeVolume);
		}

		public override string ToString()
		{
			return "HatchSide(p=" + AirPressure + ", air=" + Air + ", water=" + Water + "/" + Capacity + ", we=" + WaterElevation + ", floor=" + FloorElevation + ")";
		}
	}
}
