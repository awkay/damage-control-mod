using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class EqualizeRulesTests
    {
        [Fact]
        public void OfferedOnAClosedInteriorHatchWithBothSidesAndASeat()
        {
            Assert.True(EqualizeRules.IsOffered(true, false, true, true, true, true));
            Assert.Equal(11, EqualizeRules.SortingPriority);
        }

        [Fact]
        public void Criterion01_17_HiddenForOpenExteriorIncompleteOrNonHatch()
        {
            Assert.False(EqualizeRules.IsOffered(true, false, false, true, true, true));  // open
            Assert.False(EqualizeRules.IsOffered(true, true, true, true, true, true));    // exterior
            Assert.False(EqualizeRules.IsOffered(true, false, true, false, true, true));  // no left
            Assert.False(EqualizeRules.IsOffered(true, false, true, true, false, true));  // no right
            Assert.False(EqualizeRules.IsOffered(true, false, true, true, true, false));  // no blockade side
            Assert.False(EqualizeRules.IsOffered(false, false, true, true, true, true));  // not a hatch
        }

        [Fact]
        public void Criterion01_5_InteriorHatchPointReportsTheDifferenceAcross()
        {
            Assert.Equal(1.8, HatchPressurePoint.Reading(true, 3.0, 3.0, 1.2), 6);
            Assert.Equal(1.8, HatchPressurePoint.Reading(true, 1.2, 1.2, 3.0), 6);
        }

        [Fact]
        public void Criterion01_5_ExteriorAndOtherPointsReportTheirCompartment()
        {
            Assert.Equal(4.2, HatchPressurePoint.Reading(false, 4.2, 3.0, 1.2), 6);
        }
    }
}
