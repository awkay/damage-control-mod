using System;

namespace DamageControl.Core.Common
{
	/// <summary>What kind of Unity object the game asks for under an icon category.</summary>
	public enum IconAssetKind
	{
		Sprite = 0,
		Texture = 1,
	}

	/// <summary>Where a raw PNG shipped by the mod is served in the game's resource namespace.</summary>
	public struct IconTarget
	{
		/// <summary>Game resource key, e.g. "Icons/InteractionWheelUI/DC Enable Hatch Pressure Blocking".</summary>
		public readonly string GameKey;

		/// <summary>Category folder, e.g. "InteractionWheelUI".</summary>
		public readonly string Category;

		/// <summary>Icon name as returned by CharacterAction.GetIconName, plus any " (Disabled)"/" (Highlighted)" suffix.</summary>
		public readonly string IconName;

		public readonly IconAssetKind Kind;

		public IconTarget(string category, string iconName, IconAssetKind kind)
		{
			Category = category;
			IconName = iconName;
			Kind = kind;
			GameKey = IconKeys.GameKey(category, iconName);
		}
	}

	/// <summary>
	/// Runtime PNG icon convention (R12, 00 §2.4). The game loads every file under the mod's
	/// <c>Assets/</c> folder as a raw texture keyed by its relative path, extension included. We ship
	/// icons as
	/// <code>mod/Assets/Icons/DamageControl/&lt;area&gt;/&lt;Category&gt;/&lt;IconName&gt;.png</code>
	/// and, at startup, serve each one under the extension-less key the game UI requests:
	/// <code>Icons/&lt;Category&gt;/&lt;IconName&gt;</code>
	/// Icon names must start with <see cref="IconNamePrefix"/> so they can never shadow a base-game icon.
	/// </summary>
	public static class IconKeys
	{
		/// <summary>Resource-key folder of all raw icons (relative to the mod's Assets/ folder).</summary>
		public const string RawRoot = "Icons/DamageControl";

		/// <summary>Every icon name the mod serves starts with this.</summary>
		public const string IconNamePrefix = "DC ";

		public const string DisabledSuffix = " (Disabled)";
		public const string HighlightedSuffix = " (Highlighted)";

		public const string InteractionWheel = "InteractionWheelUI";
		public const string Actions = "Actions";
		public const string ActionsCursor = "Actions - Cursor";
		public const string ActionsBanners = "Actions (Banners)";
		public const string StatusesPortraits = "Statuses (Portraits)";

		/// <summary>Categories the game UI looks icons up in, and the asset type it asks for.</summary>
		public static readonly string[] Categories = { InteractionWheel, Actions, ActionsCursor, ActionsBanners, StatusesPortraits };

		/// <summary>The asset kind the game requests for a category: a Texture2D for the cursor, a Sprite elsewhere.</summary>
		public static bool TryGetKind(string category, out IconAssetKind kind)
		{
			kind = IconAssetKind.Sprite;
			if (category == ActionsCursor)
			{
				kind = IconAssetKind.Texture;
				return true;
			}
			return Array.IndexOf(Categories, category) >= 0;
		}

		/// <summary>"Icons/&lt;category&gt;/&lt;iconName&gt;".</summary>
		public static string GameKey(string category, string iconName)
		{
			return "Icons/" + category + "/" + iconName;
		}

		/// <summary>
		/// Maps a raw asset key ("Icons/DamageControl/&lt;area&gt;/&lt;Category&gt;/&lt;IconName&gt;.png") to the key the
		/// game UI requests. Returns false with a reason for anything that does not follow the convention.
		/// </summary>
		public static bool TryMapRawAsset(string rawKey, out IconTarget target, out string error)
		{
			target = new IconTarget();
			error = null;
			if (string.IsNullOrEmpty(rawKey))
			{
				error = "empty key";
				return false;
			}
			string key = rawKey.Replace('\\', '/');
			if (!key.StartsWith(RawRoot + "/", StringComparison.Ordinal))
			{
				error = "not under " + RawRoot + "/";
				return false;
			}
			string ext = ExtensionOf(key);
			if (ext == null)
			{
				error = "not a .png or .jpg file";
				return false;
			}
			string[] parts = key.Substring(RawRoot.Length + 1).Split('/');
			if (parts.Length != 3)
			{
				error = "expected " + RawRoot + "/<area>/<Category>/<IconName>" + ext;
				return false;
			}
			string area = parts[0];
			string category = parts[1];
			string iconName = parts[2].Substring(0, parts[2].Length - ext.Length);
			if (area.Length == 0)
			{
				error = "empty area folder";
				return false;
			}
			IconAssetKind kind;
			if (!TryGetKind(category, out kind))
			{
				error = "unknown category \"" + category + "\" (expected one of: " + string.Join(", ", Categories) + ")";
				return false;
			}
			if (!iconName.StartsWith(IconNamePrefix, StringComparison.Ordinal) || iconName.Length == IconNamePrefix.Length)
			{
				error = "icon name \"" + iconName + "\" must start with \"" + IconNamePrefix + "\"";
				return false;
			}
			target = new IconTarget(category, iconName, kind);
			return true;
		}

		private static string ExtensionOf(string key)
		{
			if (key.EndsWith(".png", StringComparison.OrdinalIgnoreCase) || key.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
				return key.Substring(key.Length - 4);
			return null;
		}
	}
}
