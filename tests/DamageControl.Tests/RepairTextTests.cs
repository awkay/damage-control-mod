using System.Globalization;
using System.Linq;
using DamageControl.Core.Common;
using DamageControl.Core.Repair;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class RepairTextTests
    {
        // ---- 04 §8 #9 (WS5 rows), D4

        [Fact]
        public void DismantleRowHasTheSpecColumns()
        {
            var row = RepoData.Rows("Crew/Actions").Single(r => r["key"] == RepairText.DismantleRowKey);
            Assert.Equal(RepairText.DismantleType, row["Type"]);
            Assert.Equal("Engineer", row["Character Tags"]);
            Assert.Equal("*", row["Character Tags (Soft Req)"]);
            Assert.Equal("Engineer", row["Group"]);
            Assert.Equal(new[] { "Normal", "Queue" }, row["Contexts"].Split(',').Select(s => s.Trim()).ToArray());
            var tags = row["Activator Tags"].Split(',').Select(s => s.Trim()).ToArray();
            Assert.Equal(RepairText.DismantleActivatorTags, tags);
            Assert.Contains("Ventillation", tags); // game spelling
        }

        [Fact]
        public void D4_VanillaRepairAndSalvageRowsAreNotOverridden()
        {
            var keys = RepoData.Rows("Crew/Actions").Select(r => r["key"]).ToArray();
            Assert.DoesNotContain("RepairAction1", keys);
            Assert.DoesNotContain("RepairAction2", keys);
            Assert.DoesNotContain("RepairAction3", keys);
            Assert.DoesNotContain("SalvageReplacementParts", keys);
        }

        // ---- 04 §8 #10, #11: locale and icons

        [Fact]
        public void DismantleKeywordHasEnglishTextAndDescription()
        {
            Assert.StartsWith(IconKeys.IconNamePrefix, RepairText.DismantleKeyword);
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English(RepairText.ActivatorsSheet, RepairText.DismantleKeyword)));
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English(RepairText.ActivatorsSheet, RepairText.DismantleDescription)));
            Assert.False(string.IsNullOrWhiteSpace(RepoData.English(RepairText.ActivatorsSheet, RepairText.DismantleProgressing)));
        }

        [Fact]
        public void VanillaRepairLabelsAreNotOverridden()
        {
            // An English-only override would replace all 13 translations with English (known issue in README).
            var keys = RepoData.Rows("Locales/Activators").Select(r => r["key"]).ToArray();
            Assert.DoesNotContain(RepairText.RepairKeyword, keys);
            Assert.DoesNotContain(RepairText.RepairRequiresPartsKeyword, keys);
        }

        [Fact]
        public void ScrapDescriptionIsLeftToTheGameSoItsTranslationsSurvive()
        {
            Assert.Null(RepoData.English(RepairText.ItemsSheet, RepairText.ScrapDescription));
        }

        [Fact]
        public void DismantleShipsTheFullIconSet()
        {
            string k = RepairText.DismantleKeyword;
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, k));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, k + IconKeys.HighlightedSuffix));
            Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, k + IconKeys.DisabledSuffix));
            Assert.True(RepoData.IconExists(IconKeys.Actions, k));
            Assert.True(RepoData.IconExists(IconKeys.ActionsCursor, k));
            Assert.True(RepoData.IconExists(IconKeys.ActionsBanners, k));
            Assert.True(RepoData.IconExists(IconKeys.StatusesPortraits, k));
        }

        [Fact]
        public void SalvageTunableShipsTheCoreDefault()
        {
            var row = RepoData.Rows("General/Settings/Damage Control").Single(r => r["key"] == RepairText.TunableSalvageScrapPerPart);
            Assert.Equal(SalvageRatio.DefaultScrapPerPart, float.Parse(row["Value"], CultureInfo.InvariantCulture));
        }
    }
}
