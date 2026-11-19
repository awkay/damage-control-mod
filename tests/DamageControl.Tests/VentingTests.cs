using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class VentingTests
    {
        private const double LargeVolume = 1000.0;

        private static VentingInput Tower(double pressure, bool hatchOpen, float deckDepth)
        {
            return new VentingInput
            {
                Dt = 0.02f,
                DeckDepth = deckDepth,
                TowerPressure = pressure,
                TowerFreeVolume = LargeVolume,
                ExteriorHatchOpen = hatchOpen,
                DieselPressure = 1.0,
                DieselFreeVolume = LargeVolume,
            };
        }

        private static VentingInput Diesel(double pressure, float deckDepth, bool snorkel, float performance)
        {
            return new VentingInput
            {
                Dt = 0.02f,
                DeckDepth = deckDepth,
                TowerPressure = 1.0,
                TowerFreeVolume = LargeVolume,
                DieselPressure = pressure,
                DieselFreeVolume = LargeVolume,
                HasSnorkel = snorkel,
                SnorkelPerformance = performance,
            };
        }

        [Fact]
        public void Criterion01_10_OpenExteriorHatchVentsTheTower()
        {
            Assert.Equal(0.04, Venting.Step(Tower(0.8, true, 10f)).TowerAirDelta, 6);
        }

        [Fact]
        public void Criterion01_10_SurfacedTowerRulesStack()
        {
            Assert.Equal(0.044, Venting.Step(Tower(0.8, true, 1f)).TowerAirDelta, 6);
        }

        [Fact]
        public void Criterion01_10_OverPressureVentsDown()
        {
            Assert.Equal(-0.1, Venting.Step(Tower(1.5, true, 10f)).TowerAirDelta, 6);
        }

        [Fact]
        public void Criterion01_10_EachRuleIsClampedToOnePerStep()
        {
            Assert.Equal(1.0, Venting.EqualizationStep(-9.0, 0.02, 10.0, 0.0), 9);
            Assert.Equal(-1.0, Venting.EqualizationStep(11.0, 0.02, 10.0, 0.0), 9);
        }

        [Fact]
        public void ClosedHatchAndDeepTowerDoNotVent()
        {
            Assert.Equal(0.0, Venting.Step(Tower(0.8, false, 10f)).TowerAirDelta);
        }

        [Fact]
        public void Criterion01_11_DieselCompartmentVentingNeedsInductionOrSnorkel()
        {
            Assert.Equal(0.0, Venting.Step(Diesel(0.7, 5f, false, 0f)).DieselAirDelta);
            Assert.Equal(0.06, Venting.Step(Diesel(0.7, 5f, true, 0.5f)).DieselAirDelta, 6);
            Assert.Equal(0.0, Venting.Step(Diesel(0.7, 5f, true, 0.49f)).DieselAirDelta);
            Assert.Equal(0.0, Venting.Step(Diesel(0.7, 8f, true, 1f)).DieselAirDelta);
        }

        [Fact]
        public void Criterion01_12_DieselDrawsAirAboveTheFloorPressure()
        {
            Assert.Equal(0.002, Venting.DieselDraw(true, 0.36, 1e-5f, 0.02), 9);
            Assert.Equal(0.0, Venting.DieselDraw(true, 0.35, 1e-5f, 0.02));
            Assert.Equal(0.0, Venting.DieselDraw(false, 0.36, 1e-5f, 0.02)); // electric engine active
        }

        [Fact]
        public void Criterion01_12_DrawIsSignInsensitiveAndCapped()
        {
            Assert.Equal(0.002, Venting.DieselDraw(true, 0.36, -1e-5f, 0.02), 9);
            Assert.Equal(0.1, Venting.DieselDraw(true, 1.0, 1f, 0.02), 9);
        }

        [Fact]
        public void Criterion01_12_DrawAppearsInTheStepResult()
        {
            VentingInput input = Diesel(0.36, 10f, false, 0f);
            input.DieselActive = true;
            input.DieselFuelUsage = 1e-5f;
            Assert.Equal(-0.002, Venting.Step(input).DieselAirDelta, 9);
        }

        [Fact]
        public void StackedRulesNeverOvershootOneAtmosphere()
        {
            // Free volume 0.05 m3 at 0.8 atm: exactly 0.01 air units bring it to 1 atm.
            VentingInput input = Tower(0.8, true, 1f);
            input.TowerFreeVolume = 0.05;
            Assert.Equal(0.01, Venting.Step(input).TowerAirDelta, 9);
        }

        [Fact]
        public void NaNPressureDoesNothing()
        {
            Assert.Equal(0.0, Venting.EqualizationStep(double.NaN, 0.02, 10.0, 0.0));
        }
    }
}
