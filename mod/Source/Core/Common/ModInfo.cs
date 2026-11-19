namespace DamageControl.Core.Common
{
	/// <summary>
	/// Mod identity. Pure logic lives under Source/Core: files there must not reference UnityEngine,
	/// game assemblies or Harmony. ./verify compiles Core alone against the BCL, and the unit tests
	/// compile it on current .NET.
	/// </summary>
	public static class ModInfo
	{
		public const string Name = "Damage Control";
		public const string Version = "0.1.0";

		/// <summary>Prefix of every log line the mod writes.</summary>
		public const string LogPrefix = "[Damage Control] ";

		/// <summary>Root namespace of every type the mod declares; data-sheet rows use it to mark our types.</summary>
		public const string RootNamespace = "DamageControl";

		public static string Describe()
		{
			return Name + " " + Version;
		}
	}
}
