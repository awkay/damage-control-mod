using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class AirQualityTests
    {
        [Theory]
        [InlineData(0.5, 1.0, 0.2f, false)]   // baseline
        [InlineData(0.04, 1.0, 0.2f, true)]   // oxygen 0.04
        [InlineData(0.5, 0.14, 0.2f, true)]   // p 0.14
        [InlineData(0.5, 0.15, 0.2f, false)]  // p 0.15
        [InlineData(0.5, 8.5, 0.2f, false)]   // p 8.5
        [InlineData(0.5, 8.51, 0.2f, true)]   // p 8.51
        [InlineData(0.5, 1.0, 1.51f, true)]   // water 1.51 m
        [InlineData(0.5, 1.0, 1.5f, false)]   // water 1.5 m
        public void Criterion01_2_UnbreathableRule(double oxygen, double pressure, float water, bool expected)
        {
            Assert.Equal(expected, AirQuality.IsUnbreathable(oxygen, pressure, water));
        }

        [Theory]
        [InlineData(0.5, 1.0, 0.2f)]
        [InlineData(0.04, 1.0, 0.2f)]
        [InlineData(0.5, 8.51, 0.2f)]
        [InlineData(0.5, 1.0, 1.51f)]
        public void Criterion01_3_ToxicityIsMinusPointOneExactlyWhenUnbreathable(double oxygen, double pressure, float water)
        {
            bool unbreathable = AirQuality.IsUnbreathable(oxygen, pressure, water);
            Assert.Equal(unbreathable ? -0.1f : -1.0f, AirQuality.Toxicity(unbreathable));
        }

        [Fact]
        public void ToxicityValuesMatchTheChokingCheckOfC18()
        {
            // C18: a sailor is hurt when Random > 0.85 - toxicity. -0.1 gives about 5 %, -1.0 never.
            Assert.Equal(0.95f, 0.85f - AirQuality.Toxicity(true), 5);
            Assert.True(0.85f - AirQuality.Toxicity(false) >= 1f);
        }
    }
}
