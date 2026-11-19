using DamageControl.Core.Common;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Time;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>Builds the Core <see cref="SimClock"/> for the current frame from Unity's time and the game's GameTime.</summary>
	public static class GameClock
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameTime gameTime;
		#pragma warning restore 0649

		/// <summary>The game's artificial time compression; 1 when GameTime is not available (menus).</summary>
		public static float ArtificialTimeScale
		{
			get { return gameTime != null ? gameTime.ArtificialTimeScale : 1f; }
		}

		/// <summary>Timing for the current Update frame (Time.deltaTime).</summary>
		public static SimClock Now()
		{
			return new SimClock(UnityEngine.Time.deltaTime, UnityEngine.Time.timeScale, ArtificialTimeScale);
		}

		/// <summary>Timing for the current FixedUpdate step (Time.fixedDeltaTime).</summary>
		public static SimClock FixedNow()
		{
			return new SimClock(UnityEngine.Time.fixedDeltaTime, UnityEngine.Time.timeScale, ArtificialTimeScale);
		}
	}

	/// <summary>Game-side <see cref="IRandom"/> over UnityEngine.Random. Main thread only.</summary>
	public sealed class UnityRandomSource : IRandom
	{
		public static readonly UnityRandomSource Instance = new UnityRandomSource();

		private UnityRandomSource()
		{
		}

		public float NextFloat()
		{
			// Random.value is in [0, 1] inclusive; keep the IRandom contract [0, 1).
			float value = UnityEngine.Random.value;
			return value >= 1f ? 0.99999994f : value;
		}
	}
}
