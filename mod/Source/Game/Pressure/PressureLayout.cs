using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;
using UnityEngine;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// The compartments the pressure features single out, found by component type and game flags on
	/// top of <see cref="BoatLayout"/> (00 §2.1 rule 4), never by object name or index:
	/// <list type="bullet">
	/// <item>conning tower: the compartment shared by the exterior hatch and the conning-tower hatch;</item>
	/// <item>diesel compartment: the compartment containing <c>PlayerShip.DieselEngine</c>;</item>
	/// <item>galley: the compartment containing the boat's <c>Kitchen</c>.</item>
	/// </list>
	/// Rebuilt whenever <see cref="BoatLayout.Version"/> changes. Main thread only.
	/// </summary>
	public static class PressureLayout
	{
		private static int builtVersion = -1;
		private static Compartment conningTower;
		private static Compartment dieselCompartment;
		private static Compartment galley;
		private static readonly Dictionary<int, Compartment> owners = new Dictionary<int, Compartment>();

		public static Compartment ConningTower
		{
			get
			{
				Refresh();
				return conningTower;
			}
		}

		public static Compartment DieselCompartment
		{
			get
			{
				Refresh();
				return dieselCompartment;
			}
		}

		public static Compartment Galley
		{
			get
			{
				Refresh();
				return galley;
			}
		}

		/// <summary>The compartment a device stands in (its parent compartment, else the one whose volume contains it). Cached per device.</summary>
		public static Compartment CompartmentOf(Component device)
		{
			if (device == null)
				return null;
			Refresh();
			int id = device.GetInstanceID();
			Compartment cached;
			if (owners.TryGetValue(id, out cached) && cached != null)
				return cached;
			Compartment found = Locate(device, BoatLayout.Ship);
			if (found != null)
				owners[id] = found;
			return found;
		}

		/// <summary>The air pressure denominator the game uses (capacity - water - fuel), or 0 when unknown.</summary>
		public static double FreeVolume(Tank tank)
		{
			if (tank == null)
				return 0.0;
			double pressure = tank.AirPressure;
			if (!(pressure > 0.0) || !DcMath.IsFinite(pressure))
				return 0.0;
			return tank.Air / pressure;
		}

		private static void Refresh()
		{
			int version = BoatLayout.Version;
			if (version == builtVersion)
				return;
			builtVersion = version;
			conningTower = null;
			dieselCompartment = null;
			galley = null;
			owners.Clear();
			if (!BoatLayout.IsKnown)
				return;

			PlayerShip ship = BoatLayout.Ship;
			conningTower = SharedCompartment(BoatLayout.ExteriorHatch, BoatLayout.ConningTowerHatch);
			if (ship.DieselEngine != null)
				dieselCompartment = Locate(ship.DieselEngine, ship);
			Kitchen kitchen = ship.GetComponentInChildren<Kitchen>(true);
			if (kitchen != null)
				galley = Locate(kitchen, ship);

			DcLog.Debug("pressure layout: conning tower " + Describe(conningTower) + ", diesel compartment " + Describe(dieselCompartment) + ", galley " + Describe(galley));
			if (conningTower == null)
				DcLog.WarnOnce("pressure:no-tower", "conning tower compartment not found; its venting is off");
			if (dieselCompartment == null)
				DcLog.WarnOnce("pressure:no-diesel", "diesel compartment not found; diesel venting and air draw are off");
		}

		/// <summary>The compartment on both hatches (the tower sits between the exterior and the conning-tower hatch); else the exterior hatch's only compartment.</summary>
		private static Compartment SharedCompartment(Hatch exterior, Hatch tower)
		{
			if (exterior == null)
				return null;
			Compartment a = exterior.LeftCompartment;
			Compartment b = exterior.RightCompartment;
			if (tower != null)
			{
				if (a != null && (a == tower.LeftCompartment || a == tower.RightCompartment))
					return a;
				if (b != null && (b == tower.LeftCompartment || b == tower.RightCompartment))
					return b;
			}
			if (a != null && b == null)
				return a;
			if (b != null && a == null)
				return b;
			return null;
		}

		private static Compartment Locate(Component device, PlayerShip ship)
		{
			Compartment parent = device.GetComponentInParent<Compartment>();
			if (parent != null)
				return parent;
			if (ship == null || ship.Tanks == null)
				return null;
			Vector3 position = device.transform.position;
			foreach (Tank tank in ship.Tanks)
			{
				Compartment compartment = tank as Compartment;
				if (compartment != null && compartment.Contains(position))
					return compartment;
			}
			return null;
		}

		private static string Describe(Compartment compartment)
		{
			return compartment != null ? "\"" + compartment.name + "\"" : "(none)";
		}
	}
}
