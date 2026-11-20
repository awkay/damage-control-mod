using DamageControl.Core.Repair;
using Xunit;

namespace DamageControl.Tests
{
    public class RepairEffectsTests
    {
        private static RepairFrame Leak(RepairTrack t, bool interacting, bool leaking, float p) =>
            RepairEffects.Step(t, interacting, false, false, false, false, true, leaking, p);

        private static RepairFrame Hatch(RepairTrack t, bool interacting, bool exterior, bool compartments, bool locked) =>
            RepairEffects.Step(t, interacting, true, exterior, compartments, locked, false, false, 0.3f);

        // ---- 04 §8 #5: leak throttle

        [Theory]
        [InlineData(0f, 1f)]
        [InlineData(0.25f, 0.75f)]
        [InlineData(0.999f, 0.001f)]
        [InlineData(1.5f, 0f)]
        [InlineData(-1f, 1f)]
        [InlineData(float.NaN, 1f)]
        public void Criterion5_ThrottleIsOneMinusProgress(float p, float expected)
        {
            Assert.Equal(expected, RepairEffects.ThrottleValue(p), 5);
        }

        [Fact]
        public void Criterion5_LeakingEquipmentIsThrottledByOneMinusProgressWhileInteracting()
        {
            var t = new RepairTrack();
            var f = Leak(t, true, true, 0.2f);
            Assert.True(f.ThrottleLeak);
            Assert.Equal(0.8f, f.ThrottleValue, 5);
            f = Leak(t, true, true, 0.6f);
            Assert.True(f.ThrottleLeak);
            Assert.Equal(0.4f, f.ThrottleValue, 5);
        }

        [Fact]
        public void Criterion5_ThrottlePersistsAfterTheLeakStatusEnds()
        {
            var t = new RepairTrack();
            Leak(t, true, true, 0.1f);
            var f = Leak(t, true, false, 0.5f);
            Assert.True(f.ThrottleLeak);
            Assert.Equal(0.5f, f.ThrottleValue, 5);
        }

        [Fact]
        public void Criterion5_EquipmentWithoutLeakingStatusIsNeverThrottled()
        {
            var t = new RepairTrack();
            Assert.False(Leak(t, true, false, 0.2f).ThrottleLeak);
            Assert.False(Leak(t, true, true, 0.4f).ThrottleLeak); // decided on the first interacting frame
            Assert.False(RepairEffects.Step(new RepairTrack(), true, false, false, false, false, false, true, 0.2f).ThrottleLeak);
        }

        [Fact]
        public void Criterion5_EffectsAreReleasedWhenNotInteracting()
        {
            var t = new RepairTrack();
            Leak(t, true, true, 0.3f);
            var f = Leak(t, false, true, 0.3f);
            Assert.True(f.Release);
            Assert.False(f.ThrottleLeak);
            Assert.Equal(1f, f.ThrottleValue);
        }

        [Fact]
        public void Criterion5_HatchRepairIsASealSourceNotAThrottle()
        {
            var t = new RepairTrack();
            var f = Hatch(t, true, false, true, true);
            Assert.True(f.SealHatch);
            Assert.False(f.ThrottleLeak);
            Assert.True(Hatch(t, false, false, true, true).Release);
        }

        // ---- 04 §8 #17 (offline part) / 00 C10: hatch held shut during repair

        [Fact]
        public void Criterion17_OpenInteriorHatchIsClosedOnceAtTheStart()
        {
            var t = new RepairTrack();
            Assert.True(Hatch(t, true, false, true, false).CloseHatch);
            Assert.False(Hatch(t, true, false, true, false).CloseHatch);
            Hatch(t, false, false, true, false);
            Assert.False(Hatch(t, true, false, true, false).CloseHatch); // resuming does not close again
        }

        [Fact]
        public void Criterion17_NothingIsClosedBeforeTheRepairerInteracts()
        {
            var t = new RepairTrack();
            Assert.False(Hatch(t, false, false, true, false).CloseHatch);
            Assert.True(Hatch(t, true, false, true, false).CloseHatch);
        }

        [Fact]
        public void Criterion17_ExteriorHatchesDoorsAndClosedHatchesAreLeftAlone()
        {
            Assert.False(Hatch(new RepairTrack(), true, true, true, false).CloseHatch);
            Assert.False(Hatch(new RepairTrack(), true, false, false, false).CloseHatch);
            Assert.False(Hatch(new RepairTrack(), true, false, true, true).CloseHatch);
        }

        [Fact]
        public void Criterion17_NonHatchEquipmentIsNeverClosed()
        {
            Assert.False(Leak(new RepairTrack(), true, true, 0f).CloseHatch);
            Assert.False(Leak(new RepairTrack(), true, true, 0f).SealHatch);
        }
    }
}
