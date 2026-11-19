namespace DamageControl.Core
{
	/// <summary>
	/// Pure logic lives under Source/Core. Files here must not reference UnityEngine, game
	/// assemblies or Harmony: ./verify compiles this folder alone against the BCL, and the unit
	/// tests compile it on current .NET.
	/// </summary>
	public static class ModInfo
	{
		public const string Name = "Damage Control";
		public const string Version = "0.1.0";

		public static string Describe()
		{
			return Name + " " + Version;
		}

		/// <summary>Clamps a fraction to [0, 1]; NaN becomes 0.</summary>
		public static float Clamp01(float value)
		{
			if (float.IsNaN(value) || value < 0f)
				return 0f;
			return value > 1f ? 1f : value;
		}
	}
}
