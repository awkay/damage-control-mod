using DamageControl.Core.Automation;
using DamageControl.Game.Settings;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Serialization;
using UBOAT.Game.Scene.Entities;

namespace DamageControl.Game.Automation
{
	/// <summary>
	/// Game-side state of H13: the shared <see cref="CarryWoundedGate"/>, re-armed at every campaign
	/// session start (D7), and the game reads it needs. Main thread only.
	/// </summary>
	internal static class CrewAutomation
	{
		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static PlayerShip playerShip;

		[Inject]
		private static SavesManager savesManager;
		#pragma warning restore 0649

		static CrewAutomation()
		{
			DcSettings.SessionStarted += CarryWoundedGate.Shared.Arm;
		}

		public static CarryWoundedGate Gate
		{
			get { return CarryWoundedGate.Shared; }
		}

		/// <summary>Crewmen lying injured (ragdolled) on the player boat; 0 without a boat.</summary>
		public static int InjuredLying
		{
			get
			{
				PlayerShip ship = playerShip;
				return ship != null && ship.RagdollCrew != null ? ship.RagdollCrew.Count : 0;
			}
		}

		public static bool IsLoading
		{
			get { return savesManager != null && savesManager.IsLoading; }
		}

		public static PlayerShip Ship
		{
			get { return playerShip; }
		}
	}
}
