using System;
using System.Collections.Generic;

namespace DamageControl.Core.Diagnostics
{
	/// <summary>
	/// When the diagnostics write: the periodic report every <see cref="ReportInterval"/> real seconds,
	/// event polls every <see cref="PollInterval"/> real seconds, and the "all dry" line at most once per
	/// <see cref="DryLineInterval"/>. Real (unscaled) time, so time compression does not multiply the output.
	/// The first report is due on the first check after <see cref="Reset"/>.
	/// </summary>
	public sealed class DiagSchedule
	{
		public const double ReportInterval = 10.0;
		public const double PollInterval = 0.5;
		public const double DryLineInterval = 60.0;

		private double nextReport = double.NaN;
		private double nextPoll = double.NaN;
		private double nextDryLine = double.NaN;

		/// <summary>True when a report is due at real time <paramref name="now"/>; schedules the next one.</summary>
		public bool ReportDue(double now)
		{
			return Due(ref nextReport, now, ReportInterval);
		}

		/// <summary>True when an event poll is due at <paramref name="now"/>; schedules the next one.</summary>
		public bool PollDue(double now)
		{
			return Due(ref nextPoll, now, PollInterval);
		}

		/// <summary>True when the "all dry" line may be written at <paramref name="now"/>; if so, holds it back for a minute.</summary>
		public bool DryLineDue(double now)
		{
			return Due(ref nextDryLine, now, DryLineInterval);
		}

		/// <summary>Makes every kind due on the next check (diagnostics switched on, new session).</summary>
		public void Reset()
		{
			nextReport = double.NaN;
			nextPoll = double.NaN;
			nextDryLine = double.NaN;
		}

		private static bool Due(ref double next, double now, double interval)
		{
			if (double.IsNaN(now) || double.IsInfinity(now))
				return false;
			// A clock that jumped backwards (should not happen) re-arms instead of going silent.
			if (double.IsNaN(next) || now >= next || next - now > interval)
			{
				next = now + interval;
				return true;
			}
			return false;
		}
	}

	/// <summary>
	/// Rate of change of a value per game second between two samples of the same key (water in a
	/// compartment between two reports). Keys compare by reference.
	/// </summary>
	public sealed class RateTracker
	{
		private struct Point
		{
			public double Value;
			public double Time;
		}

		private readonly Dictionary<object, Point> points = new Dictionary<object, Point>(ReferenceKeyComparer.Instance);

		/// <summary>
		/// Records (<paramref name="value"/>, <paramref name="gameSeconds"/>) for <paramref name="key"/> and
		/// returns the change per game second since the previous sample. NaN for the first sample, when no
		/// game time passed, or when the clock went backwards (a load).
		/// </summary>
		public double Sample(object key, double value, double gameSeconds)
		{
			if (key == null)
				return double.NaN;
			Point previous;
			bool had = points.TryGetValue(key, out previous);
			points[key] = new Point { Value = value, Time = gameSeconds };
			if (!had)
				return double.NaN;
			double dt = gameSeconds - previous.Time;
			if (!(dt > 0.0) || double.IsInfinity(dt))
				return double.NaN;
			double rate = (value - previous.Value) / dt;
			return double.IsNaN(rate) || double.IsInfinity(rate) ? double.NaN : rate;
		}

		public void Clear()
		{
			points.Clear();
		}
	}

	/// <summary>One change seen by <see cref="StateWatch"/>.</summary>
	public struct StateChange
	{
		public object Key;
		public string Label;

		/// <summary>The state before; null when the key is new.</summary>
		public string Before;

		/// <summary>The state now; null when the key was not observed this round (gone).</summary>
		public string After;
	}

	/// <summary>
	/// Watches named states of keyed objects across polling rounds (leaks, hatch damage, hatch locks) and
	/// reports what changed. The first round after <see cref="Clear"/> only records a baseline, so
	/// switching diagnostics on mid-flood does not log every existing leak as "started".
	/// Use: <see cref="BeginRound"/>, <see cref="Observe"/> each present key, <see cref="EndRound"/>.
	/// </summary>
	public sealed class StateWatch
	{
		private struct Entry
		{
			public string Label;
			public string State;
			public int Round;
		}

		private readonly Dictionary<object, Entry> entries = new Dictionary<object, Entry>(ReferenceKeyComparer.Instance);
		private readonly List<object> scratch = new List<object>();
		private int round;
		private bool primed;

		/// <summary>True once a baseline round has completed.</summary>
		public bool Primed
		{
			get { return primed; }
		}

		public void BeginRound()
		{
			round++;
		}

		/// <summary>
		/// Records <paramref name="state"/> for <paramref name="key"/>. Returns true, with the change, when the
		/// key is new or its state differs and a baseline exists.
		/// </summary>
		public bool Observe(object key, string label, string state, out StateChange change)
		{
			change = new StateChange();
			if (key == null)
				return false;
			Entry previous;
			bool had = entries.TryGetValue(key, out previous);
			entries[key] = new Entry { Label = label, State = state, Round = round };
			if (had && string.Equals(previous.State, state, StringComparison.Ordinal))
				return false;
			change.Key = key;
			change.Label = label;
			change.Before = had ? previous.State : null;
			change.After = state;
			return primed;
		}

		/// <summary>Forgets keys not observed this round and returns them as changes (After = null); none for the baseline round.</summary>
		public List<StateChange> EndRound()
		{
			var gone = new List<StateChange>();
			scratch.Clear();
			foreach (KeyValuePair<object, Entry> pair in entries)
			{
				if (pair.Value.Round != round)
					scratch.Add(pair.Key);
			}
			foreach (object key in scratch)
			{
				Entry e = entries[key];
				entries.Remove(key);
				if (primed)
					gone.Add(new StateChange { Key = key, Label = e.Label, Before = e.State, After = null });
			}
			scratch.Clear();
			primed = true;
			return gone;
		}

		public void Clear()
		{
			entries.Clear();
			primed = false;
		}
	}
}
