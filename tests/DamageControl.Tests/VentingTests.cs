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

        // ---- integration over long physics steps (WS7: time compression lengthens the fixed step)

        [Fact]
        public void IntegrateEqualsOneStepAtTheReferenceStep()
        {
            VentingInput input = Diesel(0.8, 1f, false, 0f);
            input.Dt = Venting.ReferenceStepSeconds;
            input.DieselActive = true;
            input.DieselFuelUsage = 0.001f;
            Assert.Equal(1, Venting.SubstepCount(input.Dt));
            Assert.Equal(Venting.Step(input).DieselAirDelta, Venting.Integrate(input).DieselAirDelta, 12);
        }

        [Theory]
        [InlineData(0.4333f, 33)]   // 64x: 0.01333 x (1 + 63 / 2)
        [InlineData(0.2167f, 17)]   // 32x
        [InlineData(0f, 1)]
        public void SubstepCountSplitsTheStepIntoReferenceSteps(float dt, int count)
        {
            Assert.Equal(count, Venting.SubstepCount(dt));
        }

        [Fact]
        public void DieselDrawLosesNoTimeOnALongStep()
        {
            // A diesel whose per-step draw hits the 0.1 cap: one 0.4 s step must draw what 30 reference
            // steps draw (3.0), not one capped step (0.1).
            VentingInput input = Diesel(1.0, 20f, false, 0f);
            input.DieselActive = true;
            input.DieselFuelUsage = 1f;
            input.DieselFreeVolume = 1000.0;
            input.Dt = 30 * Venting.ReferenceStepSeconds;
            Assert.Equal(-3.0, Venting.Integrate(input).DieselAirDelta, 4);
            Assert.Equal(-Venting.MaxDieselDrawPerStep, Venting.Step(input).DieselAirDelta, 9);
        }

        [Fact]
        public void DieselDrawStopsAtItsPressureFloorWithinALongStep()
        {
            // 0.4 atm in 10 m3: 0.5 air above the 0.35 floor; a 4 s step must not draw past it by more than one substep.
            VentingInput input = Diesel(0.4, 20f, false, 0f);
            input.DieselActive = true;
            input.DieselFuelUsage = 1f;
            input.DieselFreeVolume = 10.0;
            input.Dt = 4f;
            double delta = Venting.Integrate(input).DieselAirDelta;
            double end = 0.4 + delta / 10.0;
            Assert.True(end <= Venting.DieselMinPressure + 1e-9);
            Assert.True(end > Venting.DieselMinPressure - Venting.MaxDieselDrawPerStep / 10.0 - 1e-9);
            Assert.True(delta < -0.4); // the whole 0.5 above the floor, not one capped step's 0.1
        }

        [Fact]
        public void TowerVentingOverOneSecondDoesNotDependOnTheStepLength()
        {
            // 75 reference steps against one 1 s step: same end pressure (about 0.83 atm). The per-step caps
            // alone would have moved 1.5 units in the long step (0.65 atm).
            VentingInput input = Tower(0.5, true, 1f);
            input.TowerFreeVolume = 10.0;
            double pressure = 0.5;
            VentingInput step = input;
            step.Dt = Venting.ReferenceStepSeconds;
            for (int i = 0; i < 75; i++)
            {
                step.TowerPressure = pressure;
                pressure += Venting.Step(step).TowerAirDelta / 10.0;
            }
            input.Dt = 1f;
            double integrated = 0.5 + Venting.Integrate(input).TowerAirDelta / 10.0;
            Assert.Equal(pressure, integrated, 6);
            Assert.True(integrated > 0.8);
            Assert.True(0.5 + Venting.Step(input).TowerAirDelta / 10.0 < 0.7);
        }

        [Fact]
        public void NaNPressureDoesNothing()
        {
            Assert.Equal(0.0, Venting.EqualizationStep(double.NaN, 0.02, 10.0, 0.0));
        }
    }
}
