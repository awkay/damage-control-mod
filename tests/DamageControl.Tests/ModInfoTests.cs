using DamageControl.Core;
using Xunit;

namespace DamageControl.Tests
{
    public class ModInfoTests
    {
        [Fact]
        public void DescribeIncludesNameAndVersion()
        {
            Assert.Equal("Damage Control 0.1.0", ModInfo.Describe());
        }

        [Theory]
        [InlineData(-0.5f, 0f)]
        [InlineData(0.25f, 0.25f)]
        [InlineData(3f, 1f)]
        [InlineData(float.NaN, 0f)]
        public void Clamp01KeepsFractionsInRange(float input, float expected)
        {
            Assert.Equal(expected, ModInfo.Clamp01(input));
        }
    }
}
