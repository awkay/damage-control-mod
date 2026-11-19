using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class SlotTuningTests
    {
        [Theory]
        [InlineData(SlotOwner.DivingPlanesStation, 0.7f, -1)]
        [InlineData(SlotOwner.ElectricEngine, 0.6f, -1)]
        [InlineData(SlotOwner.InteriorHatch, 1.4f, 5)]
        [InlineData(SlotOwner.ConningTowerHatchSecondDefaultSlot, 1.4f, 6)]
        [InlineData(SlotOwner.ConningTowerHatchOtherSlot, 0.7f, 4)]
        public void OverridesFollowTheTableOf01Section2_7(SlotOwner owner, float tolerance, int interactionType)
        {
            SlotOverride result;
            Assert.True(SlotTuning.TryGetOverride(owner, out result));
            Assert.Equal(tolerance, result.WaterTolerance);
            Assert.Equal(interactionType, result.InteractionType);
            Assert.Equal(interactionType >= 0, result.OverridesInteractionType);
        }

        [Fact]
        public void OtherSlotsAreLeftAlone()
        {
            SlotOverride result;
            Assert.False(SlotTuning.TryGetOverride(SlotOwner.Other, out result));
        }

        [Theory]
        [InlineData(false, false, 0, SlotOwner.InteriorHatch)]
        [InlineData(false, false, -1, SlotOwner.InteriorHatch)]
        [InlineData(false, true, 1, SlotOwner.ConningTowerHatchSecondDefaultSlot)]
        [InlineData(false, true, 0, SlotOwner.ConningTowerHatchOtherSlot)]
        [InlineData(false, true, -1, SlotOwner.ConningTowerHatchOtherSlot)]
        [InlineData(true, false, 1, SlotOwner.Other)]
        public void HatchSlotsAreClassifiedByRoleNotName(bool exterior, bool conningTower, int defaultOrdinal, SlotOwner expected)
        {
            Assert.Equal(expected, SlotTuning.ClassifyHatchSlot(exterior, conningTower, defaultOrdinal));
        }
    }
}
