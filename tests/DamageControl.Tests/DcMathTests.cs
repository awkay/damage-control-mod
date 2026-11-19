using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class DcMathTests
    {
        [Theory]
        [InlineData(-0.5f, 0f)]
        [InlineData(0.25f, 0.25f)]
        [InlineData(3f, 1f)]
        [InlineData(float.NaN, 0f)]
        public void Clamp01KeepsFractionsInRange(float input, float expected)
        {
            Assert.Equal(expected, DcMath.Clamp01(input));
        }

        [Theory]
        [InlineData(5f, 0f, 10f, 5f)]
        [InlineData(-1f, 0f, 10f, 0f)]
        [InlineData(11f, 0f, 10f, 10f)]
        [InlineData(float.NaN, 2f, 10f, 2f)]
        [InlineData(5f, 3f, 1f, 3f)]
        public void ClampFloat(float value, float min, float max, float expected)
        {
            Assert.Equal(expected, DcMath.Clamp(value, min, max));
        }

        [Fact]
        public void ClampDoubleHandlesNaN()
        {
            Assert.Equal(1.0, DcMath.Clamp(double.NaN, 1.0, 2.0));
            Assert.Equal(2.0, DcMath.Clamp(7.0, 1.0, 2.0));
        }

        [Fact]
        public void ApproximatelyUsesEpsilonNotExactEquality()
        {
            Assert.True(DcMath.Approximately(1f, 1f + DcMath.Epsilon / 2f));
            Assert.False(DcMath.Approximately(1f, 1f + DcMath.Epsilon * 2f));
            Assert.False(DcMath.Approximately(float.NaN, float.NaN));
            Assert.True(DcMath.Approximately(0.1 + 0.2, 0.3));
        }

        [Fact]
        public void IsFiniteRejectsNaNAndInfinity()
        {
            Assert.True(DcMath.IsFinite(1f));
            Assert.False(DcMath.IsFinite(float.NaN));
            Assert.False(DcMath.IsFinite(float.PositiveInfinity));
            Assert.False(DcMath.IsFinite(double.NegativeInfinity));
        }
    }
}
