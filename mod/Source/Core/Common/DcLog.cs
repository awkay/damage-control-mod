using System;
using System.Collections.Generic;

namespace DamageControl.Core.Common
{
	public enum LogLevel
	{
		Debug = 0,
		Info = 1,
		Warning = 2,
		Error = 3,
	}

	/// <summary>
	/// The mod's only logging entry point. Core code logs through it; the Game layer installs a sink
	/// that forwards to Unity's log at bootstrap. Every line gets <see cref="ModInfo.LogPrefix"/>.
	///
	/// Debug lines (and any diagnostic text a feature shows) are gated by <see cref="DebugEnabled"/>,
	/// the R14 runtime flag, off by default. The "Once" variants log a key at most once per session;
	/// <see cref="ResetOnceKeys"/> starts a new session. Logging never throws.
	/// </summary>
	public static class DcLog
	{
		private static readonly object Gate = new object();
		private static readonly HashSet<string> OnceKeys = new HashSet<string>(StringComparer.Ordinal);
		private static Action<LogLevel, string> sink;
		private static bool debugEnabled;

		/// <summary>Receives (level, prefixed message). Null discards everything.</summary>
		public static Action<LogLevel, string> Sink
		{
			get { lock (Gate) return sink; }
			set { lock (Gate) sink = value; }
		}

		/// <summary>R14: runtime debug flag. Off by default.</summary>
		public static bool DebugEnabled
		{
			get { lock (Gate) return debugEnabled; }
			set { lock (Gate) debugEnabled = value; }
		}

		public static void Debug(string message)
		{
			if (DebugEnabled)
				Write(LogLevel.Debug, message);
		}

		public static void Info(string message)
		{
			Write(LogLevel.Info, message);
		}

		public static void Warn(string message)
		{
			Write(LogLevel.Warning, message);
		}

		public static void Error(string message)
		{
			Write(LogLevel.Error, message);
		}

		/// <summary>Logs a warning the first time <paramref name="key"/> is seen this session. Returns true if it logged.</summary>
		public static bool WarnOnce(string key, string message)
		{
			if (!FirstTime(key))
				return false;
			Write(LogLevel.Warning, message);
			return true;
		}

		/// <summary>Logs an error the first time <paramref name="key"/> is seen this session. Returns true if it logged.</summary>
		public static bool ErrorOnce(string key, string message)
		{
			if (!FirstTime(key))
				return false;
			Write(LogLevel.Error, message);
			return true;
		}

		/// <summary>Logs an exception once per <paramref name="key"/> per session, with its type, message and stack.</summary>
		public static bool Exception(string key, Exception exception)
		{
			return ErrorOnce("exception:" + key, key + " failed: " + Describe(exception));
		}

		/// <summary>
		/// Standard handler for an exception caught inside a Harmony patch body (00 §2.1 rule 2):
		/// log once per patch per session; the caller then falls back to vanilla.
		/// </summary>
		public static bool PatchFailed(string patchId, Exception exception)
		{
			return ErrorOnce("patch:" + patchId, "patch " + patchId + " threw and falls back to vanilla behaviour: " + Describe(exception));
		}

		/// <summary>Forgets every "once" key, so each can log again. Called when a campaign session starts.</summary>
		public static void ResetOnceKeys()
		{
			lock (Gate)
				OnceKeys.Clear();
		}

		private static bool FirstTime(string key)
		{
			lock (Gate)
				return OnceKeys.Add(key ?? "");
		}

		private static string Describe(Exception exception)
		{
			if (exception == null)
				return "(no exception)";
			return exception.GetType().Name + ": " + exception.Message + "\n" + exception.StackTrace;
		}

		private static void Write(LogLevel level, string message)
		{
			Action<LogLevel, string> target = Sink;
			if (target == null)
				return;
			try
			{
				target(level, ModInfo.LogPrefix + message);
			}
			catch
			{
				// Logging must never break the caller.
			}
		}
	}
}
