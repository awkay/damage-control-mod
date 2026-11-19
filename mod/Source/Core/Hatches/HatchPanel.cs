using System;
using System.Collections.Generic;
using System.Globalization;

namespace DamageControl.Core.Hatches
{
	/// <summary>Colour band of a side pressure on the hatch panel.</summary>
	public enum PressureBand
	{
		Normal,
		Low,
		High
	}

	/// <summary>The panel's localized texts. Formats take one argument ({0}).</summary>
	public sealed class HatchPanelText
	{
		public string LockedWarning = "";
		public string OpeningSideFormat = "{0:0.00}";
		public string ClosingSideFormat = "{0:0.00}";
		public string DurabilityFormat = "{0:0}";
		public string BadlyDamaged = "";
		public string PartiallySealed = "";
		public string Evacuated = "";
		public IFormatProvider Culture = CultureInfo.CurrentCulture;
	}

	/// <summary>What the hatch panel shows (02 §2.4), as plain values.</summary>
	public struct HatchPanelInput
	{
		public bool ShowLockedWarning;

		/// <summary>Seat side ("opening side"): air pressure plus water head above lowY, atm.</summary>
		public double OpeningSideAtm;

		/// <summary>Far side ("closing side"), same formula.</summary>
		public double ClosingSideAtm;

		public bool Intact;
		public float DesignPressure;
		public bool PartiallySealed;
		public bool ShowEvacuated;

		/// <summary>Diagnostic lines (R14), shown only when not null or empty.</summary>
		public string Debug;
	}

	/// <summary>H6: hatch info panel text.</summary>
	public static class HatchPanel
	{
		public const double HighPressure = 2.0;
		public const double LowPressure = 0.8;
		public const string WarningColor = "red";
		public const string HighColor = "red";
		public const string LowColor = "#6fa8ff";
		public const string SealedColor = "#7fd67f";

		/// <summary>Red above 2.0 atm, blue below 0.8 atm, neutral otherwise.</summary>
		public static PressureBand Classify(double atm)
		{
			if (atm > HighPressure)
				return PressureBand.High;
			if (atm < LowPressure)
				return PressureBand.Low;
			return PressureBand.Normal;
		}

		/// <summary>
		/// For the first-person viewer, the compartment beyond the hatch: a character whose compartment index
		/// is at most the hatch's left index looks at the right side; one outside any compartment counts as
		/// index 4 (02 §2.4 item 7).
		/// </summary>
		public static bool ViewerFarSideIsRight(bool viewerHasCompartment, int viewerIndex, int leftIndex)
		{
			int index = viewerHasCompartment ? viewerIndex : 4;
			return index <= leftIndex;
		}

		/// <summary>Builds the panel, lines in the order of 02 §2.4.</summary>
		public static string Build(HatchPanelInput input, HatchPanelText text)
		{
			IFormatProvider culture = text.Culture ?? CultureInfo.CurrentCulture;
			var lines = new List<string>();
			if (input.ShowLockedWarning)
				lines.Add(Colour(text.LockedWarning, WarningColor));
			lines.Add(Pressure(text.OpeningSideFormat, input.OpeningSideAtm, culture));
			lines.Add(Pressure(text.ClosingSideFormat, input.ClosingSideAtm, culture));
			lines.Add("");
			if (input.Intact)
				lines.Add(string.Format(culture, text.DurabilityFormat, input.DesignPressure));
			else
				lines.Add(Colour(text.BadlyDamaged, WarningColor));
			if (input.PartiallySealed)
				lines.Add(Colour(text.PartiallySealed, SealedColor));
			if (!string.IsNullOrEmpty(input.Debug))
				lines.Add(input.Debug);
			if (input.ShowEvacuated)
				lines.Add(text.Evacuated);
			return string.Join("\n", lines.ToArray());
		}

		private static string Pressure(string format, double atm, IFormatProvider culture)
		{
			string line = string.Format(culture, format, atm);
			switch (Classify(atm))
			{
				case PressureBand.High:
					return Colour(line, HighColor);
				case PressureBand.Low:
					return Colour(line, LowColor);
				default:
					return line;
			}
		}

		private static string Colour(string line, string colour)
		{
			return "<color=" + colour + ">" + line + "</color>";
		}

	}
}
