using DamageControl.Core.Repair;
using Xunit;

namespace DamageControl.Tests
{
    public class RepairSlotsTests
    {
        // ---- 04 §8 #6: slot preference

        [Fact]
        public void Criterion6_HatchUsesDefaultWhenItIsFree()
        {
            Assert.Equal("Default", RepairSlots.Choose(true, true, true, true));
            Assert.Equal("Default", RepairSlots.Choose(true, false, false, true));
        }

        [Fact]
        public void Criterion6_HatchWithDefaultTakenFallsBackLikeOtherEquipment()
        {
            Assert.Equal("Repair", RepairSlots.Choose(true, true, true, false));
            Assert.Equal("Block Leak", RepairSlots.Choose(true, false, true, false));
            Assert.Equal("Default", RepairSlots.Choose(true, false, false, false));
        }

        [Theory]
        [InlineData(true, true, true, "Repair")]
        [InlineData(true, false, false, "Repair")]
        [InlineData(false, true, true, "Block Leak")]
        [InlineData(false, true, false, "Block Leak")]
        [InlineData(false, false, true, "Default")]
        [InlineData(false, false, false, "Default")]
        public void Criterion6_OtherEquipmentTakesFirstFreeOfRepairBlockLeakDefault(bool repair, bool blockLeak, bool @default, string expected)
        {
            Assert.Equal(expected, RepairSlots.Choose(false, repair, blockLeak, @default));
        }

        [Theory]
        [InlineData(int.MinValue, 1)]
        [InlineData(-3, 1)]
        [InlineData(0, 1)]
        [InlineData(1, 1)]
        [InlineData(12, 12)]
        public void Criterion6_InteractionTypeIsAtLeastOne(int raw, int expected)
        {
            Assert.Equal(expected, RepairSlots.ClampInteractionType(raw));
        }

        // ---- 04 §2.1.2: availability through the Block Leak slot

        [Fact]
        public void BlockLeakSlotOffersARepairThatVanillaDisabledOnlyForSlots()
        {
            Assert.True(RepairSlots.OfferThroughBlockLeakSlot(true, false, false, false, false, true));
        }

        [Theory]
        [InlineData(false, false, false, false, false, true)] // vanilla already offers it
        [InlineData(true, true, false, false, false, true)]   // carrying a blocking item
        [InlineData(true, false, true, false, false, true)]   // someone else repairs it
        [InlineData(true, false, false, true, false, true)]   // a part is needed and there is none
        [InlineData(true, false, false, false, true, true)]   // the Repair/Default slot is usable: disabled for another reason
        [InlineData(true, false, false, false, false, false)] // no usable Block Leak slot either
        public void BlockLeakSlotNeverOverridesOtherReasons(bool disabled, bool blocking, bool other, bool parts, bool repairSlot, bool leakSlot)
        {
            Assert.False(RepairSlots.OfferThroughBlockLeakSlot(disabled, blocking, other, parts, repairSlot, leakSlot));
        }

        // ---- 04 §2.1.6 / §8 #3: part rule (vanilla 2026.1, mirrored for the availability fallback)

        [Fact]
        public void PartIsRequiredOnlyFromZeroHealthOnPartsConsumingEquipment()
        {
            Assert.False(RepairSlots.PartRequiredButMissing(1f, true, false, false, true, false));
            Assert.False(RepairSlots.PartRequiredButMissing(0f, false, false, false, true, false));
            Assert.True(RepairSlots.PartRequiredButMissing(0f, true, false, false, true, false));
        }

        [Fact]
        public void NpcsAreExemptFromParts()
        {
            Assert.False(RepairSlots.PartRequiredButMissing(0f, true, true, false, true, false));
        }

        [Fact]
        public void PartComesFromBackpackOrFromMainStorageWithManualDeliveryOff()
        {
            Assert.False(RepairSlots.PartRequiredButMissing(0f, true, false, true, true, false));
            Assert.False(RepairSlots.PartRequiredButMissing(0f, true, false, false, false, true));
            Assert.True(RepairSlots.PartRequiredButMissing(0f, true, false, false, true, true));
            Assert.True(RepairSlots.PartRequiredButMissing(0f, true, false, false, false, false));
        }
    }
}
