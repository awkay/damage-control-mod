using System;
using System.Collections.Generic;
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
            Assert.False(SettingsCatalog.DefaultValue(SettingId.Diagnostics));

            var model = SettingsModel.CreateDefaults();
            Assert.False(model.HatchPressureBlocking);
            Assert.True(model.AutoLeakBlocking);
            Assert.True(model.AutoCarryWounded);
            Assert.False(model.Diagnostics);
        }

        [Theory]
        [InlineData(SettingId.HatchPressureBlocking)]
        [InlineData(SettingId.AutoLeakBlocking)]
        [InlineData(SettingId.AutoCarryWounded)]
        [InlineData(SettingId.Diagnostics)]
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
            var model = new SettingsModel(false, false, false, false);
            model.Toggle(SettingId.AutoLeakBlocking);
            Assert.False(model.HatchPressureBlocking);
            Assert.True(model.AutoLeakBlocking);
            Assert.False(model.AutoCarryWounded);
            Assert.False(model.Diagnostics);
            model.Toggle(SettingId.Diagnostics);
            Assert.True(model.Diagnostics);
            Assert.True(model.AutoLeakBlocking);
        }

        [Fact]
        public void SetReportsWhetherTheValueChanged()
        {
            var model = new SettingsModel(false, true, true, false);
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
            Assert.Equal("DC Enable Diagnostics", model.ToggleKeyword(SettingId.Diagnostics));
        }

        [Fact]
        public void KeywordsAreDistinctAndPrefixed()
        {
            var all = SettingsCatalog.AllToggleKeywords().ToList();
            Assert.Equal(8, all.Count);
            Assert.Equal(all.Count, all.Distinct().Count());
            Assert.All(all, k => Assert.StartsWith(IconKeys.IconNamePrefix, k));
        }

        [Theory]
        [InlineData("HatchPressureBlocking", SettingId.HatchPressureBlocking)]
        [InlineData(" autoleakblocking ", SettingId.AutoLeakBlocking)]
        [InlineData("AUTOCARRYWOUNDED", SettingId.AutoCarryWounded)]
        [InlineData("Diagnostics", SettingId.Diagnostics)]
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
            var c = new SettingsModel(false, true, true, true).Clone();
            Assert.True(c.Diagnostics);
        }

        [Fact]
        public void DescriptionNamesEverySetting()
        {
            Assert.Equal("HatchPressureBlocking=False, AutoLeakBlocking=True, AutoCarryWounded=True, Diagnostics=False", SettingsModel.CreateDefaults().ToString());
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

        // ---- settings task lifecycle (R9): exactly one store per session

        private sealed class FakeTask { }

        private static SettingsTaskVerdict Start(SettingsTaskTracker<FakeTask> tracker, FakeTask task, SettingsTaskSource source, bool loadPending = false)
        {
            FakeTask displaced;
            var verdict = tracker.OnStarted(task, source, loadPending, out displaced);
            Assert.Null(displaced);
            return verdict;
        }

        [Fact]
        public void NewCampaignCreatesOneStore()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var task = new FakeTask();
            Assert.Equal(SettingsTaskVerdict.CreatedForNewCampaign, Start(tracker, task, SettingsTaskSource.Fresh));
            Assert.Same(task, tracker.Active);
            Assert.Equal(SettingsTaskVerdict.AlreadyActive, Start(tracker, task, SettingsTaskSource.Fresh));
            Assert.Equal(SettingsTaskVerdict.Duplicate, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh));
            Assert.Same(task, tracker.Active);
        }

        [Fact]
        public void MenuLoadOfPreModSaveEndsWithOneCreatedStore()
        {
            // The first-run log: CampaignStart fires before the save loads, then the game re-creates the
            // task after the load. Only the second is a session.
            var tracker = new SettingsTaskTracker<FakeTask>();
            var early = new FakeTask();
            Assert.Equal(SettingsTaskVerdict.Provisional, Start(tracker, early, SettingsTaskSource.Fresh, loadPending: true));
            Assert.True(tracker.IsProvisional);

            tracker.OnLoadingStarted();                     // the game finishes the early one
            Assert.Null(tracker.Active);
            Assert.False(tracker.OnFinished(early));

            var late = new FakeTask();                      // re-created by the game in its Loaded handler
            Assert.Equal(SettingsTaskVerdict.CreatedForSave, Start(tracker, late, SettingsTaskSource.Fresh));

            var extras = new List<FakeTask>();
            Assert.Equal(SettingsLoadOutcome.Kept, tracker.OnLoadCompleted(new[] { late }, extras));
            Assert.Empty(extras);
            tracker.OnLoadFinished();
            Assert.Same(late, tracker.Active);
            Assert.False(tracker.IsLoadInProgress);
        }

        [Fact]
        public void MenuLoadOfModSaveKeepsTheRestoredStore()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            Start(tracker, new FakeTask(), SettingsTaskSource.Fresh, loadPending: true);
            tracker.OnLoadingStarted();
            var saved = new FakeTask();
            Assert.Equal(SettingsTaskVerdict.Restored, Start(tracker, saved, SettingsTaskSource.Restored));
            var extras = new List<FakeTask>();
            Assert.Equal(SettingsLoadOutcome.Kept, tracker.OnLoadCompleted(new[] { saved }, extras));
            Assert.Empty(extras);
            Assert.Same(saved, tracker.Active);
        }

        [Fact]
        public void LoadThatLeavesNoStoreAsksForOne()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            Start(tracker, new FakeTask(), SettingsTaskSource.Fresh);
            tracker.OnLoadingStarted();
            Assert.Equal(SettingsLoadOutcome.NeedsCreate, tracker.OnLoadCompleted(new FakeTask[0], new List<FakeTask>()));
            // The one we then start is created for the save, not a new campaign.
            Assert.Equal(SettingsTaskVerdict.CreatedForSave, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh));
            tracker.OnLoadFinished();
            Assert.Equal(SettingsTaskVerdict.Duplicate, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh));
        }

        [Fact]
        public void RestoredStoreReplacesAFreshOne()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var fresh = new FakeTask();
            Start(tracker, fresh, SettingsTaskSource.Fresh);
            var saved = new FakeTask();
            FakeTask displaced;
            Assert.Equal(SettingsTaskVerdict.Restored, tracker.OnStarted(saved, SettingsTaskSource.Restored, false, out displaced));
            Assert.Same(fresh, displaced);
            Assert.Same(saved, tracker.Active);
            Assert.False(tracker.OnFinished(fresh));        // finishing the displaced one leaves the store alone
            Assert.Same(saved, tracker.Active);
        }

        [Fact]
        public void SecondRestoredStoreIsADuplicate()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            tracker.OnLoadingStarted();
            var first = new FakeTask();
            Start(tracker, first, SettingsTaskSource.Restored);
            Assert.Equal(SettingsTaskVerdict.Duplicate, Start(tracker, new FakeTask(), SettingsTaskSource.Restored));
            Assert.Equal(SettingsTaskVerdict.Duplicate, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh));
            Assert.Same(first, tracker.Active);
        }

        [Fact]
        public void LoadCompletionFinishesExtrasAndKeepsTheActiveOne()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            tracker.OnLoadingStarted();
            var a = new FakeTask();
            var b = new FakeTask();
            var c = new FakeTask();
            Start(tracker, b, SettingsTaskSource.Restored);
            var extras = new List<FakeTask>();
            Assert.Equal(SettingsLoadOutcome.Kept, tracker.OnLoadCompleted(new[] { a, null, b, c, a }, extras));
            Assert.Equal(new[] { a, c }, extras);
            Assert.Same(b, tracker.Active);
        }

        [Fact]
        public void LoadCompletionAdoptsAnUnreportedRunningStore()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var gone = new FakeTask();
            Start(tracker, gone, SettingsTaskSource.Fresh);  // finished without telling us
            var a = new FakeTask();
            var b = new FakeTask();
            var extras = new List<FakeTask>();
            Assert.Equal(SettingsLoadOutcome.AdoptedRestored, tracker.OnLoadCompleted(new[] { a, b }, extras));
            Assert.Same(a, tracker.Active);
            Assert.Equal(new[] { b }, extras);
            Assert.Equal(SettingsTaskVerdict.Duplicate, Start(tracker, new FakeTask(), SettingsTaskSource.Restored));
        }

        [Fact]
        public void ProvisionalStoreThatOutlivesTheLoadIsPromoted()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var early = new FakeTask();
            Start(tracker, early, SettingsTaskSource.Fresh, loadPending: true);
            Assert.Equal(SettingsLoadOutcome.PromotedProvisional, tracker.OnLoadCompleted(new[] { early }, new List<FakeTask>()));
            Assert.False(tracker.IsProvisional);
            Assert.Same(early, tracker.Active);
        }

        [Fact]
        public void ProvisionalStoreYieldsToTheRestoredOne()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var early = new FakeTask();
            Start(tracker, early, SettingsTaskSource.Fresh, loadPending: true);
            FakeTask displaced;
            var saved = new FakeTask();
            Assert.Equal(SettingsTaskVerdict.Restored, tracker.OnStarted(saved, SettingsTaskSource.Restored, false, out displaced));
            Assert.Same(early, displaced);
            Assert.False(tracker.IsProvisional);
        }

        [Fact]
        public void BackToMenuThenLoadAnotherSave()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            var first = new FakeTask();
            Start(tracker, first, SettingsTaskSource.Fresh);
            Assert.True(tracker.OnFinished(first));          // CampaignStop
            Assert.Null(tracker.Active);

            Assert.Equal(SettingsTaskVerdict.Provisional, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh, loadPending: true));
            tracker.OnLoadingStarted();
            var saved = new FakeTask();
            Assert.Equal(SettingsTaskVerdict.Restored, Start(tracker, saved, SettingsTaskSource.Restored));
            Assert.Equal(SettingsLoadOutcome.Kept, tracker.OnLoadCompleted(new[] { saved }, new List<FakeTask>()));
            tracker.OnLoadFinished();

            Assert.True(tracker.OnFinished(saved));          // back to the menu again, then a new campaign
            Assert.Equal(SettingsTaskVerdict.CreatedForNewCampaign, Start(tracker, new FakeTask(), SettingsTaskSource.Fresh));
        }

        [Fact]
        public void TrackerRejectsNullArguments()
        {
            var tracker = new SettingsTaskTracker<FakeTask>();
            FakeTask displaced;
            Assert.Throws<ArgumentNullException>(() => tracker.OnStarted(null, SettingsTaskSource.Fresh, false, out displaced));
            Assert.Throws<ArgumentNullException>(() => tracker.OnLoadCompleted(null, new List<FakeTask>()));
            Assert.Throws<ArgumentNullException>(() => tracker.OnLoadCompleted(new FakeTask[0], null));
            Assert.False(tracker.OnFinished(null));
        }
    }
}
