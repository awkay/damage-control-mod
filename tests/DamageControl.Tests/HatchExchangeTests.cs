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
            // Equal air, 1 m of head on the left (D1: Δ = 0.1 atm, i.e. 1 m of head): flow = 1 m x s.
            HatchFlow flow = HatchExchange.Horizontal(Side(1.0, water: 50, we: 101f), Side(1.0), Opening, 1f);
            Assert.Equal(1.0, flow.Water, 5);
            Assert.Equal(JetEffect.Right, flow.Jet);
            // Bounded by the water present.
            flow = HatchExchange.Horizontal(Side(1.0, water: 0.03, we: 101f), Side(1.0), Opening, 1f);
            Assert.Equal(0.03, flow.Water, 6);
            // Mirror: right to left plays the left jet.
            flow = HatchExchange.Horizontal(Side(1.0), Side(1.0, water: 50, we: 101f), Opening, 1f);
            Assert.Equal(-1.0, flow.Water, 5);
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

        // ---- vanilla 2026.1 parity of the open horizontal hatch

        /// <summary>
        /// Vanilla 2026.1's open horizontal hatch, restated for comparison: each side holding water above
        /// the sill offers step x (its level above the sill), at most its water and at most
        /// water x sill / level; the net of the two offers moves, no more than the receiver has room for.
        /// The step is game dt x rate with rate 2, or 2 / sqrt(S) under compression S, capped at 8.
        /// Air pressure plays no part. Positive is left to right.
        /// </summary>
        private static double VanillaWater(SimClock clock, float sillY, HatchSide left, HatchSide right)
        {
            float rate = clock.Compression > 1f ? 2f / (float)System.Math.Sqrt(clock.Compression) : 2f;
            float s = System.Math.Min(8f, clock.GameDt * rate);
            double net = VanillaOffer(left, sillY, s) - VanillaOffer(right, sillY, s);
            return net > 0 ? System.Math.Min(net, right.Room) : System.Math.Max(net, -left.Room);
        }

        private static double VanillaOffer(HatchSide side, float sillY, float s)
        {
            if (side.Water <= 0 || side.WaterElevation <= sillY)
                return 0;
            return System.Math.Min(System.Math.Min(side.Water, s * (side.WaterElevation - sillY)), side.Water * sillY / side.WaterElevation);
        }

        /// <summary>A side whose air matches its pressure (air = p x free volume), so the air step moves pressure realistically.</summary>
        private static HatchSide Room(double pressure, double water, float capacity, float we)
        {
            const double free = 100000.0;
            return new HatchSide(pressure, pressure * free, water, capacity, we, Floor, free);
        }

        /// <summary>
        /// Our whole frame as the hatch patch runs it: the substep plan, each substep re-reading both sides.
        /// Sides are test-local box tanks of <paramref name="floorArea"/> m², so levels move with the water.
        /// Returns the water moved left to right; <paramref name="left"/>/<paramref name="right"/> end in the after-frame state.
        /// </summary>
        private static double OurFrame(SimClock clock, ref HatchSide left, ref HatchSide right, bool open = true, bool leaking = false, float sealFactor = 1f, float floorArea = 200f)
        {
            FrameSubsteps plan = HatchExchange.Substeps(clock, HatchExchange.DefaultRate);
            float step = HatchExchange.GatedStep(plan.Step, open, leaking, sealFactor);
            double moved = 0;
            for (int i = 0; i < plan.Count; i++)
            {
                HatchFlow flow = HatchExchange.Horizontal(left, right, Opening, step);
                left = AddWater(left.WithAirAdded(-flow.Air), -flow.Water, floorArea);
                right = AddWater(right.WithAirAdded(flow.Air), flow.Water, floorArea);
                moved += flow.Water;
            }
            return moved;
        }

        private static double OurFrame(SimClock clock, HatchSide left, HatchSide right, bool open = true, bool leaking = false, float sealFactor = 1f)
        {
            return OurFrame(clock, ref left, ref right, open, leaking, sealFactor);
        }

        private static HatchSide AddWater(HatchSide side, double water, float floorArea)
        {
            return new HatchSide(side.AirPressure, side.Air, side.Water + water, side.Capacity, side.WaterElevation + (float)(water / floorArea), side.FloorElevation, side.FreeVolume);
        }

        public static TheoryData<float, float, float, float, float, float> ParityCases()
        {
            // unity dt, unity time scale, artificial time scale, left level, right level, receiver fill
            var cases = new TheoryData<float, float, float, float, float, float>();
            float[] heads = { 0.1f, 0.5f, 1f, 2f, 3f };
            foreach (float h in heads)
            {
                cases.Add(1f / 60f, 1f, 1f, 100f + h, 99f, 0f);          // dry receiver, 60 fps, 1x
                cases.Add(1f / 30f, 1f, 1f, 100f + h, 99f, 0.3f);        // 30 fps, receiver 30 % full
                cases.Add(1f / 60f, 1f, 4f, 100f + h, 99f, 0.1f);        // 4x
                cases.Add(1f / 30f, 1f, 15f, 100f + h, 99f, 0.2f);       // 15x: a frame is exactly one 0.5 s substep
                cases.Add(1f / 60f, 2f, 8f, 100f + h, 99f, 0f);          // 16x split over both time scales
                cases.Add(1f / 30f, 1f, 32f, 100f + h, 99f, 0.1f);       // 32x at 30 fps: 1.07 game s, 3 substeps
                cases.Add(1f / 60f, 1f, 64f, 100f + h, 99f, 0f);         // 64x at 60 fps: 1.07 game s, 3 substeps
                cases.Add(1f / 60f, 1f, 1f, 102.5f, 102.5f - h, 0.4f);   // both sides above the sill
            }
            cases.Add(1f / 60f, 1f, 1f, 101.5f, 99f, 0.6f);              // 60 % full receiver, choke not yet binding
            return cases;
        }

        [Theory]
        [MemberData(nameof(ParityCases))]
        public void OpenHatchWaterMatchesVanillaAtEqualAirPressure(float dt, float unityScale, float artificialScale, float leftLevel, float rightLevel, float receiverFill)
        {
            var clock = new SimClock(dt, unityScale, artificialScale);
            HatchSide left = Room(1.0, 400, 1000f, leftLevel);
            HatchSide right = Room(1.0, 1000 * receiverFill, 1000f, rightLevel);
            double vanilla = VanillaWater(clock, Opening.LowY, left, right);
            double ours = OurFrame(clock, left, right);
            Assert.True(vanilla > 0);
            Assert.InRange(ours, vanilla * 0.99, vanilla * 1.01);
            // The mirror image flows back at the same rate.
            double mirrored = OurFrame(clock, right, left);
            Assert.InRange(-mirrored, vanilla * 0.99, vanilla * 1.01);
        }

        [Theory]
        [InlineData(0.5f, 0.0)]
        [InlineData(1f, 0.5)]
        [InlineData(2f, 0.8)]
        [InlineData(3f, -0.2)]
        [InlineData(1f, 2.0)]
        public void OpenHatchWaterIsVanillasHeadFlowPlusTheEquivalentHeadOfThePressureDifference(float head, double extraAtmOnLeft)
        {
            // Vanilla ignores air pressure; the spec (02 §2.3g) adds it, at its physical head (1 atm = 10 m).
            var clock = new SimClock(1f / 60f, 1f, 1f);
            float s = HatchExchange.Substeps(clock).Total;
            HatchSide left = Room(1.0 + extraAtmOnLeft, 400, 1000f, 100f + head);
            HatchSide right = Room(1.0, 0, 1000f, 99f);
            double vanilla = VanillaWater(clock, Opening.LowY, left, right);
            double expected = vanilla + extraAtmOnLeft / WaterHead.AtmPerMetreOfWater * s;
            // The spec's inflow choke still bounds it: (1.01 - 0)² x 10 x s into the dry receiver (binds at 2 atm).
            expected = System.Math.Min(expected, 1.01 * 1.01 * 10 * s);
            double ours = OurFrame(clock, left, right);
            Assert.InRange(ours, expected * 0.99, expected * 1.01);
        }

        // ---- substeps (02 §2.3b without losing time)

        [Theory]
        [InlineData(1f / 60f, 1f, 1, 2f / 60f)]          // 1x: one substep, the plain step
        [InlineData(1f / 30f, 15f, 1, 0.5f * 2f / 3.872983f)] // exactly 0.5 game s: still one
        [InlineData(1f / 60f, 64f, 3, 64f / 60f * 0.25f / 3f)] // 1.07 game s: three substeps
        [InlineData(0.1f, 100f, 16, 10f * 0.2f / 16f)]   // 10 game s: capped at 16 substeps, total kept
        public void SubstepsCoverTheWholeFrameInStepsOfAtMostHalfASecond(float dt, float artificialScale, int count, float step)
        {
            FrameSubsteps plan = HatchExchange.Substeps(new SimClock(dt, 1f, artificialScale));
            Assert.Equal(count, plan.Count);
            Assert.Equal(step, plan.Step, 4);
        }

        [Fact]
        public void SubstepTotalIsClampedAtVanillasEightAndPausedFramesHaveNone()
        {
            // 0.3 s frames at 6400x: 1920 game s x 2/80 = 48 -> clamped to 8, in 16 steps of 0.5.
            FrameSubsteps plan = HatchExchange.Substeps(new SimClock(0.3f, 1f, 6400f));
            Assert.Equal(HatchExchange.MaxFrameTotalStep, plan.Total, 4);
            Assert.Equal(HatchExchange.MaxSubsteps, plan.Count);
            Assert.True(plan.Step <= HatchExchange.MaxFrameSeconds * HatchExchange.DefaultRate);
            Assert.Equal(0, HatchExchange.Substeps(new SimClock(0f, 0f, 1f)).Count);
            Assert.Equal(0f, HatchExchange.Substeps(new SimClock(0.02f, 1f, 1f), 0f).Total);
        }

        [Fact]
        public void SubstepsSettleSmallRoomsWithoutOvershootWhereOneVanillaStepWould()
        {
            // 1000x at 10 fps: 100 game s, total step 2/sqrt(1000) x 100 = 6.3. Two 10 m² rooms with 3 m of
            // level difference level out after 15 m³; one vanilla step (6.3 x 3 = 19 m³) would overshoot.
            var clock = new SimClock(0.1f, 1f, 1000f);
            HatchSide left = Room(1.0, 40, 100f, 103f);
            HatchSide right = Room(1.0, 10, 100f, 100f);
            double vanilla = VanillaWater(clock, Opening.LowY, left, right);
            Assert.True(vanilla > 15);
            double ours = OurFrame(clock, ref left, ref right, floorArea: 10f);
            Assert.True(ours > 0 && ours <= 15);
            Assert.True(left.WaterElevation >= right.WaterElevation);
            Assert.True(right.Water <= right.Capacity);
        }

        [Fact]
        public void ClosedLeakingHatchPassesTwoPercentOfTheVanillaParityRate()
        {
            foreach (var clock in new[] { new SimClock(1f / 60f, 1f, 1f), new SimClock(1f / 60f, 1f, 64f) })
            {
                HatchSide left = Room(1.0, 400, 1000f, 102f);
                HatchSide right = Room(1.0, 0, 1000f, 99f);
                double vanilla = VanillaWater(clock, Opening.LowY, left, right);
                double leak = OurFrame(clock, left, right, false, true, HatchSeals.FactorFor(0));
                Assert.InRange(leak, vanilla * 0.02 * 0.99, vanilla * 0.02 * 1.01);
                double sealedLeak = OurFrame(clock, left, right, false, true, HatchSeals.FactorFor(1));
                Assert.InRange(sealedLeak, vanilla * 0.002 * 0.99, vanilla * 0.002 * 1.01);
            }
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
