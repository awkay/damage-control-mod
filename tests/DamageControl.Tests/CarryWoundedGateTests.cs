using System.Linq;
using DamageControl.Core.Automation;
using DamageControl.Core.Common;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class CarryWoundedGateTests
    {
        private static CarryWoundedGate Disarmed()
        {
            var gate = new CarryWoundedGate();
            Assert.True(gate.BeforeTick(true, false, 0, 0)); // nobody injured: disarms
            Assert.False(gate.OneShotArmed);
            return gate;
        }

        [Fact]
        public void CarryWoundedGate_ToggleOnVanillaRuns()
        {
            // 02 §6 #18.
            var gate = Disarmed();
            Assert.True(gate.BeforeTick(true, false, 0, 3));
        }

        [Fact]
        public void CarryWoundedGate_ToggleOffAndFlagClearSkipped()
        {
            var gate = Disarmed();
            Assert.False(gate.BeforeTick(false, false, 0, 3));
        }

        [Fact]
        public void CarryWoundedGate_FlagSetAndLoadingSkipped()
        {
            var gate = new CarryWoundedGate();
            Assert.False(gate.BeforeTick(false, true, 0, 3));
            Assert.True(gate.OneShotArmed);
        }

        [Fact]
        public void CarryWoundedGate_FlagSetAndNobodyInjuredRunsOnceAndClearsFlag()
        {
            var gate = new CarryWoundedGate();
            Assert.True(gate.BeforeTick(false, false, 0, 0));
            Assert.False(gate.OneShotArmed);
            Assert.False(gate.BeforeTick(false, false, 0, 0));
        }

        [Fact]
        public void OneShotKeepsRunningWhileAnyoneLiesInjured()
        {
            var gate = new CarryWoundedGate();
            Assert.True(gate.BeforeTick(false, false, 0, 2));
            Assert.True(gate.BeforeTick(false, false, 0, 1));
            Assert.True(gate.OneShotArmed);
            Assert.True(gate.BeforeTick(false, false, 0, 0));
            Assert.False(gate.BeforeTick(false, false, 0, 1));
        }

        [Fact]
        public void DiversStillRecoveredWithAutoCarryOff()
        {
            // C27: the same vanilla routine pulls divers back aboard.
            var gate = Disarmed();
            Assert.True(gate.BeforeTick(false, false, 1, 0));
            Assert.True(gate.BeforeTick(false, true, 2, 4));
        }

        [Fact]
        public void TriggerArmsTheOneShot()
        {
            var gate = Disarmed();
            gate.Arm();
            Assert.True(gate.OneShotArmed);
            Assert.True(gate.BeforeTick(false, false, 0, 1));
        }

        [Fact]
        public void OneShotStartsArmed()
        {
            // 02 §2.10: a new session sees to its wounded once; Arm() re-arms it at every session start (D7).
            Assert.True(new CarryWoundedGate().OneShotArmed);
        }

        [Fact]
        public void CarryWoundedTrigger_EnabledIffAnyoneLiesInjured()
        {
            // 02 §6 #19.
            Assert.Equal(TriggerAvailability.Enabled, CarryWoundedGate.Availability(true, false, 1));
            Assert.Equal(TriggerAvailability.Enabled, CarryWoundedGate.Availability(true, false, 5));
            Assert.Equal(TriggerAvailability.Disabled, CarryWoundedGate.Availability(true, false, 0));
        }

        [Fact]
        public void TriggerHiddenOutsideCampaignAndWhileAutomaticCarryingIsOn()
        {
            Assert.Equal(TriggerAvailability.Hidden, CarryWoundedGate.Availability(false, false, 3));
            Assert.Equal(TriggerAvailability.Hidden, CarryWoundedGate.Availability(true, true, 3));
        }

        [Fact]
        public void TriggerKeywordHasLabelDescriptionAndIcons()
        {
            // 04 §8 #10, #11 for the WS3 action.
            string keyword = CarryWoundedGate.TriggerKeyword;
            Assert.StartsWith(IconKeys.IconNamePrefix, keyword);
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword)));
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword + " Description")));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword + IconKeys.HighlightedSuffix));
        }

        [Fact]
        public void DataRows_TriggerIsOnTheLightSwitchWithNormalQueueAndNoCharacter()
        {
            // 02 §6 #22 for the rows still in scope (the block-leak rows are vanilla's, H9/H10).
            var row = RepoData.Rows("Crew/Actions").Single(r => r["Type"] == "DamageControl.Game.Automation.CarryWoundedNowAction");
            Assert.Equal("Light Switch", row["Activator Tags"]);
            var contexts = row["Contexts"].Split(',').Select(s => s.Trim()).ToArray();
            Assert.Equal(new[] { "Normal", "Queue", "NoCharacter" }, contexts);
        }
    }
}
