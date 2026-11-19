using System;

namespace DamageControl.Core.Common
{
	/// <summary>
	/// Injected randomness (00 §2.1 rule 1). Core never calls a global RNG; the Game layer passes
	/// <c>UnityRandomSource.Instance</c>, tests pass <see cref="SeededRandom"/> or a scripted fake.
	/// </summary>
	public interface IRandom
	{
		/// <summary>Uniform value in [0, 1).</summary>
		float NextFloat();
	}

	/// <summary>Deterministic <see cref="IRandom"/> over System.Random. For tests and reproducible simulations.</summary>
	public sealed class SeededRandom : IRandom
	{
		private readonly Random random;

		public SeededRandom(int seed)
		{
			random = new Random(seed);
		}

		public float NextFloat()
		{
			// NextDouble is in [0, 1); the float cast can round up to 1, so clamp below it.
			float value = (float)random.NextDouble();
			return value >= 1f ? 0.99999994f : value;
		}
	}

	public static class RandomExtensions
	{
		/// <summary>True with probability <paramref name="probability"/> (clamped to [0, 1]).</summary>
		public static bool Chance(this IRandom random, float probability)
		{
			float p = DcMath.Clamp01(probability);
			if (p <= 0f)
				return false;
			if (p >= 1f)
				return true;
			return random.NextFloat() < p;
		}

		/// <summary>Uniform value in [min, max).</summary>
		public static float Range(this IRandom random, float min, float max)
		{
			return min + (max - min) * random.NextFloat();
		}
	}
}
