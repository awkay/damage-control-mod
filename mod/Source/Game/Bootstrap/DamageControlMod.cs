using System;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using DWS.Common.InjectionFramework;
using HarmonyLib;
using UBOAT.Game;
using UnityEngine.SceneManagement;

namespace DamageControl.Game.Bootstrap
{
	/// <summary>
	/// Entry point (R7). The game instantiates the first IUserMod implementation in the compiled mod
	/// assembly and calls OnLoaded() once, after [Inject] fields are filled and before the main scene
	/// loads (no player ship or save exists yet). Every step is isolated: an exception is logged and
	/// never reaches the game, and later steps still run.
	/// </summary>
	public sealed class DamageControlMod : IUserMod
	{
		public const string HarmonyId = "uboat.damage-control";

		private static bool loaded;

		public void OnLoaded()
		{
			if (loaded)
				return;
			loaded = true;

			Step("logging", GameLog.Install);
			DcLog.Info(ModInfo.Describe() + " loaded; applying Harmony patches.");
			// PatchAll() with no arguments patches every [HarmonyPatch] class in this assembly. A class that
			// cannot be applied is skipped through its Cleanup (PatchGuard) and runs vanilla; the rest still apply.
			Step("harmony", () => new Harmony(HarmonyId).PatchAll());
			if (PatchGuard.FailedCount == 0)
				DcLog.Info("Harmony patches applied.");
			else
				DcLog.Warn("Harmony patches applied; " + PatchGuard.FailedCount + " patch class(es) could not be applied and run vanilla (see \"(not applied)\" lines).");
			Step("debug flag", GameLog.RefreshDebugFlag);
			Step("settings save hook", DcSettings.InstallSaveHook);
			Step("icons", IconRegistry.LoadAll);
			Step("scene events", SubscribeSceneEvents);
		}

		private static void SubscribeSceneEvents()
		{
			SceneEventsListener.OnSceneAwake -= OnSceneAwake;
			SceneEventsListener.OnSceneAwake += OnSceneAwake;
		}

		/// <summary>Re-injects our static [Inject] fields for scene singletons (player ship, crew, ...).</summary>
		private static void OnSceneAwake(Scene scene)
		{
			Step("inject on scene awake (" + scene.name + ")", () => InjectionFramework.Instance.InjectIntoAssembly(typeof(DamageControlMod).Assembly));
			Step("debug flag", GameLog.RefreshDebugFlag);
		}

		private static void Step(string name, Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				DcLog.Exception("bootstrap step \"" + name + "\"", e);
			}
		}
	}
}
