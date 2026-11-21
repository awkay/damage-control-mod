using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using DamageControl.Core.Diagnostics;
using Xunit;

namespace DamageControl.Tests
{
    public class DiagReportTests
    {
        private static CompartmentSample Dry(string name)
        {
            return new CompartmentSample { Name = name, AirPressure = 1.0, Air = 50.0, Water = 0.0, Capacity = 50.0, WaterRate = 0.0, AirToxicity = 0f, Breathable = true };
        }

        private static BoatSample Boat()
        {
            return new BoatSample { GameTime = "1940-05-01 12:00:00", ElapsedGameSeconds = 160.0, Compression = 16f, Depth = 42.3f, DeckDepth = 35.1f, TrimPumpOn = true, TrimPumpWaterLeft = true, LeaksAboard = 1 };
        }

        private static LeakSample Pipe()
        {
            var leak = new LeakSample
            {
                Name = "Pipe 3",
                Where = "Bow room",
                Intensity = 0.0016f,
                BaseIntensity = 0.01f,
                Openess = 0.00056f,
                HoleSize = 0.05f,
                SeaPressure = 5.0,
                HoleUnderWater = true,
                RoomPressure = 1.5,
            };
            leak.Modifiers.Add(new ModifierSample("BlockLeak", 0.2f));
            leak.Modifiers.Add(new ModifierSample("DC Repair Throttle", 0.8f));
            leak.Workers.Add(new WorkerSample { Name = "Weber", Job = "block leak", Interacting = true, Progress = float.NaN });
            leak.Workers.Add(new WorkerSample { Name = "Kurz", Job = "repair", Interacting = false, Progress = 0.2f });
            return leak;
        }

        private static HatchSample FlowingHatch()
        {
            var h = new HatchSample { Name = "Hatch 2", Left = "Bow room", Right = "Control room", Open = false, Wrecked = true, Leaking = true, Seals = 1, DeltaP = 0.17, WindowSeconds = 10.0 };
            h.Flow = new HatchFlowWindow { LastAir = 0.0, LastWater = 0.0001, LastSeconds = 0.1, SumAir = 0.0, SumWater = 0.012 };
            return h;
        }

        // ---- selection

        [Fact]
        public void DryCompartmentAtOneAtmIsNotListed()
        {
            Assert.False(DiagReport.IsCompartmentNotable(Dry("A")));
            var almost = Dry("A");
            almost.Water = 0.01;
            almost.AirPressure = 1.019;
            Assert.False(DiagReport.IsCompartmentNotable(almost));
        }

        [Fact]
        public void WaterPressureLeakOrExchangeListsACompartment()
        {
            var wet = Dry("A"); wet.Water = 0.011;
            var high = Dry("B"); high.AirPressure = 1.021;
            var low = Dry("C"); low.AirPressure = 0.97;
            var leak = Dry("D"); leak.HasLeak = true;
            var exchange = Dry("E"); exchange.HatchExchange = true;
            Assert.All(new[] { wet, high, low, leak, exchange }, c => Assert.True(DiagReport.IsCompartmentNotable(c), c.Name));
        }

        [Fact]
        public void HatchIsListedForLastFrameOrWindowFlow()
        {
            var still = new HatchSample { WindowSeconds = 10.0 };
            still.Flow = new HatchFlowWindow { LastSeconds = 0.1 };
            Assert.False(DiagReport.IsHatchNotable(still));

            var lastFrame = new HatchSample { WindowSeconds = 10.0 };
            lastFrame.Flow = new HatchFlowWindow { LastWater = 0.00002, LastSeconds = 0.1 };   // 2e-4 m3/s
            Assert.True(DiagReport.IsHatchNotable(lastFrame));

            var window = new HatchSample { WindowSeconds = 10.0 };
            window.Flow = new HatchFlowWindow { SumAir = -0.02 };                              // 2e-3 /s
            Assert.True(DiagReport.IsHatchNotable(window));

            var noWindow = new HatchSample { WindowSeconds = double.NaN };
            noWindow.Flow = new HatchFlowWindow { SumWater = 5.0 };
            Assert.False(DiagReport.IsHatchNotable(noWindow));
        }

        [Fact]
        public void QuietBoatGivesNoReport()
        {
            var rooms = new List<CompartmentSample> { Dry("A"), Dry("B") };
            Assert.True(DiagReport.IsQuiet(rooms, new List<LeakSample>(), new List<HatchSample>()));
            Assert.Empty(DiagReport.Build(Boat(), rooms, new List<LeakSample>(), new List<HatchSample>(), DiagReport.MaxLines));
            Assert.Equal("[DC diag] all compartments dry at 1.00 atm", DiagReport.AllDryLine());
        }

        [Fact]
        public void AnyLeakMakesTheBoatNotQuiet()
        {
            Assert.False(DiagReport.IsQuiet(new[] { Dry("A") }, new[] { Pipe() }, null));
        }

        // ---- leak arithmetic

        [Fact]
        public void LeakDriveAddsHalfAnAtmosphereUnderWater()
        {
            Assert.Equal(4.0, DiagReport.LeakDrive(1.5, 5.0, true), 12);
            Assert.Equal(3.5, DiagReport.LeakDrive(1.5, 5.0, false), 12);
            Assert.Equal(-0.5, DiagReport.LeakDrive(1.5, 1.0, false), 12);
        }

        [Fact]
        public void NominalInflowIsDriveTimesHoleTimesOpeness()
        {
            Assert.Equal(4.0 * 0.05 * 0.00056, DiagReport.NominalInflow(Pipe()), 9);
            Assert.True(double.IsNaN(DiagReport.NominalInflow(new LeakSample { IsHatch = true })));
            Assert.True(double.IsNaN(DiagReport.NominalInflow(null)));
        }

        [Fact]
        public void DifficultyFactorIsRecoveredFromOpeness()
        {
            Assert.Equal(1.0, DiagReport.DifficultyFactor(0.00056f, 0.0016f), 4);
            Assert.Equal(2.0, DiagReport.DifficultyFactor(0.35f * 0.01f * 2f, 0.01f), 4);
            Assert.True(double.IsNaN(DiagReport.DifficultyFactor(0.1f, 0f)));
        }

        [Fact]
        public void ClosedHatchLeakIsTwoPercentTimesATenthPerSeal()
        {
            Assert.Equal(0.02, DiagReport.ClosedHatchLeakMultiplier(0), 9);
            Assert.Equal(0.002, DiagReport.ClosedHatchLeakMultiplier(1), 9);
            Assert.Equal(0.0002, DiagReport.ClosedHatchLeakMultiplier(2), 9);
            Assert.Equal(0.02, DiagReport.ClosedHatchLeakMultiplier(-1), 9);
        }

        [Theory]
        [InlineData(true, false, false, DiagReport.Openable)]
        [InlineData(true, true, true, DiagReport.Openable)]
        [InlineData(false, true, true, DiagReport.HeldForRepair)]
        [InlineData(false, false, true, DiagReport.PressureLocked)]
        [InlineData(false, false, false, DiagReport.VanillaLocked)]
        public void LockStateNamesTheReason(bool canBeOpened, bool repairHeld, bool blockingOn, string expected)
        {
            Assert.Equal(expected, DiagReport.LockState(canBeOpened, repairHeld, blockingOn));
        }

        [Fact]
        public void OpenHatchHasNoLockReason()
        {
            Assert.Equal(DiagReport.HatchOpen, DiagReport.HatchLockState(false, false, true, true));
            Assert.Equal(DiagReport.PressureLocked, DiagReport.HatchLockState(true, false, false, true));
        }

        [Theory]
        [InlineData(DiagReport.Openable, DiagReport.PressureLocked, true)]
        [InlineData(DiagReport.PressureLocked, DiagReport.Openable, true)]
        [InlineData(DiagReport.HatchOpen, DiagReport.HeldForRepair, true)]
        [InlineData(DiagReport.VanillaLocked, DiagReport.HatchOpen, true)]
        [InlineData(DiagReport.HatchOpen, DiagReport.Openable, false)]
        [InlineData(DiagReport.Openable, DiagReport.HatchOpen, false)]
        [InlineData(DiagReport.PressureLocked, DiagReport.PressureLocked, false)]
        [InlineData(null, DiagReport.PressureLocked, false)]
        [InlineData(DiagReport.PressureLocked, null, false)]
        public void OnlyLockChangesThatHoldAHatchAreLogged(string before, string after, bool expected)
        {
            Assert.Equal(expected, DiagReport.IsLockChangeWorthLogging(before, after));
        }

        // ---- lines

        [Fact]
        public void HeaderNamesTimeCompressionDepthAndState()
        {
            Assert.Equal("[DC diag] report at 1940-05-01 12:00:00, 160.0 game s since the last, compression x16, depth 42.3 m, deck depth 35.1 m, submerged",
                DiagReport.Header(Boat()));
            var first = new BoatSample { GameTime = "t", Compression = 1f, Depth = 0.2f, DeckDepth = 1f };
            Assert.Equal("[DC diag] report at t, first report, compression x1, depth 0.2 m, deck depth 1.0 m, surfaced", DiagReport.Header(first));
        }

        [Fact]
        public void CompartmentLineShowsPressureAirWaterRateAndAir()
        {
            var c = new CompartmentSample { Name = "Bow room", AirPressure = 1.184, Air = 98.413, Water = 6.204, Capacity = 120.0, WaterRate = 0.02134, AirToxicity = 0.25f, Breathable = false, HasLeak = true, HatchExchange = true };
            Assert.Equal("[DC diag] room \"Bow room\": p 1.18 atm, air 98.41, water 6.204 m3 (5.2 % of 120.0), dW +0.02134 m3/s, toxicity 0.25 NOT breathable, leak, hatch exchange",
                DiagReport.CompartmentLine(c));
        }

        [Fact]
        public void CompartmentLineWithUnknownRateAndMergedWater()
        {
            var c = new CompartmentSample { Name = "Upper", AirPressure = 1.0, Air = 10.0, Water = 0.5, Capacity = 0.0, AirToxicity = 0f, MergedInto = "Lower" };
            Assert.Equal("[DC diag] room \"Upper\": p 1.00 atm, air 10.00, water 0.500 m3, dW n/a m3/s, toxicity 0.00 breathable, water shared with \"Lower\"",
                DiagReport.CompartmentLine(c));
        }

        [Fact]
        public void PipeLeakLineItemisesEveryMultiplierAndWorker()
        {
            Assert.Equal("[DC diag] leak \"Pipe 3\" in \"Bow room\": intensity 0.00160 = base 0.01000 x BlockLeak 0.20 x DC Repair Throttle 0.80; "
                + "openess 0.00056 (0.35 x intensity x difficulty 1.00), hole 0.0500, sea 5.00 atm under water, drive +4.00 atm, inflow +0.00011 m3/s; "
                + "on it: Weber (block leak, working), Kurz (repair 20 %, on the way)",
                DiagReport.LeakLine(Pipe()));
        }

        [Fact]
        public void ClosedHatchLeakLineShowsSealArithmetic()
        {
            var leak = new LeakSample { Name = "Hatch 2", Where = "\"Bow room\" | \"Control room\"", IsHatch = true, Blockers = 1, Repairs = 1, HatchWaterRate = -0.00012 };
            Assert.Equal("[DC diag] leak hatch \"Hatch 2\" (\"Bow room\" | \"Control room\"): closed-leak 0.02 x seals 0.01 (2: 1 blocking, 1 repairing) = 0.0002 of the open rate, water through it -0.00012 m3/s (L->R); nobody on it",
                DiagReport.LeakLine(leak));
            leak.HatchOpen = true;
            Assert.Contains("open, full exchange", DiagReport.LeakLine(leak));
        }

        [Fact]
        public void HatchLineShowsStateDeltaPAndBothRates()
        {
            Assert.Equal("[DC diag] hatch \"Hatch 2\" (\"Bow room\" | \"Control room\"): closed, wrecked, leaking, 1 seal(s), dp(L-R) +0.17 atm, water L->R +0.00120 m3/s (last frame +0.00100), air L->R 0.0000/s (last frame 0.0000)",
                DiagReport.HatchLine(FlowingHatch()));
        }

        [Fact]
        public void TotalsCountMergedWaterOnceAndOnlyInflowingLeaks()
        {
            var a = Dry("A"); a.Water = 2.0; a.WaterRate = 0.01;
            var b = Dry("B"); b.Water = 1.0; b.WaterRate = 0.005;
            var merged = Dry("C"); merged.Water = 1.0; merged.WaterRate = 0.005; merged.MergedInto = "B";
            var outflow = Pipe(); outflow.SeaPressure = 0.5; outflow.HoleUnderWater = false;
            var boat = Boat();
            boat.CompressorAir = 0.5; boat.CompressorRoom = "Engine room";
            boat.DieselAir = -0.16; boat.DieselRoom = "Diesel room";
            string line = DiagReport.TotalsLine(boat, new[] { a, b, merged }, new[] { Pipe(), outflow, new LeakSample { IsHatch = true } });
            Assert.Equal("[DC diag] totals: water aboard 3.000 m3 (dW +0.01500 m3/s), sea inflow 0.00011 m3/s from 2 leak(s), 1 leaking hatch(es); "
                + "trim pump on (water left), auto-off hold on (1 leaking); compressor draw \"Engine room\" 0.0031/s; diesel room air \"Diesel room\" -0.0010/s",
                line);
        }

        [Fact]
        public void TotalsWithoutPumpOrRates()
        {
            var a = Dry("A"); a.WaterRate = double.NaN;
            var boat = new BoatSample { LeaksAboard = 0, ElapsedGameSeconds = double.NaN, TowerAir = 0.2, TowerRoom = "Tower" };
            Assert.Equal("[DC diag] totals: water aboard 0.000 m3 (dW n/a m3/s), sea inflow 0.00000 m3/s from 0 leak(s), 0 leaking hatch(es); trim pump none, auto-off hold off; tower venting \"Tower\" +0.2000 so far",
                DiagReport.TotalsLine(boat, new[] { a }, null));
        }

        [Fact]
        public void BuildOrdersHeaderRoomsLeaksHatchesTotals()
        {
            var wet = Dry("Bow room"); wet.Water = 3.0; wet.HasLeak = true;
            var lines = DiagReport.Build(Boat(), new[] { Dry("Dry room"), wet }, new[] { Pipe() }, new[] { FlowingHatch(), new HatchSample { Name = "Still", WindowSeconds = 10.0 } }, DiagReport.MaxLines);
            Assert.Equal(5, lines.Count);
            Assert.StartsWith("[DC diag] report at", lines[0]);
            Assert.StartsWith("[DC diag] room \"Bow room\"", lines[1]);
            Assert.StartsWith("[DC diag] leak \"Pipe 3\"", lines[2]);
            Assert.StartsWith("[DC diag] hatch \"Hatch 2\"", lines[3]);
            Assert.StartsWith("[DC diag] totals:", lines[4]);
            Assert.All(lines, l => Assert.StartsWith(DiagReport.Prefix, l));
        }

        [Fact]
        public void BuildCapsTheReportAndKeepsTheTotals()
        {
            var rooms = Enumerable.Range(0, 60).Select(i => { var c = Dry("R" + i); c.Water = 1.0; return c; }).ToList();
            var lines = DiagReport.Build(Boat(), rooms, null, null, DiagReport.MaxLines);
            Assert.Equal(DiagReport.MaxLines, lines.Count);
            Assert.StartsWith("[DC diag] report at", lines[0]);
            Assert.Equal("[DC diag] ... 23 more line(s) not shown (report capped at 40 lines)", lines[38]);
            Assert.StartsWith("[DC diag] totals: water aboard 60.000 m3", lines[39]);
        }

        [Fact]
        public void BuildExactlyAtTheCapIsNotTruncated()
        {
            var rooms = Enumerable.Range(0, 38).Select(i => { var c = Dry("R" + i); c.Water = 1.0; return c; }).ToList();
            var lines = DiagReport.Build(Boat(), rooms, null, null, DiagReport.MaxLines);
            Assert.Equal(40, lines.Count);
            Assert.DoesNotContain(lines, l => l.Contains("not shown"));
        }

        [Fact]
        public void EventLines()
        {
            Assert.Equal("[DC diag] event: leak started on \"Pipe 3\" in \"Bow room\"", DiagReport.LeakStartedLine("Pipe 3", "Bow room"));
            Assert.Equal("[DC diag] event: leak stopped on \"Pipe 3\"", DiagReport.LeakStoppedLine("Pipe 3"));
            Assert.Equal("[DC diag] event: hatch \"H\" is wrecked (health 0)", DiagReport.HatchWreckedLine("H", true));
            Assert.Equal("[DC diag] event: hatch \"H\" is no longer wrecked", DiagReport.HatchWreckedLine("H", false));
            Assert.Equal("[DC diag] event: hatch \"H\" blown open, seat side 1.00 atm, far side 1.45 atm", DiagReport.HatchBlownOpenLine("H", 1.0, 1.449));
            Assert.Equal("[DC diag] event: hatch \"H\" can be opened -> held shut by the pressure lock, dp(L-R) -0.95 atm",
                DiagReport.HatchLockLine("H", DiagReport.Openable, DiagReport.PressureLocked, -0.95));
        }

        [Fact]
        public void NumbersIgnoreTheSystemCulture()
        {
            CultureInfo saved = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                Assert.Equal("1.50", DiagReport.Num(1.5, "0.00"));
                Assert.Equal("+0.25", DiagReport.Signed(0.25, "0.00"));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = saved;
            }
        }

        [Fact]
        public void SignedOmitsThePlusOnValuesThatRoundToZero()
        {
            Assert.Equal("0.00", DiagReport.Signed(0.001, "0.00"));
            Assert.Equal("-0.50", DiagReport.Signed(-0.5, "0.00"));
            Assert.Equal("n/a", DiagReport.Signed(double.NaN, "0.00"));
            Assert.Equal("n/a", DiagReport.Num(double.PositiveInfinity, "0.00"));
        }
    }
}
