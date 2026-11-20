using System;
using System.Collections.Generic;
using System.Text;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;
using UBOAT.Game.UI.Notifications;

namespace DamageControl.Game.Pressure
{
	/// <summary>
	/// P12b (01 §2.12 items 1 to 4, 00 §4): appends a flooding breakdown to the vanilla buoyancy
	/// tooltip: water in the ballast tanks (total and per tank, with % of capacity) and water per
	/// compartment, highlighted when alarming (<see cref="FloodingBreakdown"/>). Tanks are found by
	/// identity (PlayableSubmarine.QuickDivingTanks, BallastTanks; Compartment components), never by
	/// position. A compartment merged into another is counted once, through the one it redirects to.
	/// </summary>
	[HarmonyPatch(typeof(BuoyancyNotification), "GetTooltipContents")]
	internal static class BuoyancyTooltipPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("BuoyancyTooltip", ex);
		}

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static Locale locale;
		#pragma warning restore 0649

		private static void Postfix(ref string __result)
		{
			try
			{
				PlayerShip ship = BoatLayout.Ship;
				if (ship == null || locale == null)
					return;
				var text = new StringBuilder(__result ?? "");
				AppendBallast(text, ship);
				AppendFlooding(text, ship);
				__result = text.ToString();
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("BuoyancyTooltip", e);
			}
		}

		private static void AppendBallast(StringBuilder text, PlayerShip ship)
		{
			var tanks = new List<Tank>();
			AddDistinct(tanks, ship.QuickDivingTanks);
			AddDistinct(tanks, ship.BallastTanks);
			if (tanks.Count == 0)
				return;
			double water = 0.0;
			double capacity = 0.0;
			foreach (Tank tank in tanks)
			{
				water += tank.Water;
				capacity += tank.Capacity;
			}
			AppendLine(text, locale.Format(PressureText.BallastTotal, water, FloodingBreakdown.Percent(water, capacity)));
			foreach (Tank tank in tanks)
				AppendLine(text, locale.Format(PressureText.BallastTank, DisplayName(tank), tank.Water, FloodingBreakdown.Percent(tank.Water, tank.Capacity)));
		}

		private static void AppendFlooding(StringBuilder text, PlayerShip ship)
		{
			var rooms = new List<Compartment>();
			if (ship.Tanks != null)
			{
				foreach (Tank tank in ship.Tanks)
				{
					Compartment room = tank as Compartment;
					if (room != null && room.MergedCompartment == null)
						rooms.Add(room);
				}
			}
			if (rooms.Count == 0)
				return;
			rooms.Sort(CompareByPosition);
			Compartment galley = PressureLayout.Galley;
			double total = 0.0;
			foreach (Compartment room in rooms)
				total += room.Water;
			AppendLine(text, locale.Format(PressureText.FloodingTotal, total));
			foreach (Compartment room in rooms)
			{
				double water = room.Water;
				bool alarming = FloodingBreakdown.IsAlarming(water, galley != null && room == galley);
				AppendLine(text, locale.Format(alarming ? PressureText.FloodingCompartmentAlarm : PressureText.FloodingCompartment, DisplayName(room), water));
			}
		}

		private static int CompareByPosition(Compartment a, Compartment b)
		{
			int order = a.HorizontalIndex.CompareTo(b.HorizontalIndex);
			return order != 0 ? order : a.VerticalIndex.CompareTo(b.VerticalIndex);
		}

		private static void AddDistinct(List<Tank> into, Tank[] tanks)
		{
			if (tanks == null)
				return;
			foreach (Tank tank in tanks)
			{
				if (tank != null && !into.Contains(tank))
					into.Add(tank);
			}
		}

		/// <summary>The game's own activator name for the tank or compartment, else its object name.</summary>
		private static string DisplayName(Tank tank)
		{
			string name = tank.Name ?? "";
			return locale.RetrieveString("Activators/" + name, name);
		}

		private static void AppendLine(StringBuilder text, string line)
		{
			if (text.Length > 0 && text[text.Length - 1] != '\n')
				text.Append('\n');
			text.Append(line);
		}
	}
}
