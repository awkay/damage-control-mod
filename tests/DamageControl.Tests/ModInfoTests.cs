using DamageControl.Core.Common;
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

        [Fact]
        public void LogPrefixNamesTheMod()
        {
            Assert.Equal("[Damage Control] ", ModInfo.LogPrefix);
        }
    }
}
