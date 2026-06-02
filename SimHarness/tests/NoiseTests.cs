using FoundersLands.Simulation.Mathematics;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class ValueNoiseTests
    {
        [Fact]
        public void Sample_IsDeterministicForSamePoint()
        {
            var noise = new ValueNoise(123);
            float a = noise.Sample(3.7f, -2.1f);
            float b = noise.Sample(3.7f, -2.1f);
            Assert.Equal(a, b);
        }

        [Fact]
        public void Sample_StaysInUnitInterval()
        {
            var noise = new ValueNoise(456);
            for (int i = 0; i < 5000; i++)
            {
                float x = i * 0.13f;
                float y = i * -0.07f;
                float v = noise.Sample(x, y);
                Assert.True(v >= 0f && v <= 1f, $"value out of [0,1]: {v}");
            }
        }

        [Fact]
        public void DifferentSeeds_GiveDifferentFields()
        {
            var a = new ValueNoise(1);
            var b = new ValueNoise(2);
            Assert.NotEqual(a.Sample(10.5f, 10.5f), b.Sample(10.5f, 10.5f));
        }

        [Fact]
        public void Fbm_StaysInUnitInterval()
        {
            var noise = new ValueNoise(789);
            for (int i = 0; i < 5000; i++)
            {
                float v = noise.Fbm(i * 0.05f, i * 0.03f, 5, 3f, 2f, 0.5f);
                Assert.True(v >= 0f && v <= 1f, $"fbm out of [0,1]: {v}");
            }
        }

        [Fact]
        public void Sample_IsSpatiallyContinuous()
        {
            // Value noise interpolates a hashed lattice, so nearby points are close.
            var noise = new ValueNoise(321);
            float a = noise.Sample(5.000f, 5.000f);
            float b = noise.Sample(5.001f, 5.000f);
            Assert.True(M.Abs(a - b) < 0.01f, $"noise not continuous: {a} vs {b}");
        }
    }
}
