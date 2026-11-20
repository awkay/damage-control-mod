using DamageControl.Core.Automation;
using Xunit;

namespace DamageControl.Tests
{
    public class AutomationGatesTests
    {
        private const long TicksPerSecond = 10000000L;
        // 02 §6 #20 says 30 s (the 2022.1 value); 2026.1 reads PumpTurnOffDelay (55) from its task row. Any delay works.
        private const float PumpDelaySeconds = 30f;

        /// <summary>
        /// Stand-in for one controller tick as the 2026.1 controller does it for the pump (a sustained
        /// condition tracked as a tick deadline), wrapped in the H14 hold before and after.
        /// Returns true when the pump would be switched off on this tick.
        /// </summary>
        private static bool Tick(ref long deadline, long now, bool pumpIdle, bool anyLeak)
        {
            deadline = AutomationGates.PumpTurnOffDeadline(deadline, anyLeak);
            bool fires = false;
            if (pumpIdle)
            {
                if (deadline == 0L)
                    deadline = now + (long)(PumpDelaySeconds * TicksPerSecond);
                else if (now >= deadline)
                    fires = true;
            }
            else
            {
                deadline = 0L;
            }
            deadline = AutomationGates.PumpTurnOffDeadline(deadline, anyLeak);
            return fires;
        }

        /// <summary>Runs ticks 1.5 s apart (the controller's interval) for the given span; returns the second the pump turns off, or -1.</summary>
        private static double Run(ref long deadline, ref long now, double seconds, bool pumpIdle, bool anyLeak)
        {
            long end = now + (long)(seconds * TicksPerSecond);
            while (now < end)
            {
                now += (long)(1.5 * TicksPerSecond);
                if (Tick(ref deadline, now, pumpIdle, anyLeak))
                    return now / (double)TicksPerSecond;
            }
            return -1;
        }

        [Fact]
        public void PumpAutoOff_NeverWhileAnyEquipmentLeaks()
        {
            // 02 §6 #20: no leaking equipment is part of the sustained condition.
            long deadline = 0, now = 0;
            Assert.Equal(-1, Run(ref deadline, ref now, 600, true, true));
            Assert.Equal(0L, deadline);
        }

        [Fact]
        public void PumpAutoOff_RequiresTheSustainedDelayWithoutLeaksAfterTheLastLeak()
        {
            long deadline = 0, now = 0;
            Run(ref deadline, ref now, 120, true, true);
            double leakEnded = now / (double)TicksPerSecond;
            double off = Run(ref deadline, ref now, 120, true, false);
            Assert.True(off - leakEnded >= PumpDelaySeconds, "turned off " + (off - leakEnded) + " s after the leak ended");
            Assert.True(off - leakEnded < PumpDelaySeconds + 3);
        }

        [Fact]
        public void PumpAutoOff_SingleLeakingTickResetsTheTimer()
        {
            long deadline = 0, now = 0;
            Assert.Equal(-1, Run(ref deadline, ref now, 25, true, false));
            Assert.False(Tick(ref deadline, now += (long)(1.5 * TicksPerSecond), true, true));
            double resumed = now / (double)TicksPerSecond;
            double off = Run(ref deadline, ref now, 120, true, false);
            Assert.True(off - resumed >= PumpDelaySeconds);
        }

        [Fact]
        public void PumpDeadlineUntouchedWithoutLeaks()
        {
            Assert.Equal(12345L, AutomationGates.PumpTurnOffDeadline(12345L, false));
            Assert.Equal(0L, AutomationGates.PumpTurnOffDeadline(12345L, true));
        }

        [Fact]
        public void AutoLeakBlocking_RoutineRunsOnlyWhileSettingIsOn()
        {
            // 02 §6 #27 (gate part; the dispatch itself is vanilla, checked in game).
            Assert.True(AutomationGates.LeakBlockingRuns(true));
            Assert.False(AutomationGates.LeakBlockingRuns(false));
        }
    }
}
