using System.Globalization;
using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchPanelTests
    {
        private static HatchPanelText Text()
        {
            return new HatchPanelText
            {
                LockedWarning = "LOCKED",
                OpeningSideFormat = "open {0:0.00}",
                ClosingSideFormat = "close {0:0.00}",
                DurabilityFormat = "design {0:0}",
                BadlyDamaged = "WRECKED",
                PartiallySealed = "SEALED",
                Evacuated = "EVAC",
                Culture = CultureInfo.InvariantCulture,
            };
        }

        private static HatchPanelInput Input()
        {
            return new HatchPanelInput { OpeningSideAtm = 1.0, ClosingSideAtm = 1.0, Intact = true, DesignPressure = 6f };
        }

        [Fact]
        public void PressureBandsAreRedAboveTwoAndBlueBelowPointEight()
        {
            Assert.Equal(PressureBand.High, HatchPanel.Classify(2.01));
            Assert.Equal(PressureBand.Normal, HatchPanel.Classify(2.0));
            Assert.Equal(PressureBand.Normal, HatchPanel.Classify(0.8));
            Assert.Equal(PressureBand.Low, HatchPanel.Classify(0.79));
        }

        [Fact]
        public void IntactUnlockedHatchShowsSidesBlankLineAndDurability()
        {
            Assert.Equal("open 1.00\nclose 1.00\n\ndesign 6", HatchPanel.Build(Input(), Text()));
        }

        [Fact]
        public void LinesFollowTheSpecOrderAndColours()
        {
            HatchPanelInput input = Input();
            input.ShowLockedWarning = true;
            input.OpeningSideAtm = 2.5;
            input.ClosingSideAtm = 0.5;
            input.Intact = false;
            input.PartiallySealed = true;
            input.Debug = "dbg";
            input.ShowEvacuated = true;
            string expected = "<color=red>LOCKED</color>\n"
                + "<color=red>open 2.50</color>\n"
                + "<color=" + HatchPanel.LowColor + ">close 0.50</color>\n"
                + "\n"
                + "<color=red>WRECKED</color>\n"
                + "<color=" + HatchPanel.SealedColor + ">SEALED</color>\n"
                + "dbg\n"
                + "EVAC";
            Assert.Equal(expected, HatchPanel.Build(input, Text()));
        }

        [Fact]
        public void PartiallySealedLineAppearsOnceAndDebugOnlyWhenGiven()
        {
            // 02 §7 Q6: one line however many seal sources are active; R14: debug only behind the flag.
            HatchPanelInput input = Input();
            input.PartiallySealed = true;
            string text = HatchPanel.Build(input, Text());
            Assert.Equal(text.IndexOf("SEALED"), text.LastIndexOf("SEALED"));
            Assert.DoesNotContain("dbg", text);
        }

        [Fact]
        public void ViewerLooksAtTheCompartmentBeyondTheHatch()
        {
            Assert.True(HatchPanel.ViewerFarSideIsRight(true, 2, 3));
            Assert.True(HatchPanel.ViewerFarSideIsRight(true, 3, 3));
            Assert.False(HatchPanel.ViewerFarSideIsRight(true, 4, 3));
            // Outside every compartment counts as index 4.
            Assert.False(HatchPanel.ViewerFarSideIsRight(false, 0, 3));
            Assert.True(HatchPanel.ViewerFarSideIsRight(false, 0, 4));
        }
    }
}
