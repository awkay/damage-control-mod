using System.Collections.Generic;
using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class ActionRowRulesTests
    {
        [Theory]
        [InlineData("DamageControl.Game.Settings.SettingToggleAction", true)]
        [InlineData("  DamageControl.Game.Repair.DismantleAction ", true)]
        [InlineData("UBOAT.Game.Scene.Characters.Actions.RepairAction", false)]
        [InlineData("DamageControlX.Foo", false)]
        [InlineData("DamageControl.", false)]
        [InlineData("DamageControl.Foo, damage-control", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void OnlyOurNamespaceQualifiedTypesAreModTypes(string name, bool expected)
        {
            Assert.Equal(expected, ActionRowRules.IsModTypeName(name));
        }

        [Fact]
        public void ParsesContextsLikeTheGame()
        {
            var unknown = new List<string>();
            ActionContextSet set = ActionRowRules.ParseContexts(new[] { "Normal", " Queue ", "NoCharacter", "", "Bogus", "normal" }, unknown);
            Assert.True(set.Normal);
            Assert.True(set.Queue);
            Assert.True(set.NoCharacter);
            Assert.False(set.Upgrade);
            Assert.Equal(new[] { "Bogus", "normal" }, unknown);
        }

        [Fact]
        public void EmptyContextsAreEmpty()
        {
            Assert.True(ActionRowRules.ParseContexts(null, null).IsEmpty);
            Assert.True(ActionRowRules.ParseContexts(new string[0], null).IsEmpty);
            Assert.False(ActionRowRules.ParseContexts(new[] { "Upgrade" }, null).IsEmpty);
        }

        [Fact]
        public void CleanTagsTrimsAndDropsEmpties()
        {
            Assert.Equal(new[] { "Workshop", "*" }, ActionRowRules.CleanTags(new[] { " Workshop", "", null, "* " }));
            Assert.Empty(ActionRowRules.CleanTags(null));
        }
    }
}
