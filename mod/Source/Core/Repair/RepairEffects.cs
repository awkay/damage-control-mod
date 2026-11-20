using DamageControl.Core.Common;

namespace DamageControl.Core.Repair
{
	/// <summary>What the repair does to the target this frame (R2, R3).</summary>
	public struct RepairFrame
	{
		/// <summary>Register the repair as a seal source on the hatch (C9: presence, x0.1).</summary>
		public bool SealHatch;

		/// <summary>Hold the leakable equipment's leak-intensity throttle modifier at <see cref="ThrottleValue"/>.</summary>
		public bool ThrottleLeak;

		/// <summary>1 - P while throttling, else 1.</summary>
		public float ThrottleValue;

		/// <summary>Close the hatch now (once per repair, C10).</summary>
		public bool CloseHatch;

		/// <summary>Drop every effect this repair holds (seal, throttle).</summary>
		public bool Release;
	}

	/// <summary>Per-repair memory kept by the Game layer (session-only).</summary>
	public sealed class RepairTrack
	{
		/// <summary>True once the first interacting frame has been seen.</summary>
		public bool Started;

		/// <summary>Decided on the first interacting frame: the target was leaking leakable equipment (04 §2.1.9).</summary>
		public bool Throttles;
	}

	/// <summary>
	/// R2 and R3 (04 §2.1.7 to §2.1.9 as resolved by 00 C9 and C10). While the repairer is interacting:
	/// <list type="bullet">
	/// <item>a hatch carries one repair seal source in <c>HatchSeals</c>;</item>
	/// <item>leakable equipment that was leaking when the work began has its leak intensity scaled by
	/// <c>1 - P</c>, P being the repair's overall progress; equipment that was not leaking is never
	/// throttled, so a missing leak status cannot throw;</item>
	/// <item>an interior hatch with compartments is closed once on the first interacting frame and then held
	/// by the hatch rules (H1 step 1, H2) while the repair status shows. Nothing reopens it afterwards.</item>
	/// </list>
	/// When the repairer is not interacting, or the repair stops, every effect is released.
	/// </summary>
	public static class RepairEffects
	{
		/// <summary>The leak-intensity scale while repairing: 1 - P, P clamped to [0, 1]; NaN counts as 0.</summary>
		public static float ThrottleValue(float progress)
		{
			if (float.IsNaN(progress))
				progress = 0f;
			return 1f - DcMath.Clamp01(progress);
		}

		/// <summary>True when an R3 close applies to this hatch: interior, with compartments, currently open.</summary>
		public static bool ClosesHatch(bool isExteriorHatch, bool hasCompartments, bool locked)
		{
			return !isExteriorHatch && hasCompartments && !locked;
		}

		/// <summary>
		/// One frame of a repair, after vanilla's own update. <paramref name="track"/> is updated in place.
		/// </summary>
		public static RepairFrame Step(RepairTrack track, bool interacting, bool isHatch, bool isExteriorHatch,
			bool hatchHasCompartments, bool hatchLocked, bool isLeakable, bool leakingVisible, float progress)
		{
			RepairFrame frame = new RepairFrame { ThrottleValue = 1f };
			if (!interacting)
			{
				frame.Release = true;
				return frame;
			}
			if (!track.Started)
			{
				track.Started = true;
				track.Throttles = !isHatch && isLeakable && leakingVisible;
				frame.CloseHatch = isHatch && ClosesHatch(isExteriorHatch, hatchHasCompartments, hatchLocked);
			}
			if (isHatch)
				frame.SealHatch = true;
			if (track.Throttles)
			{
				frame.ThrottleLeak = true;
				frame.ThrottleValue = ThrottleValue(progress);
			}
			return frame;
		}
	}
}
