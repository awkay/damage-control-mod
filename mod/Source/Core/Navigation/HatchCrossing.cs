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

		/// <summary>Closed and cannot be opened (see <see cref="HatchCrossing.IsBlocked"/>).</summary>
		public bool Blocked;

		public CrossingHatch(float obstacleZ, int leftCompartment, int rightCompartment, bool blocked)
		{
			ObstacleZ = obstacleZ;
			LeftCompartment = leftCompartment;
			RightCompartment = rightCompartment;
			Blocked = blocked;
		}
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
	/// B9, B10 (03 §2.F, 00 C26): crew navigation against hatches that cannot be opened. Vanilla
	/// reachability already refuses evacuated compartments (non-player orders); this adds the
	/// "cannot be opened" test, which applies to player orders too. Positions follow the game: hatches are
	/// listed left to right (increasing obstacle Z); a destination at or above the conning-tower level counts
	/// as Z = 0 (the tower sits over the control room).
	/// </summary>
	public static class HatchCrossing
	{
		/// <summary>Compartment id meaning "no compartment" (game instance ids may be negative, so not -1).</summary>
		public const int NoCompartment = int.MinValue;

		/// <summary>A hatch stops crew when it is closed and cannot be opened. An open hatch is passable.</summary>
		public static bool IsBlocked(bool locked, bool canBeOpened)
		{
			return locked && !canBeOpened;
		}

		/// <summary>
		/// B10: whether a character starting to traverse a hatch link has its orders cancelled. The exterior
		/// hatch is exempt (03 §2.F: the ladder to the bridge has no openability test).
		/// </summary>
		public static bool ShouldCancelTraverse(bool hasHatch, bool isExteriorHatch, bool locked, bool canBeOpened)
		{
			return hasHatch && !isExteriorHatch && IsBlocked(locked, canBeOpened);
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
		/// blocked, or the walk crosses the conning-tower level while that hatch is blocked (03 §6 #24).
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
