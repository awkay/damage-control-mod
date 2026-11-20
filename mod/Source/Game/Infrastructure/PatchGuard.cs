using System;
using DamageControl.Core.Common;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>
	/// Apply-time fail-safe for the attribute-declared patches. Harmony's PatchAll stops at the first patch
	/// class whose target cannot be found (a game update renamed a method, a private field moved), leaving
	/// every later class unpatched. Each patch class therefore declares
	/// <code>private static Exception Cleanup(Exception ex) { return PatchGuard.Cleanup("Id", ex); }</code>
	/// Harmony calls it after trying that class; returning null swallows the exception, so that class stays
	/// unpatched (vanilla behaviour), the failure is logged once through <see cref="DcLog.PatchFailed"/>, and
	/// PatchAll goes on with the next class.
	/// </summary>
	public static class PatchGuard
	{
		/// <summary>Patch classes that could not be applied this run.</summary>
		public static int FailedCount { get; private set; }

		/// <summary>Body of every patch class's Cleanup: logs a failure and suppresses it.</summary>
		public static Exception Cleanup(string patchId, Exception exception)
		{
			if (exception == null)
				return null;
			FailedCount++;
			DcLog.PatchFailed(patchId + " (not applied)", exception);
			return null;
		}
	}
}
