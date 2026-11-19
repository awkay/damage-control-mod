namespace DamageControl.Core.Pressure
{
	/// <summary>
	/// P3 (01 §2.3, 00 C23): rebreathers last 2.5 times as long. In 2026.1 a rebreather is an item that
	/// counts its usage time against a "Durability" parameter, so the deterministic equivalent of
	/// "skip 60 % of updates" is one x2.5 scale modifier on that parameter: consumption per second
	/// relative to durability becomes exactly 0.4 of vanilla.
	/// </summary>
	public static class RebreatherEndurance
	{
		/// <summary>Scale applied to the rebreather's durability.</summary>
		public const float DurabilityScale = 2.5f;

		/// <summary>Name of the scale modifier the Game layer adds (one per rebreather, never stacked).</summary>
		public const string ModifierName = "DC Rebreather Endurance";

		/// <summary>Durability with the modifier applied.</summary>
		public static float ScaledDurability(float baseDurability)
		{
			return baseDurability * DurabilityScale;
		}

		/// <summary>
		/// Fraction of the rebreather's durability used per second of wear, relative to vanilla
		/// (vanilla = 1). Equals 1 / <see cref="DurabilityScale"/> = 0.4.
		/// </summary>
		public static float RelativeConsumption
		{
			get { return 1f / DurabilityScale; }
		}

		/// <summary>
		/// Remaining durability (0..1) after <paramref name="usageSeconds"/> of wear, the way the game
		/// computes it (1 - usage / durability), with the modifier applied when <paramref name="scaled"/>.
		/// </summary>
		public static float Remaining(float usageSeconds, float baseDurability, bool scaled)
		{
			float durability = scaled ? ScaledDurability(baseDurability) : baseDurability;
			if (!(durability > 0f))
				return 0f;
			float remaining = 1f - usageSeconds / durability;
			return remaining < 0f ? 0f : remaining;
		}
	}
}
