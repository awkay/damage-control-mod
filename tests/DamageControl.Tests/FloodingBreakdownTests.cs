using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class FloodingBreakdownTests
    {
        [Fact]
        public void Criterion01_22_EngineRoomHighlightStartsAtFive()
        {
            Assert.False(FloodingBreakdown.IsAlarming(4.9, false));
            Assert.True(FloodingBreakdown.IsAlarming(5.0, false));
        }

        [Fact]
        public void Criterion01_22_GalleyHighlightStartsAtTwo()
        {
            Assert.True(FloodingBreakdown.IsAlarming(2.0, true));
            Assert.False(FloodingBreakdown.IsAlarming(1.9, true));
        }

        [Fact]
        public void ThresholdComparisonToleratesFloatNoise()
        {
            Assert.True(FloodingBreakdown.IsAlarming(4.99999, false));
        }

        [Fact]
        public void PercentOfCapacityHasAZeroGuard()
        {
            Assert.Equal(25f, FloodingBreakdown.Percent(5.0, 20.0), 5);
            Assert.Equal(0f, FloodingBreakdown.Percent(5.0, 0.0));
            Assert.Equal(0f, FloodingBreakdown.Percent(double.NaN, 10.0));
        }
    }
}
