using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class SimClockTests
    {
        [Fact]
        public void GameDtIsUnityDeltaTimesArtificialScale()
        {
            var clock = new SimClock(0.02f, 2f, 8f);
            Assert.Equal(0.16f, clock.GameDt, 5);
            Assert.Equal(16f, clock.Compression, 5);
        }

        [Fact]
        public void NegativeAndNaNInputsBecomeZero()
        {
            var clock = new SimClock(-1f, float.NaN, 3f);
            Assert.Equal(0f, clock.GameDt);
            Assert.Equal(0f, clock.Compression);
            Assert.True(clock.IsPaused);
        }

        [Fact]
        public void CapsApplyPerFeature()
        {
            var clock = new SimClock(2f, 1f, 1f);
            Assert.Equal(0.5f, clock.CappedGameDt(0.5f));
            Assert.Equal(0.5f, clock.CappedUnityDt(0.5f));
            Assert.Equal(2f, clock.CappedGameDt(10f));
            // Spec 01 §6 #14: frame delta 2.0 at 1x is clamped to 0.5.
        }

        [Fact]
        public void CapUsesGameTimeUnderCompression()
        {
            var clock = new SimClock(0.1f, 1f, 10f);
            Assert.Equal(1f, clock.GameDt, 5);
            Assert.Equal(0.5f, clock.CappedGameDt(0.5f));
            Assert.Equal(0.1f, clock.CappedUnityDt(0.5f), 5);
        }

        [Theory]
        [InlineData(0f, 2f)]
        [InlineData(0.5f, 2f)]
        [InlineData(1f, 2f)]
        [InlineData(4f, 1f)]
        [InlineData(16f, 0.5f)]
        public void DampedRateIsBaseOverSqrtCompressionAboveOne(float compression, float expected)
        {
            // 02 §2.3b: k = 2 when time scale <= 1, else 2 / sqrt(timeScale).
            Assert.Equal(expected, SimClock.DampedRate(2f, compression), 5);
        }

        [Fact]
        public void InstanceDampedRateUsesFrameCompression()
        {
            Assert.Equal(1f, new SimClock(0.02f, 1f, 4f).DampedRate(2f), 5);
        }

        [Fact]
        public void FromGameSecondsIsRealTime()
        {
            var clock = SimClock.FromGameSeconds(1.5f);
            Assert.Equal(1.5f, clock.GameDt);
            Assert.Equal(1f, clock.Compression);
        }
    }
}
