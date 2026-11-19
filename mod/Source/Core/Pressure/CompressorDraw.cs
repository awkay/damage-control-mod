using System;

namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P10 (01 §2.10, 00 §4, C30): the air compressor takes the air it stores from the compartment it
	/// stands in. Vanilla 2026.1 already fills the tanks with the spec's transfer law in chunks of at
	/// most 4 s, so the mod only gates it (no compression from a nearly empty room) and charges the
	/// compartment for what the tanks gained: one unit of room air per 20 units of compressed tank air,
	/// at most 0.1 per tank per chunk.
	/// </summary>
	public static class CompressorDraw
	{
		/// <summary>Longest chunk of one frame's game time (s).</summary>
		public const float ChunkSeconds = 4f;

		/// <summary>The compressor stops when its compartment is at or below this pressure (atm).</summary>
		public const double MinCompartmentPressure = 0.35;

		/// <summary>Units of compressed tank air per unit of compartment air.</summary>
		public const double CompressionRatio = 20.0;

		/// <summary>Most compartment air one transfer can take.</summary>
		public const double MaxDrawPerTransfer = 0.1;

		/// <summary>Number of chunks vanilla splits <paramref name="frameSeconds"/> into: full 4 s chunks, then the remainder (always one).</summary>
		public static int ChunkCount(float frameSeconds)
		{
			if (!(frameSeconds > ChunkSeconds))
				return 1;
			return (int)Math.Ceiling(frameSeconds / ChunkSeconds);
		}

		/// <summary>Length of chunk <paramref name="index"/> (0-based) of <paramref name="frameSeconds"/>.</summary>
		public static float ChunkLength(float frameSeconds, int index)
		{
			int count = ChunkCount(frameSeconds);
			if (index < 0 || index >= count)
				return 0f;
			if (index < count - 1)
				return ChunkSeconds;
			float rest = frameSeconds - ChunkSeconds * (count - 1);
			return rest > 0f ? rest : 0f;
		}

		/// <summary>Air moved into one tank in one chunk: (tau x C / N) x (1 - p / pMax), never negative.</summary>
		public static double Transfer(float chunkSeconds, float compressionRate, int tankCount, double tankPressure, double maxPressure)
		{
			if (tankCount <= 0 || !(maxPressure > 0.0))
				return 0.0;
			double transfer = chunkSeconds * (double)compressionRate / tankCount * (1.0 - tankPressure / maxPressure);
			return transfer > 0.0 ? transfer : 0.0;
		}

		/// <summary>Compartment air one transfer costs: min(transfer / 20, 0.1).</summary>
		public static double CompartmentCost(double transfer)
		{
			if (!(transfer > 0.0))
				return 0.0;
			return Math.Min(transfer / CompressionRatio, MaxDrawPerTransfer);
		}

		/// <summary>
		/// Whether the compressor may run this frame: its compartment's pressure is above 0.35 atm and
		/// the compartment holds more air than the next single transfer.
		/// </summary>
		public static bool MayCompress(double compartmentPressure, double compartmentAir, double nextTransfer)
		{
			return compartmentPressure > MinCompartmentPressure && compartmentAir > nextTransfer;
		}

		/// <summary>
		/// Compartment air to remove after a frame in which the tanks gained <paramref name="tankAirGained"/>
		/// in total: min(gain / 20, 0.1 x chunks x tanks).
		/// </summary>
		public static double FrameCost(double tankAirGained, int chunkCount, int tankCount)
		{
			if (!(tankAirGained > 0.0) || chunkCount <= 0 || tankCount <= 0)
				return 0.0;
			return Math.Min(tankAirGained / CompressionRatio, MaxDrawPerTransfer * chunkCount * tankCount);
		}
	}
}
