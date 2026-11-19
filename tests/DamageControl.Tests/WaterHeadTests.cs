using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class WaterHeadTests
    {
        [Fact]
        public void DecisionD1OneMetreOfWaterIsATenthOfAnAtmosphere()
        {
            Assert.Equal(0.1f, WaterHead.AtmPerMetreOfWater);
            Assert.Equal(0.1f, WaterHead.Atm(1f), 5);
            Assert.Equal(0.9f, WaterHead.Atm(9f), 5);
        }

        [Fact]
        public void NoWaterAboveTheReferenceAddsNothing()
        {
            Assert.Equal(0f, WaterHead.Atm(-0.5f));
            Assert.Equal(0f, WaterHead.Atm(float.NaN));
            Assert.Equal(1.2, WaterHead.SidePressure(1.2, 99f, 100f), 6);
        }

        [Fact]
        public void SidePressureAddsTheHeadAboveTheReference()
        {
            // 02 §2.4 panel line: P + max(0, WE - lowY) / 10.
            Assert.Equal(1.25, WaterHead.SidePressure(1.0, 102.5f, 100f), 5);
        }
    }
}
