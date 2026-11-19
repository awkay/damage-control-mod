using DamageControl.Core.Common;
using UnityEngine;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>Connects <see cref="DcLog"/> to Unity's log (output_log.txt). Installed first thing at bootstrap.</summary>
	public static class GameLog
	{
		public static void Install()
		{
			DcLog.Sink = Write;
		}

		/// <summary>Reads the R14 debug flag from the data sheets (<see cref="Tunables.DebugPath"/>); off when the row is missing.</summary>
		public static void RefreshDebugFlag()
		{
			bool enabled = Tunables.GetBool(Tunables.DebugPath, false);
			if (enabled != DcLog.DebugEnabled)
			{
				DcLog.DebugEnabled = enabled;
				DcLog.Info("debug logging " + (enabled ? "enabled" : "disabled") + " by " + Tunables.DebugPath);
			}
		}

		private static void Write(LogLevel level, string message)
		{
			switch (level)
			{
				case LogLevel.Error:
					UnityEngine.Debug.LogError(message);
					break;
				case LogLevel.Warning:
					UnityEngine.Debug.LogWarning(message);
					break;
				default:
					UnityEngine.Debug.Log(message);
					break;
			}
		}
	}
}
