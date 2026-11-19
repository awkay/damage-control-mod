using System;

namespace DamageControl.Core.Hatches
{
	/// <summary>P11: when the equalize-pressure order is offered (01 §2.11).</summary>
	public static class EqualizeRules
	{
		/// <summary>Sort priority among the hatch's orders.</summary>
		public const int SortingPriority = 11;

		/// <summary>
		/// Offered (subject to the game's usual activator and slot checks) only on a closed interior hatch
		/// that has both compartments and a pressure-blockade side. Hidden otherwise.
		/// </summary>
		public static bool IsOffered(bool isHatch, bool isExteriorHatch, bool locked, bool hasLeft, bool hasRight, bool hasSeat)
		{
			return isHatch && !isExteriorHatch && locked && hasLeft && hasRight && hasSeat;
		}
	}

	/// <summary>01 §6 #5: what an equipment pressure point attached to a hatch reports (used by WS1's P4 postfix).</summary>
	public static class HatchPressurePoint
	{
		/// <summary>
		/// An interior hatch measures the difference across it, |p_L - p_R|. Exterior hatches and other
		/// equipment report their owning compartment's pressure unchanged.
		/// </summary>
		public static double Reading(bool isInteriorHatch, double owningCompartmentPressure, double leftPressure, double rightPressure)
		{
			return isInteriorHatch ? Math.Abs(leftPressure - rightPressure) : owningCompartmentPressure;
		}
	}
}
