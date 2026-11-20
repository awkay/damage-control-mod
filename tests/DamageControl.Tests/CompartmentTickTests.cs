using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class CompartmentTickTests
    {
        [Theory]
        [InlineData(3.0, true, 0f)]        // p 3, submerged
        [InlineData(5.0, false, 0f)]       // p 5, not submerged
        [InlineData(5.0, true, 0.0025f)]   // p 5, submerged: 0.1 x clamp(0.025)
        [InlineData(20.0, false, 0.04f)]   // p 20, not submerged: 0.1 x 0.4
        [InlineData(50.0, false, 0.1f)]    // capped
        public void Criterion01_6_BulbDamageChance(double pressure, bool submerged, float expected)
        {
            Assert.Equal(expected, CompartmentTick.BulbDamageChance(submerged, pressure, 1f), 6);
        }

        [Fact]
        public void Criterion01_6_BrokenBulbIsNeverDamaged()
        {
            Assert.Equal(0f, CompartmentTick.BulbDamageChance(true, 50.0, 0f));
        }

        [Fact]
        public void Criterion01_6_SpecExamples()
        {
            Assert.Equal(0.01f, CompartmentTick.BulbChanceAtPressure(8.0), 6);
            Assert.Equal(0.03f, CompartmentTick.BulbChanceAtPressure(16.0), 6);
            Assert.Equal(0.09f, CompartmentTick.BulbChanceAtPressure(40.0), 6);
        }

        [Fact]
        public void Criterion01_7_NonHatchIsStressedOnlyOnceWaterPassesThePoint()
        {
            Assert.False(CompartmentTick.IsOverstressed(false, 0.5f, 0.8f, 10f, 5f));
            Assert.True(CompartmentTick.IsOverstressed(false, 0.9f, 0.8f, 10f, 5f));
        }

        [Fact]
        public void Criterion01_7_HatchIsStressedInADryCompartment()
        {
            Assert.True(CompartmentTick.IsOverstressed(true, 0f, 0.8f, 2f, 1f));
            Assert.False(CompartmentTick.IsOverstressed(true, 0f, 0.8f, 1f, 1f));
        }

        [Fact]
        public void Criterion01_7_DamageChanceFollowsDurability()
        {
            Assert.Equal(0.01f, CompartmentTick.OverstressDamageChance(0.8f), 6);
            Assert.Equal(0f, CompartmentTick.OverstressDamageChance(1f));
            Assert.Equal(0.05f, CompartmentTick.OverstressDamageChance(-3f), 6);
        }

        [Fact]
        public void ChancesArePerSecondAndTheTickIsOneSecond()
        {
            // D7: at the 1 s tick the per-step chance equals the per-second chance.
            Assert.Equal(1f, CompartmentTick.TickSeconds);
            Assert.Equal(0.04f, Probability.PerStep(0.04f, CompartmentTick.TickSeconds), 6);
        }

        [Fact]
        public void Criterion01_8_BeingDamagedStatusIsIdempotent()
        {
            var latch = new PressureDamageLatch(false);
            Assert.Equal(StatusChange.Attach, latch.Set(true));
            Assert.Equal(StatusChange.None, latch.Set(true));   // true twice: one status
            Assert.Equal(StatusChange.Detach, latch.Set(false));
            Assert.Equal(StatusChange.None, latch.Set(false));  // false again: no-op
            Assert.Equal(StatusChange.Attach, latch.Set(true)); // and it can attach again (vanilla cannot)
            Assert.True(latch.Attached);
        }

        [Fact]
        public void Criterion01_9_SlotUnderwaterFromOwnWater()
        {
            Assert.False(CompartmentTick.IsSlotUnderwater(false, 1.4f, 1.0f, null, 0));
            Assert.True(CompartmentTick.IsSlotUnderwater(false, 1.4f, 1.5f, null, 0));
        }

        [Fact]
        public void Criterion01_9_ContainingNeighbourAboveToleranceMakesTheSlotUnderwater()
        {
            Assert.True(CompartmentTick.IsSlotUnderwater(false, 1.4f, 1.0f, new[] { 1.5f }, 1));
        }

        [Fact]
        public void Criterion01_9_AnySideWinsPerD7()
        {
            // 01 §6 #9 expected "neighbour overrides" (false); D7 / C24 decide "any side": own 1.5 m is enough.
            Assert.True(CompartmentTick.IsSlotUnderwater(false, 1.4f, 1.5f, new[] { 0.2f }, 1));
            // Several neighbours: the order does not matter.
            Assert.True(CompartmentTick.IsSlotUnderwater(false, 1.4f, 0f, new[] { 1.5f, 0.2f }, 2));
            Assert.True(CompartmentTick.IsSlotUnderwater(false, 1.4f, 0f, new[] { 0.2f, 1.5f }, 2));
        }

        [Fact]
        public void NeighbourCountLimitsTheEntriesRead()
        {
            Assert.False(CompartmentTick.IsSlotUnderwater(false, 1.4f, 0f, new[] { 0.2f, 1.5f }, 1));
        }

        // ---- elapsed time per tick (WS7: the timed queue runs once per frame at most)

        [Fact]
        public void FirstTickAndClockResetCountOneTickInterval()
        {
            Assert.Equal(CompartmentTick.TickSeconds, CompartmentTick.ElapsedSeconds(-1, 50_000_000));
            Assert.Equal(CompartmentTick.TickSeconds, CompartmentTick.ElapsedSeconds(90_000_000, 50_000_000));
        }

        [Theory]
        [InlineData(10_000_000L, 1f)]      // 1x: one tick per game second
        [InlineData(21_300_000L, 2.13f)]   // 64x at 30 fps: the tick can only run once per 2.13 s frame
        [InlineData(0L, 0f)]
        public void ElapsedIsTheGameTimeSinceThePreviousTick(long delta, float seconds)
        {
            Assert.Equal(seconds, CompartmentTick.ElapsedSeconds(1_000_000_000L, 1_000_000_000L + delta), 4);
        }

        [Fact]
        public void ElapsedIsClampedAfterATimeSkip()
        {
            Assert.Equal(CompartmentTick.MaxElapsedSeconds, CompartmentTick.ElapsedSeconds(0, 36_000_000_000L));
        }

        [Fact]
        public void DamageOddsPerGameSecondDoNotDependOnTheTickLength()
        {
            // Two 1 s ticks at 1x and one 2 s tick at 64x give the same chance of no damage.
            float chance = CompartmentTick.OverstressDamageChance(0f);
            double twoTicks = System.Math.Pow(1.0 - Probability.PerStep(chance, 1f), 2);
            double oneTick = 1.0 - Probability.PerStep(chance, CompartmentTick.ElapsedSeconds(0, 20_000_000));
            Assert.Equal(twoTicks, oneTick, 6);
        }

        [Fact]
        public void UnbreathableAirPutsEverySlotUnderwater()
        {
            // C24: vanilla 2026.1 rule kept, keyed on P2's unbreathable test.
            Assert.True(CompartmentTick.IsSlotUnderwater(true, 1.4f, 0f, null, 0));
        }
    }
}
