using System;
using DamageControl.Core.Common;

namespace DamageControl.Core.Repair
{
	/// <summary>Saved progress of one dismantle order (the action keeps these three numbers).</summary>
	public struct DismantleState
	{
		/// <summary>Progress toward the next health point, 0 to 1.</summary>
		public float PointProgress;

		/// <summary>Cumulative progress, in health points.</summary>
		public float TotalProgress;

		/// <summary>Health when the work began (W), at least 1.</summary>
		public float TotalWork;
	}

	/// <summary>Result of one dismantle frame.</summary>
	public struct DismantleStep
	{
		public DismantleState State;

		/// <summary>The device health to write back.</summary>
		public float Health;

		/// <summary>The device reached 0: yield scrap and end the order.</summary>
		public bool Finished;

		/// <summary>Overall progress for the bar, below 1 until finished.</summary>
		public float Progress;

		/// <summary>
		/// The order finished and its own work took the device apart (at least one point of work done), so
		/// scrap is due. A device that lost its health to damage before any work was done yields nothing.
		/// </summary>
		public bool YieldsScrap;
	}

	/// <summary>
	/// R6: "dismantle for scrap" (04 §2.3, Q3). An engineer takes a working device apart one health point
	/// at a time, at repair speed without the order's performance parameter, and recovers 1 scrap
	/// (2 with Thrifty) when it reaches 0. Fixed against the original (D7): a non-integer remainder below
	/// 1 is clamped to 0 so the order always ends, and the order checks occupancy, duplicates, flooding
	/// and a working device before it is offered.
	/// </summary>
	public static class Dismantle
	{
		public const float HandymanMultiplier = 1.25f;

		/// <summary>Wheel position: listed last.</summary>
		public const int SortingPriority = -1;

		/// <summary>Bar progress never reaches 1 before the order ends (the use behaviour would end it early).</summary>
		public const float ProgressCap = 0.999f;

		/// <summary>The order is offered only for equipment with an interactable and at least 1 health.</summary>
		public static bool IsOffered(bool hasEquipment, bool hasInteractable, float health)
		{
			return hasEquipment && hasInteractable && !float.IsNaN(health) && health >= 1f;
		}

		/// <summary>1.25 with the Handyman perk, else 1.</summary>
		public static float PerformanceMultiplier(bool handyman)
		{
			return handyman ? HandymanMultiplier : 1f;
		}

		/// <summary>Scrap recovered: 1, or 2 with the Thrifty perk.</summary>
		public static int ScrapYield(bool thrifty)
		{
			return thrifty ? 2 : 1;
		}

		/// <summary>
		/// Seconds per health point: RepairTime / (character performance x multiplier). A non-positive
		/// or non-finite repair time counts as 1 s; a non-positive performance means no progress.
		/// </summary>
		public static float RatePerSecond(float characterPerformance, float multiplier, float repairTime)
		{
			if (!DcMath.IsFinite(repairTime) || repairTime <= 0f)
				repairTime = 1f;
			if (!DcMath.IsFinite(characterPerformance) || characterPerformance <= 0f || !DcMath.IsFinite(multiplier) || multiplier <= 0f)
				return 0f;
			return characterPerformance * multiplier / repairTime;
		}

		/// <summary>The state at the start of the work: W = current health (at least 1).</summary>
		public static DismantleState Begin(float health)
		{
			return new DismantleState { TotalWork = DcMath.IsFinite(health) && health > 1f ? health : 1f };
		}

		/// <summary>
		/// Advances the work by <paramref name="dt"/> seconds at <paramref name="ratePerSecond"/> points per
		/// second against the device's current <paramref name="health"/>. Every full point removes 1 health;
		/// whatever is left below 1 is clamped to 0, which finishes the order.
		/// </summary>
		public static DismantleStep Advance(DismantleState state, float health, float ratePerSecond, float dt)
		{
			if (!DcMath.IsFinite(health) || health < 0f)
				health = 0f;
			if (state.TotalWork < 1f)
				state.TotalWork = 1f;
			float delta = DcMath.IsFinite(dt) && dt > 0f && DcMath.IsFinite(ratePerSecond) && ratePerSecond > 0f ? ratePerSecond * dt : 0f;
			state.PointProgress += delta;
			state.TotalProgress += delta;
			while (state.PointProgress >= 1f && health >= 1f)
			{
				state.PointProgress -= 1f;
				health -= 1f;
			}
			if (health < 1f)
				health = 0f;
			DismantleStep step = new DismantleStep { State = state, Health = health, Finished = health <= 0f };
			step.YieldsScrap = step.Finished && state.TotalProgress >= 1f;
			step.Progress = Math.Min(ProgressCap, ProgressCap * state.TotalProgress / state.TotalWork);
			return step;
		}
	}
}
