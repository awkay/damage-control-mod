using System;
using DamageControl.Core.Common;

namespace DamageControl.Core.Hatches
{
	/// <summary>Which water-jet effect should play: the one on the side that receives water.</summary>
	public enum JetEffect
	{
		None,
		Left,
		Right
	}

	/// <summary>
	/// Result of one exchange step across a hatch. Positive amounts move left to right, negative right to left.
	/// </summary>
	public struct HatchFlow
	{
		public readonly double Air;
		public readonly double Water;
		public readonly JetEffect Jet;

		public HatchFlow(double air, double water, JetEffect jet)
		{
			Air = air;
			Water = water;
			Jet = jet;
		}

		public static HatchFlow None
		{
			get { return new HatchFlow(0.0, 0.0, JetEffect.None); }
		}

		public override string ToString()
		{
			return "HatchFlow(air=" + Air + ", water=" + Water + ", jet=" + Jet + ")";
		}
	}

	/// <summary>
	/// Rate constants of one user of the exchange kernel (00 C3): the hatch passage itself (02 §2.3) and
	/// the equalize action (01 §2.11) share the formulas and differ only here.
	/// </summary>
	public struct ExchangeProfile
	{
		/// <summary>Air conductance per metre of dry opening above the source water.</summary>
		public readonly float AirSlope;

		/// <summary>Upper bound of the air conductance.</summary>
		public readonly float AirCap;

		/// <summary>Smallest dry clearance (m) used in the conductance.</summary>
		public readonly float AirMinClearance;

		/// <summary>Air moves only when |Δp| exceeds this.</summary>
		public readonly float AirEpsilon;

		/// <summary>Air may also pass when the source has (almost) no water on its floor, even above the vent.</summary>
		public readonly bool AirPassesOverDryFloor;

		/// <summary>Water moves only when |Δ| exceeds this.</summary>
		public readonly float WaterEpsilon;

		/// <summary>Gain of the (1.01 - fill)² inflow choke; 0 disables the choke.</summary>
		public readonly float WaterChokeGain;

		public ExchangeProfile(float airSlope, float airCap, float airMinClearance, float airEpsilon, bool airPassesOverDryFloor, float waterEpsilon, float waterChokeGain)
		{
			AirSlope = airSlope;
			AirCap = airCap;
			AirMinClearance = airMinClearance;
			AirEpsilon = airEpsilon;
			AirPassesOverDryFloor = airPassesOverDryFloor;
			WaterEpsilon = waterEpsilon;
			WaterChokeGain = waterChokeGain;
		}

		/// <summary>An open (or leaking) hatch, 02 §2.3g.</summary>
		public static ExchangeProfile Passage
		{
			get { return new ExchangeProfile(10f, 10f, 0f, 0.001f, false, 1e-6f, 10f); }
		}

		/// <summary>The equalize action's vent and valve, 01 §2.11.</summary>
		public static ExchangeProfile Equalize
		{
			get { return new ExchangeProfile(250f, 50f, 0.01f, 0.001f, true, 0.001f, 0f); }
		}
	}

	/// <summary>
	/// The hatch air/water exchange kernel (H4, owner of 00 §2.5 "exchange kernel"). Pure functions of two
	/// <see cref="HatchSide"/>s, the hatch geometry and a step. The Game layer applies the returned flow.
	///
	/// Differences from the spec text, all guards (D7): a source never gives air below
	/// <see cref="AirFloor"/>; air never moves past the amount that equalizes the two pressures (keeps
	/// small compartments from oscillating at low frame rates); water never overfills the receiving side.
	/// </summary>
	public static class HatchExchange
	{
		/// <summary>Per-frame cap on the game-time delta (02 §2.3b, 01 §2.11).</summary>
		public const float MaxFrameSeconds = 0.5f;

		/// <summary>Base exchange rate k at 1x compression (02 §2.3b).</summary>
		public const float DefaultRate = 2f;

		/// <summary>The equalize step is the hatch step divided by this (01 §2.11: k = dt x r / 10).</summary>
		public const float EqualizeDivisor = 10f;

		/// <summary>A compartment never gives away air below this amount.</summary>
		public const double AirFloor = 0.2;

		/// <summary>A closed, leaking hatch passes this fraction of the open rate (02 §2.3e).</summary>
		public const float ClosedLeakFactor = 0.02f;

		/// <summary>Water that the vertical passage drains downward at least, per unit step (02 §2.3f).</summary>
		public const float VerticalMinDrain = 0.1f;

		/// <summary>Fallback opening height of the vertical passage above the lower floor (02 §2.3f), when the geometry is unusable.</summary>
		public const float DefaultVerticalOpeningHeight = 1.85f;

		/// <summary>
		/// Step s of 02 §2.3b: game dt capped at 0.5 s, times the base rate damped as rate / sqrt(S) under
		/// compression S &gt; 1. Zero while paused.
		/// </summary>
		public static float FrameStep(SimClock clock, float baseRate)
		{
			return clock.CappedGameDt(MaxFrameSeconds) * clock.DampedRate(baseRate);
		}

		/// <summary><see cref="FrameStep(SimClock, float)"/> at <see cref="DefaultRate"/>.</summary>
		public static float FrameStep(SimClock clock)
		{
			return FrameStep(clock, DefaultRate);
		}

		/// <summary>Equalize factor k of 01 §2.11: the hatch step / 10 (0.2/s at 1x).</summary>
		public static float EqualizeStep(SimClock clock, float baseRate)
		{
			return FrameStep(clock, baseRate) / EqualizeDivisor;
		}

		/// <summary>
		/// Flow gate of 02 §2.3e. Open: the full step. Closed and leaking: step x 0.02 x
		/// <paramref name="sealFactor"/> (see <see cref="HatchSeals.SealFactor"/>). Closed and tight: 0.
		/// </summary>
		public static float GatedStep(float step, bool open, bool leaking, float sealFactor)
		{
			if (open)
				return step;
			if (!leaking)
				return 0f;
			return step * ClosedLeakFactor * DcMath.Clamp01(sealFactor);
		}

		/// <summary>An open horizontal passage (02 §2.3g): air through the opening up to highY, water over the sill at lowY.</summary>
		public static HatchFlow Horizontal(HatchSide left, HatchSide right, HatchGeometry geometry, float step)
		{
			return Exchange(left, right, geometry.HighY, geometry.LowY, ExchangeProfile.Passage, step);
		}

		/// <summary>
		/// The equalize action (01 §2.11) with the vent at highY and the valve at lowY (00 C3).
		/// <paramref name="k"/> is <see cref="EqualizeStep"/>.
		/// </summary>
		public static HatchFlow Equalize(HatchSide left, HatchSide right, HatchGeometry geometry, float k)
		{
			HatchFlow flow = Exchange(left, right, geometry.HighY, geometry.LowY, ExchangeProfile.Equalize, k);
			return new HatchFlow(flow.Air, flow.Water, JetEffect.None);
		}

		/// <summary>
		/// The shared kernel: air step, then water step using the pressures after the air step.
		/// <paramref name="ventY"/> is the top of the air path, <paramref name="sillY"/> the water reference height.
		/// </summary>
		public static HatchFlow Exchange(HatchSide left, HatchSide right, float ventY, float sillY, ExchangeProfile profile, float step)
		{
			if (!(step > 0f))
				return HatchFlow.None;

			double air = AirStep(left, right, ventY, profile, step);
			if (air != 0.0)
			{
				left = left.WithAirAdded(-air);
				right = right.WithAirAdded(air);
			}

			double d = WaterHead.SidePressure(left.AirPressure, left.WaterElevation, sillY)
				- WaterHead.SidePressure(right.AirPressure, right.WaterElevation, sillY);
			double water = 0.0;
			JetEffect jet = JetEffect.None;
			if (d > profile.WaterEpsilon && left.WaterElevation > sillY)
			{
				water = WaterAmount(d, left, right, profile, step);
				jet = JetEffect.Right;
			}
			else if (d < -profile.WaterEpsilon && right.WaterElevation > sillY)
			{
				water = -WaterAmount(-d, right, left, profile, step);
				jet = JetEffect.Left;
			}
			return new HatchFlow(air, water, jet);
		}

		/// <summary>
		/// Vertical passage (02 §2.3f): left is the lower compartment, right the upper one.
		/// <paramref name="openingHeight"/> is the opening's height above the lower floor (see
		/// <see cref="VerticalOpeningHeight"/>). No water jets are driven.
		/// </summary>
		public static HatchFlow Vertical(HatchSide lower, HatchSide upper, float openingHeight, float step)
		{
			if (!(step > 0f))
				return HatchFlow.None;

			float hLower = lower.WaterAboveFloor;
			double dp = lower.AirPressure - upper.AirPressure;
			double air = 0.0;
			if (hLower < openingHeight)
			{
				double conductance = Math.Min(10.0, (openingHeight - hLower) * 10.0);
				if (dp > 0.001)
					air = AirAmount(lower, upper, dp * conductance * step);
				else if (dp < -0.001)
					air = -AirAmount(upper, lower, -dp * conductance * step);
			}
			if (air != 0.0)
			{
				lower = lower.WithAirAdded(-air);
				upper = upper.WithAirAdded(air);
			}

			double d2 = lower.AirPressure - upper.AirPressure - WaterHead.Atm(upper.WaterAboveFloor);
			double water = 0.0;
			if (d2 > 1e-6 && hLower > openingHeight)
			{
				double choke = Square(1.005 - Math.Min(1.005, upper.Fill)) * step;
				water = Math.Min(lower.Water, Math.Min(d2 * step, choke));
				water = Math.Min(Math.Max(0.0, water), upper.Room);
			}
			else if (upper.Water > 0.0)
			{
				double choke = Square(1.005 - Math.Min(1.005, lower.Fill)) * step;
				double down = Math.Min(upper.Water, Math.Max(VerticalMinDrain * step, Math.Min(-d2 * step, choke)));
				water = -Math.Min(Math.Max(0.0, down), lower.Room);
			}
			return new HatchFlow(air, water, JetEffect.None);
		}

		/// <summary>
		/// Opening height of the vertical passage above the lower compartment's floor: the hatch's lowY
		/// minus that floor (00 C12, replaces the 2022.1 constant). Falls back to 1.85 m when not positive.
		/// </summary>
		public static float VerticalOpeningHeight(float lowY, float lowerFloorElevation)
		{
			float h = lowY - lowerFloorElevation;
			return DcMath.IsFinite(h) && h > DcMath.Epsilon ? h : DefaultVerticalOpeningHeight;
		}

		private static double AirStep(HatchSide left, HatchSide right, float ventY, ExchangeProfile profile, float step)
		{
			double dp = left.AirPressure - right.AirPressure;
			if (dp > profile.AirEpsilon && AirCanLeave(left, ventY, profile))
				return AirAmount(left, right, dp * Conductance(left, ventY, profile) * step);
			if (dp < -profile.AirEpsilon && AirCanLeave(right, ventY, profile))
				return -AirAmount(right, left, -dp * Conductance(right, ventY, profile) * step);
			return 0.0;
		}

		private static bool AirCanLeave(HatchSide source, float ventY, ExchangeProfile profile)
		{
			return source.WaterElevation < ventY || (profile.AirPassesOverDryFloor && source.WaterAboveFloor < 0.01f);
		}

		private static double Conductance(HatchSide source, float ventY, ExchangeProfile profile)
		{
			double clearance = Math.Max(profile.AirMinClearance, ventY - source.WaterElevation);
			return Math.Min(profile.AirCap, clearance * profile.AirSlope);
		}

		/// <summary>Amount of air moving from <paramref name="source"/> to <paramref name="sink"/>, limited (always &gt;= 0).</summary>
		private static double AirAmount(HatchSide source, HatchSide sink, double wanted)
		{
			double amount = Math.Min(Math.Max(0.0, source.Air - AirFloor), wanted);
			if (source.FreeVolume > 0.0 && sink.FreeVolume > 0.0)
			{
				double dp = source.AirPressure - sink.AirPressure;
				double equalizing = dp / (1.0 / source.FreeVolume + 1.0 / sink.FreeVolume);
				amount = Math.Min(amount, Math.Max(0.0, equalizing));
			}
			return amount > 0.0 && DcMath.IsFinite(amount) ? amount : 0.0;
		}

		/// <summary>Water moving from <paramref name="source"/> to <paramref name="sink"/> for driving term d &gt; 0 (always &gt;= 0).</summary>
		private static double WaterAmount(double d, HatchSide source, HatchSide sink, ExchangeProfile profile, float step)
		{
			double amount = Math.Min(source.Water, d * step);
			if (profile.WaterChokeGain > 0f)
				amount = Math.Min(amount, Square(1.01 - Math.Min(1.01, sink.Fill)) * step * profile.WaterChokeGain);
			amount = Math.Min(amount, sink.Room);
			return amount > 0.0 && DcMath.IsFinite(amount) ? amount : 0.0;
		}

		private static double Square(double x)
		{
			return x * x;
		}
	}
}
