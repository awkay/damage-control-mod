using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchDamageRulesTests
    {
        private sealed class FixedRandom : IRandom
        {
            private readonly float value;

            public FixedRandom(float value)
            {
                this.value = value;
            }

            public float NextFloat()
            {
                return value;
            }
        }

        [Fact]
        public void Criterion02_11_BlowOpenNeverWhenIntactOpenAnimatingOrBeingRepaired()
        {
            // Far side 10 atm against seat 1 atm, design 6: the pressure part alone would trigger.
            Assert.False(HatchDamageRules.CanBlowOpen(1f, false, true, false, 1.0, 10.0, 6f));
            Assert.False(HatchDamageRules.CanBlowOpen(0f, false, false, false, 1.0, 10.0, 6f));
            Assert.False(HatchDamageRules.CanBlowOpen(0f, false, true, true, 1.0, 10.0, 6f));
            Assert.False(HatchDamageRules.CanBlowOpen(0f, true, true, false, 1.0, 10.0, 6f));
            Assert.True(HatchDamageRules.CanBlowOpen(0f, false, true, false, 1.0, 10.0, 6f));
        }

        [Fact]
        public void Criterion02_11_BlowOpenTriggersIffFarExceedsSeatByAThirdOfDesign()
        {
            Assert.True(HatchDamageRules.CanBlowOpen(0f, false, true, false, 1.0, 3.01, 6f));
            Assert.False(HatchDamageRules.CanBlowOpen(0f, false, true, false, 1.0, 3.0, 6f));
            Assert.False(HatchDamageRules.CanBlowOpen(0f, false, true, false, 3.0, 1.0, 6f));
        }

        [Fact]
        public void Criterion02_11_BlowOpenIsAPerSecondHazard()
        {
            // D7: chance over dt is 1 - (1 - p)^dt; the roll is stubbed.
            float p = Probability.PerStep(0.75f, 0.5f);
            Assert.Equal(0.5f, p, 4);
            Assert.True(HatchDamageRules.RollBlowOpen(new FixedRandom(0.49f), 0.75f, 0.5f));
            Assert.False(HatchDamageRules.RollBlowOpen(new FixedRandom(0.51f), 0.75f, 0.5f));
            Assert.False(HatchDamageRules.RollBlowOpen(new FixedRandom(0f), 0.75f, 0f));
        }

        [Fact]
        public void Criterion02_12_DestroyedClosedIdleHatchGetsExactlyOneLeak()
        {
            Assert.Equal(LeakStatusChange.Add, HatchDamageRules.UpkeepLeak(0f, true, false, false));
            Assert.Equal(LeakStatusChange.None, HatchDamageRules.UpkeepLeak(0f, true, false, true));
            Assert.Equal(LeakStatusChange.None, HatchDamageRules.UpkeepLeak(0f, false, false, false));
            Assert.Equal(LeakStatusChange.None, HatchDamageRules.UpkeepLeak(0f, true, true, false));
        }

        [Fact]
        public void Criterion02_12_AnyHealthRemovesTheLeak()
        {
            Assert.Equal(LeakStatusChange.Remove, HatchDamageRules.UpkeepLeak(1f, true, false, true));
            Assert.Equal(LeakStatusChange.Remove, HatchDamageRules.UpkeepLeak(0.01f, false, true, true));
            Assert.Equal(LeakStatusChange.None, HatchDamageRules.UpkeepLeak(1f, true, false, false));
        }

        [Fact]
        public void DisabledHatchStaysShutOnlyWhileTheSeatSidePushesHarder()
        {
            // H3 (02 §3 OnDisable).
            Assert.True(HatchDamageRules.StaysShutWhenDisabled(true, 2.0, 1.0));
            Assert.False(HatchDamageRules.StaysShutWhenDisabled(true, 1.0, 1.0));
            Assert.False(HatchDamageRules.StaysShutWhenDisabled(false, 2.0, 1.0));
        }
    }
}
