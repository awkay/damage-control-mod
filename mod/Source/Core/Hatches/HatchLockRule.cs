using System;

namespace DamageControl.Core.Hatches
{
	/// <summary>Everything the "can be opened" rule reads, as plain values.</summary>
	public struct HatchLockInput
	{
		/// <summary>The hatch carries a visible "is being repaired" status.</summary>
		public bool BeingRepaired;

		/// <summary>The game's pressure-blockade flag on the hatch.</summary>
		public bool IsPressureBlockade;

		/// <summary>The P14 setting (hatch pressure blocking).</summary>
		public bool BlockingEnabled;

		public bool HasLeft;
		public bool HasRight;

		/// <summary>The hatch has a pressure-blockade (seat-side) compartment.</summary>
		public bool HasSeat;

		/// <summary>The seat side is one of left/right, so a far side exists.</summary>
		public bool HasFar;

		public double SeatAirPressure;
		public float SeatWaterElevation;
		public double FarAirPressure;
		public float FarWaterElevation;

		/// <summary>The opening's lowest point (02 conventions).</summary>
		public float LowY;

		/// <summary>The hatch belongs to a ship.</summary>
		public bool OnShip;

		public float DeckDepth;

		/// <summary>Lock threshold in atm (tunable, D1).</summary>
		public float Threshold;
	}

	/// <summary>
	/// H1: the pressure-lock "can be opened" rule of 02 §2.1, with water head in physical units (D1).
	/// The Game postfix applies it only while P14 is on (C25); step 1 (repair hold) always applies (C10).
	/// </summary>
	public static class HatchLockRule
	{
		/// <summary>D1: lock threshold, retuned in game through the data row "Hatch Lock Threshold".</summary>
		public const float DefaultThreshold = 0.9f;

		/// <summary>Step 6: a compartment-less hatch on a ship opens only above this deck depth.</summary>
		public const float DeckDepthLimit = 2.5f;

		/// <summary>The full rule, steps 1 to 6 in order.</summary>
		public static bool CanBeOpened(HatchLockInput input)
		{
			if (input.BeingRepaired)
				return false;
			if (!input.IsPressureBlockade)
				return true;
			if (!input.BlockingEnabled)
				return true;
			if (input.HasLeft && input.HasRight && input.HasSeat && input.HasFar)
			{
				double seat = WaterHead.SidePressure(input.SeatAirPressure, input.SeatWaterElevation, input.LowY);
				double far = WaterHead.SidePressure(input.FarAirPressure, input.FarWaterElevation, input.LowY);
				return IsBelowThreshold(seat, far, input.Threshold);
			}
			if (input.HasSeat)
				return true;
			if (input.OnShip)
				return input.DeckDepth < DeckDepthLimit;
			return true;
		}

		/// <summary>Step 4's comparison: seat &lt; far + threshold (strict).</summary>
		public static bool IsBelowThreshold(double seatPressure, double farPressure, float threshold)
		{
			return seatPressure < farPressure + threshold;
		}

		/// <summary>A usable threshold: the tunable when positive and finite, else <see cref="DefaultThreshold"/>.</summary>
		public static float SanitizeThreshold(float value)
		{
			return value > 0f && !float.IsInfinity(value) ? value : DefaultThreshold;
		}
	}
}
