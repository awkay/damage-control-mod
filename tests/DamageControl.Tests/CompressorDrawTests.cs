using DamageControl.Core.Pressure;
using Xunit;

namespace DamageControl.Tests
{
    public class CompressorDrawTests
    {
        [Fact]
        public void Criterion01_13_TenSecondsSplitIntoFourFourTwo()
        {
            Assert.Equal(3, CompressorDraw.ChunkCount(10f));
            Assert.Equal(4f, CompressorDraw.ChunkLength(10f, 0));
            Assert.Equal(4f, CompressorDraw.ChunkLength(10f, 1));
            Assert.Equal(2f, CompressorDraw.ChunkLength(10f, 2), 5);
            Assert.Equal(0f, CompressorDraw.ChunkLength(10f, 3));
        }

        [Theory]
        [InlineData(0f, 1)]
        [InlineData(0.02f, 1)]
        [InlineData(4f, 1)]
        [InlineData(8f, 2)]
        [InlineData(8.5f, 3)]
        public void ChunkCountMatchesTheVanillaLoop(float seconds, int expected)
        {
            Assert.Equal(expected, CompressorDraw.ChunkCount(seconds));
        }

        [Fact]
        public void Criterion01_13_TransferPerTankPerChunk()
        {
            // (4 x 0.5 / 2) x (1 - 100 / 200) = 0.5; the compartment loses 0.5 / 20 = 0.025.
            double transfer = CompressorDraw.Transfer(4f, 0.5f, 2, 100.0, 200.0);
            Assert.Equal(0.5, transfer, 9);
            Assert.Equal(0.025, CompressorDraw.CompartmentCost(transfer), 9);
        }

        [Fact]
        public void Criterion01_13_FullTankGetsNothing()
        {
            Assert.Equal(0.0, CompressorDraw.Transfer(4f, 0.5f, 2, 200.0, 200.0));
            Assert.Equal(0.0, CompressorDraw.Transfer(4f, 0.5f, 2, 250.0, 200.0));
        }

        [Fact]
        public void Criterion01_13_CompartmentAtPointThreeFiveStopsTheCompressor()
        {
            Assert.False(CompressorDraw.MayCompress(0.35, 100.0, 0.5));
            Assert.True(CompressorDraw.MayCompress(0.36, 100.0, 0.5));
        }

        [Fact]
        public void CompartmentMustHoldMoreAirThanATransfer()
        {
            Assert.False(CompressorDraw.MayCompress(1.0, 0.5, 0.5));
        }

        [Fact]
        public void Criterion01_13_FrameCostMatchesPerTransferCosts()
        {
            // Two tanks, chunks 4, 4, 2 at constant 0.5 / 0.5 / 0.25 per tank: gain 2.5, cost 0.125.
            Assert.Equal(0.125, CompressorDraw.FrameCost(2.5, 3, 2), 9);
        }

        [Fact]
        public void FrameCostIsCappedAtPointOnePerTransfer()
        {
            Assert.Equal(0.6, CompressorDraw.FrameCost(1000.0, 3, 2), 9);
            Assert.Equal(0.1, CompressorDraw.CompartmentCost(1000.0), 9);
        }

        [Fact]
        public void NoGainCostsNothing()
        {
            Assert.Equal(0.0, CompressorDraw.FrameCost(0.0, 1, 2));
            Assert.Equal(0.0, CompressorDraw.FrameCost(-1.0, 1, 2));
        }
    }
}
