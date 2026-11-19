using DamageControl.Core.Hatches;
using Xunit;

namespace DamageControl.Tests
{
    public class HatchSealsTests
    {
        [Fact]
        public void UnsealedHatchHasFactorOne()
        {
            var seals = new HatchSeals();
            var hatch = new object();
            Assert.Equal(0, seals.Count(hatch));
            Assert.False(seals.IsSealed(hatch));
            Assert.Equal(1f, seals.SealFactor(hatch));
            Assert.Equal(0.02f, seals.ClosedLeakMultiplier(hatch), 6);
        }

        [Fact]
        public void Criterion02_15_PresenceOfEachSourceCutsTheLeakToATenth()
        {
            // C9: blocker x0.1, repair x0.1, multiplied; the leak factor is 0.02 times their product.
            var seals = new HatchSeals();
            var hatch = new object();
            var blocker = new object();
            var repairer = new object();
            Assert.True(seals.Add(hatch, SealSource.Blocker, blocker));
            Assert.Equal(0.1f, seals.SealFactor(hatch), 6);
            Assert.Equal(0.002f, seals.ClosedLeakMultiplier(hatch), 6);
            Assert.True(seals.Add(hatch, SealSource.Repair, repairer));
            Assert.Equal(0.01f, seals.SealFactor(hatch), 6);
            Assert.Equal(0.0002f, seals.ClosedLeakMultiplier(hatch), 7);
            Assert.Equal(1, seals.Count(hatch, SealSource.Blocker));
            Assert.Equal(1, seals.Count(hatch, SealSource.Repair));
        }

        [Fact]
        public void AddIsIdempotentPerOwner()
        {
            var seals = new HatchSeals();
            var hatch = new object();
            var owner = new object();
            Assert.True(seals.Add(hatch, SealSource.Blocker, owner));
            Assert.False(seals.Add(hatch, SealSource.Blocker, owner));
            Assert.False(seals.Add(hatch, SealSource.Repair, owner));
            Assert.Equal(1, seals.Count(hatch));
            Assert.Equal(1, seals.Count(hatch, SealSource.Repair));
            Assert.True(seals.Has(hatch, owner));
        }

        [Fact]
        public void RemoveDropsOnlyThatOwner()
        {
            var seals = new HatchSeals();
            var hatch = new object();
            var a = new object();
            var b = new object();
            seals.Add(hatch, SealSource.Blocker, a);
            seals.Add(hatch, SealSource.Repair, b);
            Assert.True(seals.Remove(hatch, a));
            Assert.False(seals.Remove(hatch, a));
            Assert.Equal(1, seals.Count(hatch));
            Assert.False(seals.Has(hatch, a));
            Assert.True(seals.Has(hatch, b));
            Assert.True(seals.Remove(hatch, b));
            Assert.False(seals.IsSealed(hatch));
        }

        [Fact]
        public void RemoveOwnerClearsItAcrossHatches()
        {
            var seals = new HatchSeals();
            var h1 = new object();
            var h2 = new object();
            var owner = new object();
            var other = new object();
            seals.Add(h1, SealSource.Repair, owner);
            seals.Add(h2, SealSource.Repair, owner);
            seals.Add(h2, SealSource.Blocker, other);
            Assert.Equal(2, seals.RemoveOwner(owner));
            Assert.Equal(0, seals.Count(h1));
            Assert.Equal(1, seals.Count(h2));
        }

        [Fact]
        public void ClearAndClearHatchForgetSeals()
        {
            var seals = new HatchSeals();
            var h1 = new object();
            var h2 = new object();
            seals.Add(h1, SealSource.Blocker, new object());
            seals.Add(h2, SealSource.Blocker, new object());
            seals.ClearHatch(h1);
            Assert.False(seals.IsSealed(h1));
            Assert.True(seals.IsSealed(h2));
            seals.Clear();
            Assert.False(seals.IsSealed(h2));
        }

        [Fact]
        public void KeysCompareByReferenceAndNullsAreIgnored()
        {
            var seals = new HatchSeals();
            Assert.False(seals.Add(null, SealSource.Blocker, new object()));
            Assert.False(seals.Add(new object(), SealSource.Blocker, null));
            Assert.False(seals.Remove(null, null));
            Assert.Equal(0, seals.Count(null));
            // Two equal-by-value keys are still two hatches.
            seals.Add("hatch", SealSource.Blocker, new object());
            Assert.False(seals.IsSealed(new string('h', 1) + "atch"));
        }

        [Fact]
        public void FactorForMultipliesPerSource()
        {
            Assert.Equal(1f, HatchSeals.FactorFor(0));
            Assert.Equal(0.1f, HatchSeals.FactorFor(1), 6);
            Assert.Equal(0.001f, HatchSeals.FactorFor(3), 7);
        }
    }
}
