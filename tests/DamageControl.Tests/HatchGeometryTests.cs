using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchGeometryTests
    {
        [Fact]
        public void FromBoxColliderUsesCentreSizeAndScale()
        {
            // Transform at y=100, collider centred 0.5 above it, 1 m tall, scaled 1.2 vertically.
            HatchGeometry g = HatchGeometry.FromBoxCollider(100f, 0.5f, 1f, 1.2f);
            Assert.Equal(100f + 0.6f - 0.6f, g.LowY, 4);
            Assert.Equal(101.2f, g.HighY, 4);
            Assert.Equal(1.2f, g.Height, 4);
            Assert.True(g.IsValid);
        }

        [Fact]
        public void NegativeScaleFlipsButKeepsPositiveHeight()
        {
            HatchGeometry g = HatchGeometry.FromBoxCollider(100f, 0.5f, 1f, -1f);
            Assert.Equal(99f, g.LowY, 4);
            Assert.Equal(100f, g.HighY, 4);
            Assert.True(g.IsValid);
        }

        [Fact]
        public void FromLowestPointUsesTheMarkerAsLowYAndColliderHeightAboveIt()
        {
            // 02 conventions: lowY = lowest point, highY = lowY + collider height x vertical scale.
            HatchGeometry g = HatchGeometry.FromLowestPoint(99.8f, 1.5f, -1.2f);
            Assert.Equal(99.8f, g.LowY, 4);
            Assert.Equal(101.6f, g.HighY, 4);
            Assert.True(g.IsValid);
            Assert.False(HatchGeometry.FromLowestPoint(99.8f, 0f, 1f).IsValid);
        }

        [Fact]
        public void DegenerateOrNonFiniteIsInvalid()
        {
            Assert.False(HatchGeometry.FromBoxCollider(100f, 0f, 0f, 1f).IsValid);
            Assert.False(new HatchGeometry(float.NaN, 1f).IsValid);
            Assert.False(new HatchGeometry(2f, 1f).IsValid);
            Assert.False(new HatchGeometry().IsValid);
        }
    }
}
