using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class BreachAirFloorTests
    {
        [Fact]
        public void Criterion01_1_OpenBreachRaisesCompartmentAirToTheFloor()
        {
            Assert.Equal(0.1, BreachAirFloor.Apply(true, 0.3f, 0.02), 9);
        }

        [Fact]
        public void Criterion01_1_AirAboveTheFloorIsUnchanged()
        {
            Assert.Equal(0.5, BreachAirFloor.Apply(true, 0.3f, 0.5));
        }

        [Fact]
        public void Criterion01_1_BallastTankIsUnchanged()
        {
            Assert.Equal(0.02, BreachAirFloor.Apply(false, 0.3f, 0.02));
        }

        [Fact]
        public void Criterion01_1_ClosedBreachIsUnchanged()
        {
            Assert.Equal(0.02, BreachAirFloor.Apply(true, 0f, 0.02));
        }

        [Fact]
        public void NaNAirIsRepairedToTheFloorOnAnOpenBreach()
        {
            Assert.Equal(BreachAirFloor.Floor, BreachAirFloor.Apply(true, 1f, double.NaN));
        }
    }
}
