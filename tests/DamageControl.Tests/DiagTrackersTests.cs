using System.Linq;
using DamageControl.Core.Diagnostics;
using Xunit;

namespace DamageControl.Tests
{
    public class DiagTrackersTests
    {
        // ---- DiagSchedule

        [Fact]
        public void FirstReportIsDueAtOnceThenEveryTenRealSeconds()
        {
            var s = new DiagSchedule();
            Assert.True(s.ReportDue(100.0));
            Assert.False(s.ReportDue(100.1));
            Assert.False(s.ReportDue(109.9));
            Assert.True(s.ReportDue(110.0));
            Assert.False(s.ReportDue(115.0));
            Assert.True(s.ReportDue(121.0));
        }

        [Fact]
        public void PollsEveryHalfSecond()
        {
            var s = new DiagSchedule();
            Assert.True(s.PollDue(0.0));
            Assert.False(s.PollDue(0.4));
            Assert.True(s.PollDue(0.5));
        }

        [Fact]
        public void DryLineAtMostOncePerMinute()
        {
            var s = new DiagSchedule();
            Assert.True(s.DryLineDue(10.0));
            Assert.False(s.DryLineDue(20.0));
            Assert.False(s.DryLineDue(69.0));
            Assert.True(s.DryLineDue(70.0));
        }

        [Fact]
        public void ResetMakesEverythingDueAgain()
        {
            var s = new DiagSchedule();
            Assert.True(s.ReportDue(5.0));
            Assert.True(s.DryLineDue(5.0));
            s.Reset();
            Assert.True(s.ReportDue(6.0));
            Assert.True(s.DryLineDue(6.0));
        }

        [Fact]
        public void ClockGoingBackwardsRearms()
        {
            var s = new DiagSchedule();
            Assert.True(s.ReportDue(1000.0));
            Assert.True(s.ReportDue(3.0));
            Assert.False(s.ReportDue(4.0));
        }

        [Fact]
        public void NonFiniteTimeIsNeverDue()
        {
            var s = new DiagSchedule();
            Assert.False(s.ReportDue(double.NaN));
            Assert.False(s.PollDue(double.PositiveInfinity));
            Assert.True(s.ReportDue(1.0));
        }

        // ---- RateTracker

        [Fact]
        public void RateIsChangePerGameSecond()
        {
            var t = new RateTracker();
            var room = new object();
            Assert.True(double.IsNaN(t.Sample(room, 1.0, 100.0)));
            Assert.Equal(0.05, t.Sample(room, 1.5, 110.0), 12);
            Assert.Equal(-0.1, t.Sample(room, 0.5, 120.0), 12);
        }

        [Fact]
        public void NoGameTimeOrBackwardsClockGivesNaN()
        {
            var t = new RateTracker();
            var room = new object();
            t.Sample(room, 1.0, 100.0);
            Assert.True(double.IsNaN(t.Sample(room, 2.0, 100.0)));   // paused
            Assert.True(double.IsNaN(t.Sample(room, 2.0, 50.0)));    // a load
            Assert.Equal(0.1, t.Sample(room, 3.0, 60.0), 12);       // measured from the new baseline
        }

        [Fact]
        public void KeysAreSeparateAndClearForgets()
        {
            var t = new RateTracker();
            var a = new object();
            var b = new object();
            t.Sample(a, 0.0, 0.0);
            t.Sample(b, 10.0, 0.0);
            Assert.Equal(1.0, t.Sample(a, 1.0, 1.0), 12);
            Assert.Equal(-1.0, t.Sample(b, 9.0, 1.0), 12);
            t.Clear();
            Assert.True(double.IsNaN(t.Sample(a, 5.0, 2.0)));
            Assert.True(double.IsNaN(t.Sample(null, 5.0, 2.0)));
        }

        // ---- StateWatch

        [Fact]
        public void BaselineRoundReportsNothing()
        {
            var w = new StateWatch();
            w.BeginRound();
            Assert.False(w.Observe(new object(), "Pipe", "leaking", out _));
            Assert.Empty(w.EndRound());
            Assert.True(w.Primed);
        }

        [Fact]
        public void ReportsNewChangedAndGoneKeys()
        {
            var w = new StateWatch();
            var pipe = new object();
            var hatch = new object();
            w.BeginRound();
            w.Observe(pipe, "Pipe", "leaking", out _);
            w.Observe(hatch, "Hatch", "can be opened", out _);
            w.EndRound();

            w.BeginRound();
            Assert.False(w.Observe(pipe, "Pipe", "leaking", out _));
            Assert.True(w.Observe(hatch, "Hatch", "held shut", out StateChange changed));
            Assert.Equal("can be opened", changed.Before);
            Assert.Equal("held shut", changed.After);
            var hole = new object();
            Assert.True(w.Observe(hole, "Hole", "leaking", out StateChange added));
            Assert.Null(added.Before);
            Assert.Equal("Hole", added.Label);
            Assert.Empty(w.EndRound());

            w.BeginRound();
            w.Observe(hatch, "Hatch", "held shut", out _);
            var gone = w.EndRound();
            Assert.Equal(2, gone.Count);
            Assert.All(gone, g => Assert.Null(g.After));
            Assert.Equal(new[] { "Hole", "Pipe" }, gone.Select(g => g.Label).OrderBy(x => x));

            w.BeginRound();
            w.Observe(hatch, "Hatch", "held shut", out _);
            Assert.Empty(w.EndRound());
        }

        [Fact]
        public void ClearStartsANewBaseline()
        {
            var w = new StateWatch();
            var pipe = new object();
            w.BeginRound();
            w.Observe(pipe, "Pipe", "leaking", out _);
            w.EndRound();
            w.Clear();
            Assert.False(w.Primed);
            w.BeginRound();
            Assert.False(w.Observe(new object(), "Other", "leaking", out _));
            Assert.Empty(w.EndRound());
        }
    }
}
