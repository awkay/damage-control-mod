using DamageControl.Core.Leaks;
using Xunit;

namespace DamageControl.Tests
{
    public class LeakBlockRulesTests
    {
        [Fact]
        public void BlockerLeavingPutsBackTheStatusOfARepairStillRunning()
        {
            // WS7: vanilla's blocker stop strips "is being repaired" whoever added it (P8 lets both share a target).
            Assert.True(LeakBlockRules.RestoresRepairStatus(true, true, false));
        }

        [Fact]
        public void BlockerLeavingLeavesTheStatusAloneOtherwise()
        {
            Assert.False(LeakBlockRules.RestoresRepairStatus(true, false, false)); // no repair left: vanilla's removal stands
            Assert.False(LeakBlockRules.RestoresRepairStatus(true, true, true));   // still there: nothing to do
            Assert.False(LeakBlockRules.RestoresRepairStatus(false, true, false)); // there was none
        }

        [Fact]
        public void BlockLeakHiddenWhenNoVisibleLeak()
        {
            // 02 §6 #16, first half.
            Assert.False(LeakBlockRules.IsOffered(false, false, false, true));
            Assert.False(LeakBlockRules.IsOffered(false, true, false, true));
        }

        [Fact]
        public void BlockLeakHiddenOnOpenHatch()
        {
            // 02 §6 #16, second half.
            Assert.False(LeakBlockRules.IsOffered(true, true, true, true));
            Assert.True(LeakBlockRules.IsOffered(true, true, false, true));
        }

        [Fact]
        public void BlockLeakHiddenOnHatchWithoutInteractionSlots()
        {
            Assert.False(LeakBlockRules.IsOffered(true, true, false, false));
        }

        [Fact]
        public void BlockLeakOfferedOnLeakingNonHatchEquipment()
        {
            // The open-hatch and slot rules apply only to hatches.
            Assert.True(LeakBlockRules.IsOffered(true, false, true, false));
        }

        [Fact]
        public void NoExceptionWhenBlockingLeakingHatch_HatchTakesTheNonLeakablePath()
        {
            // C28: a hatch is not leakable equipment, so vanilla's LeakableEquipment cast must be skipped.
            Assert.True(LeakBlockRules.TakesNonLeakablePath(true, false));
            Assert.False(LeakBlockRules.TakesNonLeakablePath(true, true));
            // Leak gone (no equipment): vanilla's own leak-gone cancel handles it.
            Assert.False(LeakBlockRules.TakesNonLeakablePath(false, false));
        }

        [Fact]
        public void HatchIsSealedOnlyWhileInteracting()
        {
            // 02 §2.6: the seal holds while the crewman is in the slot, not while he walks there.
            var walking = LeakBlockRules.HatchStep(false, false);
            Assert.False(walking.Seal);
            Assert.False(walking.Cancel);

            var working = LeakBlockRules.HatchStep(false, true);
            Assert.True(working.Seal);
            Assert.False(working.Cancel);
        }

        [Fact]
        public void OpeningTheHatchCancelsTheBlockerAndDropsTheSeal()
        {
            // 02 §2.6 termination; 02 §6 #25 "opening the hatch cancels the blocker".
            var step = LeakBlockRules.HatchStep(true, true);
            Assert.True(step.Cancel);
            Assert.False(step.Seal);
        }
    }
}
