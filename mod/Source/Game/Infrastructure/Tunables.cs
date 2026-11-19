using System;
using System.Globalization;
using DamageControl.Core.Common;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Data;

namespace DamageControl.Game.Infrastructure
{
	/// <summary>
	/// Null-safe reads of the mod's own data rows. Mod tunables live in our General sheet, worksheet
	/// "Settings", group "/Damage Control" (column "Value"), so their paths are
	/// "General/Settings/Damage Control/&lt;Name&gt;". A missing or malformed row yields the caller's
	/// default and one warning per path per session.
	/// </summary>
	public static class Tunables
	{
		public const string Root = "General/Settings/Damage Control/";

		/// <summary>R14 runtime debug flag (False in the shipped sheet).</summary>
		public const string DebugPath = Root + "Debug";

		#pragma warning disable 0649 // assigned by the game's injection framework
		[Inject]
		private static GameData gameData;
		#pragma warning restore 0649

		/// <summary>The row at <paramref name="path"/>, or null when it does not exist (or GameData is not up yet).</summary>
		public static GameDataRow GetRow(string path)
		{
			if (gameData == null || string.IsNullOrEmpty(path))
				return null;
			return gameData.GetDataRow(path);
		}

		/// <summary>Column 1 ("Value") of the row at <paramref name="path"/>, or null.</summary>
		public static string GetString(string path)
		{
			GameDataRow row = GetRow(path);
			if (row == null)
				return null;
			string value = row.GetString(1);
			return value == null ? null : value.Trim();
		}

		public static bool GetBool(string path, bool defaultValue)
		{
			string text = GetString(path);
			if (text == null)
				return defaultValue;
			bool value;
			if (bool.TryParse(text, out value))
				return value;
			if (text == "1")
				return true;
			if (text == "0")
				return false;
			DcLog.WarnOnce("tunable:" + path, "data row " + path + " has \"" + text + "\", expected True or False; using " + defaultValue);
			return defaultValue;
		}

		public static float GetFloat(string path, float defaultValue)
		{
			string text = GetString(path);
			if (text == null)
				return defaultValue;
			float value;
			if (float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && DcMath.IsFinite(value))
				return value;
			DcLog.WarnOnce("tunable:" + path, "data row " + path + " has \"" + text + "\", expected a number; using " + defaultValue.ToString(CultureInfo.InvariantCulture));
			return defaultValue;
		}

		/// <summary>Rows under a data path (e.g. "Crew/Actions"); never null.</summary>
		public static GameDataRow[] GetRows(string path)
		{
			if (gameData == null)
				return new GameDataRow[0];
			GameDataRow[] rows = gameData.GetDataRows(path);
			return rows ?? new GameDataRow[0];
		}
	}
}
