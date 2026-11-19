using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Settings;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Tasks;

namespace DamageControl.Game.Settings
{
	/// <summary>
	/// The one access point for the per-campaign settings (R9). Read with <see cref="Get"/> or the named
	/// properties; outside a campaign (menus, loading) reads return the D3 defaults, so callers never
	/// need a null check. Writes go through <see cref="Toggle"/>/<see cref="Set"/>, which update the
	/// active <see cref="DamageControlSettingsTask"/> (and so the save) and raise <see cref="Changed"/>.
	///
	/// Also the campaign-session signal: <see cref="SessionStarted"/> fires when a campaign starts or a
	/// save finishes loading. Session-only static state (one-shot flags and the like) resets there.
	/// Main thread only.
	/// </summary>
	public static class DcSettings
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static BackgroundTasksManager backgroundTasksManager;
		#pragma warning restore 0649

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static SavesManager savesManager;
		#pragma warning restore 0649

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameState gameState;
		#pragma warning restore 0649

		private static DamageControlSettingsTask active;
		private static SettingsModel model;
		private static bool saveHookInstalled;

		/// <summary>(setting, new value) after any change made through this class.</summary>
		public static event Action<SettingId, bool> Changed;

		/// <summary>A campaign session began: new campaign, or a save (with or without prior mod data) finished loading.</summary>
		public static event Action SessionStarted;

		/// <summary>True while a campaign's settings store is active.</summary>
		public static bool IsCampaignActive
		{
			get { return active != null && model != null; }
		}

		/// <summary>P14. Default off.</summary>
		public static bool HatchPressureBlocking
		{
			get { return Get(SettingId.HatchPressureBlocking); }
		}

		/// <summary>H12: vanilla sailors block leaks on their own. Default on.</summary>
		public static bool AutoLeakBlocking
		{
			get { return Get(SettingId.AutoLeakBlocking); }
		}

		/// <summary>H13: vanilla crew carry the wounded to safety on their own. Default on.</summary>
		public static bool AutoCarryWounded
		{
			get { return Get(SettingId.AutoCarryWounded); }
		}

		public static bool Get(SettingId id)
		{
			SettingsModel current = model;
			return current != null ? current.Get(id) : SettingsCatalog.DefaultValue(id);
		}

		/// <summary>Keyword of the toggle action for <paramref name="id"/> in the current state.</summary>
		public static string ToggleKeyword(SettingId id)
		{
			return SettingsCatalog.ToggleKeyword(id, Get(id));
		}

		/// <summary>Flips a setting and returns its new value. Outside a campaign nothing changes and the default is returned.</summary>
		public static bool Toggle(SettingId id)
		{
			return Set(id, !Get(id));
		}

		/// <summary>Sets a setting and returns the value now in effect. Outside a campaign nothing changes.</summary>
		public static bool Set(SettingId id, bool value)
		{
			if (!IsCampaignActive)
			{
				DcLog.WarnOnce("settings:inactive:" + id, "cannot change " + id + ": no campaign settings store is active");
				return Get(id);
			}
			if (model.Set(id, value))
			{
				active.Store(model);
				DcLog.Info("setting " + id + " is now " + (value ? "on" : "off"));
				Raise(id, value);
			}
			return value;
		}

		/// <summary>Bootstrap: after every save load, make sure a settings store exists (pre-mod saves have none).</summary>
		public static void InstallSaveHook()
		{
			if (saveHookInstalled)
				return;
			if (savesManager == null)
			{
				DcLog.Warn("SavesManager not available; pre-mod saves will run with default settings and nothing to save them in");
				return;
			}
			savesManager.Loaded += OnSaveLoaded;
			saveHookInstalled = true;
		}

		/// <summary>
		/// Creates the settings task if a campaign is in gameplay and none is running. The game creates it
		/// on CampaignStart; this covers saves made before the mod was installed. The new task starts with
		/// the D3 defaults and is saved with the campaign from then on.
		/// </summary>
		public static void EnsureTask()
		{
			if (active != null || backgroundTasksManager == null)
				return;
			if (savesManager != null && savesManager.IsLoading)
				return;
			if (gameState == null || gameState.CurrentState != GameState.State.Gameplay)
				return;
			DamageControlSettingsTask running = backgroundTasksManager.GetRunningTask<DamageControlSettingsTask>();
			if (running != null)
			{
				OnTaskStarted(running, true);
				return;
			}
			DcLog.Info("this save has no Damage Control settings yet; creating them with defaults");
			backgroundTasksManager.StartTask<DamageControlSettingsTask>();
			if (active == null)
				DcLog.Error("could not create the settings task; is the General/Background Tasks row \"" + DamageControlSettingsTask.RowKey + "\" missing?");
		}

		internal static void OnTaskStarted(DamageControlSettingsTask task, bool restored)
		{
			if (active != null && !ReferenceEquals(active, task))
			{
				if (!restored)
				{
					// A second fresh instance (e.g. CampaignStart after EnsureTask): keep the first.
					DcLog.Info("duplicate settings task ignored");
					task.Finish();
					return;
				}
				// A restored instance carries the saved values and wins over one we created.
				DamageControlSettingsTask previous = active;
				active = null;
				model = null;
				previous.Finish();
			}
			active = task;
			model = task.ToModel();
			DcLog.ResetOnceKeys();
			DcLog.Info("campaign settings " + (restored ? "restored" : "created") + ": " + model);
			Action handler = SessionStarted;
			if (handler != null)
			{
				foreach (Delegate d in handler.GetInvocationList())
				{
					try
					{
						((Action)d)();
					}
					catch (Exception e)
					{
						DcLog.Exception("DcSettings.SessionStarted handler", e);
					}
				}
			}
		}

		internal static void OnTaskFinished(DamageControlSettingsTask task)
		{
			if (!ReferenceEquals(active, task))
				return;
			active = null;
			model = null;
		}

		private static void OnSaveLoaded(Queue<Action> afterLoad)
		{
			// Queued so it runs after the game has resumed every restored task.
			if (afterLoad != null)
				afterLoad.Enqueue(SafeEnsureTask);
			else
				SafeEnsureTask();
		}

		private static void SafeEnsureTask()
		{
			try
			{
				EnsureTask();
			}
			catch (Exception e)
			{
				DcLog.Exception("DcSettings.EnsureTask", e);
			}
		}

		private static void Raise(SettingId id, bool value)
		{
			Action<SettingId, bool> handler = Changed;
			if (handler == null)
				return;
			foreach (Delegate d in handler.GetInvocationList())
			{
				try
				{
					((Action<SettingId, bool>)d)(id, value);
				}
				catch (Exception e)
				{
					DcLog.Exception("DcSettings.Changed handler", e);
				}
			}
		}
	}
}
