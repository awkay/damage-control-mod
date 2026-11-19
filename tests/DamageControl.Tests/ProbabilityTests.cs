using System;
using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class ProbabilityTests
    {
        [Fact]
        public void PerStepMatchesClosedForm()
        {
            Assert.Equal(1.0 - Math.Pow(0.9, 0.5), Probability.PerStep(0.1f, 0.5f), 5);
            Assert.Equal(0.1f, Probability.PerStep(0.1f, 1f), 5);
        }

        [Fact]
        public void PerStepComposesAcrossSteps()
        {
            // Ten 0.1 s steps must give the same total chance as one 1 s step (frame-rate independence, D7).
            float step = Probability.PerStep(0.3f, 0.1f);
            double none = Math.Pow(1.0 - step, 10);
            Assert.Equal(0.3, 1.0 - none, 4);
        }

        [Theory]
        [InlineData(0f, 1f, 0f)]
        [InlineData(0.5f, 0f, 0f)]
        [InlineData(0.5f, -1f, 0f)]
        [InlineData(1f, 0.01f, 1f)]
        [InlineData(2f, 0.01f, 1f)]
        [InlineData(float.NaN, 1f, 0f)]
        [InlineData(0.5f, float.NaN, 0f)]
        public void PerStepEdgeCases(float p, float dt, float expected)
        {
            Assert.Equal(expected, Probability.PerStep(p, dt));
        }

        [Fact]
        public void RollUsesThePerStepChance()
        {
            Assert.False(Probability.Roll(new SeededRandom(1), 0f, 1f));
            Assert.True(Probability.Roll(new SeededRandom(1), 1f, 1f));
        }
    }
}
