using DamageControl.Core.Leaks;
using Xunit;

namespace DamageControl.Tests
{
    public class SlotCrowdingTests
    {
        [Theory]
        [InlineData(CrowdingKind.Hole)]
        [InlineData(CrowdingKind.Pipe)]
        [InlineData(CrowdingKind.Hatch)]
        public void OneManHolePipeOrHatchTakesTwo(CrowdingKind kind)
        {
            // 01 §2.8.
            Assert.Equal(2, SlotCrowding.NewMaximum(kind, 1));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(4)]
        public void OnlyExactlyOneIsRaised(int maximum)
        {
            Assert.Equal(maximum, SlotCrowding.NewMaximum(CrowdingKind.Pipe, maximum));
        }

        [Fact]
        public void OtherInteractablesAreUntouched()
        {
            Assert.Equal(1, SlotCrowding.NewMaximum(CrowdingKind.None, 1));
        }

        [Fact]
        public void RaisingIsIdempotent()
        {
            int once = SlotCrowding.NewMaximum(CrowdingKind.Hatch, 1);
            Assert.Equal(once, SlotCrowding.NewMaximum(CrowdingKind.Hatch, once));
        }

        [Fact]
        public void OnlyHolesClearSiblingDisables()
        {
            Assert.True(SlotCrowding.ClearsSiblingDisables(CrowdingKind.Hole));
            Assert.False(SlotCrowding.ClearsSiblingDisables(CrowdingKind.Pipe));
            Assert.False(SlotCrowding.ClearsSiblingDisables(CrowdingKind.Hatch));
            Assert.False(SlotCrowding.ClearsSiblingDisables(CrowdingKind.None));
        }

        [Theory]
        [InlineData(true, false, false, CrowdingKind.Hatch)]
        [InlineData(false, true, true, CrowdingKind.Hole)]
        [InlineData(false, false, true, CrowdingKind.Pipe)]
        [InlineData(false, false, false, CrowdingKind.None)]
        public void ClassifiesByComponent(bool isHatch, bool isHullHole, bool isLeakable, CrowdingKind expected)
        {
            Assert.Equal(expected, SlotCrowding.Classify(isHatch, isHullHole, isLeakable));
        }
    }
}
