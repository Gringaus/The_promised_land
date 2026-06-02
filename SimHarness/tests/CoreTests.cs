using FoundersLands.Simulation.Core;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class StableHashTests
    {
        [Fact]
        public void Fnv1a_IsStableForSameInput()
        {
            Assert.Equal(StableHash.Fnv1a64("green-valley"), StableHash.Fnv1a64("green-valley"));
        }

        [Fact]
        public void Fnv1a_DiffersForDifferentInput()
        {
            Assert.NotEqual(StableHash.Fnv1a64("green-valley"), StableHash.Fnv1a64("northern-marsh"));
        }

        [Fact]
        public void Fnv1a_KnownVector()
        {
            // FNV-1a 64-bit of the empty string is the offset basis; "a" has a known value.
            Assert.Equal(StableHash.Fnv1aOffset, StableHash.Fnv1a64(""));
            Assert.Equal(0xAF63DC4C8601EC8CUL, StableHash.Fnv1a64("a"));
        }

        [Fact]
        public void Combine_OrderMatters()
        {
            ulong a = StableHash.Combine(StableHash.Combine(StableHash.Fnv1aOffset, 1), 2);
            ulong b = StableHash.Combine(StableHash.Combine(StableHash.Fnv1aOffset, 2), 1);
            Assert.NotEqual(a, b);
        }
    }

    public class DeterministicRngTests
    {
        [Fact]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new DeterministicRng(12345);
            var b = new DeterministicRng(12345);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(a.NextULong(), b.NextULong());
            }
        }

        [Fact]
        public void DifferentSeed_ProducesDifferentSequence()
        {
            var a = new DeterministicRng(1);
            var b = new DeterministicRng(2);
            Assert.NotEqual(a.NextULong(), b.NextULong());
        }

        [Fact]
        public void NextFloat_IsInUnitInterval()
        {
            var rng = new DeterministicRng(99);
            for (int i = 0; i < 100000; i++)
            {
                float f = rng.NextFloat();
                Assert.True(f >= 0f && f < 1f, $"value out of [0,1): {f}");
            }
        }

        [Fact]
        public void NextInt_RespectsBounds()
        {
            var rng = new DeterministicRng(7);
            for (int i = 0; i < 100000; i++)
            {
                int v = rng.NextInt(5, 10);
                Assert.InRange(v, 5, 9);
            }
        }

        [Fact]
        public void Streams_AreIndependentPerStreamId()
        {
            var s1 = DeterministicRng.Stream(42, 1);
            var s2 = DeterministicRng.Stream(42, 2);
            Assert.NotEqual(s1.NextULong(), s2.NextULong());
        }

        [Fact]
        public void Streams_AreReproducible()
        {
            var s1 = DeterministicRng.Stream(42, 1);
            var s2 = DeterministicRng.Stream(42, 1);
            Assert.Equal(s1.NextULong(), s2.NextULong());
        }
    }
}
