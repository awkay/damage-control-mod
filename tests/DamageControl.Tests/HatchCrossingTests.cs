using DamageControl.Core.Navigation;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchCrossingTests
    {
        // A five-compartment boat: compartments 10..14 left to right, hatches between them at Z = -20, -10, 0, 10.
        private const float Tower = 101.5f;

        private static CrossingHatch[] Boat(params bool[] blocked)
        {
            var z = new[] { -20f, -10f, 0f, 10f };
            var hatches = new CrossingHatch[z.Length];
            for (int i = 0; i < z.Length; i++)
                hatches[i] = new CrossingHatch(z[i], 10 + i, 11 + i, blocked.Length > i && blocked[i]);
            return hatches;
        }

        private static CrossingQuery Walk(float fromZ, float toZ, int compartment = HatchCrossing.NoCompartment, float fromY = 100f, float toY = 100f)
        {
            return new CrossingQuery
            {
                OriginZ = fromZ,
                OriginY = fromY,
                DestinationZ = toZ,
                DestinationY = toY,
                CurrentCompartment = compartment,
                ConningTowerLevel = Tower,
            };
        }

        // ---- 03 §6 #24: false when a crossed hatch cannot be opened, even for a player order

        [Fact]
        public void Criterion24_BlockedHatchOnTheWayMakesTheDestinationUnreachable()
        {
            Assert.True(HatchCrossing.IsPathBlocked(Boat(false, true, false, false), Walk(-25f, 5f), false));
            Assert.True(HatchCrossing.IsPathBlocked(Boat(false, true, false, false), Walk(5f, -25f), false));
        }

        [Fact]
        public void Criterion24_BlockedHatchElsewhereDoesNotMatter()
        {
            // Walking from compartment 13 to 14 crosses only the hatch at Z = 10.
            Assert.False(HatchCrossing.IsPathBlocked(Boat(true, true, true, false), Walk(5f, 15f), false));
            // Walking inside one compartment crosses nothing.
            Assert.False(HatchCrossing.IsPathBlocked(Boat(true, true, true, true), Walk(-14f, -12f), false));
        }

        [Fact]
        public void Criterion24_PlayerOrdersAreBlockedToo()
        {
            // The postfix runs after vanilla returned true, which vanilla does at once for player orders.
            bool vanillaForPlayerOrder = true;
            bool blocked = HatchCrossing.IsPathBlocked(Boat(false, false, true, false), Walk(-5f, 5f), false);
            Assert.False(HatchCrossing.Combine(vanillaForPlayerOrder, blocked));
        }

        [Fact]
        public void Criterion24_ConningTowerHatchBlocksBothWays()
        {
            var boat = Boat();
            Assert.True(HatchCrossing.IsPathBlocked(boat, Walk(0f, 0f, HatchCrossing.NoCompartment, 100f, 102f), true));
            Assert.True(HatchCrossing.IsPathBlocked(boat, Walk(0f, 0f, HatchCrossing.NoCompartment, 102f, 100f), true));
            Assert.False(HatchCrossing.IsPathBlocked(boat, Walk(0f, 0f, HatchCrossing.NoCompartment, 100f, 101f), true));
            Assert.False(HatchCrossing.IsPathBlocked(boat, Walk(0f, 0f, HatchCrossing.NoCompartment, 100f, 102f), false));
        }

        [Fact]
        public void DestinationInTheTowerCountsAsAboveTheControlRoom()
        {
            // From the bow room (Z = -15) up into the tower: the walk goes aft to Z = 0, crossing the hatch at -10.
            var q = Walk(-15f, 40f, HatchCrossing.NoCompartment, 100f, 102f);
            Assert.Equal(0f, HatchCrossing.EffectiveDestinationZ(q));
            Assert.True(HatchCrossing.IsPathBlocked(Boat(false, true, false, false), q, false));
            Assert.False(HatchCrossing.IsPathBlocked(Boat(false, false, false, true), q, false));
        }

        [Fact]
        public void WalkStartsAtTheHatchOfTheCurrentCompartment()
        {
            // The agent's Z says compartment 12, but the character is registered in 11: the walk starts at
            // the hatch whose left side is 11 (Z = -10), like vanilla's own walk.
            int first, last;
            Assert.True(HatchCrossing.TryGetCrossedRange(Boat(), Walk(-5f, 15f, 11), out first, out last));
            Assert.Equal(1, first);
            Assert.Equal(3, last);

            Assert.True(HatchCrossing.TryGetCrossedRange(Boat(), Walk(-5f, -25f, 13), out first, out last));
            Assert.Equal(0, first);
            Assert.Equal(2, last);
        }

        [Fact]
        public void WalkWithNoHatchesCrossesNothing()
        {
            int first, last;
            Assert.False(HatchCrossing.TryGetCrossedRange(new CrossingHatch[0], Walk(-5f, 5f), out first, out last));
            Assert.False(HatchCrossing.TryGetCrossedRange(Boat(), Walk(-30f, -25f), out first, out last));
            Assert.False(HatchCrossing.TryGetCrossedRange(Boat(), Walk(30f, 25f), out first, out last));
        }

        // ---- 03 §6 #25: evacuation flags stay vanilla's job; the postfix can only narrow vanilla's answer

        [Fact]
        public void Criterion25_PostfixNeverMakesAnUnreachableDestinationReachable()
        {
            // Vanilla says false for an evacuation-flagged compartment on a non-player order: it stays false.
            Assert.False(HatchCrossing.Combine(false, false));
            Assert.False(HatchCrossing.Combine(false, true));
        }

        [Fact]
        public void Criterion25_PlayerOrderIntoAnEvacuatedCompartmentStaysReachable()
        {
            // Vanilla says true for a player order whatever the evacuation flags; with no blocked hatch it stays true.
            bool blocked = HatchCrossing.IsPathBlocked(Boat(), Walk(-5f, 5f), false);
            Assert.True(HatchCrossing.Combine(true, blocked));
        }

        // ---- hatch state

        [Theory]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(false, false, false)] // an open hatch is passable
        [InlineData(false, true, false)]
        public void BlockedMeansClosedAndCannotBeOpened(bool locked, bool canBeOpened, bool expected)
        {
            Assert.Equal(expected, HatchCrossing.IsBlocked(locked, canBeOpened));
        }

        // ---- B10 (03 §2.F door and ladder traverse; part of 03 §6 #26)

        [Fact]
        public void B10_TraverseThroughABlockedHatchCancelsOrders()
        {
            Assert.True(HatchCrossing.ShouldCancelTraverse(true, false, true, false));
        }

        [Fact]
        public void B10_ExteriorHatchIsExempt()
        {
            Assert.False(HatchCrossing.ShouldCancelTraverse(true, true, true, false));
        }

        [Theory]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, true, true)]
        [InlineData(true, false, false, false)]
        public void B10_NoCancelWithoutABlockedHatch(bool hasHatch, bool exterior, bool locked, bool canBeOpened)
        {
            Assert.False(HatchCrossing.ShouldCancelTraverse(hasHatch, exterior, locked, canBeOpened));
        }
    }
}
