using HarmonyLib;
using UBOAT.Game;
using UnityEngine;

namespace DamageControl
{
	/// <summary>
	/// Entry point. The game instantiates the first IUserMod implementation it finds in the
	/// compiled mod assembly and calls OnLoaded() once, after [Inject] fields are filled.
	/// </summary>
	public sealed class DamageControlMod : IUserMod
	{
		public const string HarmonyId = "uboat.damage-control";

		public void OnLoaded()
		{
			Debug.Log("[Damage Control] " + Core.ModInfo.Describe() + " loaded; applying Harmony patches.");
			// PatchAll() with no arguments lets Harmony find the calling assembly itself, so this
			// file needs no System.Reflection reference.
			new Harmony(HarmonyId).PatchAll();
			Debug.Log("[Damage Control] Harmony patches applied.");
		}
	}
}
