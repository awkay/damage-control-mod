using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DamageControl.Core.Common;
using DamageControl.Core.Repair;
using DamageControl.Game.Infrastructure;
using DamageControl.Game.Settings;
using HarmonyLib;
using UBOAT.Game.Scene.Characters;
using UBOAT.Game.Scene.Characters.Actions;
using UBOAT.Game.Scene.Characters.Activators;
using GameActivator = UBOAT.Game.Scene.Characters.Activators.Activator;

namespace DamageControl.Game.Repair
{
	/// <summary>
	/// R5 (04 §2.2, 00 §4): the vanilla salvage order keeps its code, time scale and visual clue; three
	/// transpilers replace each of its <c>2f</c> scrap constants (threshold, AI profit, amount consumed and
	/// the "enough left" check) with a call to <see cref="ScrapPerPart"/>. A method whose constant count is
	/// not the expected one is left unpatched and logged, and then the ratio stays vanilla's everywhere.
	/// </summary>
	public static class SalvageRuntime
	{
		private static readonly SalvagePatchState state = new SalvagePatchState();
		private static bool initialized;
		private static bool loaded;
		private static float configured = SalvageRatio.DefaultScrapPerPart;

		/// <summary>Scrap per part as the patched game code sees it. Called from the game's salvage methods.</summary>
		public static float ScrapPerPart()
		{
			if (!initialized)
			{
				initialized = true;
				DcSettings.SessionStarted += delegate { loaded = false; };
			}
			if (!loaded)
			{
				loaded = true;
				configured = SalvageRatio.Sanitize(Tunables.GetFloat(Tunables.Root + RepairText.TunableSalvageScrapPerPart, SalvageRatio.DefaultScrapPerPart));
				if (!state.AllApplied)
					DcLog.WarnOnce("SalvageRatio", "salvage ratio: the game's salvage code did not match; keeping the vanilla " + SalvageRatio.VanillaScrapPerPart + " scrap per part");
				else
					DcLog.Debug("salvage ratio: " + configured + " scrap per part");
			}
			return SalvageRatio.Effective(state.AllApplied, configured);
		}

		internal static IEnumerable<CodeInstruction> Rewrite(string method, IEnumerable<CodeInstruction> instructions)
		{
			List<CodeInstruction> list = new List<CodeInstruction>(instructions);
			try
			{
				int found = 0;
				for (int i = 0; i < list.Count; i++)
				{
					if (IsVanillaRatio(list[i]))
						found++;
				}
				if (!state.Record(method, found))
				{
					DcLog.Warn("salvage ratio: SalvageReplacementParts." + method + " has " + found + " scrap constants, expected "
						+ SalvagePatchState.ExpectedMatches(method) + "; left unpatched");
					return list;
				}
				MethodInfo getter = AccessTools.Method(typeof(SalvageRuntime), "ScrapPerPart");
				for (int i = 0; i < list.Count; i++)
				{
					if (IsVanillaRatio(list[i]))
					{
						// Same stack effect (pushes one float); labels and exception blocks stay on the instruction.
						list[i].opcode = OpCodes.Call;
						list[i].operand = getter;
					}
				}
			}
			catch (Exception e)
			{
				// Any call already swapped in returns the vanilla ratio while the swap is incomplete.
				state.Record(method, -1);
				DcLog.PatchFailed("SalvageRatio." + method, e);
			}
			return list;
		}

		private static bool IsVanillaRatio(CodeInstruction instruction)
		{
			return instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float && (float)instruction.operand == SalvageRatio.VanillaScrapPerPart;
		}
	}

	[HarmonyPatch(typeof(SalvageReplacementParts), "GetState", new Type[] { typeof(GameActivator), typeof(PlayableCharacter), typeof(bool), typeof(bool), typeof(ActionContext) })]
	internal static class SalvageStateRatioPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("SalvageStateRatio", ex);
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return SalvageRuntime.Rewrite("GetState", instructions);
		}
	}

	[HarmonyPatch(typeof(SalvageReplacementParts), "GetActionProfit", new Type[] { typeof(GameActivator), typeof(PlayableCharacter) })]
	internal static class SalvageProfitRatioPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("SalvageProfitRatio", ex);
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return SalvageRuntime.Rewrite("GetActionProfit", instructions);
		}
	}

	[HarmonyPatch(typeof(SalvageReplacementParts), "UpdateAction")]
	internal static class SalvageUpdateRatioPatch
	{
		private static Exception Cleanup(Exception ex)
		{
			return PatchGuard.Cleanup("SalvageUpdateRatio", ex);
		}

		private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
		{
			return SalvageRuntime.Rewrite("UpdateAction", instructions);
		}
	}
}
