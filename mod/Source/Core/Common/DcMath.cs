using System;

namespace DamageControl.Core.Common
{
	/// <summary>Small numeric helpers shared by every workstream. All are total: NaN never escapes.</summary>
	public static class DcMath
	{
		/// <summary>Default tolerance for "equal" comparisons of game quantities (D7: no exact float equality).</summary>
		public const float Epsilon = 1e-4f;

		/// <summary>Clamps a fraction to [0, 1]; NaN becomes 0.</summary>
		public static float Clamp01(float value)
		{
			if (float.IsNaN(value) || value < 0f)
				return 0f;
			return value > 1f ? 1f : value;
		}

		/// <summary>Clamps to [min, max]; NaN becomes min. If min &gt; max, returns min.</summary>
		public static float Clamp(float value, float min, float max)
		{
			if (float.IsNaN(value) || value < min)
				return min;
			return value > max ? (max < min ? min : max) : value;
		}

		/// <summary>Clamps to [min, max]; NaN becomes min. If min &gt; max, returns min.</summary>
		public static double Clamp(double value, double min, double max)
		{
			if (double.IsNaN(value) || value < min)
				return min;
			return value > max ? (max < min ? min : max) : value;
		}

		/// <summary>|a - b| &lt;= epsilon. NaN is never approximately equal to anything.</summary>
		public static bool Approximately(float a, float b, float epsilon = Epsilon)
		{
			return Math.Abs(a - b) <= epsilon;
		}

		/// <summary>|a - b| &lt;= epsilon. NaN is never approximately equal to anything.</summary>
		public static bool Approximately(double a, double b, double epsilon = Epsilon)
		{
			return Math.Abs(a - b) <= epsilon;
		}

		/// <summary>True for a real, finite number.</summary>
		public static bool IsFinite(float value)
		{
			return !float.IsNaN(value) && !float.IsInfinity(value);
		}

		/// <summary>True for a real, finite number.</summary>
		public static bool IsFinite(double value)
		{
			return !double.IsNaN(value) && !double.IsInfinity(value);
		}
	}
}
