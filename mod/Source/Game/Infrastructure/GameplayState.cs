using DWS.Common.InjectionFramework;
using UBOAT.Game.Core;
using UBOAT.Game.Core.Serialization;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>
	/// Whether the mod's simulation and hatch rules may act: the game is in its Gameplay state (the main
	/// scene is loaded) and no save is being loaded. In the main menu, during scene loading and while a save
	/// restores the boat, the gated patches stand aside and vanilla runs, so a half-restored boat (for
	/// example a hatch whose saved "open" state is being applied) is never judged by the mod's rules.
	/// Main thread only.
	/// </summary>
	public static class GameplayState
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameState gameState;

		[Inject]
		private static SavesManager savesManager;
		#pragma warning restore 0649

		/// <summary>True in the Gameplay state with no save load in progress.</summary>
		public static bool IsActive
		{
			get
			{
				if (gameState == null || gameState.CurrentState != GameState.State.Gameplay)
					return false;
				return savesManager == null || !savesManager.IsLoading;
			}
		}
	}
}
