using DamageControl.Core.Repair;
using Xunit;

namespace DamageControl.Tests
{
    public class DismantleTests
    {
        // Runs the order to completion in fixed steps; returns (seconds, finished step).
        private static (float seconds, DismantleStep last) Run(float health, float rate, float dt = 0.1f, int maxSteps = 100000)
        {
            var state = Dismantle.Begin(health);
            DismantleStep step = default;
            float t = 0f;
            for (int i = 0; i < maxSteps; i++)
            {
                step = Dismantle.Advance(state, health, rate, dt);
                state = step.State;
                health = step.Health;
                t += dt;
                if (step.Finished)
                    break;
                Assert.True(step.Progress < 1f);
            }
            return (t, step);
        }

        // ---- 04 §8 #8: dismantle

        [Theory]
        [InlineData(0f, false)]
        [InlineData(0.5f, false)]
        [InlineData(0.999f, false)]
        [InlineData(1f, true)]
        [InlineData(5f, true)]
        [InlineData(float.NaN, false)]
        public void Criterion8_HiddenBelowOneHealth(float health, bool offered)
        {
            Assert.Equal(offered, Dismantle.IsOffered(true, true, health));
        }

        [Fact]
        public void Criterion8_HiddenWithoutEquipmentOrInteractable()
        {
            Assert.False(Dismantle.IsOffered(false, true, 5f));
            Assert.False(Dismantle.IsOffered(true, false, 5f));
        }

        [Fact]
        public void Criterion8_OnePointPerRepairTimeAtCharacterPerformance()
        {
            Assert.Equal(1f / 20f, Dismantle.RatePerSecond(1f, 1f, 20f), 6);
            Assert.Equal(0.8f / 20f, Dismantle.RatePerSecond(0.8f, 1f, 20f), 6);
        }

        [Fact]
        public void Criterion8_HandymanIsOnePointTwentyFiveTimesFaster()
        {
            Assert.Equal(1.25f, Dismantle.PerformanceMultiplier(true));
            Assert.Equal(1f, Dismantle.PerformanceMultiplier(false));
            Assert.Equal(1.25f / 20f, Dismantle.RatePerSecond(1f, Dismantle.PerformanceMultiplier(true), 20f), 6);
        }

        [Fact]
        public void Criterion8_ConsumesOneHealthPerPoint()
        {
            var state = Dismantle.Begin(3f);
            var step = Dismantle.Advance(state, 3f, 1f, 0.5f);
            Assert.Equal(3f, step.Health);
            step = Dismantle.Advance(step.State, step.Health, 1f, 0.5f);
            Assert.Equal(2f, step.Health);
            Assert.False(step.Finished);
        }

        [Fact]
        public void Criterion8_TakesHealthTimesRepairTime()
        {
            var (seconds, last) = Run(4f, Dismantle.RatePerSecond(1f, 1f, 10f));
            Assert.True(last.Finished);
            Assert.Equal(0f, last.Health);
            Assert.InRange(seconds, 39.9f, 40.2f);
        }

        [Fact]
        public void Criterion8_YieldsOneScrapOrTwoWithThrifty()
        {
            Assert.Equal(1, Dismantle.ScrapYield(false));
            Assert.Equal(2, Dismantle.ScrapYield(true));
        }

        [Theory]
        [InlineData(2.5f)]
        [InlineData(1.2f)]
        [InlineData(3.999f)]
        public void Criterion8_NonIntegerHealthStillTerminates(float health)
        {
            var (_, last) = Run(health, 1f, 0.25f, 1000);
            Assert.True(last.Finished);
            Assert.Equal(0f, last.Health);
        }

        [Fact]
        public void Criterion8_HealthLostToDamageMidOrderEndsIt()
        {
            var state = Dismantle.Begin(5f);
            var step = Dismantle.Advance(state, 0.4f, 0.1f, 0.1f);
            Assert.True(step.Finished);
            Assert.Equal(0f, step.Health);
            Assert.False(step.YieldsScrap);
        }

        [Fact]
        public void Criterion8_ScrapIsDueWhenTheWorkTookTheDeviceApart()
        {
            var (_, last) = Run(2f, 1f, 0.25f, 1000);
            Assert.True(last.YieldsScrap);
            (_, last) = Run(1.5f, 1f, 0.25f, 1000);
            Assert.True(last.YieldsScrap);
        }

        [Fact]
        public void ALargeFrameRemovesSeveralPointsButNeverGoesNegative()
        {
            var step = Dismantle.Advance(Dismantle.Begin(3f), 3f, 1f, 10f);
            Assert.True(step.Finished);
            Assert.Equal(0f, step.Health);
        }

        [Fact]
        public void ProgressRisesTowardButStaysBelowOne()
        {
            var state = Dismantle.Begin(2f);
            var step = Dismantle.Advance(state, 2f, 1f, 0.5f);
            Assert.Equal(0.999f * 0.5f / 2f, step.Progress, 5);
            step = Dismantle.Advance(step.State, step.Health, 1f, 1.4f);
            Assert.True(step.Progress < 1f);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void NoTimeOrNoRateMakesNoProgress(float dt)
        {
            var step = Dismantle.Advance(Dismantle.Begin(3f), 3f, 1f, dt);
            Assert.Equal(3f, step.Health);
            Assert.Equal(0f, step.Progress);
            step = Dismantle.Advance(Dismantle.Begin(3f), 3f, 0f, 1f);
            Assert.Equal(3f, step.Health);
        }

        [Fact]
        public void BadRepairTimeCountsAsOneSecondAndBadPerformanceAsNoProgress()
        {
            Assert.Equal(1f, Dismantle.RatePerSecond(1f, 1f, 0f));
            Assert.Equal(1f, Dismantle.RatePerSecond(1f, 1f, float.NaN));
            Assert.Equal(0f, Dismantle.RatePerSecond(0f, 1f, 10f));
            Assert.Equal(0f, Dismantle.RatePerSecond(float.NaN, 1f, 10f));
        }

        [Fact]
        public void ListedLastOnTheWheel()
        {
            Assert.Equal(-1, Dismantle.SortingPriority);
        }
    }
}
