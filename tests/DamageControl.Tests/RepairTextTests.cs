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
        public void RepairLabelsAreOverriddenSoEachReadsNaturally()
        {
            string plain = RepoData.English(RepairText.ActivatorsSheet, RepairText.RepairKeyword);
            string parts = RepoData.English(RepairText.ActivatorsSheet, RepairText.RepairRequiresPartsKeyword);
            Assert.False(string.IsNullOrWhiteSpace(plain));
            Assert.DoesNotContain("Replacement Parts", plain);
            Assert.Contains("<sprite name=\"Replacement Parts\">", parts);
        }

        [Fact]
        public void ScrapDescriptionStatesFourToOne()
        {
            string text = RepoData.English(RepairText.ItemsSheet, RepairText.ScrapDescription);
            Assert.Contains("4", text);
            Assert.Contains("1", text);
            Assert.DoesNotContain("2:1", text);
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
