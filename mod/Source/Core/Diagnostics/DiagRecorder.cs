using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace DamageControl.Core.Diagnostics
{
	/// <summary>Compares keys by reference (Unity objects and plain test objects alike).</summary>
	public sealed class ReferenceKeyComparer : IEqualityComparer<object>
	{
		public static readonly ReferenceKeyComparer Instance = new ReferenceKeyComparer();

		private ReferenceKeyComparer()
		{
		}

		public new bool Equals(object x, object y)
		{
			return ReferenceEquals(x, y);
		}

		public int GetHashCode(object obj)
		{
			return RuntimeHelpers.GetHashCode(obj);
		}
	}

	/// <summary>What one hatch moved: the last frame it ran, and the sums since the last report.</summary>
	public struct HatchFlowWindow
	{
		/// <summary>Air moved in the last recorded frame, left to right (negative: right to left).</summary>
		public double LastAir;

		/// <summary>Water (m³) moved in the last recorded frame, left to right.</summary>
		public double LastWater;

		/// <summary>Game seconds of the last recorded frame (0 while paused).</summary>
		public double LastSeconds;

		/// <summary>Air moved since the last <see cref="DiagRecorder.ResetWindow"/>, left to right.</summary>
		public double SumAir;

		/// <summary>Water (m³) moved since the last <see cref="DiagRecorder.ResetWindow"/>, left to right.</summary>
		public double SumWater;

		/// <summary>Air rate of the last frame per game second; NaN when that frame had no game time.</summary>
		public double LastAirRate
		{
			get { return LastSeconds > 0.0 ? LastAir / LastSeconds : double.NaN; }
		}

		/// <summary>Water rate (m³/s) of the last frame; NaN when that frame had no game time.</summary>
		public double LastWaterRate
		{
			get { return LastSeconds > 0.0 ? LastWater / LastSeconds : double.NaN; }
		}
	}

	/// <summary>
	/// Diagnostics instrumentation (read-only with respect to the simulation): the hatch exchange and the
	/// air draws report what they already moved, and the diagnostics report reads the sums back. Nothing
	/// here feeds back into any rule. Recording is a no-op while <see cref="Enabled"/> is false, so the
	/// instrumented patches pay one field read when diagnostics are off. Main thread only.
	/// </summary>
	public sealed class DiagRecorder
	{
		/// <summary>The recorder the game uses.</summary>
		public static readonly DiagRecorder Shared = new DiagRecorder();

		private readonly Dictionary<object, HatchFlowWindow> hatches = new Dictionary<object, HatchFlowWindow>(ReferenceKeyComparer.Instance);
		private readonly Dictionary<string, double> sums = new Dictionary<string, double>(StringComparer.Ordinal);
		private readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);
		private readonly List<object> scratch = new List<object>();

		/// <summary>When false every Record/Add call returns at once.</summary>
		public bool Enabled;

		/// <summary>
		/// One hatch frame: the summed flow of all substeps (positive left to right) over
		/// <paramref name="gameSeconds"/>. Record zero flows too, so "last frame" is never stale.
		/// </summary>
		public void RecordHatch(object hatch, double air, double water, double gameSeconds)
		{
			if (!Enabled || hatch == null)
				return;
			HatchFlowWindow w;
			hatches.TryGetValue(hatch, out w);
			w.LastAir = Finite(air);
			w.LastWater = Finite(water);
			w.LastSeconds = gameSeconds > 0.0 && !double.IsInfinity(gameSeconds) ? gameSeconds : 0.0;
			w.SumAir += w.LastAir;
			w.SumWater += w.LastWater;
			hatches[hatch] = w;
		}

		/// <summary>Adds <paramref name="amount"/> to the named counter (e.g. air drawn by the compressor).</summary>
		public void Add(string key, double amount)
		{
			if (!Enabled || key == null)
				return;
			double current;
			sums.TryGetValue(key, out current);
			sums[key] = current + Finite(amount);
		}

		/// <summary><see cref="Add(string, double)"/> plus a label for the counter (e.g. the compartment it draws from).</summary>
		public void Add(string key, double amount, string label)
		{
			if (!Enabled || key == null)
				return;
			Add(key, amount);
			labels[key] = label;
		}

		/// <summary>The counter's sum since the last <see cref="ResetWindow"/>; 0 when never added.</summary>
		public double Sum(string key)
		{
			double value;
			return key != null && sums.TryGetValue(key, out value) ? value : 0.0;
		}

		/// <summary>True when the counter received anything since the last <see cref="ResetWindow"/>.</summary>
		public bool HasSum(string key)
		{
			return key != null && sums.ContainsKey(key);
		}

		/// <summary>The counter's last label, or null.</summary>
		public string Label(string key)
		{
			string value;
			return key != null && labels.TryGetValue(key, out value) ? value : null;
		}

		public bool TryGetHatch(object hatch, out HatchFlowWindow window)
		{
			window = new HatchFlowWindow();
			return hatch != null && hatches.TryGetValue(hatch, out window);
		}

		/// <summary>Appends every hatch that recorded since the last <see cref="Clear"/> to <paramref name="into"/>.</summary>
		public void CopyHatchKeys(List<object> into)
		{
			if (into != null)
				into.AddRange(hatches.Keys);
		}

		/// <summary>Number of hatches with a record.</summary>
		public int HatchCount
		{
			get { return hatches.Count; }
		}

		/// <summary>Starts a new report window: zeroes the sums and counters, keeps each hatch's last frame.</summary>
		public void ResetWindow()
		{
			scratch.Clear();
			scratch.AddRange(hatches.Keys);
			foreach (object key in scratch)
			{
				HatchFlowWindow w = hatches[key];
				w.SumAir = 0.0;
				w.SumWater = 0.0;
				hatches[key] = w;
			}
			scratch.Clear();
			sums.Clear();
		}

		/// <summary>Forgets everything (diagnostics switched off, new session). Leaves <see cref="Enabled"/> alone.</summary>
		public void Clear()
		{
			hatches.Clear();
			sums.Clear();
			labels.Clear();
		}

		private static double Finite(double value)
		{
			return double.IsNaN(value) || double.IsInfinity(value) ? 0.0 : value;
		}
	}
}
