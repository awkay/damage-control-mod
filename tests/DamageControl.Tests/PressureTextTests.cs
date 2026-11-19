using System;
using DamageControl.Core.Pressure;
using DamageControl.Tests.Support;
using Xunit;

namespace DamageControl.Tests
{
    public class PressureTextTests
    {
        [Fact]
        public void EveryKeyHasEnglishText()
        {
            foreach (string key in PressureText.All)
            {
                int slash = key.IndexOf('/');
                string english = RepoData.English(key.Substring(0, slash), key.Substring(slash + 1));
                Assert.False(string.IsNullOrWhiteSpace(english), "missing English text for " + key);
            }
        }

        [Theory]
        [InlineData(PressureText.BallastTotal, 2)]
        [InlineData(PressureText.BallastTank, 3)]
        [InlineData(PressureText.FloodingTotal, 1)]
        [InlineData(PressureText.FloodingCompartment, 2)]
        [InlineData(PressureText.FloodingCompartmentAlarm, 2)]
        [InlineData(PressureText.CompartmentAirPressure, 1)]
        public void EnglishTextFormatsWithTheArgumentsTheGameLayerPasses(string key, int argumentCount)
        {
            int slash = key.IndexOf('/');
            string english = RepoData.English(key.Substring(0, slash), key.Substring(slash + 1));
            var args = new object[argumentCount];
            for (int i = 0; i < argumentCount; i++)
                args[i] = i == 0 && argumentCount > 1 && key != PressureText.BallastTotal ? (object)"Engine Room" : 1.25;
            string formatted = string.Format(System.Globalization.CultureInfo.InvariantCulture, english, args);
            Assert.DoesNotContain("{", formatted);
            Assert.Throws<FormatException>(() => string.Format(english, new object[argumentCount - 1]));
        }
    }
}
