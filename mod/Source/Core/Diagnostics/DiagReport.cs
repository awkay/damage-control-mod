using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DamageControl.Core.Hatches;

namespace DamageControl.Core.Diagnostics
{
	/// <summary>One compartment as the report sees it. The Game layer fills it from the live compartment.</summary>
	public sealed class CompartmentSample
	{
		public string Name;

		/// <summary>Air pressure in atm (Compartment.AirPressure).</summary>
		public double AirPressure = double.NaN;

		/// <summary>Air amount (Compartment.Air; m³ at 1 atm).</summary>
		public double Air = double.NaN;

		/// <summary>Water in m³ (of the merged water body when merged).</summary>
		public double Water = double.NaN;

		public double Capacity = double.NaN;

		/// <summary>Water change in m³ per game second since the last report; NaN when unknown.</summary>
		public double WaterRate = double.NaN;

		public float AirToxicity = float.NaN;
		public bool Breathable = true;

		/// <summary>A leaking piece of equipment stands in this compartment.</summary>
		public bool HasLeak;

		/// <summary>A hatch on this compartment moved air or water noticeably (see <see cref="DiagReport.IsHatchNotable"/>).</summary>
		public bool HatchExchange;

		/// <summary>Name of the compartment whose water this one shares (merged decks), or null.</summary>
		public string MergedInto;
	}

	/// <summary>A crewman whose current order targets a leaking piece of equipment.</summary>
	public struct WorkerSample
	{
		public string Name;

		/// <summary>"block leak" or "repair".</summary>
		public string Job;

		/// <summary>At the slot, working (not walking there).</summary>
		public bool Interacting;

		/// <summary>Order progress 0..1, or NaN when it has none worth showing.</summary>
		public float Progress;
	}

	/// <summary>A scale modifier on a leak's intensity, as the game holds it (name, value).</summary>
	public struct ModifierSample
	{
		public string Name;
		public float Value;

		public ModifierSample(string name, float value)
		{
			Name = name;
			Value = value;
		}
	}

	/// <summary>One leaking piece of equipment: pipe, valve, hull fitting (leakable equipment) or hatch.</summary>
	public sealed class LeakSample
	{
		public string Name;

		/// <summary>Compartment name (leakable equipment) or "A | B" (hatch).</summary>
		public string Where;

		public bool IsHatch;

		// Leakable equipment (the game's sea leak).

		/// <summary>LeakIntensity's value (base x every scale modifier).</summary>
		public float Intensity = float.NaN;

		/// <summary>LeakIntensity before its scale modifiers (the delta modifiers' sum, 0.01 in vanilla).</summary>
		public float BaseIntensity = float.NaN;

		/// <summary>Every scale modifier on LeakIntensity (vanilla "BlockLeak" 0.2, the mod's repair throttle 1 - P, others).</summary>
		public readonly List<ModifierSample> Modifiers = new List<ModifierSample>();

		/// <summary>The hole's Openess the game set this frame (0.35 x intensity x damage-difficulty multiplier).</summary>
		public float Openess = float.NaN;

		public float HoleSize = float.NaN;

		/// <summary>Sea pressure at the hole in atm (ExternalPressure.Pressure).</summary>
		public double SeaPressure = double.NaN;

		public bool HoleUnderWater;

		/// <summary>Air pressure of the leaking compartment in atm.</summary>
		public double RoomPressure = double.NaN;

		// Hatch.

		public bool HatchOpen;

		/// <summary>Seal sources on the hatch (HatchSeals): crewmen blocking it.</summary>
		public int Blockers;

		/// <summary>Seal sources on the hatch (HatchSeals): crewmen repairing it.</summary>
		public int Repairs;

		/// <summary>Water through the hatch, m³ per game second over the report window (sign: left to right); NaN unknown.</summary>
		public double HatchWaterRate = double.NaN;

		public readonly List<WorkerSample> Workers = new List<WorkerSample>();
	}

	/// <summary>One hatch with its recorded flow.</summary>
	public sealed class HatchSample
	{
		public string Name;
		public string Left;
		public string Right;
		public bool Open;
		public bool Wrecked;
		public bool Leaking;
		public int Seals;

		/// <summary>Air pressure left minus right, atm.</summary>
		public double DeltaP = double.NaN;

		public HatchFlowWindow Flow;

		/// <summary>Game seconds of the report window the flow sums cover; 0 or NaN when unknown.</summary>
		public double WindowSeconds = double.NaN;

		public double AverageWaterRate
		{
			get { return WindowSeconds > 0.0 ? Flow.SumWater / WindowSeconds : double.NaN; }
		}

		public double AverageAirRate
		{
			get { return WindowSeconds > 0.0 ? Flow.SumAir / WindowSeconds : double.NaN; }
		}
	}

	/// <summary>The boat-wide part of a report.</summary>
	public sealed class BoatSample
	{
		/// <summary>The game's clock as text (the Game layer formats it).</summary>
		public string GameTime;

		/// <summary>Game seconds since the previous report; NaN for the first.</summary>
		public double ElapsedGameSeconds = double.NaN;

		public float Compression = float.NaN;
		public float Depth = float.NaN;
		public float DeckDepth = float.NaN;

		/// <summary>Trim pump state: null when the boat has none.</summary>
		public bool? TrimPumpOn;

		public bool TrimPumpWaterLeft;

		/// <summary>Leaking equipment aboard. While above 0 the mod holds the trim pump's auto-off (H14).</summary>
		public int LeaksAboard;

		/// <summary>Air the compressor took from its compartment this window; NaN when it did not run.</summary>
		public double CompressorAir = double.NaN;

		public string CompressorRoom;

		/// <summary>Net air change of the diesel compartment from venting and diesel draw this window; NaN when not run.</summary>
		public double DieselAir = double.NaN;

		public string DieselRoom;

		/// <summary>Net air change of the conning tower from venting this window; NaN when not run.</summary>
		public double TowerAir = double.NaN;

		public string TowerRoom;
	}

	/// <summary>
	/// The diagnostics report (player switch "Damage Control diagnostics"): which compartments, leaks and
	/// hatches are worth a line, and the text of every line. All lines start with <see cref="Prefix"/>.
	/// Pure: the Game layer samples the boat and writes the lines with DcLog.Info. Numbers use the
	/// invariant culture so the log reads the same on every system.
	/// </summary>
	public static class DiagReport
	{
		public const string Prefix = "[DC diag] ";

		/// <summary>Most lines in one report, header and totals included.</summary>
		public const int MaxLines = 40;

		/// <summary>A compartment with more water than this (m³) is listed.</summary>
		public const double WaterThreshold = 0.01;

		/// <summary>A compartment whose pressure differs from 1 atm by more than this is listed.</summary>
		public const double PressureDeviation = 0.02;

		/// <summary>A hatch moving more water than this (m³/s) is listed.</summary>
		public const double HatchWaterRateThreshold = 1e-4;

		/// <summary>A hatch moving more air than this (per s) is listed.</summary>
		public const double HatchAirRateThreshold = 1e-3;

		/// <summary>The game sets a leak's hole Openess to this x intensity x the damage-difficulty multiplier.</summary>
		public const float OpenessPerIntensity = 0.35f;

		/// <summary>The game adds this (atm) to the sea pressure at a hole under water when it computes the leak.</summary>
		public const float UnderWaterHeadBonus = 0.5f;

		/// <summary>Deck depth (m) below which the report calls the boat surfaced (the venting rule's diesel induction depth).</summary>
		public const float SurfacedDeckDepth = 3.5f;

		/// <summary>Water rates (m³/s) are written with five decimals (0.01 litre per second).</summary>
		public const string RateFormat = "0.00000";

		private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

		// ---- selection

		public static bool IsCompartmentNotable(CompartmentSample c)
		{
			if (c == null)
				return false;
			return c.Water > WaterThreshold
				|| Math.Abs(c.AirPressure - 1.0) > PressureDeviation
				|| c.HasLeak
				|| c.HatchExchange;
		}

		/// <summary>A hatch moved non-negligible air or water in its last frame or over the report window.</summary>
		public static bool IsHatchNotable(HatchSample h)
		{
			if (h == null)
				return false;
			return Above(h.Flow.LastWaterRate, HatchWaterRateThreshold)
				|| Above(h.AverageWaterRate, HatchWaterRateThreshold)
				|| Above(h.Flow.LastAirRate, HatchAirRateThreshold)
				|| Above(h.AverageAirRate, HatchAirRateThreshold);
		}

		/// <summary>True when there is nothing to report: no notable compartment, no leak, no notable hatch.</summary>
		public static bool IsQuiet(IList<CompartmentSample> compartments, IList<LeakSample> leaks, IList<HatchSample> hatches)
		{
			if (leaks != null && leaks.Count > 0)
				return false;
			if (compartments != null)
			{
				foreach (CompartmentSample c in compartments)
				{
					if (IsCompartmentNotable(c))
						return false;
				}
			}
			if (hatches != null)
			{
				foreach (HatchSample h in hatches)
				{
					if (IsHatchNotable(h))
						return false;
				}
			}
			return true;
		}

		// ---- leak arithmetic (the game's own leak law, read back for display)

		/// <summary>
		/// Pressure (atm) pushing water in through a hole: the sea pressure at the hole (plus 0.5 while the hole
		/// is under water) minus the compartment's air pressure. Negative: air is pushed out instead.
		/// </summary>
		public static double LeakDrive(double roomPressure, double seaPressure, bool holeUnderWater)
		{
			return (holeUnderWater ? seaPressure + UnderWaterHeadBonus : seaPressure) - roomPressure;
		}

		/// <summary>Water inflow (m³ per game second) the game's leak law gives for this hole now, before its per-step caps.</summary>
		public static double NominalInflow(LeakSample leak)
		{
			if (leak == null || leak.IsHatch)
				return double.NaN;
			return LeakDrive(leak.RoomPressure, leak.SeaPressure, leak.HoleUnderWater) * leak.HoleSize * leak.Openess;
		}

		/// <summary>The damage-difficulty multiplier implied by Openess = 0.35 x intensity x multiplier; NaN when the intensity is 0.</summary>
		public static double DifficultyFactor(float openess, float intensity)
		{
			double denominator = OpenessPerIntensity * (double)intensity;
			if (!(Math.Abs(denominator) > 1e-12))
				return double.NaN;
			return openess / denominator;
		}

		/// <summary>Fraction of the open rate a closed, leaking hatch passes with <paramref name="seals"/> seal sources (0.02 x 0.1^n).</summary>
		public static double ClosedHatchLeakMultiplier(int seals)
		{
			return HatchExchange.ClosedLeakFactor * HatchSeals.FactorFor(Math.Max(0, seals));
		}

		// ---- the report

		/// <summary>
		/// The report's lines: header, one line per notable compartment, per leak, per notable hatch, then totals.
		/// Empty when <see cref="IsQuiet"/>. At most <paramref name="maxLines"/> lines (at least 3): when the
		/// body is too long its tail is replaced by one "more lines not shown" line, and the totals stay.
		/// </summary>
		public static List<string> Build(BoatSample boat, IList<CompartmentSample> compartments, IList<LeakSample> leaks, IList<HatchSample> hatches, int maxLines)
		{
			var lines = new List<string>();
			if (IsQuiet(compartments, leaks, hatches))
				return lines;
			if (boat == null)
				boat = new BoatSample();

			var body = new List<string>();
			if (compartments != null)
			{
				foreach (CompartmentSample c in compartments)
				{
					if (IsCompartmentNotable(c))
						body.Add(CompartmentLine(c));
				}
			}
			if (leaks != null)
			{
				foreach (LeakSample leak in leaks)
				{
					if (leak != null)
						body.Add(LeakLine(leak));
				}
			}
			if (hatches != null)
			{
				foreach (HatchSample h in hatches)
				{
					if (IsHatchNotable(h))
						body.Add(HatchLine(h));
				}
			}

			lines.Add(Header(boat));
			int room = Math.Max(3, maxLines) - 2;
			if (body.Count > room)
			{
				int keep = room - 1;
				lines.AddRange(body.GetRange(0, keep));
				lines.Add(Prefix + "... " + (body.Count - keep) + " more line(s) not shown (report capped at " + Math.Max(3, maxLines) + " lines)");
			}
			else
			{
				lines.AddRange(body);
			}
			lines.Add(TotalsLine(boat, compartments, leaks));
			return lines;
		}

		public static string Header(BoatSample boat)
		{
			var sb = new StringBuilder(Prefix);
			sb.Append("report at ").Append(string.IsNullOrEmpty(boat.GameTime) ? "?" : boat.GameTime);
			sb.Append(", ");
			if (double.IsNaN(boat.ElapsedGameSeconds))
				sb.Append("first report");
			else
				sb.Append(Num(boat.ElapsedGameSeconds, "0.0")).Append(" game s since the last");
			sb.Append(", compression x").Append(Num(boat.Compression, "0.##"));
			sb.Append(", depth ").Append(Num(boat.Depth, "0.0")).Append(" m");
			sb.Append(", deck depth ").Append(Num(boat.DeckDepth, "0.0")).Append(" m");
			sb.Append(", ").Append(boat.DeckDepth < SurfacedDeckDepth ? "surfaced" : "submerged");
			return sb.ToString();
		}

		public static string CompartmentLine(CompartmentSample c)
		{
			var sb = new StringBuilder(Prefix);
			sb.Append("room ").Append(Quote(c.Name)).Append(": ");
			sb.Append("p ").Append(Num(c.AirPressure, "0.00")).Append(" atm");
			sb.Append(", air ").Append(Num(c.Air, "0.00"));
			sb.Append(", water ").Append(Num(c.Water, "0.000")).Append(" m3");
			if (c.Capacity > 0.0 && !double.IsNaN(c.Water))
				sb.Append(" (").Append(Num(100.0 * c.Water / c.Capacity, "0.0")).Append(" % of ").Append(Num(c.Capacity, "0.0")).Append(")");
			sb.Append(", dW ").Append(Signed(c.WaterRate, RateFormat)).Append(" m3/s");
			sb.Append(", toxicity ").Append(Num(c.AirToxicity, "0.00"));
			sb.Append(c.Breathable ? " breathable" : " NOT breathable");
			if (c.HasLeak)
				sb.Append(", leak");
			if (c.HatchExchange)
				sb.Append(", hatch exchange");
			if (!string.IsNullOrEmpty(c.MergedInto))
				sb.Append(", water shared with ").Append(Quote(c.MergedInto));
			return sb.ToString();
		}

		public static string LeakLine(LeakSample leak)
		{
			var sb = new StringBuilder(Prefix);
			if (leak.IsHatch)
			{
				int seals = leak.Blockers + leak.Repairs;
				sb.Append("leak hatch ").Append(Quote(leak.Name)).Append(" (").Append(leak.Where).Append("): ");
				if (leak.HatchOpen)
				{
					sb.Append("open, full exchange");
				}
				else
				{
					sb.Append("closed-leak 0.02 x seals ").Append(Num(HatchSeals.FactorFor(seals), "0.###"));
					sb.Append(" (").Append(seals).Append(": ").Append(leak.Blockers).Append(" blocking, ").Append(leak.Repairs).Append(" repairing)");
					sb.Append(" = ").Append(Num(ClosedHatchLeakMultiplier(seals), "0.#####")).Append(" of the open rate");
				}
				sb.Append(", water through it ").Append(Signed(leak.HatchWaterRate, RateFormat)).Append(" m3/s (L->R)");
			}
			else
			{
				sb.Append("leak ").Append(Quote(leak.Name)).Append(" in ").Append(Quote(leak.Where)).Append(": ");
				sb.Append("intensity ").Append(Num(leak.Intensity, "0.00000"));
				sb.Append(" = base ").Append(Num(leak.BaseIntensity, "0.00000"));
				foreach (ModifierSample m in leak.Modifiers)
					sb.Append(" x ").Append(string.IsNullOrEmpty(m.Name) ? "?" : m.Name).Append(" ").Append(Num(m.Value, "0.00"));
				sb.Append("; openess ").Append(Num(leak.Openess, "0.00000"));
				sb.Append(" (0.35 x intensity x difficulty ").Append(Num(DifficultyFactor(leak.Openess, leak.Intensity), "0.00")).Append(")");
				sb.Append(", hole ").Append(Num(leak.HoleSize, "0.0000"));
				sb.Append(", sea ").Append(Num(leak.SeaPressure, "0.00")).Append(" atm").Append(leak.HoleUnderWater ? " under water" : " above water");
				sb.Append(", drive ").Append(Signed(LeakDrive(leak.RoomPressure, leak.SeaPressure, leak.HoleUnderWater), "0.00")).Append(" atm");
				sb.Append(", inflow ").Append(Signed(NominalInflow(leak), RateFormat)).Append(" m3/s");
			}
			sb.Append("; ").Append(WorkersText(leak.Workers));
			return sb.ToString();
		}

		public static string HatchLine(HatchSample h)
		{
			var sb = new StringBuilder(Prefix);
			sb.Append("hatch ").Append(Quote(h.Name)).Append(" (").Append(Quote(h.Left)).Append(" | ").Append(Quote(h.Right)).Append("): ");
			sb.Append(h.Open ? "open" : "closed");
			if (h.Wrecked)
				sb.Append(", wrecked");
			if (h.Leaking)
				sb.Append(", leaking");
			if (h.Seals > 0)
				sb.Append(", ").Append(h.Seals).Append(" seal(s)");
			sb.Append(", dp(L-R) ").Append(Signed(h.DeltaP, "0.00")).Append(" atm");
			sb.Append(", water L->R ").Append(Signed(h.AverageWaterRate, RateFormat)).Append(" m3/s (last frame ").Append(Signed(h.Flow.LastWaterRate, RateFormat)).Append(")");
			sb.Append(", air L->R ").Append(Signed(h.AverageAirRate, "0.0000")).Append("/s (last frame ").Append(Signed(h.Flow.LastAirRate, "0.0000")).Append(")");
			return sb.ToString();
		}

		public static string TotalsLine(BoatSample boat, IList<CompartmentSample> compartments, IList<LeakSample> leaks)
		{
			double water = 0.0;
			double rate = 0.0;
			bool rateKnown = true;
			if (compartments != null)
			{
				foreach (CompartmentSample c in compartments)
				{
					// A merged compartment reports its shared water body; count that body once.
					if (c == null || !string.IsNullOrEmpty(c.MergedInto))
						continue;
					if (!double.IsNaN(c.Water))
						water += c.Water;
					if (double.IsNaN(c.WaterRate))
						rateKnown = false;
					else
						rate += c.WaterRate;
				}
			}
			double inflow = 0.0;
			int seaLeaks = 0;
			int hatchLeaks = 0;
			if (leaks != null)
			{
				foreach (LeakSample leak in leaks)
				{
					if (leak == null)
						continue;
					if (leak.IsHatch)
					{
						hatchLeaks++;
						continue;
					}
					seaLeaks++;
					double q = NominalInflow(leak);
					if (q > 0.0)
						inflow += q;
				}
			}

			var sb = new StringBuilder(Prefix);
			sb.Append("totals: water aboard ").Append(Num(water, "0.000")).Append(" m3");
			sb.Append(" (dW ").Append(rateKnown ? Signed(rate, RateFormat) : "n/a").Append(" m3/s)");
			sb.Append(", sea inflow ").Append(Num(inflow, RateFormat)).Append(" m3/s from ").Append(seaLeaks).Append(" leak(s)");
			sb.Append(", ").Append(hatchLeaks).Append(" leaking hatch(es)");
			sb.Append("; trim pump ");
			if (boat.TrimPumpOn.HasValue)
				sb.Append(boat.TrimPumpOn.Value ? "on" : "off").Append(boat.TrimPumpWaterLeft ? " (water left)" : " (dry)");
			else
				sb.Append("none");
			sb.Append(", auto-off hold ").Append(boat.LeaksAboard > 0 ? "on (" + boat.LeaksAboard + " leaking)" : "off");
			AppendDraw(sb, "compressor draw", boat.CompressorAir, boat.CompressorRoom, boat.ElapsedGameSeconds, true);
			AppendDraw(sb, "diesel room air", boat.DieselAir, boat.DieselRoom, boat.ElapsedGameSeconds, false);
			AppendDraw(sb, "tower venting", boat.TowerAir, boat.TowerRoom, boat.ElapsedGameSeconds, false);
			return sb.ToString();
		}

		// ---- immediate event lines

		public static string LeakStartedLine(string name, string where)
		{
			return Prefix + "event: leak started on " + Quote(name) + " in " + Quote(where);
		}

		public static string LeakStoppedLine(string name)
		{
			return Prefix + "event: leak stopped on " + Quote(name);
		}

		public static string HatchWreckedLine(string name, bool wrecked)
		{
			return Prefix + "event: hatch " + Quote(name) + (wrecked ? " is wrecked (health 0)" : " is no longer wrecked");
		}

		public static string HatchBlownOpenLine(string name, double seatPressure, double farPressure)
		{
			return Prefix + "event: hatch " + Quote(name) + " blown open, seat side " + Num(seatPressure, "0.00") + " atm, far side " + Num(farPressure, "0.00") + " atm";
		}

		/// <summary>A hatch's openability changed. <paramref name="state"/> is <see cref="LockState"/>'s text.</summary>
		public static string HatchLockLine(string name, string before, string after, double deltaP)
		{
			return Prefix + "event: hatch " + Quote(name) + " " + (before ?? "?") + " -> " + (after ?? "?") + ", dp(L-R) " + Signed(deltaP, "0.00") + " atm";
		}

		/// <summary>The one-liner written instead of a report when nothing is worth reporting (at most once a minute).</summary>
		public static string AllDryLine()
		{
			return Prefix + "all compartments dry at 1.00 atm";
		}

		public const string Openable = "can be opened";
		public const string HeldForRepair = "held shut for a repair";
		public const string PressureLocked = "held shut by the pressure lock";
		public const string VanillaLocked = "held shut by the game's own rule";

		/// <summary>
		/// Why a hatch can or cannot be opened, as the event lines name it. The repair hold wins, then the
		/// mod's pressure lock (only while hatch pressure blocking is on), else the game's own rule.
		/// </summary>
		public static string LockState(bool canBeOpened, bool repairHeld, bool pressureBlockingOn)
		{
			if (canBeOpened)
				return Openable;
			if (repairHeld)
				return HeldForRepair;
			return pressureBlockingOn ? PressureLocked : VanillaLocked;
		}

		/// <summary>State of an open hatch in the lock watch (openability does not matter while it stands open).</summary>
		public const string HatchOpen = "open";

		/// <summary>The lock-watch state of a hatch: <see cref="HatchOpen"/> while open, else <see cref="LockState"/>.</summary>
		public static string HatchLockState(bool locked, bool canBeOpened, bool repairHeld, bool pressureBlockingOn)
		{
			return locked ? LockState(canBeOpened, repairHeld, pressureBlockingOn) : HatchOpen;
		}

		/// <summary>
		/// True when a lock-watch change is worth an event line: something held a hatch shut before or after.
		/// Plain opening and closing of a free hatch (open &lt;-&gt; can be opened) is not logged.
		/// </summary>
		public static bool IsLockChangeWorthLogging(string before, string after)
		{
			if (before == null || after == null || string.Equals(before, after, StringComparison.Ordinal))
				return false;
			return !(IsFree(before) && IsFree(after));
		}

		private static bool IsFree(string state)
		{
			return state == HatchOpen || state == Openable;
		}

		// ---- formatting helpers

		/// <summary>Invariant-culture number; "n/a" for NaN or infinity.</summary>
		public static string Num(double value, string format)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
				return "n/a";
			return value.ToString(format, Invariant);
		}

		/// <summary>Like <see cref="Num"/> but with an explicit "+" on positive values.</summary>
		public static string Signed(double value, string format)
		{
			if (double.IsNaN(value) || double.IsInfinity(value))
				return "n/a";
			string text = value.ToString(format, Invariant);
			return value > 0.0 && text.IndexOf('-') < 0 && HasNonZeroDigit(text) ? "+" + text : text;
		}

		private static bool HasNonZeroDigit(string text)
		{
			foreach (char ch in text)
			{
				if (ch >= '1' && ch <= '9')
					return true;
			}
			return false;
		}

		private static string Quote(string name)
		{
			return "\"" + (name ?? "?") + "\"";
		}

		private static bool Above(double value, double threshold)
		{
			return !double.IsNaN(value) && Math.Abs(value) > threshold;
		}

		private static void AppendDraw(StringBuilder sb, string what, double amount, string room, double seconds, bool positiveIsDraw)
		{
			if (double.IsNaN(amount))
				return;
			sb.Append("; ").Append(what);
			if (!string.IsNullOrEmpty(room))
				sb.Append(" ").Append(Quote(room));
			sb.Append(" ");
			if (seconds > 0.0)
				sb.Append(positiveIsDraw ? Num(amount / seconds, "0.0000") : Signed(amount / seconds, "0.0000")).Append("/s");
			else
				sb.Append(positiveIsDraw ? Num(amount, "0.0000") : Signed(amount, "0.0000")).Append(" so far");
		}

		private static string WorkersText(List<WorkerSample> workers)
		{
			if (workers == null || workers.Count == 0)
				return "nobody on it";
			var sb = new StringBuilder("on it: ");
			for (int i = 0; i < workers.Count; i++)
			{
				WorkerSample w = workers[i];
				if (i > 0)
					sb.Append(", ");
				sb.Append(string.IsNullOrEmpty(w.Name) ? "?" : w.Name).Append(" (").Append(string.IsNullOrEmpty(w.Job) ? "?" : w.Job);
				if (!float.IsNaN(w.Progress))
					sb.Append(" ").Append(Num(100.0 * w.Progress, "0")).Append(" %");
				sb.Append(w.Interacting ? ", working" : ", on the way").Append(")");
			}
			return sb.ToString();
		}
	}
}
