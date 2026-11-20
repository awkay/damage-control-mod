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

        [Theory]
        [InlineData(CrowdingKind.Hole)]
        [InlineData(CrowdingKind.Pipe)]
        [InlineData(CrowdingKind.Hatch)]
        public void OnlyLeakingUnmannedTargetsAreWidened(CrowdingKind kind)
        {
            Assert.True(SlotCrowding.AppliesTo(kind, true, false));
            Assert.False(SlotCrowding.AppliesTo(kind, false, false));
            Assert.False(SlotCrowding.AppliesTo(kind, true, true));
        }

        [Fact]
        public void OtherEquipmentIsNeverWidened()
        {
            Assert.False(SlotCrowding.AppliesTo(CrowdingKind.None, true, false));
        }

        [Theory]
        [InlineData(CrowdingKind.Hatch, true, 2, true)]
        [InlineData(CrowdingKind.Pipe, true, 2, true)]
        [InlineData(CrowdingKind.Hole, false, 2, false)]
        [InlineData(CrowdingKind.Hole, true, 1, false)]
        [InlineData(CrowdingKind.Hole, true, 3, false)]
        [InlineData(CrowdingKind.None, true, 2, false)]
        public void SharedRulesNeedALeakingTwoSeatTarget(CrowdingKind kind, bool leaking, int maximum, bool expected)
        {
            Assert.Equal(expected, SlotCrowding.IsShared(kind, leaking, maximum));
        }

        [Fact]
        public void SecondBlockerJoinsTheFirstWhenASlotIsFree()
        {
            Assert.Equal(1, SlotCrowding.BlockersToKeep(1, 0, true));
        }

        [Fact]
        public void SecondBlockerReplacesTheFirstWhenNoSlotIsFree()
        {
            // Vanilla's replacement: otherwise he would find no slot.
            Assert.Equal(0, SlotCrowding.BlockersToKeep(1, 0, false));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void BlockerBesideARepairerReplacesAnyOtherBlocker(bool freeSlot)
        {
            // The repairer and the new blocker already make two.
            Assert.Equal(0, SlotCrowding.BlockersToKeep(1, 1, freeSlot));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ThirdBlockerReplacesOneOfTwo(bool freeSlot)
        {
            Assert.Equal(1, SlotCrowding.BlockersToKeep(2, 0, freeSlot));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(1, false)]
        public void NobodyToKeepWithoutEarlierBlockers(int repairers, bool freeSlot)
        {
            Assert.Equal(0, SlotCrowding.BlockersToKeep(0, repairers, freeSlot));
        }

        [Theory]
        [InlineData(1, 0, true)]
        [InlineData(1, 0, false)]
        [InlineData(1, 1, true)]
        [InlineData(2, 0, true)]
        [InlineData(2, 0, false)]
        [InlineData(3, 0, true)]
        [InlineData(1, 2, true)]
        public void NeverMoreThanTwoAtTheLeak(int blockers, int repairers, bool freeSlot)
        {
            int keep = SlotCrowding.BlockersToKeep(blockers, repairers, freeSlot);
            Assert.InRange(keep, 0, blockers);
            if (keep > 0)
                Assert.True(keep + repairers + 1 <= SlotCrowding.CrowdedMaximum);
        }

        [Fact]
        public void BlockerPrefersTheBlockLeakSlot()
        {
            Assert.Equal("Block Leak", SlotCrowding.BlockerSlotTag(true, true, true));
        }

        [Fact]
        public void BlockerFallsBackToDefaultThenRepair()
        {
            Assert.Equal("Default", SlotCrowding.BlockerSlotTag(false, true, true));
            Assert.Equal("Repair", SlotCrowding.BlockerSlotTag(false, false, true));
        }

        [Fact]
        public void BlockerKeepsVanillasSlotWhenNothingIsFree()
        {
            Assert.Equal("Block Leak", SlotCrowding.BlockerSlotTag(false, false, false));
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(3, false)]
        public void RepairJoinsOnlyBelowTwoWorkers(int othersAtWork, bool expected)
        {
            Assert.Equal(expected, SlotCrowding.AdmitsAnother(othersAtWork));
        }
    }
}
