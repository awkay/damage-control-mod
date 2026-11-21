using System.Collections.Generic;
using DamageControl.Core.Diagnostics;
using Xunit;

namespace DamageControl.Tests
{
    public class DiagRecorderTests
    {
        private static DiagRecorder Enabled()
        {
            return new DiagRecorder { Enabled = true };
        }

        [Fact]
        public void RecordsNothingWhileDisabled()
        {
            var r = new DiagRecorder();
            var hatch = new object();
            r.RecordHatch(hatch, 1.0, 2.0, 0.1);
            r.Add("compressor", 0.5, "Engine room");
            Assert.False(r.TryGetHatch(hatch, out _));
            Assert.Equal(0, r.HatchCount);
            Assert.False(r.HasSum("compressor"));
            Assert.Null(r.Label("compressor"));
        }

        [Fact]
        public void KeepsTheLastFrameAndSumsTheWindow()
        {
            var r = Enabled();
            var hatch = new object();
            r.RecordHatch(hatch, 0.2, 0.01, 0.02);
            r.RecordHatch(hatch, 0.4, -0.03, 0.04);
            Assert.True(r.TryGetHatch(hatch, out HatchFlowWindow w));
            Assert.Equal(0.4, w.LastAir, 12);
            Assert.Equal(-0.03, w.LastWater, 12);
            Assert.Equal(0.6, w.SumAir, 12);
            Assert.Equal(-0.02, w.SumWater, 12);
            Assert.Equal(10.0, w.LastAirRate, 9);
            Assert.Equal(-0.75, w.LastWaterRate, 9);
        }

        [Fact]
        public void PausedFrameHasNoRate()
        {
            var r = Enabled();
            var hatch = new object();
            r.RecordHatch(hatch, 0.0, 0.0, 0.0);
            Assert.True(r.TryGetHatch(hatch, out HatchFlowWindow w));
            Assert.True(double.IsNaN(w.LastWaterRate));
            Assert.True(double.IsNaN(w.LastAirRate));
        }

        [Fact]
        public void NonFiniteFlowsCountAsZero()
        {
            var r = Enabled();
            var hatch = new object();
            r.RecordHatch(hatch, double.NaN, double.PositiveInfinity, double.PositiveInfinity);
            Assert.True(r.TryGetHatch(hatch, out HatchFlowWindow w));
            Assert.Equal(0.0, w.SumAir);
            Assert.Equal(0.0, w.SumWater);
            Assert.Equal(0.0, w.LastSeconds);
        }

        [Fact]
        public void HatchesAreKeyedByReference()
        {
            var r = Enabled();
            var a = new string('x', 3);
            var b = new string('x', 3);
            r.RecordHatch(a, 1.0, 0.0, 1.0);
            r.RecordHatch(b, 2.0, 0.0, 1.0);
            Assert.Equal(2, r.HatchCount);
            var keys = new List<object>();
            r.CopyHatchKeys(keys);
            Assert.Contains(a, keys);
            Assert.Contains(b, keys);
        }

        [Fact]
        public void ResetWindowZeroesSumsButKeepsLastFrameAndLabels()
        {
            var r = Enabled();
            var hatch = new object();
            r.RecordHatch(hatch, 0.5, 0.25, 0.5);
            r.Add("compressor", 0.1, "Engine room");
            r.Add("compressor", 0.2);
            Assert.Equal(0.3, r.Sum("compressor"), 12);
            r.ResetWindow();
            Assert.True(r.TryGetHatch(hatch, out HatchFlowWindow w));
            Assert.Equal(0.0, w.SumAir);
            Assert.Equal(0.0, w.SumWater);
            Assert.Equal(0.5, w.LastAir);
            Assert.False(r.HasSum("compressor"));
            Assert.Equal(0.0, r.Sum("compressor"));
            Assert.Equal("Engine room", r.Label("compressor"));
        }

        [Fact]
        public void ClearForgetsEverythingButEnabled()
        {
            var r = Enabled();
            r.RecordHatch(new object(), 1.0, 1.0, 1.0);
            r.Add("diesel", -0.1, "Diesel room");
            r.Clear();
            Assert.Equal(0, r.HatchCount);
            Assert.False(r.HasSum("diesel"));
            Assert.Null(r.Label("diesel"));
            Assert.True(r.Enabled);
        }

        [Fact]
        public void NullKeysAreIgnored()
        {
            var r = Enabled();
            r.RecordHatch(null, 1.0, 1.0, 1.0);
            r.Add(null, 1.0);
            Assert.Equal(0, r.HatchCount);
            Assert.False(r.TryGetHatch(null, out _));
            Assert.Equal(0.0, r.Sum(null));
        }
    }
}
