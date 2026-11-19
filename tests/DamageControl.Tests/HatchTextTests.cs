using System.Globalization;
using System.Linq;
using DamageControl.Core.Common;
using DamageControl.Core.Hatches;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchTextTests
    {
        [Fact]
        public void EqualizeKeywordHasLabelDescriptionAndIcons()
        {
            // 04 §8 #10, #11 for the WS2 action.
            Assert.StartsWith("DC ", HatchText.EqualizeKeyword);
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English(HatchText.Sheet, HatchText.EqualizeKeyword)));
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English(HatchText.Sheet, HatchText.EqualizeKeyword + " Description")));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, HatchText.EqualizeKeyword));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, HatchText.EqualizeKeyword + IconKeys.HighlightedSuffix));
        }

        [Fact]
        public void EveryPanelKeyHasEnglishText()
        {
            foreach (string key in HatchText.PanelKeys)
                Assert.False(string.IsNullOrWhiteSpace(RepoData.English(HatchText.Sheet, key)), key);
            Assert.Contains("{0", RepoData.English(HatchText.Sheet, HatchText.PanelOpeningSide));
            Assert.Contains("{0", RepoData.English(HatchText.Sheet, HatchText.PanelClosingSide));
        }

        [Fact]
        public void EqualizeRowIsOnHatchesWithNormalQueueAndNoCharacter()
        {
            // 01 §3 #16: activator tag Hatch, contexts Normal, Queue, NoCharacter.
            var row = RepoData.Rows("Crew/Actions").Single(r => r["Type"] == "DamageControl.Game.Hatches.EqualizePressureAction");
            Assert.Equal("Hatch", row["Activator Tags"]);
            var contexts = row["Contexts"].Split(',').Select(s => s.Trim()).ToArray();
            Assert.Equal(new[] { "Normal", "Queue", "NoCharacter" }, contexts);
        }

        [Fact]
        public void TunableRowsShipTheCoreDefaults()
        {
            var rows = RepoData.Rows("General/Settings/Damage Control");
            float Value(string key) => float.Parse(rows.Single(r => r["key"] == key)["Value"], CultureInfo.InvariantCulture);
            Assert.Equal(HatchLockRule.DefaultThreshold, Value(HatchText.TunableLockThreshold));
            Assert.Equal(HatchExchange.DefaultRate, Value(HatchText.TunableFlowRate));
            Assert.Equal(HatchDamageRules.DefaultBlowOpenChancePerSecond, Value(HatchText.TunableBlowOpenChance));
        }
    }
}
