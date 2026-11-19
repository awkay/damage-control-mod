using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchLockRuleTests
    {
        private const float LowY = 100f;

        /// <summary>A closed interior blockade hatch with both sides, P14 on, both sides dry at 1 atm.</summary>
        private static HatchLockInput Interior(double seatP = 1.0, double farP = 1.0, float seatWe = 99f, float farWe = 99f)
        {
            return new HatchLockInput
            {
                IsPressureBlockade = true,
                BlockingEnabled = true,
                HasLeft = true,
                HasRight = true,
                HasSeat = true,
                HasFar = true,
                SeatAirPressure = seatP,
                SeatWaterElevation = seatWe,
                FarAirPressure = farP,
                FarWaterElevation = farWe,
                LowY = LowY,
                Threshold = HatchLockRule.DefaultThreshold,
            };
        }

        [Fact]
        public void Criterion02_1_BeingRepairedCannotBeOpenedWhateverElse()
        {
            var input = Interior();
            input.BeingRepaired = true;
            Assert.False(HatchLockRule.CanBeOpened(input));
            input.IsPressureBlockade = false;
            input.BlockingEnabled = false;
            Assert.False(HatchLockRule.CanBeOpened(input));
        }

        [Fact]
        public void Criterion02_2_NonBlockadeHatchOrSettingOffCanBeOpened()
        {
            var input = Interior(seatP: 5.0);
            input.IsPressureBlockade = false;
            Assert.True(HatchLockRule.CanBeOpened(input));

            input = Interior(seatP: 5.0);
            input.BlockingEnabled = false;
            Assert.True(HatchLockRule.CanBeOpened(input));
        }

        [Fact]
        public void Criterion02_3_ThresholdIsAStrictPointNineAtm()
        {
            Assert.False(HatchLockRule.CanBeOpened(Interior(seatP: 2.0, farP: 1.0)));
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatP: 1.8, farP: 1.0)));
            Assert.False(HatchLockRule.CanBeOpened(Interior(seatP: 1.9, farP: 1.0)));
        }

        [Fact]
        public void Criterion02_4_WaterHeadCountsInPhysicalUnitsPerD1()
        {
            // D1: 1 m of water = 0.1 atm, so 0.5 m and 1.0 m of head no longer lock the hatch.
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatWe: LowY + 0.5f)));
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatWe: LowY + 1.0f)));
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatWe: LowY + 8.5f)));
            Assert.False(HatchLockRule.CanBeOpened(Interior(seatWe: LowY + 9.5f)));
            // Water below the sill adds nothing; far-side head offsets seat-side head.
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatP: 1.85, seatWe: LowY - 1f)));
            Assert.True(HatchLockRule.CanBeOpened(Interior(seatWe: LowY + 9.5f, farWe: LowY + 2f)));
        }

        [Fact]
        public void LockThresholdIsTunable()
        {
            var input = Interior(seatWe: LowY + 1.0f);
            input.Threshold = 0.05f;
            Assert.False(HatchLockRule.CanBeOpened(input));
            Assert.Equal(0.9f, HatchLockRule.SanitizeThreshold(0f));
            Assert.Equal(0.9f, HatchLockRule.SanitizeThreshold(float.NaN));
            Assert.Equal(0.3f, HatchLockRule.SanitizeThreshold(0.3f));
        }

        [Fact]
        public void Criterion02_5_NoCompartmentsOnAShipDependsOnDeckDepth()
        {
            var input = new HatchLockInput { IsPressureBlockade = true, BlockingEnabled = true, OnShip = true, DeckDepth = 2.4f };
            Assert.True(HatchLockRule.CanBeOpened(input));
            input.DeckDepth = 2.5f;
            Assert.False(HatchLockRule.CanBeOpened(input));
            input.OnShip = false;
            Assert.True(HatchLockRule.CanBeOpened(input));
        }

        [Fact]
        public void OnlyTheSeatSideCanBeOpened()
        {
            var input = new HatchLockInput { IsPressureBlockade = true, BlockingEnabled = true, HasSeat = true, SeatAirPressure = 9.0, OnShip = true, DeckDepth = 50f };
            Assert.True(HatchLockRule.CanBeOpened(input));
        }
    }
}
