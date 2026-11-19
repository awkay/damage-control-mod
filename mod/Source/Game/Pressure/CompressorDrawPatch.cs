using System;
using DamageControl.Core.Common;
using DamageControl.Core.Pressure;
using DamageControl.Game.Infrastructure;
using HarmonyLib;
using UBOAT.Game.Core.Data;
using UBOAT.Game.Scene.Items;
using UBOAT.Game.Scene.Physics;

namespace DamageControl.Game.Pressure
{
	/// <summary>What the prefix hands to the postfix for one compressor frame.</summary>
	internal struct CompressorFrame
	{
		public bool Active;
		public Compartment Room;
		public double TankAirBefore;
		public int Chunks;
	}

	/// <summary>
	/// P10 (01 §2.10, 00 §4, C30): no replacement. The prefix skips the vanilla compressor frame when
	/// the compressor's compartment is at or below 0.35 atm or holds less air than one transfer; the
	/// postfix charges the compartment for the air the tanks actually gained (<see cref="CompressorDraw"/>).
	/// </summary>
	[HarmonyPatch(typeof(AirCompressor), "Update")]
	internal static class CompressorDrawPatch
	{
		private static bool Prefix(AirCompressor __instance, Tank[] ___tanks, Parameter ___oxygenCompression, float ___maxPressure, out CompressorFrame __state)
		{
			__state = new CompressorFrame();
			try
			{
				if (___tanks == null || ___tanks.Length == 0 || ___oxygenCompression == null)
					return true;
				Compartment room = PressureLayout.CompartmentOf(__instance);
				if (room == null)
				{
					DcLog.WarnOnce("pressure:compressor-room", "air compressor \"" + __instance.name + "\" is in no compartment; it does not draw room air");
					return true;
				}
				float frameSeconds = GameClock.Now().GameDt;
				float firstChunk = CompressorDraw.ChunkLength(frameSeconds, 0);
				float rate = ___oxygenCompression.Value;
				double nextTransfer = 0.0;
				double tankAir = 0.0;
				for (int i = 0; i < ___tanks.Length; i++)
				{
					Tank tank = ___tanks[i];
					if (tank == null)
						return true; // unexpected layout: leave vanilla alone
					tankAir += tank.Air;
					double transfer = CompressorDraw.Transfer(firstChunk, rate, ___tanks.Length, tank.AirPressure, ___maxPressure);
					if (transfer > nextTransfer)
						nextTransfer = transfer;
				}
				if (!CompressorDraw.MayCompress(room.AirPressure, room.Air, nextTransfer))
					return false;
				__state.Active = true;
				__state.Room = room;
				__state.TankAirBefore = tankAir;
				__state.Chunks = CompressorDraw.ChunkCount(frameSeconds);
				return true;
			}
			catch (Exception e)
			{
				__state.Active = false;
				DcLog.PatchFailed("CompressorDraw", e);
				return true;
			}
		}

		private static void Postfix(Tank[] ___tanks, CompressorFrame __state)
		{
			if (!__state.Active)
				return;
			try
			{
				double tankAir = 0.0;
				for (int i = 0; i < ___tanks.Length; i++)
					tankAir += ___tanks[i].Air;
				double cost = CompressorDraw.FrameCost(tankAir - __state.TankAirBefore, __state.Chunks, ___tanks.Length);
				if (cost > 0.0)
				{
					double air = __state.Room.Air - cost;
					__state.Room.Air = air > 0.0 ? air : 0.0;
				}
			}
			catch (Exception e)
			{
				DcLog.PatchFailed("CompressorDraw", e);
			}
		}
	}
}
