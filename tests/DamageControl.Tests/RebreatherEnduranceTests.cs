using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class RebreatherEnduranceTests
    {
        [Fact]
        public void Criterion01_4_ConsumptionIsExactlyPointFourOfVanilla()
        {
            Assert.Equal(0.4f, RebreatherEndurance.RelativeConsumption, 6);
        }

        [Fact]
        public void Criterion01_4_DurabilityUsedPerSecondIsPointFourOfVanilla()
        {
            const float baseDurability = 600f;
            const float wear = 120f;
            float vanillaUsed = 1f - RebreatherEndurance.Remaining(wear, baseDurability, false);
            float moddedUsed = 1f - RebreatherEndurance.Remaining(wear, baseDurability, true);
            Assert.Equal(0.4f, moddedUsed / vanillaUsed, 5);
        }

        [Fact]
        public void EnduranceIsTwoAndAHalfTimesVanilla()
        {
            const float baseDurability = 600f;
            Assert.Equal(0f, RebreatherEndurance.Remaining(baseDurability, baseDurability, false));
            Assert.True(RebreatherEndurance.Remaining(baseDurability * 2.49f, baseDurability, true) > 0f);
            Assert.Equal(0f, RebreatherEndurance.Remaining(baseDurability * 2.5f, baseDurability, true), 5);
        }

        [Fact]
        public void ZeroDurabilityReportsEmpty()
        {
            Assert.Equal(0f, RebreatherEndurance.Remaining(1f, 0f, true));
        }
    }
}
