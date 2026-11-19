using System;
using System.Collections.Generic;

namespace DamageControl.Core.Common
{
	/// <summary>The contexts named in a Crew/Actions row (column F). The Game layer maps these onto the game's ActionContext flags.</summary>
	public struct ActionContextSet
	{
		public readonly bool Normal;
		public readonly bool Queue;
		public readonly bool NoCharacter;
		public readonly bool Upgrade;

		public ActionContextSet(bool normal, bool queue, bool noCharacter, bool upgrade)
		{
			Normal = normal;
			Queue = queue;
			NoCharacter = noCharacter;
			Upgrade = upgrade;
		}

		public bool IsEmpty
		{
			get { return !Normal && !Queue && !NoCharacter && !Upgrade; }
		}
	}

	/// <summary>
	/// Rules for the Crew/Actions rows our registration postfix handles (R10, D2). They mirror how the
	/// game's own loader reads a row: tokens are trimmed, and context names match exactly.
	/// </summary>
	public static class ActionRowRules
	{
		public const string ContextNormal = "Normal";
		public const string ContextQueue = "Queue";
		public const string ContextNoCharacter = "NoCharacter";
		public const string ContextUpgrade = "Upgrade";

		/// <summary>
		/// True if column B names a type the mod declares (namespace-qualified, under <see cref="ModInfo.RootNamespace"/>).
		/// Only these rows are registered by our postfix; every other row is left to the game.
		/// </summary>
		public static bool IsModTypeName(string typeName)
		{
			if (string.IsNullOrEmpty(typeName))
				return false;
			string name = typeName.Trim();
			return name.StartsWith(ModInfo.RootNamespace + ".", StringComparison.Ordinal)
				&& name.IndexOf(',') < 0
				&& name.Length > ModInfo.RootNamespace.Length + 1;
		}

		/// <summary>Parses the context tokens; unrecognised tokens are appended to <paramref name="unknown"/> when it is not null.</summary>
		public static ActionContextSet ParseContexts(string[] tokens, List<string> unknown)
		{
			bool normal = false, queue = false, noCharacter = false, upgrade = false;
			if (tokens != null)
			{
				foreach (string raw in tokens)
				{
					string token = raw == null ? "" : raw.Trim();
					if (token.Length == 0)
						continue;
					if (token == ContextNormal)
						normal = true;
					else if (token == ContextQueue)
						queue = true;
					else if (token == ContextNoCharacter)
						noCharacter = true;
					else if (token == ContextUpgrade)
						upgrade = true;
					else if (unknown != null)
						unknown.Add(token);
				}
			}
			return new ActionContextSet(normal, queue, noCharacter, upgrade);
		}

		/// <summary>Trims each tag and drops empty ones, as the game does when indexing tags.</summary>
		public static string[] CleanTags(string[] tags)
		{
			if (tags == null)
				return new string[0];
			var result = new List<string>(tags.Length);
			foreach (string raw in tags)
			{
				string tag = raw == null ? "" : raw.Trim();
				if (tag.Length > 0)
					result.Add(tag);
			}
			return result.ToArray();
		}
	}
}
