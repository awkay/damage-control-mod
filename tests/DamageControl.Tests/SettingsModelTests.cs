using System;
using System.Linq;
using DamageControl.Core.Common;
using DamageControl.Core.Settings;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class SettingsModelTests
    {
        [Fact]
        public void DefaultsFollowDecisionD3()
        {
            // D3: vanilla auto leak blocking ON, vanilla auto carry wounded ON, hatch pressure blocking OFF.
            Assert.False(SettingsCatalog.DefaultValue(SettingId.HatchPressureBlocking));
            Assert.True(SettingsCatalog.DefaultValue(SettingId.AutoLeakBlocking));
            Assert.True(SettingsCatalog.DefaultValue(SettingId.AutoCarryWounded));

            var model = SettingsModel.CreateDefaults();
            Assert.False(model.HatchPressureBlocking);
            Assert.True(model.AutoLeakBlocking);
            Assert.True(model.AutoCarryWounded);
        }

        [Theory]
        [InlineData(SettingId.HatchPressureBlocking)]
        [InlineData(SettingId.AutoLeakBlocking)]
        [InlineData(SettingId.AutoCarryWounded)]
        public void TwoTogglesReturnToTheOriginalState(SettingId id)
        {
            // 02 §6 #17, 01 §6 #23.
            var model = SettingsModel.CreateDefaults();
            bool original = model.Get(id);
            Assert.Equal(!original, model.Toggle(id));
            Assert.Equal(!original, model.Get(id));
            Assert.Equal(original, model.Toggle(id));
            Assert.Equal(original, model.Get(id));
        }

        [Fact]
        public void ToggleChangesOnlyItsOwnSetting()
        {
            var model = new SettingsModel(false, false, false);
            model.Toggle(SettingId.AutoLeakBlocking);
            Assert.False(model.HatchPressureBlocking);
            Assert.True(model.AutoLeakBlocking);
            Assert.False(model.AutoCarryWounded);
        }

        [Fact]
        public void SetReportsWhetherTheValueChanged()
        {
            var model = new SettingsModel(false, true, true);
            Assert.False(model.Set(SettingId.AutoLeakBlocking, true));
            Assert.True(model.Set(SettingId.AutoLeakBlocking, false));
            Assert.False(model.AutoLeakBlocking);
        }

        [Fact]
        public void LabelNamesTheNextAction()
        {
            var model = SettingsModel.CreateDefaults();
            Assert.Equal("DC Enable Hatch Pressure Blocking", model.ToggleKeyword(SettingId.HatchPressureBlocking));
            model.Toggle(SettingId.HatchPressureBlocking);
            Assert.Equal("DC Disable Hatch Pressure Blocking", model.ToggleKeyword(SettingId.HatchPressureBlocking));
            Assert.Equal("DC Disable Automatic Leak Blocking", model.ToggleKeyword(SettingId.AutoLeakBlocking));
        }

        [Fact]
        public void KeywordsAreDistinctAndPrefixed()
        {
            var all = SettingsCatalog.AllToggleKeywords().ToList();
            Assert.Equal(6, all.Count);
            Assert.Equal(all.Count, all.Distinct().Count());
            Assert.All(all, k => Assert.StartsWith(IconKeys.IconNamePrefix, k));
        }

        [Theory]
        [InlineData("HatchPressureBlocking", SettingId.HatchPressureBlocking)]
        [InlineData(" autoleakblocking ", SettingId.AutoLeakBlocking)]
        [InlineData("AUTOCARRYWOUNDED", SettingId.AutoCarryWounded)]
        public void ParsesRowParameter(string text, SettingId expected)
        {
            SettingId id;
            Assert.True(SettingsCatalog.TryParse(text, out id));
            Assert.Equal(expected, id);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("1")]
        [InlineData("Debug")]
        public void RejectsUnknownRowParameter(string text)
        {
            SettingId id;
            Assert.False(SettingsCatalog.TryParse(text, out id));
        }

        [Fact]
        public void CloneIsIndependent()
        {
            var a = SettingsModel.CreateDefaults();
            var b = a.Clone();
            b.Toggle(SettingId.HatchPressureBlocking);
            Assert.False(a.HatchPressureBlocking);
        }

        [Fact]
        public void UnknownIdThrows()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SettingsCatalog.DefaultValue((SettingId)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => SettingsModel.CreateDefaults().Get((SettingId)99));
            Assert.False(SettingsCatalog.IsDefined((SettingId)99));
        }

        // ---- data completeness (04 §8 #10, #11 for the toggles)

        [Fact]
        public void EveryToggleKeywordHasEnglishNameAndDescription()
        {
            foreach (string keyword in SettingsCatalog.AllToggleKeywords())
            {
                Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword)), keyword);
                Assert.False(string.IsNullOrWhiteSpace(RepoData.English("Activators", keyword + " Description")), keyword + " Description");
            }
        }

        [Fact]
        public void EveryToggleKeywordHasWheelIcons()
        {
            foreach (string keyword in SettingsCatalog.AllToggleKeywords())
            {
                Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword), keyword);
                Assert.True(RepoData.IconExists(IconKeys.InteractionWheel, keyword + IconKeys.HighlightedSuffix), keyword + IconKeys.HighlightedSuffix);
            }
        }

        [Fact]
        public void EverySettingHasOneToggleRowOnTheWorkshop()
        {
            var rows = RepoData.Rows("Crew/Actions")
                .Where(r => r.TryGetValue("Type", out var t) && t == "DamageControl.Game.Settings.SettingToggleAction")
                .ToList();
            foreach (SettingId id in SettingsCatalog.All)
            {
                var row = Assert.Single(rows, r => r["…"].Replace(" ", "") == SettingsCatalog.ToggleRowParameter + "=" + SettingsCatalog.ParameterName(id));
                Assert.Equal("Workshop", row["Activator Tags"]);
                // C6: Normal, Queue and NoCharacter.
                Assert.Equal(new[] { "NoCharacter", "Normal", "Queue" }, row["Contexts"].Split(',').Select(s => s.Trim()).OrderBy(s => s, StringComparer.Ordinal));
            }
        }
    }
}
