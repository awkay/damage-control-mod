using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchExchangeTests
    {
        private const float Floor = 99f;
        private static readonly HatchGeometry Opening = new HatchGeometry(100f, 102f);

        /// <summary>A compartment with a large free volume, so the equalizing-amount guard never binds.</summary>
        private static HatchSide Side(double pressure = 1.0, double air = 100.0, double water = 0.0, float capacity = 1000f, float we = Floor, double freeVolume = 10000.0)
        {
            return new HatchSide(pressure, air, water, capacity, we, Floor, freeVolume);
        }

        // ---- step (02 §6 #6, 01 §6 #14)

        [Fact]
        public void Criterion02_6_FrameDeltaIsCappedAtHalfASecond()
        {
            Assert.Equal(0.5f * 2f, HatchExchange.FrameStep(SimClock.FromGameSeconds(1.0f)), 5);
        }

        [Fact]
        public void Criterion02_6_RateIsTwoAtOneTimesAndOneAtFourTimes()
        {
            Assert.Equal(0.1f * 2f, HatchExchange.FrameStep(new SimClock(0.1f, 1f, 1f)), 5);
            Assert.Equal(0.1f * 1f, HatchExchange.FrameStep(new SimClock(0.1f, 4f, 1f)), 5);
            // Compression from the game's own time scale damps the same way (C2).
            Assert.Equal(0.4f * 1f, HatchExchange.FrameStep(new SimClock(0.1f, 1f, 4f)), 5);
            Assert.Equal(0f, HatchExchange.FrameStep(new SimClock(0f, 0f, 1f)));
        }

        [Fact]
        public void Criterion01_14_EqualizeRateFactor()
        {
            Assert.Equal(0.004f, HatchExchange.EqualizeStep(new SimClock(0.02f, 1f, 1f), 2f), 6);
            Assert.Equal(0.002f, HatchExchange.EqualizeStep(new SimClock(0.02f, 4f, 1f), 2f), 6);
            Assert.Equal(0.1f, HatchExchange.EqualizeStep(new SimClock(2.0f, 1f, 1f), 2f), 6);
        }

        // ---- flow gate (02 §6 #7)

        [Fact]
        public void Criterion02_7_ClosedLeakingHatchPassesTwoPercentTimesATenthPerSeal()
        {
            Assert.Equal(1f, HatchExchange.GatedStep(1f, true, false, 1f));
            Assert.Equal(0f, HatchExchange.GatedStep(1f, false, false, 1f));
            Assert.Equal(0.02f, HatchExchange.GatedStep(1f, false, true, HatchSeals.FactorFor(0)), 6);
            Assert.Equal(0.002f, HatchExchange.GatedStep(1f, false, true, HatchSeals.FactorFor(1)), 6);
            Assert.Equal(0.0002f, HatchExchange.GatedStep(1f, false, true, HatchSeals.FactorFor(2)), 7);
        }

        [Fact]
        public void Criterion02_7_SealRegistryDrivesTheGate()
        {
            var seals = new HatchSeals();
            var hatch = new object();
            seals.Add(hatch, SealSource.Blocker, new object());
            Assert.Equal(0.002f, HatchExchange.GatedStep(1f, false, true, seals.SealFactor(hatch)), 6);
            seals.Add(hatch, SealSource.Repair, new object());
            Assert.Equal(0.0002f, HatchExchange.GatedStep(1f, false, true, seals.SealFactor(hatch)), 7);
        }

        // ---- horizontal air (02 §6 #8)

        [Fact]
        public void Criterion02_8_AirFlowFollowsPressureDifferenceAndDryOpening()
        {
            // 1.5 m of dry opening: conductance saturates at 10.
            HatchFlow flow = HatchExchange.Horizontal(Side(2.0, we: 100.5f), Side(1.0), Opening, 0.01f);
            Assert.Equal(1.0 * 10 * 0.01, flow.Air, 6);
            // 0.2 m of dry opening: conductance 2.
            flow = HatchExchange.Horizontal(Side(2.0, we: 101.8f), Side(1.0), Opening, 0.01f);
            Assert.Equal(1.0 * 2 * 0.01, flow.Air, 5);
            // Mirror direction.
            flow = HatchExchange.Horizontal(Side(1.0), Side(1.5, we: 100.5f), Opening, 0.01f);
            Assert.Equal(-0.5 * 10 * 0.01, flow.Air, 6);
        }

        [Fact]
        public void Criterion02_8_AirNeverTakesACompartmentBelowPointTwo()
        {
            HatchFlow flow = HatchExchange.Horizontal(Side(5.0, air: 0.25), Side(1.0), Opening, 1f);
            Assert.Equal(0.05, flow.Air, 6);
            flow = HatchExchange.Horizontal(Side(5.0, air: 0.15), Side(1.0), Opening, 1f);
            Assert.Equal(0.0, flow.Air);
        }

        [Fact]
        public void AirDoesNotPassFromASubmergedOpeningOrBelowEpsilon()
        {
            Assert.Equal(0.0, HatchExchange.Horizontal(Side(2.0, we: 102f), Side(1.0), Opening, 0.01f).Air);
            Assert.Equal(0.0, HatchExchange.Horizontal(Side(1.0005), Side(1.0), Opening, 0.01f).Air);
        }

        [Fact]
        public void AirNeverOvershootsEqualPressure()
        {
            // Free volumes 10 and 10, Δp 1 atm: at most 5 units equalize the two sides.
            HatchFlow flow = HatchExchange.Horizontal(Side(2.0, air: 20, freeVolume: 10), Side(1.0, air: 10, freeVolume: 10), Opening, 1f);
            Assert.Equal(5.0, flow.Air, 6);
        }

        // ---- horizontal water (02 §6 #9)

        [Fact]
        public void Criterion02_9_NoWaterFlowsFromASideAtOrBelowTheSill()
        {
            HatchFlow flow = HatchExchange.Horizontal(Side(2.0, water: 50, we: 100f), Side(1.0), Opening, 0.01f);
            Assert.Equal(0.0, flow.Water);
            Assert.Equal(JetEffect.None, flow.Jet);
        }

        [Fact]
        public void Criterion02_9_WaterFlowIsDrivenByHeadAndBoundedBySourceWater()
        {
            // Equal air, 1 m of head on the left (D1: Δ = 0.1 atm): flow = Δ x s.
            HatchFlow flow = HatchExchange.Horizontal(Side(1.0, water: 50, we: 101f), Side(1.0), Opening, 1f);
            Assert.Equal(0.1, flow.Water, 5);
            Assert.Equal(JetEffect.Right, flow.Jet);
            // Bounded by the water present.
            flow = HatchExchange.Horizontal(Side(1.0, water: 0.03, we: 101f), Side(1.0), Opening, 1f);
            Assert.Equal(0.03, flow.Water, 6);
            // Mirror: right to left plays the left jet.
            flow = HatchExchange.Horizontal(Side(1.0), Side(1.0, water: 50, we: 101f), Opening, 1f);
            Assert.Equal(-0.1, flow.Water, 5);
            Assert.Equal(JetEffect.Left, flow.Jet);
        }

        [Fact]
        public void Criterion02_9_InflowIsChokedAsTheReceivingSideFills()
        {
            // Receiving side 95 % full: choke = (1.01 - 0.95)² x 10 x s = 0.036 x s.
            HatchFlow flow = HatchExchange.Horizontal(Side(3.0, air: 0.1, water: 50, we: 101f), Side(1.0, water: 950, capacity: 1000), Opening, 1f);
            Assert.Equal(0.06 * 0.06 * 10, flow.Water, 5);
            // A full receiver takes nothing.
            flow = HatchExchange.Horizontal(Side(3.0, air: 0.1, water: 50, we: 101f), Side(1.0, water: 1000, capacity: 1000), Opening, 1f);
            Assert.Equal(0.0, flow.Water);
        }

        [Fact]
        public void EqualHeadsStopTheJets()
        {
            HatchFlow flow = HatchExchange.Horizontal(Side(1.0, water: 50, we: 101f), Side(1.0, water: 50, we: 101f), Opening, 1f);
            Assert.Equal(0.0, flow.Water);
            Assert.Equal(JetEffect.None, flow.Jet);
        }

        // ---- vertical passage (02 §6 #10)

        [Fact]
        public void Criterion02_10_UpperWaterDrainsDownAtLeastATenthOfTheStep()
        {
            // Lower floor water 0.5 m (below the 1.85 m opening), upper holds water, d2 >= 0.
            HatchSide lower = Side(1.2, water: 5, we: Floor + 0.5f);
            HatchSide upper = Side(1.0, water: 2, we: Floor + 0.2f);
            HatchFlow flow = HatchExchange.Vertical(lower, upper, 1.85f, 0.5f);
            Assert.Equal(-System.Math.Min(2.0, 0.1 * 0.5), flow.Water, 6);
            Assert.Equal(JetEffect.None, flow.Jet);
            // Little water above: drains all of it.
            flow = HatchExchange.Vertical(lower, Side(1.0, water: 0.01, we: Floor + 0.01f), 1.85f, 0.5f);
            Assert.Equal(-0.01, flow.Water, 6);
        }

        [Fact]
        public void VerticalWaterRisesOnlyWhenTheLowerSideFloodsPastTheOpening()
        {
            HatchSide lower = Side(1.5, water: 500, we: Floor + 2f);
            HatchFlow flow = HatchExchange.Vertical(lower, Side(1.0), 1.85f, 0.1f);
            Assert.Equal(System.Math.Min(0.5 * 0.1, 1.005 * 1.005 * 0.1), flow.Water, 6);
            // Lower air stops venting upward once the opening is submerged.
            Assert.Equal(0.0, flow.Air);
        }

        [Fact]
        public void VerticalAirPassesWhileTheOpeningIsDry()
        {
            HatchFlow flow = HatchExchange.Vertical(Side(2.0, we: Floor + 0.85f), Side(1.0), 1.85f, 0.01f);
            Assert.Equal(1.0 * 10 * 0.01, flow.Air, 6);
            flow = HatchExchange.Vertical(Side(1.0, we: Floor + 1.75f), Side(2.0), 1.85f, 0.01f);
            Assert.Equal(-1.0 * 1 * 0.01, flow.Air, 5);
        }

        [Fact]
        public void VerticalOpeningHeightComesFromGeometryWithFallback()
        {
            Assert.Equal(2.1f, HatchExchange.VerticalOpeningHeight(101.6f, 99.5f), 4);
            Assert.Equal(1.85f, HatchExchange.VerticalOpeningHeight(99f, 99.5f));
            Assert.Equal(1.85f, HatchExchange.VerticalOpeningHeight(float.NaN, 99.5f));
        }

        // ---- equalize (01 §6 #15, #16)

        [Fact]
        public void Criterion01_15_EqualizeAirFlowUsesTheVentConductance()
        {
            // Vent (highY) 102, left water at 99: clearance 3 m -> conductance min(50, 750) = 50.
            HatchFlow flow = HatchExchange.Equalize(Side(2.0, air: 10, we: 99f), Side(1.0), Opening, 0.004f);
            Assert.Equal(0.2, flow.Air, 6);
            flow = HatchExchange.Equalize(Side(2.0, air: 0.25, we: 99f), Side(1.0), Opening, 0.004f);
            Assert.Equal(0.05, flow.Air, 6);
            flow = HatchExchange.Equalize(Side(1.0005, air: 10, we: 99f), Side(1.0), Opening, 0.004f);
            Assert.Equal(0.0, flow.Air);
            // Vent submerged: water at 103 with 3 m on the floor.
            var submerged = new HatchSide(2.0, 10, 500, 1000, 103f, 100f, 10000);
            flow = HatchExchange.Equalize(submerged, Side(1.0), Opening, 0.004f);
            Assert.Equal(0.0, flow.Air);
        }

        [Fact]
        public void EqualizeAirAlsoPassesOverADryFloorAboveTheVent()
        {
            var dryButHigh = new HatchSide(2.0, 10, 0, 1000, 103f, 103f, 10000);
            HatchFlow flow = HatchExchange.Equalize(dryButHigh, Side(1.0), Opening, 0.004f);
            Assert.Equal(1.0 * 0.01 * 250 * 0.004, flow.Air, 6);
        }

        [Fact]
        public void Criterion01_16_EqualizeWaterFlowUsesTheValveHeightInPhysicalUnits()
        {
            // Valve (lowY) 100; left water 1.5 m above it, right below. D1: Δ = 0.15 atm -> 0.15 x 0.004.
            HatchFlow flow = HatchExchange.Equalize(Side(1.0, water: 50, we: 101.5f), Side(1.0, we: 99f), Opening, 0.004f);
            Assert.Equal(0.0006, flow.Water, 7);
            Assert.Equal(JetEffect.None, flow.Jet);
            flow = HatchExchange.Equalize(Side(1.0, water: 50, we: 99.5f), Side(1.0, we: 99f), Opening, 0.004f);
            Assert.Equal(0.0, flow.Water);
        }

        [Fact]
        public void EqualizeWaterUsesPressuresAfterTheAirStep()
        {
            // Before the air step Δ = 0.0031 would push water; air moves first (free volumes 1 and 1, so
            // 0.0015 units equalize the air), leaving Δ = 0.0001 atm of head, below the 0.001 threshold.
            HatchSide left = new HatchSide(1.003, 1.003, 1, 1000, 100.001f, Floor, 1.0);
            HatchSide right = new HatchSide(1.0, 1.0, 0, 1000, 99f, Floor, 1.0);
            HatchFlow flow = HatchExchange.Equalize(left, right, Opening, 1f);
            Assert.Equal(0.0015, flow.Air, 6);
            Assert.Equal(0.0, flow.Water);
        }

        [Fact]
        public void NoStepNoFlow()
        {
            HatchFlow flow = HatchExchange.Horizontal(Side(3.0, water: 50, we: 101f), Side(1.0), Opening, 0f);
            Assert.Equal(0.0, flow.Air);
            Assert.Equal(0.0, flow.Water);
            Assert.Equal(0.0, HatchExchange.Vertical(Side(3.0), Side(1.0, water: 5), 1.85f, 0f).Water);
        }
    }
}
