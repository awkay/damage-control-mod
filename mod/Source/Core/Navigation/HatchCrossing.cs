using System.Collections.Generic;
using DamageControl.Core.Common;

namespace DamageControl.Core.Navigation
{
	/// <summary>One interior hatch as the reachability walk sees it. Compartments are plain ids (<see cref="HatchCrossing.NoCompartment"/> = none).</summary>
	public struct CrossingHatch
	{
		/// <summary>Position of the hatch along the hull (the game's obstacle Z).</summary>
		public float ObstacleZ;

		/// <summary>Compartment on the lower-Z ("left") side.</summary>
		public int LeftCompartment;

		/// <summary>Compartment on the higher-Z ("right") side.</summary>
		public int RightCompartment;

		/// <summary>Held shut by Damage Control (see <see cref="HatchCrossing.HoldOf"/>).</summary>
		public bool Blocked;

		public CrossingHatch(float obstacleZ, int leftCompartment, int rightCompartment, bool blocked)
		{
			ObstacleZ = obstacleZ;
			LeftCompartment = leftCompartment;
			RightCompartment = rightCompartment;
			Blocked = blocked;
		}
	}

	/// <summary>Why Damage Control is holding a hatch shut (B9, B10). Vanilla's own lock rules never count.</summary>
	public enum HatchHold
	{
		/// <summary>Not held by the mod: open, exterior, compartment-less, or closed only by vanilla's rules.</summary>
		None,

		/// <summary>A repair is under way and shows its status (02 C10, the R3 repair hold; applies with P14 off too).</summary>
		RepairHold,

		/// <summary>The pressure lock (H1) while hatch pressure blocking (P14) is on.</summary>
		PressureLock
	}

	/// <summary>One hatch's state as the hold rule reads it, as plain values.</summary>
	public struct HatchHoldState
	{
		/// <summary>The hatch is closed.</summary>
		public bool Locked;

		/// <summary>The exterior (bridge) hatch: never held by the mod (C11).</summary>
		public bool IsExterior;

		/// <summary>The hatch has a left, right or pressure-blockade compartment; doors and the cargo hatch do not.</summary>
		public bool HasCompartments;

		/// <summary>The hatch shows a visible "is being repaired" status.</summary>
		public bool RepairHeld;

		/// <summary>The P14 setting (hatch pressure blocking).</summary>
		public bool PressureBlockingEnabled;

		/// <summary>The (patched) "can be opened" answer.</summary>
		public bool CanBeOpened;
	}

	/// <summary>The crewman starting through a hatch link, as the B10 exemptions read him.</summary>
	public struct TraverserState
	{
		/// <summary>He is carrying out an evacuate-to-safety order.</summary>
		public bool Evacuating;

		/// <summary>He is getting off the deck or bridge because the boat goes under (escape-danger or hide-in-ship behaviour).</summary>
		public bool LeavingDeckForDive;

		/// <summary>He was already on this link's queue when the traverse started again (mid-climb).</summary>
		public bool AlreadyTraversing;
	}

	/// <summary>Where a walk starts and where it wants to go, in the boat's interior coordinates.</summary>
	public struct CrossingQuery
	{
		/// <summary>Agent position along the hull; 0 when the agent is not on the interior layer.</summary>
		public float OriginZ;

		/// <summary>Agent height.</summary>
		public float OriginY;

		public float DestinationZ;
		public float DestinationY;

		/// <summary>The character's current compartment id, or <see cref="HatchCrossing.NoCompartment"/>.</summary>
		public int CurrentCompartment;

		/// <summary>The level between control room and conning tower (lowY of the conning-tower hatch; C12).</summary>
		public float ConningTowerLevel;
	}

	/// <summary>
	/// B9, B10 (03 §2.F, 00 C26, C33): crew navigation against hatches Damage Control holds shut. Vanilla
	/// reachability already refuses evacuated compartments (non-player orders); this adds the mod's hold
	/// (<see cref="HoldOf"/>), which applies to player orders too. Positions follow the game: hatches are
	/// listed left to right (increasing obstacle Z); a destination at or above the conning-tower level counts
	/// as Z = 0 (the tower sits over the control room).
	/// </summary>
	public static class HatchCrossing
	{
		/// <summary>Compartment id meaning "no compartment" (game instance ids may be negative, so not -1).</summary>
		public const int NoCompartment = int.MinValue;

		/// <summary>
		/// Whether, and why, Damage Control itself is holding a hatch shut: the same hatches the lock guard
		/// (H2) refuses to open. A closed interior hatch with compartments is held while it is under repair,
		/// or while P14 is on and it cannot be opened. Vanilla's own rules (the deck-depth rule for hatches
		/// without a blockade compartment, the flooded-blockade rule with P14 off) never count: vanilla opens
		/// those hatches on traverse, so treating them as blocked would strand crew (for example on a dive).
		/// </summary>
		public static HatchHold HoldOf(HatchHoldState hatch)
		{
			if (!hatch.Locked || hatch.IsExterior || !hatch.HasCompartments)
				return HatchHold.None;
			if (hatch.RepairHeld)
				return HatchHold.RepairHold;
			if (hatch.PressureBlockingEnabled && !hatch.CanBeOpened)
				return HatchHold.PressureLock;
			return HatchHold.None;
		}

		/// <summary>
		/// B10: whether a crewman starting through a hatch link has his orders cancelled. Only a hatch the mod
		/// holds shut counts, and never for a man who is evacuating, leaving the deck because the boat dives,
		/// or already part way through that link.
		/// </summary>
		public static bool ShouldCancelTraverse(HatchHold hold, TraverserState who)
		{
			return hold != HatchHold.None && !who.Evacuating && !who.LeavingDeckForDive && !who.AlreadyTraversing;
		}

		/// <summary>Short reason text for the debug log.</summary>
		public static string Describe(HatchHold hold)
		{
			switch (hold)
			{
				case HatchHold.RepairHold:
					return "held shut for a repair";
				case HatchHold.PressureLock:
					return "pressure-locked (hatch pressure blocking on)";
				default:
					return "not held";
			}
		}

		/// <summary>The postfix may only turn "reachable" into "unreachable", never the reverse (00 C26).</summary>
		public static bool Combine(bool vanillaReachable, bool blocked)
		{
			return vanillaReachable && !blocked;
		}

		/// <summary>
		/// The hatches a walk along the hull crosses, as an index range into <paramref name="hatches"/>
		/// (left to right). Returns false when nothing is crossed. Moving right, the walk starts at the hatch
		/// whose left side is the current compartment, else at the first hatch at or past the origin, and stops
		/// before the first hatch beyond the destination. Moving left mirrors it.
		/// </summary>
		public static bool TryGetCrossedRange(IList<CrossingHatch> hatches, CrossingQuery q, out int first, out int last)
		{
			first = 0;
			last = -1;
			if (hatches == null || hatches.Count == 0)
				return false;
			int n = hatches.Count;
			float destZ = EffectiveDestinationZ(q);
			float originZ = q.OriginZ;

			if (destZ > originZ)
			{
				int start = -1;
				if (q.CurrentCompartment != NoCompartment)
				{
					for (int i = 0; i < n; i++)
					{
						if (hatches[i].LeftCompartment == q.CurrentCompartment)
						{
							start = i;
							break;
						}
					}
				}
				if (start < 0)
				{
					start = n;
					for (int i = 0; i < n; i++)
					{
						if (hatches[i].ObstacleZ >= originZ)
						{
							start = i;
							break;
						}
					}
				}
				int end = start - 1;
				for (int i = start; i < n && hatches[i].ObstacleZ <= destZ; i++)
					end = i;
				first = start;
				last = end;
				return last >= first;
			}
			else
			{
				int start = -1;
				if (q.CurrentCompartment != NoCompartment)
				{
					for (int i = n - 1; i >= 0; i--)
					{
						if (hatches[i].RightCompartment == q.CurrentCompartment)
						{
							start = i;
							break;
						}
					}
				}
				if (start < 0)
				{
					for (int i = n - 1; i >= 0; i--)
					{
						if (hatches[i].ObstacleZ <= originZ)
						{
							start = i;
							break;
						}
					}
				}
				int end = start + 1;
				for (int i = start; i >= 0 && hatches[i].ObstacleZ >= destZ; i--)
					end = i;
				first = end;
				last = start;
				return start >= 0 && last >= first;
			}
		}

		/// <summary>True when the walk changes level across the conning-tower hatch (either direction).</summary>
		public static bool CrossesConningTowerLevel(CrossingQuery q)
		{
			if (!DcMath.IsFinite(q.ConningTowerLevel) || !DcMath.IsFinite(q.OriginY) || !DcMath.IsFinite(q.DestinationY))
				return false;
			return (q.OriginY < q.ConningTowerLevel && q.DestinationY > q.ConningTowerLevel)
				|| (q.OriginY > q.ConningTowerLevel && q.DestinationY < q.ConningTowerLevel);
		}

		/// <summary>
		/// B9 decision with every hatch's state known up front: unreachable when a crossed interior hatch is
		/// held shut by the mod, or the walk crosses the conning-tower level while that hatch is (03 §6 #24).
		/// </summary>
		public static bool IsPathBlocked(IList<CrossingHatch> hatches, CrossingQuery q, bool conningTowerHatchBlocked)
		{
			int first, last;
			if (TryGetCrossedRange(hatches, q, out first, out last))
			{
				for (int i = first; i <= last; i++)
				{
					if (hatches[i].Blocked)
						return true;
				}
			}
			return conningTowerHatchBlocked && CrossesConningTowerLevel(q);
		}

		/// <summary>The destination's hull position as the walk uses it: 0 at or above the conning-tower level.</summary>
		public static float EffectiveDestinationZ(CrossingQuery q)
		{
			return DcMath.IsFinite(q.ConningTowerLevel) && q.DestinationY >= q.ConningTowerLevel ? 0f : q.DestinationZ;
		}
	}
}
