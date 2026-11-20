using System;
using System.Collections.Generic;

namespace DamageControl.Core.Settings
{
	/// <summary>
	/// The per-campaign player settings (R9, P14, H12, H13). Values are persisted by the Game layer's
	/// settings task under field names derived from these; append new members, never renumber.
	/// </summary>
	public enum SettingId
	{
		/// <summary>P14: pressure difference can hold an interior hatch shut (WS2, H1).</summary>
		HatchPressureBlocking = 0,

		/// <summary>H12: the vanilla routine that sends sailors to block leaks may run (WS3).</summary>
		AutoLeakBlocking = 1,

		/// <summary>H13: the vanilla routine that carries wounded crew to safety may run (WS3).</summary>
		AutoCarryWounded = 2,
	}

	/// <summary>Static facts about each setting: defaults (D3), data-sheet names and the wheel label keywords.</summary>
	public static class SettingsCatalog
	{
		/// <summary>Every setting, in display order.</summary>
		public static readonly SettingId[] All = { SettingId.HatchPressureBlocking, SettingId.AutoLeakBlocking, SettingId.AutoCarryWounded };

		/// <summary>Name of the Crew/Actions row parameter that tells a toggle row which setting it flips.</summary>
		public const string ToggleRowParameter = "Setting";

		/// <summary>
		/// D3: vanilla automatic leak blocking and automatic carrying of wounded stay on (the mod only adds
		/// an off switch); hatch pressure blocking is opt-in. Pre-mod saves and new campaigns both get these.
		/// </summary>
		public static bool DefaultValue(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return false;
				case SettingId.AutoLeakBlocking:
					return true;
				case SettingId.AutoCarryWounded:
					return true;
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}

		/// <summary>True for a defined <see cref="SettingId"/>.</summary>
		public static bool IsDefined(SettingId id)
		{
			return Array.IndexOf(All, id) >= 0;
		}

		/// <summary>The name used in data rows ("Setting = HatchPressureBlocking").</summary>
		public static string ParameterName(SettingId id)
		{
			return id.ToString();
		}

		/// <summary>Parses a data-row parameter value: the enum member name, case-insensitive, surrounding spaces ignored. Numbers are rejected.</summary>
		public static bool TryParse(string text, out SettingId id)
		{
			id = SettingId.HatchPressureBlocking;
			if (string.IsNullOrEmpty(text))
				return false;
			string name = text.Trim();
			foreach (SettingId candidate in All)
			{
				if (string.Equals(candidate.ToString(), name, StringComparison.OrdinalIgnoreCase))
				{
					id = candidate;
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Localization keyword (and icon name) of the toggle action, which names what choosing it will do:
		/// the "Enable" keyword while the setting is off, the "Disable" keyword while it is on.
		/// The game looks the label up as "Activators/&lt;keyword&gt;" and the description as "Activators/&lt;keyword&gt; Description".
		/// </summary>
		public static string ToggleKeyword(SettingId id, bool currentValue)
		{
			return "DC " + (currentValue ? "Disable " : "Enable ") + DisplayStem(id);
		}

		/// <summary>Both keywords of every setting (for data and icon completeness checks).</summary>
		public static IEnumerable<string> AllToggleKeywords()
		{
			foreach (SettingId id in All)
			{
				yield return ToggleKeyword(id, false);
				yield return ToggleKeyword(id, true);
			}
		}

		private static string DisplayStem(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return "Hatch Pressure Blocking";
				case SettingId.AutoLeakBlocking:
					return "Automatic Leak Blocking";
				case SettingId.AutoCarryWounded:
					return "Automatic Wounded Carrying";
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}
	}

	/// <summary>
	/// The values of all settings plus the toggle logic. The Game layer's settings task owns the stored
	/// fields and copies them in and out of this model; nothing else stores settings.
	/// </summary>
	public sealed class SettingsModel
	{
		private bool hatchPressureBlocking;
		private bool autoLeakBlocking;
		private bool autoCarryWounded;

		public SettingsModel(bool hatchPressureBlocking, bool autoLeakBlocking, bool autoCarryWounded)
		{
			this.hatchPressureBlocking = hatchPressureBlocking;
			this.autoLeakBlocking = autoLeakBlocking;
			this.autoCarryWounded = autoCarryWounded;
		}

		/// <summary>A model holding the D3 defaults.</summary>
		public static SettingsModel CreateDefaults()
		{
			return new SettingsModel(
				SettingsCatalog.DefaultValue(SettingId.HatchPressureBlocking),
				SettingsCatalog.DefaultValue(SettingId.AutoLeakBlocking),
				SettingsCatalog.DefaultValue(SettingId.AutoCarryWounded));
		}

		public bool HatchPressureBlocking
		{
			get { return hatchPressureBlocking; }
		}

		public bool AutoLeakBlocking
		{
			get { return autoLeakBlocking; }
		}

		public bool AutoCarryWounded
		{
			get { return autoCarryWounded; }
		}

		public bool Get(SettingId id)
		{
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					return hatchPressureBlocking;
				case SettingId.AutoLeakBlocking:
					return autoLeakBlocking;
				case SettingId.AutoCarryWounded:
					return autoCarryWounded;
				default:
					throw new ArgumentOutOfRangeException("id", id, "unknown setting");
			}
		}

		/// <summary>Sets a value. Returns true if it changed.</summary>
		public bool Set(SettingId id, bool value)
		{
			bool old = Get(id);
			switch (id)
			{
				case SettingId.HatchPressureBlocking:
					hatchPressureBlocking = value;
					break;
				case SettingId.AutoLeakBlocking:
					autoLeakBlocking = value;
					break;
				case SettingId.AutoCarryWounded:
					autoCarryWounded = value;
					break;
			}
			return old != value;
		}

		/// <summary>Flips a setting and returns its new value.</summary>
		public bool Toggle(SettingId id)
		{
			bool value = !Get(id);
			Set(id, value);
			return value;
		}

		/// <summary>The keyword of the toggle action for <paramref name="id"/> in the current state.</summary>
		public string ToggleKeyword(SettingId id)
		{
			return SettingsCatalog.ToggleKeyword(id, Get(id));
		}

		public SettingsModel Clone()
		{
			return new SettingsModel(hatchPressureBlocking, autoLeakBlocking, autoCarryWounded);
		}

		public override string ToString()
		{
			return "HatchPressureBlocking=" + hatchPressureBlocking + ", AutoLeakBlocking=" + autoLeakBlocking + ", AutoCarryWounded=" + autoCarryWounded;
		}
	}

	/// <summary>How a settings task instance came to exist.</summary>
	public enum SettingsTaskSource
	{
		/// <summary>Built by the game's task manager and given <c>Start()</c> (new campaign, or a save that had none).</summary>
		Fresh = 0,

		/// <summary>Deserialized from the save and given <c>Resume()</c>.</summary>
		Restored = 1,
	}

	/// <summary>What the caller does with a task reported to <see cref="SettingsTaskTracker{T}.OnStarted"/>.</summary>
	public enum SettingsTaskVerdict
	{
		/// <summary>The active store for a brand-new campaign. Start the session.</summary>
		CreatedForNewCampaign = 0,

		/// <summary>The active store, with defaults, for a save that had none (made before the mod). Start the session.</summary>
		CreatedForSave = 1,

		/// <summary>The active store, with the save's values. Start the session.</summary>
		Restored = 2,

		/// <summary>
		/// Active, but a save is about to load (loading a save from the main menu enters gameplay, and so
		/// the game's new-campaign event, before the save starts loading). The game finishes it when the
		/// load starts. Do not start the session for it.
		/// </summary>
		Provisional = 3,

		/// <summary>Another store is already active. Finish this one.</summary>
		Duplicate = 4,

		/// <summary>This instance is already the active store (reported twice). Do nothing.</summary>
		AlreadyActive = 5,
	}

	/// <summary>What the caller does once a save has finished loading (see <see cref="SettingsTaskTracker{T}.OnLoadCompleted"/>).</summary>
	public enum SettingsLoadOutcome
	{
		/// <summary>The active store survived the load as it is. Nothing to do.</summary>
		Kept = 0,

		/// <summary>No store exists. Create one (it reports back through <c>OnStarted</c> as created for the save).</summary>
		NeedsCreate = 1,

		/// <summary>A running store nobody reported was adopted as restored. Start the session for it.</summary>
		AdoptedRestored = 2,

		/// <summary>The provisional store outlived the load (the load never replaced it). It is now the real store. Start the session.</summary>
		PromotedProvisional = 3,
	}

	/// <summary>
	/// R9: keeps exactly one settings task active per campaign session, whatever order the game creates,
	/// restores and finishes instances in. The Game layer feeds it the task's Start/Resume/finish calls and
	/// the save's loading-started/loaded events and does what the verdicts say. Main thread only.
	///
	/// Game facts this encodes (docs/GAME-API-NOTES.md §6.3): loading a save from the main menu creates a
	/// fresh task on entering gameplay before the save starts loading; loading-started finishes every saved
	/// task; restored tasks are resumed after the load; then the game itself re-creates any new-campaign
	/// task the save lacked.
	/// </summary>
	public sealed class SettingsTaskTracker<T> where T : class
	{
		private T active;
		private bool activeRestored;
		private bool activeProvisional;
		private bool loadInProgress;

		/// <summary>The active store, or null.</summary>
		public T Active
		{
			get { return active; }
		}

		/// <summary>True while the active store is provisional (a save is about to replace it).</summary>
		public bool IsProvisional
		{
			get { return active != null && activeProvisional; }
		}

		/// <summary>True between a save's loading-started and <see cref="OnLoadFinished"/>.</summary>
		public bool IsLoadInProgress
		{
			get { return loadInProgress; }
		}

		/// <summary>
		/// A task instance started (<paramref name="source"/> Fresh) or resumed (Restored).
		/// <paramref name="loadPending"/>: the game is about to load, or is loading, a save.
		/// When the verdict needs a previously active store finished, it is returned in
		/// <paramref name="displaced"/> (already forgotten here); otherwise that is null.
		/// </summary>
		public SettingsTaskVerdict OnStarted(T task, SettingsTaskSource source, bool loadPending, out T displaced)
		{
			displaced = null;
			if (task == null)
				throw new ArgumentNullException("task");
			if (ReferenceEquals(task, active))
				return SettingsTaskVerdict.AlreadyActive;
			bool restored = source == SettingsTaskSource.Restored;
			if (active != null)
			{
				// Saved values beat defaults: a restored instance replaces a fresh or provisional one.
				// Otherwise the first one wins (also for two restored instances in one save).
				if (!restored || activeRestored)
					return SettingsTaskVerdict.Duplicate;
				displaced = active;
			}
			active = task;
			activeRestored = restored;
			activeProvisional = !restored && loadPending;
			if (restored)
				return SettingsTaskVerdict.Restored;
			if (activeProvisional)
				return SettingsTaskVerdict.Provisional;
			return loadInProgress ? SettingsTaskVerdict.CreatedForSave : SettingsTaskVerdict.CreatedForNewCampaign;
		}

		/// <summary>A task instance finished. Returns true if it was the active store (now forgotten).</summary>
		public bool OnFinished(T task)
		{
			if (task == null || !ReferenceEquals(task, active))
				return false;
			Forget();
			return true;
		}

		/// <summary>A save started loading: the game finishes the current campaign's tasks, so forget the active store.</summary>
		public void OnLoadingStarted()
		{
			Forget();
			loadInProgress = true;
		}

		/// <summary>
		/// The save has loaded and the game has resumed and re-created its tasks. <paramref name="running"/>
		/// lists every settings task the game still runs. Exactly one survives: the active store if it is
		/// among them, else the first. The rest are added to <paramref name="extras"/> for the caller to
		/// finish. Call <see cref="OnLoadFinished"/> after acting on the outcome.
		/// </summary>
		public SettingsLoadOutcome OnLoadCompleted(IList<T> running, IList<T> extras)
		{
			if (running == null)
				throw new ArgumentNullException("running");
			if (extras == null)
				throw new ArgumentNullException("extras");
			T survivor = null;
			if (active != null && running.Contains(active))
				survivor = active;
			else if (active != null)
				Forget(); // finished without telling us
			for (int i = 0; i < running.Count; i++)
			{
				T candidate = running[i];
				if (candidate == null)
					continue;
				if (survivor == null)
					survivor = candidate;
				else if (!ReferenceEquals(candidate, survivor) && !extras.Contains(candidate))
					extras.Add(candidate);
			}
			if (survivor == null)
				return SettingsLoadOutcome.NeedsCreate;
			if (active == null)
			{
				// Every Start/Resume reports here, so an instance we never heard of came from the save.
				active = survivor;
				activeRestored = true;
				activeProvisional = false;
				return SettingsLoadOutcome.AdoptedRestored;
			}
			if (activeProvisional)
			{
				activeProvisional = false;
				return SettingsLoadOutcome.PromotedProvisional;
			}
			return SettingsLoadOutcome.Kept;
		}

		/// <summary>Ends the load window opened by <see cref="OnLoadingStarted"/>.</summary>
		public void OnLoadFinished()
		{
			loadInProgress = false;
		}

		private void Forget()
		{
			active = null;
			activeRestored = false;
			activeProvisional = false;
		}
	}
}
