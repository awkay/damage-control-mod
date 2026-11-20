using DamageControl.Core.Repair;
using Xunit;

namespace DamageControl.Tests
{
    public class SalvageRatioTests
    {
        // ---- 04 §8 #7: salvage ratio (default is the game's 2 scrap : 1 part; tests below use an explicit 4 to exercise a tuned ratio)

        [Theory]
        [InlineData(0f, false)]
        [InlineData(1f, false)]
        [InlineData(1.99f, false)]
        [InlineData(2f, true)]
        [InlineData(9f, true)]
        public void Criterion7_DisabledBelowTwoScrapAtTheDefault(float scrap, bool enabled)
        {
            Assert.Equal(enabled, SalvageRatio.HasEnoughScrap(scrap, SalvageRatio.Effective(true, SalvageRatio.DefaultScrapPerPart)));
        }

        [Fact]
        public void Criterion7_EachCycleRemovesFourScrapAndAddsOnePart()
        {
            float scrap = 10f, parts = 0f;
            Assert.True(SalvageRatio.Cycle(ref scrap, ref parts, true, 4f));
            Assert.Equal(6f, scrap);
            Assert.Equal(1f, parts);
        }

        [Fact]
        public void Criterion7_StopsWhenFewerThanFourRemain()
        {
            float scrap = 9f, parts = 0f;
            Assert.True(SalvageRatio.Cycle(ref scrap, ref parts, true, 4f));
            Assert.False(SalvageRatio.Cycle(ref scrap, ref parts, true, 4f));
            Assert.Equal(1f, scrap);
            Assert.Equal(2f, parts);
        }

        [Fact]
        public void Criterion7_StopsWithoutConsumingWhenStorageIsFull()
        {
            float scrap = 12f, parts = 3f;
            Assert.False(SalvageRatio.Cycle(ref scrap, ref parts, false, 4f));
            Assert.Equal(12f, scrap);
            Assert.Equal(3f, parts);
        }

        [Fact]
        public void Criterion7_OneCycleIsHundredSecondsAtDefaultTimeScale()
        {
            Assert.Equal(100f, SalvageRatio.SecondsPerPart);
        }

        [Theory]
        [InlineData(4f, 4f)]
        [InlineData(6f, 6f)]
        [InlineData(1f, 1f)]
        [InlineData(0.5f, 2f)]
        [InlineData(-2f, 2f)]
        [InlineData(float.NaN, 2f)]
        [InlineData(float.PositiveInfinity, 2f)]
        public void ConfiguredRatioIsSanitized(float configured, float expected)
        {
            Assert.Equal(expected, SalvageRatio.Sanitize(configured));
        }

        // ---- 00 §6 WS5: the salvage transpiler logs and no-ops if the IL shape changes

        [Fact]
        public void TranspilerSwapAppliesOnlyWhenEveryMethodMatchesItsExpectedCount()
        {
            var state = new SalvagePatchState();
            Assert.True(state.Record("GetState", 1));
            Assert.True(state.Record("GetActionProfit", 1));
            Assert.False(state.AllApplied);
            Assert.True(state.Record("UpdateAction", 2));
            Assert.True(state.AllApplied);
            Assert.Equal(4f, SalvageRatio.Effective(state.AllApplied, 4f));
        }

        [Theory]
        [InlineData("GetState", 0)]
        [InlineData("GetState", 2)]
        [InlineData("UpdateAction", 1)]
        [InlineData("UpdateAction", 3)]
        [InlineData("GetActionProfit", 0)]
        public void TranspilerLeavesAMethodWithAnUnexpectedCountUnpatchedAndTheRatioVanilla(string method, int found)
        {
            var state = new SalvagePatchState();
            foreach (var m in SalvagePatchState.Methods)
                state.Record(m, SalvagePatchState.ExpectedMatches(m));
            Assert.False(state.Record(method, found));
            Assert.False(state.AllApplied);
            Assert.Equal(2f, SalvageRatio.Effective(state.AllApplied, 4f));
        }

        [Fact]
        public void UnknownMethodsAreNeverPatched()
        {
            var state = new SalvagePatchState();
            Assert.False(state.Record("StartMainCharacter", 1));
            Assert.Equal(-1, SalvagePatchState.ExpectedMatches("StartMainCharacter"));
        }

        [Fact]
        public void ExpectedCountsMatchThe2026_1Methods()
        {
            Assert.Equal(1, SalvagePatchState.ExpectedMatches("GetState"));
            Assert.Equal(1, SalvagePatchState.ExpectedMatches("GetActionProfit"));
            Assert.Equal(2, SalvagePatchState.ExpectedMatches("UpdateAction"));
        }
    }
}
