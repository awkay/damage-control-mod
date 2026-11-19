using System;

namespace DamageControl.Core.Pressure
{
	/// <summary>Plain inputs for one physics step of P9.</summary>
	public struct VentingInput
	{
		/// <summary>Game seconds of this physics step.</summary>
		public float Dt;

		/// <summary>Deck depth (m); below 0 means the deck is above the surface.</summary>
		public float DeckDepth;

		/// <summary>Conning-tower air pressure (atm) and free volume (m3: capacity - water - fuel).</summary>
		public double TowerPressure;
		public double TowerFreeVolume;
		public bool ExteriorHatchOpen;

		/// <summary>Diesel-compartment air pressure (atm) and free volume.</summary>
		public double DieselPressure;
		public double DieselFreeVolume;

		/// <summary>A snorkel is fitted, and its performance (0..1).</summary>
		public bool HasSnorkel;
		public float SnorkelPerformance;

		/// <summary>The active propulsion engine is the diesel, and its fuel usage rate (game units per second, any sign).</summary>
		public bool DieselActive;
		public float DieselFuelUsage;
	}

	/// <summary>Air-amount changes for one physics step of P9.</summary>
	public struct VentingResult
	{
		public double TowerAirDelta;
		public double DieselAirDelta;
	}

	/// <summary>
	/// P9 (01 §2.9): the conning tower and the diesel compartment re-ventilate toward 1 atm while they
	/// are connected to the atmosphere, and a running diesel draws air from its compartment. Each rule
	/// is evaluated on the pressures at the start of the step and their effects stack.
	/// </summary>
	public static class Venting
	{
		public const double TargetPressure = 1.0;

		/// <summary>Rate through the open exterior hatch, and from a nearly surfaced tower (1/s).</summary>
		public const double TowerHatchRate = 10.0;
		public const double TowerSurfacedRate = 1.0;
		public const float TowerSurfacedDeckDepth = 2f;

		/// <summary>Diesel induction above water, and through a working snorkel (1/s).</summary>
		public const double DieselInductionRate = 10.0;
		public const float DieselInductionDeckDepth = 3.5f;
		public const double DieselSnorkelRate = 10.0;
		public const float DieselSnorkelDeckDepth = 7f;
		public const float MinSnorkelPerformance = 0.5f;

		/// <summary>The diesel stops drawing once its compartment is at or below this pressure (atm).</summary>
		public const double DieselMinPressure = 0.35;
		public const double DieselDrawScale = 10000.0;
		public const double MaxStepChange = 1.0;
		public const double MaxDieselDrawPerStep = 0.1;

		/// <summary>
		/// One equalization rule: sign(1 - p) x min(|1 - p| x dt x k, 1). With a known free volume
		/// (&gt; 0) the change is also capped by <see cref="CapToTarget"/>; pass 0 to skip that cap.
		/// </summary>
		public static double EqualizationStep(double pressure, double dt, double rate, double freeVolume)
		{
			if (double.IsNaN(pressure) || !(dt > 0.0) || !(rate > 0.0))
				return 0.0;
			double delta = TargetPressure - pressure;
			double magnitude = Math.Min(Math.Abs(delta) * dt * rate, MaxStepChange);
			return CapToTarget(delta < 0.0 ? -magnitude : magnitude, pressure, freeVolume);
		}

		/// <summary>Air drawn by a running diesel this step (a positive amount to subtract).</summary>
		public static double DieselDraw(bool dieselActive, double dieselPressure, float fuelUsage, double dt)
		{
			if (!dieselActive || !(dieselPressure > DieselMinPressure) || !(dt > 0.0))
				return 0.0;
			double draw = Math.Abs(fuelUsage * dt * DieselDrawScale);
			if (double.IsNaN(draw))
				return 0.0;
			return Math.Min(draw, MaxDieselDrawPerStep);
		}

		/// <summary>
		/// Caps a summed equalization change at the amount that brings the pressure exactly to 1 atm
		/// (|1 - p| x free volume), so stacked rules or long steps never overshoot. A free volume of 0 or
		/// less means unknown and leaves the change alone.
		/// </summary>
		public static double CapToTarget(double change, double pressure, double freeVolume)
		{
			if (!(freeVolume > 0.0) || double.IsNaN(pressure))
				return change;
			double limit = Math.Abs(TargetPressure - pressure) * freeVolume;
			if (change > limit)
				return limit;
			if (change < -limit)
				return -limit;
			return change;
		}

		public static VentingResult Step(VentingInput input)
		{
			var result = new VentingResult();
			double dt = input.Dt;

			double tower = 0.0;
			if (input.ExteriorHatchOpen)
				tower += EqualizationStep(input.TowerPressure, dt, TowerHatchRate, 0.0);
			if (input.DeckDepth < TowerSurfacedDeckDepth)
				tower += EqualizationStep(input.TowerPressure, dt, TowerSurfacedRate, 0.0);
			result.TowerAirDelta = CapToTarget(tower, input.TowerPressure, input.TowerFreeVolume);

			double diesel = 0.0;
			if (input.DeckDepth < DieselInductionDeckDepth)
				diesel += EqualizationStep(input.DieselPressure, dt, DieselInductionRate, 0.0);
			if (input.DeckDepth < DieselSnorkelDeckDepth && input.HasSnorkel && input.SnorkelPerformance >= MinSnorkelPerformance)
				diesel += EqualizationStep(input.DieselPressure, dt, DieselSnorkelRate, 0.0);
			result.DieselAirDelta = CapToTarget(diesel, input.DieselPressure, input.DieselFreeVolume)
				- DieselDraw(input.DieselActive, input.DieselPressure, input.DieselFuelUsage, dt);
			return result;
		}
	}
}
