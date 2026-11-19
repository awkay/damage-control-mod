using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;
using UnityEngine;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>
	/// Per-boat geometry and identity (00 §2.1 rule 4, C3, C12): which hatch is exterior, which leads to
	/// the conning tower, the hatch order, the ballast tanks, and each hatch's lowY/highY. Everything is
	/// found by component type and game flags, never by object name or list position.
	///
	/// Built lazily from the injected PlayerShip and rebuilt when the player ship instance changes.
	/// When something cannot be resolved the layout is "unknown"; features call <see cref="Require"/>,
	/// which logs once and returns false, and must then leave vanilla behaviour alone. Main thread only.
	/// </summary>
	public static class BoatLayout
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static PlayerShip playerShip;
		#pragma warning restore 0649

		private static PlayerShip builtFor;
		private static bool built;
		private static bool known;
		private static string unknownReason = "no player ship";
		private static int version;
		private static Hatch exteriorHatch;
		private static Hatch conningTowerHatch;
		private static readonly List<Hatch> hatchesLeftToRight = new List<Hatch>();
		private static readonly List<Tank> ballastTanks = new List<Tank>();
		private static readonly Dictionary<Hatch, HatchGeometry> geometry = new Dictionary<Hatch, HatchGeometry>();

		/// <summary>Raised after every rebuild (new ship, or <see cref="Invalidate"/> then next access).</summary>
		public static event Action Rebuilt;

		/// <summary>The player ship the layout describes, or null (menus, loading).</summary>
		public static PlayerShip Ship
		{
			get
			{
				Refresh();
				return builtFor;
			}
		}

		/// <summary>True when the ship and every required piece was resolved.</summary>
		public static bool IsKnown
		{
			get
			{
				Refresh();
				return known;
			}
		}

		/// <summary>Why the layout is unknown, for log lines. Empty when known.</summary>
		public static string UnknownReason
		{
			get
			{
				Refresh();
				return known ? "" : unknownReason;
			}
		}

		/// <summary>Increments on every rebuild. Lets callers cache derived data per layout.</summary>
		public static int Version
		{
			get
			{
				Refresh();
				return version;
			}
		}

		/// <summary>Interior hatches in the ship's own left-to-right (bow to stern) order. Empty when unknown.</summary>
		public static IReadOnlyList<Hatch> HatchesLeftToRight
		{
			get
			{
				Refresh();
				return hatchesLeftToRight;
			}
		}

		/// <summary>The hatch to the outside (Hatch.IsExteriorHatch), or null.</summary>
		public static Hatch ExteriorHatch
		{
			get
			{
				Refresh();
				return exteriorHatch;
			}
		}

		/// <summary>The hatch between the control room and the conning tower, or null.</summary>
		public static Hatch ConningTowerHatch
		{
			get
			{
				Refresh();
				return conningTowerHatch;
			}
		}

		/// <summary>The ship's main ballast tanks, by identity (PlayableSubmarine.BallastTanks). Empty when unknown.</summary>
		public static IReadOnlyList<Tank> BallastTanks
		{
			get
			{
				Refresh();
				return ballastTanks;
			}
		}

		/// <summary>True if <paramref name="tank"/> is one of the main ballast tanks.</summary>
		public static bool IsBallastTank(Tank tank)
		{
			Refresh();
			return tank != null && ballastTanks.Contains(tank);
		}

		/// <summary>lowY/highY of a hatch on the current boat (02 conventions).</summary>
		public static bool TryGetGeometry(Hatch hatch, out HatchGeometry result)
		{
			Refresh();
			if (hatch != null && geometry.TryGetValue(hatch, out result))
				return true;
			result = new HatchGeometry();
			return false;
		}

		/// <summary>lowY of the conning-tower hatch: the interior level between control room and tower (replaces 2022.1's 101.55 m).</summary>
		public static bool TryGetConningTowerLevel(out float y)
		{
			return TryGetLowY(ConningTowerHatch, out y);
		}

		/// <summary>lowY of the exterior hatch: the level between tower and outside (replaces 2022.1's 102.8 m).</summary>
		public static bool TryGetExteriorLevel(out float y)
		{
			return TryGetLowY(ExteriorHatch, out y);
		}

		/// <summary>
		/// Gate for a feature that needs the layout. Returns true when known; otherwise logs
		/// "&lt;feature&gt; disabled: &lt;reason&gt;" once per feature per session and returns false.
		/// </summary>
		public static bool Require(string feature)
		{
			if (IsKnown)
				return true;
			DcLog.WarnOnce("layout:" + feature, feature + " disabled: boat layout unknown (" + unknownReason + ")");
			return false;
		}

		/// <summary>Forces a rebuild on next access (e.g. after the boat's equipment changes).</summary>
		public static void Invalidate()
		{
			built = false;
		}

		private static bool TryGetLowY(Hatch hatch, out float y)
		{
			HatchGeometry g;
			if (hatch != null && TryGetGeometry(hatch, out g))
			{
				y = g.LowY;
				return true;
			}
			y = 0f;
			return false;
		}

		private static void Refresh()
		{
			PlayerShip current = playerShip;
			bool currentAlive = current != null; // Unity null check: false once destroyed
			bool builtAlive = builtFor != null;
			if (built && currentAlive == builtAlive && ReferenceEquals(current, builtFor))
				return;
			Rebuild(currentAlive ? current : null);
		}

		private static void Rebuild(PlayerShip ship)
		{
			built = true;
			builtFor = ship;
			known = false;
			version++;
			exteriorHatch = null;
			conningTowerHatch = null;
			hatchesLeftToRight.Clear();
			ballastTanks.Clear();
			geometry.Clear();
			try
			{
				unknownReason = Build(ship);
				known = unknownReason == null;
				if (known)
					DcLog.Debug("boat layout built: " + Describe());
				else if (ship != null)
					DcLog.Warn("boat layout unknown: " + unknownReason);
			}
			catch (Exception e)
			{
				known = false;
				unknownReason = "exception while reading the boat: " + e.Message;
				DcLog.Exception("BoatLayout.Rebuild", e);
			}
			Action handler = Rebuilt;
			if (handler != null)
			{
				try
				{
					handler();
				}
				catch (Exception e)
				{
					DcLog.Exception("BoatLayout.Rebuilt handler", e);
				}
			}
		}

		/// <summary>Fills the layout; returns null on success or the reason it is unknown.</summary>
		private static string Build(PlayerShip ship)
		{
			if (ship == null)
				return "no player ship";

			Hatch[] ordered = ship.HatchesLeftToRight;
			if (ordered == null || ordered.Length == 0)
				return "PlayerShip.HatchesLeftToRight is empty";
			foreach (Hatch hatch in ordered)
			{
				if (hatch != null)
					hatchesLeftToRight.Add(hatch);
			}

			Tank[] tanks = ship.BallastTanks;
			if (tanks != null)
			{
				foreach (Tank tank in tanks)
				{
					if (tank != null)
						ballastTanks.Add(tank);
				}
			}

			exteriorHatch = ship.ExteriorHatch;
			if (exteriorHatch != null && !exteriorHatch.IsExteriorHatch)
				DcLog.Warn("PlayerShip.ExteriorHatch is not flagged IsExteriorHatch; using it anyway");
			conningTowerHatch = ship.HatchToConningTower;

			var all = new List<Hatch>(hatchesLeftToRight);
			if (exteriorHatch != null && !all.Contains(exteriorHatch))
				all.Add(exteriorHatch);
			if (conningTowerHatch != null && !all.Contains(conningTowerHatch))
				all.Add(conningTowerHatch);

			string missing = null;
			foreach (Hatch hatch in all)
			{
				HatchGeometry g;
				if (TryMeasure(hatch, out g))
					geometry[hatch] = g;
				else if (missing == null)
					missing = "hatch \"" + hatch.name + "\" has no usable BoxCollider";
			}
			if (missing != null)
				return missing;
			if (exteriorHatch == null)
				return "no exterior hatch";
			if (conningTowerHatch == null)
				return "no conning-tower hatch";
			return null;
		}

		private static bool TryMeasure(Hatch hatch, out HatchGeometry result)
		{
			result = new HatchGeometry();
			BoxCollider box = hatch.GetComponent<BoxCollider>();
			if (box == null)
				return false;
			Transform t = hatch.transform;
			result = HatchGeometry.FromBoxCollider(t.position.y, box.center.y, box.size.y, t.lossyScale.y);
			return result.IsValid;
		}

		private static string Describe()
		{
			var parts = new List<string>();
			foreach (KeyValuePair<Hatch, HatchGeometry> pair in geometry)
			{
				string role = pair.Key == exteriorHatch ? " [exterior]" : pair.Key == conningTowerHatch ? " [conning tower]" : "";
				parts.Add(pair.Key.name + role + " lowY=" + pair.Value.LowY.ToString("0.###") + " highY=" + pair.Value.HighY.ToString("0.###"));
			}
			return hatchesLeftToRight.Count + " interior hatches, " + ballastTanks.Count + " ballast tanks; " + string.Join("; ", parts.ToArray());
		}
	}
}
