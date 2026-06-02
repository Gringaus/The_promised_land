using System;

namespace FoundersLands.Simulation.Mathematics
{
    /// <summary>
    /// Deterministic value noise and fractal Brownian motion (fBm). Uses integer-lattice
    /// hashing rather than <see cref="System.Random"/>, so a given (seed, x, y) returns
    /// the same value on every platform. Drives the height, moisture and temperature
    /// fields of world generation (GDD §7).
    /// </summary>
    public sealed class ValueNoise
    {
        private readonly ulong _seed;

        public ValueNoise(ulong seed)
        {
            _seed = seed;
        }

        private static ulong Mix(ulong z)
        {
            unchecked
            {
                z += 0x9E3779B97F4A7C15UL;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>Hash an integer lattice point to a stable value in [0, 1).</summary>
        private float Lattice(int ix, int iy)
        {
            unchecked
            {
                ulong h = _seed;
                h = Mix(h ^ ((ulong)(uint)ix * 0x9E3779B1UL));
                h = Mix(h ^ ((ulong)(uint)iy * 0x85EBCA77UL));
                return (h >> 40) * (1.0f / 16777216.0f);
            }
        }

        /// <summary>Single-octave bilinear value noise at (x, y); returns [0, 1].</summary>
        public float Sample(float x, float y)
        {
            int x0 = FloorToInt(x);
            int y0 = FloorToInt(y);
            float tx = M.SmoothStep(x - x0);
            float ty = M.SmoothStep(y - y0);

            float v00 = Lattice(x0, y0);
            float v10 = Lattice(x0 + 1, y0);
            float v01 = Lattice(x0, y0 + 1);
            float v11 = Lattice(x0 + 1, y0 + 1);

            float a = M.LerpUnclamped(v00, v10, tx);
            float b = M.LerpUnclamped(v01, v11, tx);
            return M.LerpUnclamped(a, b, ty);
        }

        /// <summary>Multi-octave fBm, normalized to [0, 1].</summary>
        public float Fbm(float x, float y, int octaves, float frequency, float lacunarity, float persistence)
        {
            float sum = 0f;
            float amp = 1f;
            float freq = frequency;
            float maxAmp = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Sample(x * freq, y * freq);
                maxAmp += amp;
                amp *= persistence;
                freq *= lacunarity;
            }
            return maxAmp > 0f ? sum / maxAmp : 0f;
        }

        private static int FloorToInt(float v)
        {
            int i = (int)v;
            return (v < i) ? i - 1 : i;
        }
    }
}
