using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using ActionInfo = UBOAT.Game.Scene.Characters.Actions.CharacterActionsDatabase.CharacterActionInfo;

namespace DamageControl.Game.Registration
{
	/// <summary>
	/// R10 / D2: registers the mod's own action types. The game's loader resolves Crew/Actions column B
	/// with Type.GetType, which cannot see a mod assembly, so it silently skips our rows. This postfix
	/// walks the rows again and, for each row that is still unregistered and whose type is in the
	/// DamageControl namespace, resolves the type in this assembly only, builds the template through its
	/// (GameDataRow) constructor and indexes it by name, activator tag and character tag exactly as the
	/// game does.
	///
	/// Coexistence with the Custom Actions utility mod: it replaces the loader and resolves types in all
	/// assemblies. Our postfix runs last and skips any row name already registered, so each action is
	/// registered once whichever mod gets to it.
	/// </summary>
	[HarmonyPatch(typeof(CharacterActionsDatabase), "LoadActions")]
	internal static class ActionRegistrationPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("ActionRegistration", ex);
		}

		private const string ActionsPath = "Crew/Actions";

		[HarmonyPriority(Priority.Last)]
		private static void Postfix(
			Dictionary<string, ActionInfo> ___actionsByName,
			Dictionary<string, List<ActionInfo>> ___actionsByActivatorTag,
			Dictionary<string, List<ActionInfo>> ___actionsByCharacterTag)
		{
			try
			{
				int added = RegisterModRows(___actionsByName, ___actionsByActivatorTag, ___actionsByCharacterTag);
				DcLog.Debug("action registration: " + added + " mod action rows registered");
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("ActionRegistration", e);
			}
		}

		private static int RegisterModRows(
			Dictionary<string, ActionInfo> byName,
			Dictionary<string, List<ActionInfo>> byActivatorTag,
			Dictionary<string, List<ActionInfo>> byCharacterTag)
		{
			if (byName == null || byActivatorTag == null || byCharacterTag == null)
			{
				DcLog.ErrorOnce("registration:fields", "action registration skipped: the action database layout changed");
				return 0;
			}
			int added = 0;
			foreach (GameDataRow row in Tunables.GetRows(ActionsPath))
			{
				string name = row.GetString(0);
				string typeName = row.GetString(1);
				if (string.IsNullOrEmpty(name) || !ActionRowRules.IsModTypeName(typeName))
					continue;
				if (byName.ContainsKey(name))
					continue; // registered already (by the game or by another mod)

				CharacterAction template = CreateTemplate(row, name, typeName.Trim());
				if (template == null)
					continue;

				var unknown = new List<string>();
				ActionContextSet contexts = ActionRowRules.ParseContexts(row.GetArray(5), unknown);
				if (unknown.Count > 0)
					DcLog.WarnOnce("registration:contexts:" + name, "action row " + name + ": unknown contexts ignored: " + string.Join(", ", unknown.ToArray()));
				if (contexts.IsEmpty)
					DcLog.WarnOnce("registration:nocontext:" + name, "action row " + name + " has no context; it will never be offered");

				string[] activatorTags = ActionRowRules.CleanTags(row.GetArray(2));
				string[] characterTags = ActionRowRules.CleanTags(row.GetArray(3));
				var info = new ActionInfo(name, template, activatorTags, characterTags, ToGame(contexts), row.GetString(6), row.GetString(7));

				byName[name] = info;
				AddToIndex(byActivatorTag, activatorTags, info);
				AddToIndex(byCharacterTag, characterTags, info);
				added++;
			}
			return added;
		}

		private static CharacterAction CreateTemplate(GameDataRow row, string name, string typeName)
		{
			Type type = typeof(ActionRegistrationPatch).Assembly.GetType(typeName, false);
			if (type == null)
			{
				DcLog.ErrorOnce("registration:type:" + name, "action row " + name + ": type " + typeName + " not found in the mod assembly");
				return null;
			}
			if (type.IsAbstract || !typeof(CharacterAction).IsAssignableFrom(type))
			{
				DcLog.ErrorOnce("registration:type:" + name, "action row " + name + ": " + typeName + " is not a concrete CharacterAction");
				return null;
			}
			try
			{
				return (CharacterAction)System.Activator.CreateInstance(type, row);
			}
			catch (Exception e)
			{
				DcLog.Exception("action row " + name + " (" + typeName + ") constructor", e);
				return null;
			}
		}

		private static ActionContext ToGame(ActionContextSet set)
		{
			ActionContext result = (ActionContext)0;
			if (set.Normal)
				result |= ActionContext.Normal;
			if (set.Queue)
				result |= ActionContext.Queue;
			if (set.NoCharacter)
				result |= ActionContext.NoCharacter;
			if (set.Upgrade)
				result |= ActionContext.Upgrade;
			return result;
		}

		private static void AddToIndex(Dictionary<string, List<ActionInfo>> index, string[] tags, ActionInfo info)
		{
			foreach (string tag in tags)
			{
				List<ActionInfo> list;
				if (!index.TryGetValue(tag, out list))
				{
					list = new List<ActionInfo>();
					index[tag] = list;
				}
				list.Add(info);
			}
		}
	}
}
