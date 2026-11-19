using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using Xunit;

namespace DamageControl.Tests
{
    // DcLog is static; keep its tests in one class so xUnit runs them sequentially.
    public class DcLogTests : IDisposable
    {
        private readonly List<KeyValuePair<LogLevel, string>> lines = new List<KeyValuePair<LogLevel, string>>();

        public DcLogTests()
        {
            DcLog.Sink = (level, message) => lines.Add(new KeyValuePair<LogLevel, string>(level, message));
            DcLog.DebugEnabled = false;
            DcLog.ResetOnceKeys();
        }

        public void Dispose()
        {
            DcLog.Sink = null;
            DcLog.DebugEnabled = false;
            DcLog.ResetOnceKeys();
        }

        [Fact]
        public void LinesArePrefixedAndLevelled()
        {
            DcLog.Info("hello");
            DcLog.Warn("careful");
            DcLog.Error("broken");
            Assert.Equal(new[] { LogLevel.Info, LogLevel.Warning, LogLevel.Error }, lines.ConvertAll(l => l.Key));
            Assert.Equal("[Damage Control] hello", lines[0].Value);
        }

        [Fact]
        public void DebugLinesNeedTheFlag()
        {
            DcLog.Debug("hidden");
            Assert.Empty(lines);
            DcLog.DebugEnabled = true;
            DcLog.Debug("shown");
            Assert.Single(lines);
            Assert.Equal(LogLevel.Debug, lines[0].Key);
        }

        [Fact]
        public void OnceKeysLogOncePerSession()
        {
            Assert.True(DcLog.WarnOnce("k", "first"));
            Assert.False(DcLog.WarnOnce("k", "second"));
            Assert.False(DcLog.ErrorOnce("k", "third"));
            Assert.Single(lines);
            DcLog.ResetOnceKeys();
            Assert.True(DcLog.ErrorOnce("k", "again"));
            Assert.Equal(2, lines.Count);
        }

        [Fact]
        public void PatchFailuresLogOncePerPatch()
        {
            var e = new InvalidOperationException("boom");
            Assert.True(DcLog.PatchFailed("HatchDoUpdate", e));
            Assert.False(DcLog.PatchFailed("HatchDoUpdate", e));
            Assert.True(DcLog.PatchFailed("OtherPatch", e));
            Assert.Equal(2, lines.Count);
            Assert.Contains("HatchDoUpdate", lines[0].Value);
            Assert.Contains("InvalidOperationException: boom", lines[0].Value);
            Assert.Equal(LogLevel.Error, lines[0].Key);
        }

        [Fact]
        public void ThrowingOrMissingSinkNeverPropagates()
        {
            DcLog.Sink = (level, message) => { throw new Exception("sink broke"); };
            DcLog.Error("x");
            DcLog.Sink = null;
            DcLog.Error("y");
        }
    }
}
