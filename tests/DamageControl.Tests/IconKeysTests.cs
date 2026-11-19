using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class IconKeysTests
    {
        [Fact]
        public void WheelIconMapsToExtensionlessSpriteKey()
        {
            IconTarget t;
            string error;
            Assert.True(IconKeys.TryMapRawAsset("Icons/DamageControl/common/InteractionWheelUI/DC Enable Hatch Pressure Blocking.png", out t, out error), error);
            Assert.Equal("Icons/InteractionWheelUI/DC Enable Hatch Pressure Blocking", t.GameKey);
            Assert.Equal(IconAssetKind.Sprite, t.Kind);
            Assert.Equal("InteractionWheelUI", t.Category);
        }

        [Fact]
        public void VariantSuffixesStayInTheName()
        {
            IconTarget t;
            string error;
            Assert.True(IconKeys.TryMapRawAsset("Icons/DamageControl/repair/InteractionWheelUI/DC Dismantle (Highlighted).PNG", out t, out error), error);
            Assert.Equal("Icons/InteractionWheelUI/DC Dismantle (Highlighted)", t.GameKey);
        }

        [Fact]
        public void CursorCategoryIsATexture()
        {
            IconTarget t;
            string error;
            Assert.True(IconKeys.TryMapRawAsset("Icons/DamageControl/repair/Actions - Cursor/DC Dismantle.jpg", out t, out error), error);
            Assert.Equal(IconAssetKind.Texture, t.Kind);
            Assert.Equal("Icons/Actions - Cursor/DC Dismantle", t.GameKey);
        }

        [Fact]
        public void BackslashesAreAccepted()
        {
            IconTarget t;
            string error;
            Assert.True(IconKeys.TryMapRawAsset("Icons\\DamageControl\\common\\Actions\\DC X.png", out t, out error), error);
            Assert.Equal("Icons/Actions/DC X", t.GameKey);
        }

        [Theory]
        [InlineData("")]
        [InlineData("Icons/Other/common/InteractionWheelUI/DC X.png")]
        [InlineData("Icons/DamageControl/common/InteractionWheelUI/DC X.txt")]
        [InlineData("Icons/DamageControl/InteractionWheelUI/DC X.png")]
        [InlineData("Icons/DamageControl/common/extra/InteractionWheelUI/DC X.png")]
        [InlineData("Icons/DamageControl/common/Wheel/DC X.png")]
        [InlineData("Icons/DamageControl/common/InteractionWheelUI/Repair.png")]
        [InlineData("Icons/DamageControl/common/InteractionWheelUI/DC .png")]
        [InlineData("Icons/DamageControl//InteractionWheelUI/DC X.png")]
        public void RejectsKeysOutsideTheConvention(string key)
        {
            IconTarget t;
            string error;
            Assert.False(IconKeys.TryMapRawAsset(key, out t, out error));
            Assert.False(string.IsNullOrEmpty(error));
        }

        [Fact]
        public void EveryCategoryHasAKind()
        {
            foreach (string category in IconKeys.Categories)
            {
                IconAssetKind kind;
                Assert.True(IconKeys.TryGetKind(category, out kind), category);
            }
        }
    }
}
