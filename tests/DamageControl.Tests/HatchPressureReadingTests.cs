using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchPressureReadingTests
    {
        [Fact]
        public void Criterion01_5_InteriorHatchReportsTheDifferenceEitherWay()
        {
            Assert.Equal(1.8f, HatchPressureReading.Reported(true, 3.0, 1.2, 99f), 5);
            Assert.Equal(1.8f, HatchPressureReading.Reported(true, 1.2, 3.0, 99f), 5);
        }

        [Fact]
        public void Criterion01_5_ExteriorHatchAndOtherPointsKeepTheGamesReading()
        {
            // 00 §4: only interior-hatch points change; others keep the vanilla getter's value.
            Assert.Equal(2.5f, HatchPressureReading.Reported(false, 3.0, 1.2, 2.5f));
        }

        [Fact]
        public void NonFinitePressureKeepsTheGamesReading()
        {
            Assert.Equal(2.5f, HatchPressureReading.Reported(true, double.NaN, 1.0, 2.5f));
            Assert.Equal(2.5f, HatchPressureReading.Reported(true, double.PositiveInfinity, 1.0, 2.5f));
        }
    }
}
