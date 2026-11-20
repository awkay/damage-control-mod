using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Core.Settings;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Sandbox.Missions;
using UBOAT.Game.Scene.Tasks;

namespace DamageControl.Game.Settings
{
	/// <summary>
	/// The one access point for the per-campaign settings (R9). Read with <see cref="Get"/> or the named
	/// properties; outside a campaign (menus, loading) reads return the D3 defaults, so callers never
	/// need a null check. Writes go through <see cref="Toggle"/>/<see cref="Set"/>, which update the
	/// active <see cref="DamageControlSettingsTask"/> (and so the save) and raise <see cref="Changed"/>.
	///
	/// Also the campaign-session signal: <see cref="SessionStarted"/> fires once per session, when a new
	/// campaign's settings are created or a load ends with its one settings store (restored, or created with
	/// defaults for a pre-mod save). Session-only static state (one-shot flags and the like) resets there.
	/// Which instance is the store is decided by Core's <see cref="SettingsTaskTracker{T}"/>.
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

		private static readonly SettingsTaskTracker<DamageControlSettingsTask> tracker = new SettingsTaskTracker<DamageControlSettingsTask>();
		private static SettingsModel model;
		private static bool saveHookInstalled;

		/// <summary>(setting, new value) after any change made through this class.</summary>
		public static event Action<SettingId, bool> Changed;

		/// <summary>A campaign session began: new campaign, or a save (with or without prior mod data) finished loading.</summary>
		public static event Action SessionStarted;

		/// <summary>True while a campaign's settings store is active.</summary>
		public static bool IsCampaignActive
		{
			get { return tracker.Active != null && model != null; }
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
				tracker.Active.Store(model);
				DcLog.Info("setting " + id + " is now " + (value ? "on" : "off"));
				Raise(id, value);
			}
			return value;
		}

		/// <summary>Bootstrap: track save loads so each load ends with exactly one settings store (pre-mod saves have none).</summary>
		public static void InstallSaveHook()
		{
			if (saveHookInstalled)
				return;
			if (savesManager == null)
			{
				DcLog.Warn("SavesManager not available; pre-mod saves will run with default settings and nothing to save them in");
				return;
			}
			savesManager.LoadingStarted += OnLoadingStarted;
			savesManager.Loaded += OnSaveLoaded;
			saveHookInstalled = true;
		}

		/// <summary>
		/// True when a save is loading or about to. Loading from the main menu sets the start mode, then loads
		/// "Main Scene", which enters gameplay and fires the game's CampaignStart before the load begins
		/// (the game's own PreserveGameplaySettingsInGameState checks the same flag).
		/// </summary>
		private static bool LoadPending()
		{
			if (savesManager != null && savesManager.IsLoading)
				return true;
			return StartScript.overrideMode == StartScript.StartMode.LoadGameState;
		}

		internal static void OnTaskStarted(DamageControlSettingsTask task, SettingsTaskSource source)
		{
			DamageControlSettingsTask displaced;
			SettingsTaskVerdict verdict = tracker.OnStarted(task, source, LoadPending(), out displaced);
			if (displaced != null)
			{
				DcLog.Info("settings task created before the save's own was restored; finishing it");
				model = null;
				displaced.Finish();
			}
			switch (verdict)
			{
				case SettingsTaskVerdict.AlreadyActive:
					return;
				case SettingsTaskVerdict.Duplicate:
					DcLog.Info("duplicate settings task (" + (source == SettingsTaskSource.Restored ? "restored" : "new") + ") finished; keeping the active one");
					task.Finish();
					return;
				case SettingsTaskVerdict.Provisional:
					model = task.ToModel();
					DcLog.Info("settings task held as provisional: a save is about to load and will replace it");
					return;
				case SettingsTaskVerdict.Restored:
					BeginSession(task, "restored from the save");
					return;
				case SettingsTaskVerdict.CreatedForSave:
					BeginSession(task, "created with defaults (the save had none)");
					return;
				default:
					BeginSession(task, "created for a new campaign");
					return;
			}
		}

		internal static void OnTaskFinished(DamageControlSettingsTask task)
		{
			if (tracker.OnFinished(task))
				model = null;
		}

		private static void BeginSession(DamageControlSettingsTask task, string how)
		{
			model = task.ToModel();
			DcLog.ResetOnceKeys();
			DcLog.Info("campaign settings " + how + ": " + model);
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

		private static void OnLoadingStarted()
		{
			// The game finishes every saved task now; the save brings its own (or none).
			tracker.OnLoadingStarted();
			model = null;
		}

		private static void OnSaveLoaded(Queue<Action> afterLoad)
		{
			// Queued so it runs after every Loaded handler, i.e. after the game resumed the restored tasks
			// and re-created the missing CampaignStart ones.
			if (afterLoad != null)
				afterLoad.Enqueue(SafeAfterLoad);
			else
				SafeAfterLoad();
		}

		private static void SafeAfterLoad()
		{
			try
			{
				ReconcileAfterLoad();
			}
			catch (Exception e)
			{
				DcLog.Exception("DcSettings.ReconcileAfterLoad", e);
			}
			finally
			{
				tracker.OnLoadFinished();
			}
		}

		/// <summary>
		/// After a load: keep exactly one settings task. Finishes extras, adopts an unreported restored
		/// instance, and creates one with defaults if the load left none (the game normally re-creates it
		/// itself for a pre-mod save; this is the fallback).
		/// </summary>
		private static void ReconcileAfterLoad()
		{
			if (backgroundTasksManager == null)
				return;
			List<IBackgroundTask> all = new List<IBackgroundTask>();
			backgroundTasksManager.GetRunningTasks(all);
			List<DamageControlSettingsTask> running = new List<DamageControlSettingsTask>();
			foreach (IBackgroundTask t in all)
			{
				DamageControlSettingsTask ours = t as DamageControlSettingsTask;
				if (ours != null)
					running.Add(ours);
			}
			List<DamageControlSettingsTask> extras = new List<DamageControlSettingsTask>();
			SettingsLoadOutcome outcome = tracker.OnLoadCompleted(running, extras);
			if (tracker.Active == null)
				model = null;
			foreach (DamageControlSettingsTask extra in extras)
			{
				DcLog.Info("duplicate settings task found after the load; finishing it");
				extra.Finish();
			}
			switch (outcome)
			{
				case SettingsLoadOutcome.AdoptedRestored:
					BeginSession(tracker.Active, "restored from the save");
					return;
				case SettingsLoadOutcome.PromotedProvisional:
					BeginSession(tracker.Active, "created for a new campaign");
					return;
				case SettingsLoadOutcome.NeedsCreate:
					if (gameState == null || gameState.CurrentState != GameState.State.Gameplay)
						return;
					DcLog.Info("no settings task after the load; starting one");
					backgroundTasksManager.StartTask<DamageControlSettingsTask>();
					if (tracker.Active == null)
						DcLog.Error("could not create the settings task; is the General/Background Tasks row \"" + DamageControlSettingsTask.RowKey + "\" missing?");
					return;
				default:
					return;
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
