using System;
using System.Collections.Generic;
using DamageControl.Core.Common;
using DWS.Common.InjectionFramework;
using DWS.Common.Resources;
using HarmonyLib;
using UnityEngine;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>
	/// R12: serves the mod's raw PNG icons to the game UI without an asset bundle.
	///
	/// The game registers every file under the mod's Assets/ folder as a Texture2D resource keyed by
	/// its relative path with extension (ResourceManager.AddFileResource), but its UI asks for
	/// extension-less Sprite keys such as "Icons/InteractionWheelUI/&lt;IconName&gt;". At startup we load each
	/// texture under "Icons/DamageControl/" (convention in <see cref="IconKeys"/>), wrap it in a Sprite
	/// (or keep the Texture2D for the cursor category), and insert a DirectResource under the game key
	/// in ResourceManager's private resource map. The stock lookup then finds it; no lookup is patched.
	/// Missing art is not fatal: the game falls back to its generic icon.
	/// </summary>
	public static class IconRegistry
	{
		private const float PixelsPerUnit = 100f;

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static ResourceManager resourceManager;
		#pragma warning restore 0649

		private static readonly object Owner = new object();
		private static readonly Dictionary<string, UnityEngine.Object> served = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
		private static AccessTools.FieldRef<ResourceManager, Dictionary<string, IResource>> resourcesField;
		private static int pending;

		/// <summary>Game keys currently served, e.g. "Icons/InteractionWheelUI/DC Enable Hatch Pressure Blocking".</summary>
		public static ICollection<string> ServedKeys
		{
			get { return served.Keys; }
		}

		/// <summary>True once <paramref name="gameKey"/> has a loaded asset.</summary>
		public static bool IsServed(string gameKey)
		{
			return gameKey != null && served.ContainsKey(gameKey);
		}

		/// <summary>Starts loading every raw icon of the mod. Safe to call more than once; later calls re-insert loaded icons.</summary>
		public static void LoadAll()
		{
			if (resourceManager == null)
			{
				DcLog.Warn("icons not loaded: ResourceManager is not available");
				return;
			}
			if (resourcesField == null)
				resourcesField = AccessTools.FieldRefAccess<ResourceManager, Dictionary<string, IResource>>("resources");

			string[] keys = resourceManager.GetAssetsBelow(IconKeys.RawRoot);
			if (keys == null || keys.Length == 0)
			{
				DcLog.Warn("no raw icons found under Assets/" + IconKeys.RawRoot);
				return;
			}

			foreach (string rawKey in keys)
			{
				IconTarget target;
				string error;
				if (!IconKeys.TryMapRawAsset(rawKey, out target, out error))
				{
					DcLog.WarnOnce("icon:" + rawKey, "icon " + rawKey + " skipped: " + error);
					continue;
				}
				UnityEngine.Object existing;
				if (served.TryGetValue(target.GameKey, out existing) && existing != null)
				{
					Insert(target.GameKey, existing);
					continue;
				}
				Load(rawKey, target);
			}
		}

		private static void Load(string rawKey, IconTarget target)
		{
			pending++;
			resourceManager.RetrieveAssetAsync<Texture2D>(rawKey, Owner, delegate (Resource<Texture2D> loaded)
			{
				pending--;
				try
				{
					OnLoaded(rawKey, target, loaded.Asset);
				}
				catch (Exception e)
				{
					DcLog.Exception("IconRegistry " + rawKey, e);
				}
				if (pending == 0)
					DcLog.Info("icons ready: " + served.Count + " served");
			});
		}

		private static void OnLoaded(string rawKey, IconTarget target, Texture2D texture)
		{
			if (texture == null)
			{
				DcLog.WarnOnce("icon:" + rawKey, "icon " + rawKey + " failed to load");
				return;
			}
			texture.name = target.IconName;
			UnityEngine.Object asset;
			if (target.Kind == IconAssetKind.Texture)
			{
				asset = texture;
			}
			else
			{
				Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), PixelsPerUnit);
				sprite.name = target.IconName;
				asset = sprite;
			}
			served[target.GameKey] = asset;
			Insert(target.GameKey, asset);
			DcLog.Debug("icon " + rawKey + " -> " + target.GameKey);
		}

		private static void Insert(string gameKey, UnityEngine.Object asset)
		{
			Dictionary<string, IResource> resources = resourcesField(resourceManager);
			// Our keys all start with "DC ", so this never replaces a base-game icon. It does replace a
			// placeholder entry the game may have cached if the UI asked for the key before we loaded.
			resources[gameKey] = new DirectResource(asset);
		}
	}
}
