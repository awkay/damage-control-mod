using System;
using System.Collections.Generic;
using System.Globalization;
using DamageControl.Core.Common;
using DamageControl.Core.Diagnostics;
using DamageControl.Core.Hatches;
using DamageControl.Game.Hatches;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Core.Time;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Entities;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Items.Statuses;
using UBOAT.Game.Scene.Physics;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Diagnostics
{
	/// <summary>
	/// The "Damage Control diagnostics" setting: while it is on, writes a "[DC diag]" report of air, water,
	/// leaks and hatch flows every 10 real seconds, and event lines (leak started/stopped, hatch wrecked or
	/// blown open, hatch held shut/released) within half a real second. Reads only: nothing here changes the
	/// simulation. Driven by <see cref="DiagnosticsTickPatch"/> once per frame of the player ship (no
	/// execution-queue callbacks, so nothing lands in a save). Selection and text are Core's
	/// <see cref="DiagReport"/>; hatch flows and air draws come from <see cref="DiagRecorder.Shared"/>,
	/// fed by the hatch exchange, venting and compressor patches. Main thread only.
	/// </summary>
	public static class DiagnosticsRuntime
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameTime gameTime;

		[Inject]
		private static PlayerCrew playerCrew;
		#pragma warning restore 0649

		/// <summary>DiagRecorder counter keys.</summary>
		public const string CompressorKey = "compressor";
		public const string DieselKey = "diesel";
		public const string TowerKey = "tower";

		private static readonly DiagSchedule schedule = new DiagSchedule();
		private static readonly RateTracker waterRates = new RateTracker();
		private static readonly StateWatch leakWatch = new StateWatch();
		private static readonly StateWatch wreckWatch = new StateWatch();
		private static readonly StateWatch lockWatch = new StateWatch();
		private static readonly List<Equipment> leaking = new List<Equipment>();
		private static readonly List<Hatch> watchedHatches = new List<Hatch>();
		private static bool initialized;
		private static bool active;
		private static long lastReportTicks = -1L;
		private static AccessTools.FieldRef<LeakableEquipment, ExternalPressure> holeField;
		private static bool holeFieldFailed;

		/// <summary>One frame of the player ship (from <see cref="DiagnosticsTickPatch"/>), Gameplay state only.</summary>
		public static void Tick(PlayerShip ship)
		{
			EnsureInitialized();
			if (!DcSettings.Diagnostics)
			{
				if (active)
					Deactivate("diagnostics off");
				return;
			}
			double now = UnityEngine.Time.realtimeSinceStartup;
			if (!active)
				Activate(ship, now);
			if (schedule.PollDue(now))
				PollEvents(ship);
			if (schedule.ReportDue(now))
				Report(ship, now);
		}

		/// <summary>The hatch exchange blew a wrecked hatch open (H5). Logged at once while diagnostics are on.</summary>
		public static void OnHatchBlownOpen(Hatch hatch, double seatPressure, double farPressure)
		{
			if (!active || hatch == null)
				return;
			DcLog.Info(DiagReport.HatchBlownOpenLine(hatch.name, seatPressure, farPressure));
		}

		private static void EnsureInitialized()
		{
			if (initialized)
				return;
			initialized = true;
			DcSettings.SessionStarted += OnSessionStarted;
		}

		private static void OnSessionStarted()
		{
			// Start from fresh baselines in every campaign session; Tick re-activates if the setting is on.
			if (active)
				Deactivate(null);
		}

		private static void Activate(PlayerShip ship, double now)
		{
			active = true;
			DiagRecorder.Shared.Clear();
			DiagRecorder.Shared.Enabled = true;
			schedule.Reset();
			schedule.ReportDue(now); // the first report comes one interval from now, with rates measured over it
			waterRates.Clear();
			leakWatch.Clear();
			wreckWatch.Clear();
			lockWatch.Clear();
			lastReportTicks = GameClock.Ticks;
			double seconds = lastReportTicks * 1e-7;
			foreach (Compartment c in Compartments(ship))
				waterRates.Sample(c, c.Water, seconds);
			DcLog.Info(DiagReport.Prefix + "diagnostics on: a report every " + DiagSchedule.ReportInterval + " real seconds while anything is wet, off 1 atm, leaking or flowing");
		}

		private static void Deactivate(string why)
		{
			active = false;
			DiagRecorder.Shared.Enabled = false;
			DiagRecorder.Shared.Clear();
			waterRates.Clear();
			leakWatch.Clear();
			wreckWatch.Clear();
			lockWatch.Clear();
			leaking.Clear();
			watchedHatches.Clear();
			lastReportTicks = -1L;
			if (why != null)
				DcLog.Info(DiagReport.Prefix + why);
		}

		// ---- immediate events

		private static void PollEvents(PlayerShip ship)
		{
			CollectLeaking(ship, leaking);
			StateChange change;
			leakWatch.BeginRound();
			foreach (Equipment e in leaking)
			{
				if (leakWatch.Observe(e, e.name, "leaking", out change))
					DcLog.Info(DiagReport.LeakStartedLine(e.name, WhereOf(e)));
			}
			foreach (StateChange gone in leakWatch.EndRound())
				DcLog.Info(DiagReport.LeakStoppedLine(gone.Label));

			CollectHatches(watchedHatches);
			bool blocking = DcSettings.HatchPressureBlocking;
			wreckWatch.BeginRound();
			lockWatch.BeginRound();
			foreach (Hatch h in watchedHatches)
			{
				bool wrecked = h.Health <= 0f;
				if (wreckWatch.Observe(h, h.name, wrecked ? "wrecked" : "intact", out change) && change.Before != null)
					DcLog.Info(DiagReport.HatchWreckedLine(h.name, wrecked));
				string lockState = DiagReport.HatchLockState(h.Locked, h.CanBeOpened, HatchRuntime.IsRepairHeld(h), blocking);
				if (lockWatch.Observe(h, h.name, lockState, out change) && DiagReport.IsLockChangeWorthLogging(change.Before, change.After))
					DcLog.Info(DiagReport.HatchLockLine(h.name, change.Before, change.After, DeltaP(h)));
			}
			wreckWatch.EndRound();
			lockWatch.EndRound();
		}

		// ---- the periodic report

		private static void Report(PlayerShip ship, double now)
		{
			long ticks = GameClock.Ticks;
			double elapsed = lastReportTicks >= 0L && ticks >= 0L ? (ticks - lastReportTicks) * 1e-7 : double.NaN;
			if (!double.IsNaN(elapsed) && elapsed < 0.0)
			{
				// The game clock went backwards (a load): start a new window from here.
				DiagRecorder.Shared.ResetWindow();
				lastReportTicks = ticks;
				return;
			}
			if (!double.IsNaN(elapsed) && !(elapsed > 0.0))
				return; // paused: nothing moved, keep the window open
			double seconds = ticks * 1e-7;
			DiagRecorder recorder = DiagRecorder.Shared;

			// Hatches that ran the exchange, with their recorded flow.
			var hatchKeys = new List<object>();
			recorder.CopyHatchKeys(hatchKeys);
			var hatchSamples = new List<HatchSample>();
			var exchanging = new HashSet<Compartment>();
			foreach (object key in hatchKeys)
			{
				Hatch h = key as Hatch;
				if (h == null)
					continue;
				HatchSample sample = SampleHatch(h, recorder, elapsed);
				hatchSamples.Add(sample);
				if (DiagReport.IsHatchNotable(sample))
				{
					if (h.LeftCompartment != null)
						exchanging.Add(h.LeftCompartment);
					if (h.RightCompartment != null)
						exchanging.Add(h.RightCompartment);
				}
			}

			// Leaks, with whoever works on them.
			CollectLeaking(ship, leaking);
			Dictionary<Equipment, List<WorkerSample>> workers = CollectWorkers();
			var leakSamples = new List<LeakSample>();
			var leakingRooms = new HashSet<Compartment>();
			foreach (Equipment e in leaking)
			{
				LeakSample sample = SampleLeak(e, recorder, elapsed);
				List<WorkerSample> crew;
				if (workers.TryGetValue(e, out crew))
					sample.Workers.AddRange(crew);
				leakSamples.Add(sample);
				LeakableEquipment leakable = e as LeakableEquipment;
				if (leakable != null && leakable.Compartment != null)
					leakingRooms.Add(leakable.Compartment);
			}

			var roomSamples = new List<CompartmentSample>();
			foreach (Compartment c in Compartments(ship))
			{
				var sample = new CompartmentSample
				{
					Name = c.name,
					AirPressure = c.AirPressure,
					Air = c.Air,
					Water = c.Water,
					Capacity = c.Capacity,
					WaterRate = waterRates.Sample(c, c.Water, seconds),
					AirToxicity = c.AirToxicity,
					Breathable = !c.IsAirToxic,
					HasLeak = leakingRooms.Contains(c),
					HatchExchange = exchanging.Contains(c),
				};
				Compartment merged = c.MergedCompartment;
				if (merged != null && merged != c)
					sample.MergedInto = merged.name;
				roomSamples.Add(sample);
			}

			BoatSample boat = SampleBoat(ship, recorder, elapsed);
			boat.LeaksAboard = leaking.Count;
			List<string> lines = DiagReport.Build(boat, roomSamples, leakSamples, hatchSamples, DiagReport.MaxLines);
			if (lines.Count == 0)
			{
				if (schedule.DryLineDue(now))
					DcLog.Info(DiagReport.AllDryLine());
			}
			else
			{
				foreach (string line in lines)
					DcLog.Info(line);
			}
			recorder.ResetWindow();
			lastReportTicks = ticks;
		}

		private static HatchSample SampleHatch(Hatch h, DiagRecorder recorder, double elapsed)
		{
			var sample = new HatchSample
			{
				Name = h.name,
				Left = h.LeftCompartment != null ? h.LeftCompartment.name : null,
				Right = h.RightCompartment != null ? h.RightCompartment.name : null,
				Open = !h.Locked,
				Wrecked = h.Health <= 0f,
				Leaking = h.HasStatus<Leaking>(),
				Seals = HatchSeals.Shared.Count(h),
				DeltaP = DeltaP(h),
				WindowSeconds = elapsed,
			};
			HatchFlowWindow flow;
			if (recorder.TryGetHatch(h, out flow))
				sample.Flow = flow;
			return sample;
		}

		private static LeakSample SampleLeak(Equipment e, DiagRecorder recorder, double elapsed)
		{
			var sample = new LeakSample { Name = e.name, Where = WhereOf(e) };
			Hatch hatch = e as Hatch;
			if (hatch != null)
			{
				sample.IsHatch = true;
				sample.HatchOpen = !hatch.Locked;
				sample.Blockers = HatchSeals.Shared.Count(hatch, SealSource.Blocker);
				sample.Repairs = HatchSeals.Shared.Count(hatch, SealSource.Repair);
				HatchFlowWindow flow;
				if (elapsed > 0.0 && recorder.TryGetHatch(hatch, out flow))
					sample.HatchWaterRate = flow.SumWater / elapsed;
				return sample;
			}
			LeakableEquipment leakable = e as LeakableEquipment;
			if (leakable == null)
				return sample; // some other equipment with the Leaking status: name and workers only
			Parameter intensity = leakable.LeakIntensity;
			if (intensity != null)
			{
				sample.Intensity = intensity.Value;
				sample.BaseIntensity = intensity.GetDeltaModifiersValue();
				List<Modifier> scale = intensity.ScaleModifiers;
				if (scale != null)
				{
					foreach (Modifier m in scale)
					{
						if (m != null)
							sample.Modifiers.Add(new ModifierSample(m.Name, m.Value));
					}
				}
			}
			Compartment room = leakable.Compartment;
			if (room != null)
				sample.RoomPressure = room.AirPressure;
			ExternalPressure hole = ReadHole(leakable);
			if (hole != null)
			{
				sample.Openess = hole.Openess;
				sample.HoleSize = hole.HoleSize;
				sample.SeaPressure = hole.Pressure;
				sample.HoleUnderWater = hole.IsUnderWater;
			}
			return sample;
		}

		private static BoatSample SampleBoat(PlayerShip ship, DiagRecorder recorder, double elapsed)
		{
			var boat = new BoatSample
			{
				GameTime = gameTime != null ? gameTime.CurrentDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) : null,
				ElapsedGameSeconds = elapsed,
				Compression = GameClock.Now().Compression,
				Depth = ship.Depth,
				DeckDepth = ship.DeckDepth,
			};
			TrimPump pump = ship.GetEquipment<TrimPump>();
			if (pump != null)
			{
				boat.TrimPumpOn = pump.enabled;
				boat.TrimPumpWaterLeft = pump.IsWaterLeft;
			}
			if (recorder.HasSum(CompressorKey))
			{
				boat.CompressorAir = recorder.Sum(CompressorKey);
				boat.CompressorRoom = recorder.Label(CompressorKey);
			}
			if (recorder.HasSum(DieselKey))
			{
				boat.DieselAir = recorder.Sum(DieselKey);
				boat.DieselRoom = recorder.Label(DieselKey);
			}
			if (recorder.HasSum(TowerKey))
			{
				boat.TowerAir = recorder.Sum(TowerKey);
				boat.TowerRoom = recorder.Label(TowerKey);
			}
			return boat;
		}

		// ---- game reads

		private static IEnumerable<Compartment> Compartments(PlayerShip ship)
		{
			var list = new List<Compartment>();
			Tank[] tanks = ship != null ? ship.Tanks : null;
			if (tanks != null)
			{
				foreach (Tank tank in tanks)
				{
					Compartment c = tank as Compartment;
					if (c != null)
						list.Add(c);
				}
			}
			list.Sort((a, b) =>
			{
				int byH = a.HorizontalIndex.CompareTo(b.HorizontalIndex);
				return byH != 0 ? byH : a.VerticalIndex.CompareTo(b.VerticalIndex);
			});
			return list;
		}

		private static void CollectLeaking(PlayerShip ship, List<Equipment> into)
		{
			into.Clear();
			List<Equipment> equipment = ship != null ? ship.Equipment : null;
			if (equipment == null)
				return;
			for (int i = 0; i < equipment.Count; i++)
			{
				Equipment e = equipment[i];
				if (e != null && e.HasStatus<Leaking>())
					into.Add(e);
			}
		}

		/// <summary>Interior hatches with compartments: the bulkhead hatches plus the conning-tower hatch.</summary>
		private static void CollectHatches(List<Hatch> into)
		{
			into.Clear();
			IReadOnlyList<Hatch> row = BoatLayout.HatchesLeftToRight;
			if (row != null)
			{
				for (int i = 0; i < row.Count; i++)
					AddHatch(into, row[i]);
			}
			AddHatch(into, BoatLayout.ConningTowerHatch);
		}

		private static void AddHatch(List<Hatch> into, Hatch h)
		{
			if (h != null && !HatchRuntime.HasNoCompartments(h) && !into.Contains(h))
				into.Add(h);
		}

		/// <summary>Crewmen whose current order is to block or repair a piece of equipment, by that equipment.</summary>
		private static Dictionary<Equipment, List<WorkerSample>> CollectWorkers()
		{
			var map = new Dictionary<Equipment, List<WorkerSample>>();
			if (playerCrew == null)
				return map;
			for (int i = 0; i < playerCrew.Count; i++)
			{
				PlayableCharacter c = playerCrew[i];
				if (c == null)
					continue;
				CharacterAction action = c.Action;
				string job;
				float progress = float.NaN;
				if (action is BlockLeakAction)
				{
					job = "block leak";
				}
				else if (action is RepairAction)
				{
					job = "repair";
					progress = action.Progress;
				}
				else
				{
					continue;
				}
				GameActivator activator = action.Activator;
				Equipment target = activator != null ? activator.AssociatedEquipment : null;
				if (target == null)
					continue;
				List<WorkerSample> list;
				if (!map.TryGetValue(target, out list))
				{
					list = new List<WorkerSample>(2);
					map.Add(target, list);
				}
				list.Add(new WorkerSample { Name = c.Name, Job = job, Interacting = c.IsInteracting, Progress = progress });
			}
			return map;
		}

		private static string WhereOf(Equipment e)
		{
			Hatch hatch = e as Hatch;
			if (hatch != null)
				return "\"" + NameOf(hatch.LeftCompartment) + "\" | \"" + NameOf(hatch.RightCompartment) + "\"";
			LeakableEquipment leakable = e as LeakableEquipment;
			if (leakable != null)
				return NameOf(leakable.Compartment);
			return NameOf(e != null ? e.GetComponentInParent<Compartment>() : null);
		}

		private static string NameOf(Compartment c)
		{
			return c != null ? c.name : "?";
		}

		private static double DeltaP(Hatch h)
		{
			Compartment l = h.LeftCompartment;
			Compartment r = h.RightCompartment;
			return l != null && r != null ? l.AirPressure - r.AirPressure : double.NaN;
		}

		/// <summary>LeakableEquipment's private hole (2026.1 field "hole"); null when it cannot be read.</summary>
		private static ExternalPressure ReadHole(LeakableEquipment leakable)
		{
			if (holeFieldFailed)
				return null;
			try
			{
				if (holeField == null)
					holeField = AccessTools.FieldRefAccess<LeakableEquipment, ExternalPressure>("hole");
				return holeField(leakable);
			}
			catch (Exception e)
			{
				holeFieldFailed = true;
				DcLog.Warn(DiagReport.Prefix + "cannot read leak holes (" + e.GetType().Name + ": " + e.Message + "); leak lines show no openess, sea pressure or inflow");
				return null;
			}
		}
	}
}
