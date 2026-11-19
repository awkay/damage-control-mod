using System;

namespace DamageControl.Core.Common
{
	/// <summary>
	/// The one clock (00 §2.1 rule 5, C2). Captures one frame's timing as plain values:
	/// <list type="bullet">
	/// <item><see cref="UnityDeltaTime"/>: Unity's frame delta, already multiplied by Unity's time scale.</item>
	/// <item><see cref="UnityTimeScale"/>: Unity's time scale.</item>
	/// <item><see cref="ArtificialTimeScale"/>: the game's own time compression (GameTime.ArtificialTimeScale).</item>
	/// </list>
	/// Game time for the frame is <see cref="GameDt"/> = UnityDeltaTime x ArtificialTimeScale, and the effective
	/// compression is <see cref="Compression"/> = UnityTimeScale x ArtificialTimeScale. Every "frame delta"
	/// and "time scale" in specs 01 to 03 means these two values. The Game layer builds it with
	/// <c>GameClock.Now()</c>; tests construct it directly.
	/// </summary>
	public struct SimClock
	{
		public readonly float UnityDeltaTime;
		public readonly float UnityTimeScale;
		public readonly float ArtificialTimeScale;

		public SimClock(float unityDeltaTime, float unityTimeScale, float artificialTimeScale)
		{
			UnityDeltaTime = NonNegative(unityDeltaTime);
			UnityTimeScale = NonNegative(unityTimeScale);
			ArtificialTimeScale = NonNegative(artificialTimeScale);
		}

		/// <summary>A clock for a fixed game-time step at 1x compression (tests, fixed-rate ticks).</summary>
		public static SimClock FromGameSeconds(float seconds)
		{
			return new SimClock(seconds, 1f, 1f);
		}

		/// <summary>Game seconds elapsed this frame, uncapped.</summary>
		public float GameDt
		{
			get { return UnityDeltaTime * ArtificialTimeScale; }
		}

		/// <summary>Effective time compression S (1 = real time; 0 while paused).</summary>
		public float Compression
		{
			get { return UnityTimeScale * ArtificialTimeScale; }
		}

		/// <summary>True when no game time passes this frame.</summary>
		public bool IsPaused
		{
			get { return GameDt <= 0f; }
		}

		/// <summary>Game seconds this frame, capped at <paramref name="maxSeconds"/> (each spec states its cap, e.g. 0.5 s).</summary>
		public float CappedGameDt(float maxSeconds)
		{
			float dt = GameDt;
			return dt > maxSeconds ? NonNegative(maxSeconds) : dt;
		}

		/// <summary>Unity frame delta (not artificially compressed), capped at <paramref name="maxSeconds"/>.</summary>
		public float CappedUnityDt(float maxSeconds)
		{
			float dt = UnityDeltaTime;
			return dt > maxSeconds ? NonNegative(maxSeconds) : dt;
		}

		/// <summary>
		/// Stability damping under compression (02 §2.3b, C2): returns <paramref name="baseRate"/> when
		/// <paramref name="compression"/> is at most 1, else baseRate / sqrt(compression).
		/// </summary>
		public static float DampedRate(float baseRate, float compression)
		{
			if (!(compression > 1f))
				return baseRate;
			return baseRate / (float)Math.Sqrt(compression);
		}

		/// <summary><see cref="DampedRate(float, float)"/> at this frame's <see cref="Compression"/>.</summary>
		public float DampedRate(float baseRate)
		{
			return DampedRate(baseRate, Compression);
		}

		public override string ToString()
		{
			return "SimClock(dt=" + UnityDeltaTime + ", timeScale=" + UnityTimeScale + ", artificial=" + ArtificialTimeScale + ")";
		}

		private static float NonNegative(float value)
		{
			return float.IsNaN(value) || value < 0f ? 0f : value;
		}
	}
}
