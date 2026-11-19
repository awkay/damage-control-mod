using System;
using System.Collections.Generic;
using DamageControl.Core.Common;

namespace DamageControl.Core.Ballast
{
	/// <summary>Why the Exhaust Blow order is not available right now (B2, 03 §2.A, §7.4, 00 D5).</summary>
	public enum ExhaustBlowBlock
	{
		/// <summary>Available.</summary>
		None = 0,

		/// <summary>The boat is docked.</summary>
		Docked = 1,

		/// <summary>The diesel is off, unmounted, or turning too slowly to give exhaust.</summary>
		DieselNotRunning = 2,

		/// <summary>The deck is not above the water, so the exhaust cannot be led into the tanks.</summary>
		DeckUnderWater = 3,

		/// <summary>The main ballast is already dry; there is nothing to blow.</summary>
		BallastEmpty = 4,
	}

	/// <summary>Plain readings the availability and cancel rules need. The Game layer fills it from the player ship.</summary>
	public struct ExhaustBlowConditions
	{
		public bool Docked;

		/// <summary>The diesel engine component exists and is enabled (not unmounted, not broken off).</summary>
		public bool DieselEnabled;

		/// <summary>Diesel crankshaft speed in RPM; the sign (ahead or astern) is ignored.</summary>
		public float DieselRpm;

		/// <summary>Deck depth in metres, unclamped: negative while the deck is above the water.</summary>
		public float DeckDepth;

		/// <summary>Water left in the main ballast tanks, summed.</summary>
		public double BallastWater;
	}

	/// <summary>Tunable numbers of the Exhaust Blow. <see cref="Default"/> holds the spec values.</summary>
	public struct ExhaustBlowSettings
	{
		/// <summary>Air units per crankshaft revolution (03 §2.A: 0.1933768).</summary>
		public float FlowPerRevolution;

		/// <summary>The end tanks must each hold less than this much water before the middle tanks get air (03 §2.A: 5).</summary>
		public float Stage2WaterThreshold;

		/// <summary>The blow is complete once the ballast holds at most this much water in total (03 §7.3: epsilon instead of exact 0).</summary>
		public double CompletionWater;

		/// <summary>Below this RPM the diesel counts as not running (03 §7.4: RPM &gt; 0).</summary>
		public float MinimumRpm;

		/// <summary>The deck counts as above the water while its depth is below this (metres; 0 = exactly at the waterline).</summary>
		public float DeckClearance;

		/// <summary>A running blow is cancelled once a gate has failed for this many game seconds without a break (waves).</summary>
		public float CancelGraceSeconds;

		/// <summary>Largest game-time step one frame may integrate, so a hitch or a load does not dump a burst of air.</summary>
		public float MaxStepSeconds;

		public static ExhaustBlowSettings Default
		{
			get
			{
				return new ExhaustBlowSettings
				{
					FlowPerRevolution = ExhaustBlow.FlowPerRevolution,
					Stage2WaterThreshold = ExhaustBlow.Stage2WaterThreshold,
					CompletionWater = ExhaustBlow.CompletionWater,
					MinimumRpm = ExhaustBlow.MinimumRpm,
					DeckClearance = ExhaustBlow.DeckClearance,
					CancelGraceSeconds = ExhaustBlow.CancelGraceSeconds,
					MaxStepSeconds = ExhaustBlow.MaxStepSeconds,
				};
			}
		}
	}

	/// <summary>
	/// B2 Exhaust Blow (03 §2.A, 00 D5): a crewman at the flooding-and-blowing valve station leads diesel
	/// exhaust into the main ballast, which empties the tanks without spending bottle air. v1 is a manual
	/// order only, available while the diesel runs and the deck is above water, and cancelled when either
	/// stops being true (03 §7.4, D7).
	///
	/// Tank roles: the ship's ballast tanks in the game's own list order. The two end tanks of that list are
	/// blown first (stage 1); the tanks between them join once both ends are nearly dry (stage 2). For the
	/// five-tank boats this is the spec's position 1..5 mapping exactly; other tank counts use the same
	/// ends-then-middle rule with the middle share split evenly.
	/// </summary>
	public static class ExhaustBlow
	{
		public const float FlowPerRevolution = 0.1933768f;
		public const float Stage2WaterThreshold = 5f;
		public const double CompletionWater = 0.05;
		public const float MinimumRpm = 1f;
		public const float DeckClearance = 0f;
		public const float CancelGraceSeconds = 3f;
		public const float MaxStepSeconds = 5f;

		/// <summary>Progress never reaches 1, so the game's own "skill progress reached 1" completion never fires; we complete explicitly.</summary>
		public const float ProgressScale = 0.999f;

		/// <summary>Stage 1 shares of the first and the last tank in the list (03 §2.A: 52.5 % and 47.5 %).</summary>
		public const float FirstEndShare = 0.525f;
		public const float LastEndShare = 0.475f;

		/// <summary>Stage 2 shares of the three middle tanks of a five-tank boat, in list order (03 §2.A: 20 %, 55 %, 25 %).</summary>
		private static readonly float[] FiveTankMiddleShares = { 0.20f, 0.55f, 0.25f };

		/// <summary>
		/// Exhaust air for one step: (rpm / 60) x flow-per-revolution x dt. Zero when the diesel is disabled,
		/// whatever the RPM (03 §6 #1), and for a non-positive or non-finite step.
		/// </summary>
		public static float Flow(bool dieselEnabled, float rpm, float dt, float flowPerRevolution)
		{
			if (!dieselEnabled || !DcMath.IsFinite(rpm) || !DcMath.IsFinite(dt) || dt <= 0f || !DcMath.IsFinite(flowPerRevolution) || flowPerRevolution <= 0f)
				return 0f;
			return Math.Abs(rpm) / 60f * flowPerRevolution * dt;
		}

		/// <summary><see cref="Flow(bool, float, float, float)"/> with the spec constant.</summary>
		public static float Flow(bool dieselEnabled, float rpm, float dt)
		{
			return Flow(dieselEnabled, rpm, dt, FlowPerRevolution);
		}

		/// <summary>
		/// Splits <paramref name="flow"/> over the tanks (03 §6 #2, #3). <paramref name="air"/> receives the air to
		/// add to each tank, same order as <paramref name="water"/>; it must be at least as long. A tank with no
		/// water (water &lt;= 0) gets nothing. Stage 2 adds to stage 1, it is not a hand-off (03 §2.A, §7.5).
		/// </summary>
		public static void Distribute(float flow, IList<double> water, float[] air, float stage2WaterThreshold)
		{
			if (water == null)
				throw new ArgumentNullException("water");
			if (air == null)
				throw new ArgumentNullException("air");
			int n = water.Count;
			if (air.Length < n)
				throw new ArgumentException("air must have at least one slot per tank", "air");
			for (int i = 0; i < n; i++)
				air[i] = 0f;
			if (n == 0 || !DcMath.IsFinite(flow) || flow <= 0f)
				return;

			if (n == 1)
			{
				Give(air, water, 0, flow);
				return;
			}

			int last = n - 1;
			Give(air, water, 0, flow * FirstEndShare);
			Give(air, water, last, flow * LastEndShare);

			if (n == 2 || !(water[0] < stage2WaterThreshold && water[last] < stage2WaterThreshold))
				return;

			int middle = n - 2;
			for (int k = 0; k < middle; k++)
			{
				float share = middle == FiveTankMiddleShares.Length ? FiveTankMiddleShares[k] : 1f / middle;
				Give(air, water, k + 1, flow * share);
			}
		}

		/// <summary><see cref="Distribute(float, IList{double}, float[], float)"/> with the spec threshold.</summary>
		public static void Distribute(float flow, IList<double> water, float[] air)
		{
			Distribute(flow, water, air, Stage2WaterThreshold);
		}

		/// <summary>
		/// 0.999 x (capacity - water) / capacity over all tanks (03 §6 #4). Never reaches 1. A boat with no
		/// capacity reports 0.
		/// </summary>
		public static float Progress(IList<double> water, IList<float> capacity)
		{
			if (water == null || capacity == null)
				return 0f;
			double totalCapacity = 0, totalWater = 0;
			int n = Math.Min(water.Count, capacity.Count);
			for (int i = 0; i < n; i++)
			{
				if (capacity[i] > 0f && DcMath.IsFinite(capacity[i]))
				{
					totalCapacity += capacity[i];
					totalWater += DcMath.Clamp(water[i], 0.0, capacity[i]);
				}
			}
			if (totalCapacity <= 0)
				return 0f;
			return ProgressScale * DcMath.Clamp01((float)((totalCapacity - totalWater) / totalCapacity));
		}

		/// <summary>Total water in the tanks; negative readings count as 0.</summary>
		public static double TotalWater(IList<double> water)
		{
			double total = 0;
			if (water == null)
				return total;
			for (int i = 0; i < water.Count; i++)
			{
				if (water[i] > 0)
					total += water[i];
			}
			return total;
		}

		/// <summary>Completion (03 §6 #5 with the §7.3 epsilon): the ballast holds at most <paramref name="completionWater"/> in total.</summary>
		public static bool IsComplete(double totalWater, double completionWater)
		{
			if (double.IsNaN(totalWater))
				return false;
			return totalWater <= Math.Max(0.0, completionWater);
		}

		/// <summary><see cref="IsComplete(double, double)"/> with the spec epsilon.</summary>
		public static bool IsComplete(double totalWater)
		{
			return IsComplete(totalWater, CompletionWater);
		}

		/// <summary>The diesel gives exhaust: enabled and turning faster than <paramref name="minimumRpm"/>, ahead or astern.</summary>
		public static bool IsDieselRunning(bool dieselEnabled, float rpm, float minimumRpm)
		{
			return dieselEnabled && DcMath.IsFinite(rpm) && Math.Abs(rpm) > Math.Max(0f, minimumRpm);
		}

		/// <summary>The deck is above the water: its unclamped depth is below <paramref name="clearance"/>.</summary>
		public static bool IsDeckAboveWater(float deckDepth, float clearance)
		{
			return DcMath.IsFinite(deckDepth) && deckDepth < clearance;
		}

		/// <summary>
		/// Availability (00 §6 WS4, 03 §6 #7, §7.4). Checked in this order: docked, diesel, deck, ballast dry.
		/// The device, duplicate and slot checks are the game's own and run after this.
		/// </summary>
		public static ExhaustBlowBlock Availability(ExhaustBlowConditions c, ExhaustBlowSettings s)
		{
			if (c.Docked)
				return ExhaustBlowBlock.Docked;
			if (!IsDieselRunning(c.DieselEnabled, c.DieselRpm, s.MinimumRpm))
				return ExhaustBlowBlock.DieselNotRunning;
			if (!IsDeckAboveWater(c.DeckDepth, s.DeckClearance))
				return ExhaustBlowBlock.DeckUnderWater;
			if (IsComplete(c.BallastWater, s.CompletionWater))
				return ExhaustBlowBlock.BallastEmpty;
			return ExhaustBlowBlock.None;
		}

		/// <summary>The gates a running blow must keep passing: not docked, diesel running, deck above water.</summary>
		public static bool GatesHold(ExhaustBlowConditions c, ExhaustBlowSettings s)
		{
			ExhaustBlowBlock block = Availability(c, s);
			return block == ExhaustBlowBlock.None || block == ExhaustBlowBlock.BallastEmpty;
		}

		private static void Give(float[] air, IList<double> water, int index, float amount)
		{
			if (water[index] > 0)
				air[index] += amount;
		}
	}

	/// <summary>
	/// Debounces the cancel gate of a running Exhaust Blow: it trips once the gates have failed for longer than
	/// the grace period without a break, so a wave washing over the deck for a moment does not end the order.
	/// </summary>
	public sealed class ExhaustBlowGateWatch
	{
		private float failingFor;

		/// <summary>How long the gates have been failing, in game seconds.</summary>
		public float FailingFor
		{
			get { return failingFor; }
		}

		/// <summary>Feeds one step; returns true when the order must be cancelled.</summary>
		public bool Update(bool gatesHold, float dt, float graceSeconds)
		{
			if (gatesHold)
			{
				failingFor = 0f;
				return false;
			}
			if (DcMath.IsFinite(dt) && dt > 0f)
				failingFor += dt;
			return failingFor > Math.Max(0f, graceSeconds);
		}

		public void Reset()
		{
			failingFor = 0f;
		}
	}
}
