using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchSideTests
    {
        [Fact]
        public void CreateDerivesFreeVolumeFromAirAndPressure()
        {
            HatchSide side = HatchSide.Create(2.0, 40.0, 10.0, 50f, 99.5f, 99f);
            Assert.Equal(20.0, side.FreeVolume, 6);
            Assert.Equal(0.5f, side.WaterAboveFloor, 5);
            Assert.Equal(0.2, side.Fill, 6);
            Assert.Equal(40.0, side.Room, 6);
        }

        [Fact]
        public void CreateFallsBackToCapacityMinusWaterWithoutAir()
        {
            HatchSide side = HatchSide.Create(0.0, 0.0, 10.0, 50f, 99.5f, 99f);
            Assert.Equal(40.0, side.FreeVolume, 6);
        }

        [Fact]
        public void AddingAirRaisesPressureOverTheFreeVolume()
        {
            HatchSide side = HatchSide.Create(2.0, 40.0, 10.0, 50f, 99.5f, 99f).WithAirAdded(-10.0);
            Assert.Equal(30.0, side.Air, 6);
            Assert.Equal(1.5, side.AirPressure, 6);
            Assert.Equal(0.0, side.WithAirAdded(-100.0).Air);
        }

        [Fact]
        public void FullWhenCapacityIsZero()
        {
            HatchSide side = new HatchSide(1.0, 1.0, 0.0, 0f, 0f, 0f, 0.0);
            Assert.Equal(1.0, side.Fill);
            Assert.Equal(0.0, side.Room);
            // Without a free volume, pressure scales with the air amount.
            Assert.Equal(0.5, side.WithAirAdded(-0.5).AirPressure, 6);
        }
    }
}
