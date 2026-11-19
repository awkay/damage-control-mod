using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    public class IRandomTests
    {
        private sealed class Fixed : IRandom
        {
            private readonly float value;
            public Fixed(float value) { this.value = value; }
            public float NextFloat() { return value; }
        }

        [Fact]
        public void SeededRandomIsDeterministicAndInRange()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 1000; i++)
            {
                float x = a.NextFloat();
                Assert.Equal(x, b.NextFloat());
                Assert.InRange(x, 0f, 0.99999994f);
            }
        }

        [Fact]
        public void ChanceComparesStrictlyBelowProbability()
        {
            Assert.True(new Fixed(0.29f).Chance(0.3f));
            Assert.False(new Fixed(0.3f).Chance(0.3f));
        }

        [Fact]
        public void ChanceClampsProbability()
        {
            Assert.False(new Fixed(0f).Chance(0f));
            Assert.False(new Fixed(0f).Chance(-1f));
            Assert.True(new Fixed(0.99f).Chance(1f));
            Assert.True(new Fixed(0.99f).Chance(5f));
            Assert.False(new Fixed(0f).Chance(float.NaN));
        }

        [Fact]
        public void RangeMapsUnitInterval()
        {
            Assert.Equal(15f, new Fixed(0.5f).Range(10f, 20f));
            Assert.Equal(10f, new Fixed(0f).Range(10f, 20f));
        }
    }
}
