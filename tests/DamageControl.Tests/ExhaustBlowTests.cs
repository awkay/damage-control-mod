using System.Linq;
using DamageControl.Core.Ballast;
using DamageControl.Core.Common;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class ExhaustBlowTests
    {
        private const float Tol = 1e-5f;

        // ---- 03 §6 #1: exhaust flow

        [Theory]
        [InlineData(600f, 1f)]
        [InlineData(300f, 0.5f)]
        [InlineData(-450f, 2f)] // astern: the crankshaft still turns
        public void Criterion1_FlowIsRpmOver60TimesConstantTimesDt(float rpm, float dt)
        {
            float expected = System.Math.Abs(rpm) / 60f * 0.1933768f * dt;
            Assert.Equal(expected, ExhaustBlow.Flow(true, rpm, dt), 5);
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(600f)]
        [InlineData(10000f)]
        public void Criterion1_FlowIsZeroWhenDieselDisabledWhateverTheRpm(float rpm)
        {
            Assert.Equal(0f, ExhaustBlow.Flow(false, rpm, 1f));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(-1f)]
        [InlineData(float.NaN)]
        public void FlowIsZeroForNoTimeStep(float dt)
        {
            Assert.Equal(0f, ExhaustBlow.Flow(true, 600f, dt));
        }

        // ---- 03 §6 #2, #3: distribution over the five main ballast tanks (spec positions 1..5 = list 0..4)

        [Fact]
        public void Criterion2_Stage1Gives52Point5And47Point5PercentToTheEndTanks()
        {
            var water = new double[] { 20, 30, 30, 30, 20 };
            var air = new float[5];
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0.525f, air[0], 5);
            Assert.Equal(0.475f, air[4], 5);
            Assert.Equal(0f, air[1]);
            Assert.Equal(0f, air[2]);
            Assert.Equal(0f, air[3]);
        }

        [Fact]
        public void Criterion2_ATankWithNoWaterGetsNothing()
        {
            var water = new double[] { 0, 30, 30, 30, 6 };
            var air = new float[5];
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0f, air[0]);
            Assert.Equal(0.475f, air[4], 5);

            water = new double[] { -0.1, 30, 30, 30, 6 };
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0f, air[0]);
        }

        [Fact]
        public void Criterion3_MiddleTanksGet20_55_25PercentWhenBothEndsHoldUnder5()
        {
            var water = new double[] { 4.9, 10, 10, 10, 1 };
            var air = new float[5];
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0.20f, air[1], 5);
            Assert.Equal(0.55f, air[2], 5);
            Assert.Equal(0.25f, air[3], 5);
            // Stage 2 adds to stage 1 (03 §7.5): the ends still get theirs.
            Assert.Equal(0.525f, air[0], 5);
            Assert.Equal(0.475f, air[4], 5);
            Assert.Equal(2f, air.Sum(), 4);
        }

        [Theory]
        [InlineData(5.0, 1.0)]
        [InlineData(1.0, 5.0)]
        [InlineData(12.0, 12.0)]
        public void Criterion3_MiddleTanksGetNothingUnlessBothEndsAreUnder5(double first, double last)
        {
            var water = new double[] { first, 10, 10, 10, last };
            var air = new float[5];
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0f, air[1]);
            Assert.Equal(0f, air[2]);
            Assert.Equal(0f, air[3]);
        }

        [Fact]
        public void Criterion3_DryMiddleTankGetsNothingInStage2()
        {
            var water = new double[] { 1, 10, 0, 10, 1 };
            var air = new float[5];
            ExhaustBlow.Distribute(1f, water, air);
            Assert.Equal(0f, air[2]);
            Assert.Equal(0.20f, air[1], 5);
        }

        [Fact]
        public void OtherTankCountsUseEndsThenEvenMiddle()
        {
            var air = new float[8];
            ExhaustBlow.Distribute(1f, new double[] { 1, 10, 10, 1 }, air);
            Assert.Equal(new[] { 0.525f, 0.5f, 0.5f, 0.475f }, air.Take(4).ToArray());

            ExhaustBlow.Distribute(1f, new double[] { 3, 10, 3 }, air);
            Assert.Equal(1f, air[1], 5);

            ExhaustBlow.Distribute(1f, new double[] { 3 }, air);
            Assert.Equal(1f, air[0], 5);

            var none = new float[0];
            ExhaustBlow.Distribute(1f, new double[0], none); // no tanks: nothing to do, no throw
        }

        [Fact]
        public void DistributeClearsStaleValuesAndIgnoresNoFlow()
        {
            var air = new float[] { 9, 9, 9, 9, 9 };
            ExhaustBlow.Distribute(0f, new double[] { 10, 10, 10, 10, 10 }, air);
            Assert.All(air, a => Assert.Equal(0f, a));
        }

        // ---- 03 §6 #4: progress

        [Fact]
        public void Criterion4_ProgressIs0Point999TimesEmptiedFraction()
        {
            var capacity = new float[] { 10, 20, 30, 20, 20 };
            var water = new double[] { 5, 10, 15, 10, 10 }; // half of 100
            Assert.Equal(0.999f * 0.5f, ExhaustBlow.Progress(water, capacity), 5);
            Assert.Equal(0f, ExhaustBlow.Progress(new double[] { 10, 20, 30, 20, 20 }, capacity), 5);
        }

        [Fact]
        public void Criterion4_ProgressNeverReachesOne()
        {
            var capacity = new float[] { 10, 20, 30, 20, 20 };
            float progress = ExhaustBlow.Progress(new double[5], capacity);
            Assert.Equal(0.999f, progress, 5);
            Assert.True(progress < 1f);
        }

        [Fact]
        public void ProgressWithNoCapacityIsZero()
        {
            Assert.Equal(0f, ExhaustBlow.Progress(new double[] { 1 }, new float[] { 0 }));
            Assert.Equal(0f, ExhaustBlow.Progress(new double[0], new float[0]));
        }

        // ---- 03 §6 #5 with the §7.3 epsilon

        [Fact]
        public void Criterion5_CompletionFiresAtZeroWater()
        {
            Assert.True(ExhaustBlow.IsComplete(0.0));
        }

        [Fact]
        public void Criterion5_EpsilonCompletionToleratesATrickle()
        {
            Assert.True(ExhaustBlow.IsComplete(ExhaustBlow.CompletionWater));
            Assert.True(ExhaustBlow.IsComplete(0.0001));
            Assert.False(ExhaustBlow.IsComplete(0.5));
            Assert.False(ExhaustBlow.IsComplete(double.NaN));
        }

        [Fact]
        public void TotalWaterIgnoresNegativeReadings()
        {
            Assert.Equal(3.0, ExhaustBlow.TotalWater(new double[] { 1, -2, 2 }), 6);
        }

        // ---- 03 §6 #7 and the 00 §6 WS4 gate: unavailable when docked, when the diesel is off, or the deck is not above water

        private static ExhaustBlowConditions Surfaced()
        {
            return new ExhaustBlowConditions { Docked = false, DieselEnabled = true, DieselRpm = 400f, DeckDepth = -1.2f, BallastWater = 40 };
        }

        [Fact]
        public void Criterion7_AvailableOnTheSurfaceWithTheDieselRunning()
        {
            Assert.Equal(ExhaustBlowBlock.None, ExhaustBlow.Availability(Surfaced(), ExhaustBlowSettings.Default));
        }

        [Fact]
        public void Criterion7_DisabledWhenDocked()
        {
            var c = Surfaced();
            c.Docked = true;
            Assert.Equal(ExhaustBlowBlock.Docked, ExhaustBlow.Availability(c, ExhaustBlowSettings.Default));
        }

        [Fact]
        public void WS4Gate_UnavailableWhenTheDieselIsDisabled()
        {
            var c = Surfaced();
            c.DieselEnabled = false;
            Assert.Equal(ExhaustBlowBlock.DieselNotRunning, ExhaustBlow.Availability(c, ExhaustBlowSettings.Default));
        }

        [Fact]
        public void WS4Gate_UnavailableWhenTheDieselIsStopped()
        {
            var c = Surfaced();
            c.DieselRpm = 0f;
            Assert.Equal(ExhaustBlowBlock.DieselNotRunning, ExhaustBlow.Availability(c, ExhaustBlowSettings.Default));
        }

        [Theory]
        [InlineData(0f)]
        [InlineData(0.3f)]
        [InlineData(12f)]
        public void WS4Gate_UnavailableWhenTheDeckIsNotAboveWater(float deckDepth)
        {
            var c = Surfaced();
            c.DeckDepth = deckDepth;
            Assert.Equal(ExhaustBlowBlock.DeckUnderWater, ExhaustBlow.Availability(c, ExhaustBlowSettings.Default));
        }

        [Fact]
        public void UnavailableWhenTheBallastIsAlreadyDry()
        {
            var c = Surfaced();
            c.BallastWater = 0.01;
            Assert.Equal(ExhaustBlowBlock.BallastEmpty, ExhaustBlow.Availability(c, ExhaustBlowSettings.Default));
        }

        [Fact]
        public void WS4Gate_RunningBlowCancelsWhenTheDieselStops()
        {
            var s = ExhaustBlowSettings.Default;
            var c = Surfaced();
            Assert.True(ExhaustBlow.GatesHold(c, s));
            c.DieselEnabled = false;
            Assert.False(ExhaustBlow.GatesHold(c, s));

            // With no grace the first failing step cancels.
            var watch = new ExhaustBlowGateWatch();
            Assert.True(watch.Update(ExhaustBlow.GatesHold(c, s), 0.02f, 0f));
        }

        [Fact]
        public void WS4Gate_RunningBlowCancelsWhenTheDeckGoesUnderAfterTheGrace()
        {
            var s = ExhaustBlowSettings.Default;
            var c = Surfaced();
            c.DeckDepth = 0.5f;
            var watch = new ExhaustBlowGateWatch();
            Assert.False(watch.Update(ExhaustBlow.GatesHold(c, s), 1f, 3f));
            Assert.False(watch.Update(ExhaustBlow.GatesHold(c, s), 1f, 3f));
            Assert.False(watch.Update(ExhaustBlow.GatesHold(c, s), 1f, 3f));
            Assert.True(watch.Update(ExhaustBlow.GatesHold(c, s), 0.1f, 3f));
        }

        [Fact]
        public void AWaveThatClearsWithinTheGraceDoesNotCancel()
        {
            var watch = new ExhaustBlowGateWatch();
            Assert.False(watch.Update(false, 2.5f, 3f));
            Assert.False(watch.Update(true, 0.02f, 3f));
            Assert.Equal(0f, watch.FailingFor);
            Assert.False(watch.Update(false, 2.5f, 3f));
        }

        [Fact]
        public void DryBallastDoesNotCancelARunningBlow()
        {
            // A dry ballast completes the order instead of cancelling it.
            var c = Surfaced();
            c.BallastWater = 0;
            Assert.True(ExhaustBlow.GatesHold(c, ExhaustBlowSettings.Default));
        }

        // ---- data completeness (04 §8 #10, #11 for this action)

        [Fact]
        public void ActionRowIsOnTheValveStationWithTheValveContexts()
        {
            var row = Assert.Single(RepoData.Rows("Crew/Actions"), r => r["Type"] == "DamageControl.Game.Ballast.ExhaustBlowAction");
            Assert.Equal("Valves", row["Activator Tags"]);
            Assert.Equal(new[] { "NoCharacter", "Normal", "Queue" }, row["Contexts"].Split(',').Select(s => s.Trim()).OrderBy(s => s, System.StringComparer.Ordinal));
        }

        [Fact]
        public void KeywordHasEnglishTextAndWheelIcons()
        {
            const string keyword = "DC Exhaust Blow";
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword)));
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword + " Description")));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword + IconKeys.HighlightedSuffix));
        }

        [Theory]
        [InlineData("DC Warning Diesel Not Running")]
        [InlineData("DC Warning Deck Under Water")]
        [InlineData("DC Warning Ballast Empty")]
        public void EveryWarningHasEnglishText(string key)
        {
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Actions", key)));
        }

        [Fact]
        public void ShippedTunablesMatchTheSpecDefaults()
        {
            var rows = RepoData.Rows("General/Settings/Damage Control").ToDictionary(r => r["key"], r => r["Value"]);
            var d = ExhaustBlowSettings.Default;
            Assert.Equal(d.FlowPerRevolution, float.Parse(rows["Exhaust Blow Flow Per Revolution"], System.Globalization.CultureInfo.InvariantCulture), 6);
            Assert.Equal(d.Stage2WaterThreshold, float.Parse(rows["Exhaust Blow Stage 2 Water Threshold"], System.Globalization.CultureInfo.InvariantCulture));
            Assert.Equal(d.CompletionWater, double.Parse(rows["Exhaust Blow Completion Water"], System.Globalization.CultureInfo.InvariantCulture), 6);
        }
    }
}
