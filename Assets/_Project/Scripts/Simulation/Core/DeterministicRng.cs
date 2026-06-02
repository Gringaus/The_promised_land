namespace FoundersLands.Simulation.Core
{
    /// <summary>
    /// Deterministic, platform-stable PRNG (SplitMix64). The same seed always yields
    /// the same sequence regardless of OS or .NET version, so every random decision in
    /// world generation and the simulation can be fully reproduced from a saved seed
    /// (GDD §7, §21). Intentionally avoids <see cref="System.Random"/>, whose output is
    /// not contractually stable across runtimes.
    /// </summary>
    public struct DeterministicRng
    {
        private ulong _state;

        public DeterministicRng(ulong seed)
        {
            _state = seed;
        }

        public ulong State { get { return _state; } }

        public ulong NextULong()
        {
            unchecked
            {
                _state += 0x9E3779B97F4A7C15UL;
                ulong z = _state;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        public uint NextUInt()
        {
            return (uint)(NextULong() >> 32);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat()
        {
            // Top 24 bits -> float mantissa for a uniform [0,1) value.
            return (NextULong() >> 40) * (1.0f / 16777216.0f);
        }

        public float NextFloat(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform int in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            ulong range = (ulong)(maxExclusive - minInclusive);
            return minInclusive + (int)(NextULong() % range);
        }

        public bool NextBool(float probability)
        {
            return NextFloat() < probability;
        }

        /// <summary>
        /// Derive an independent sub-stream from a base seed and a stream id. Lets each
        /// generation layer (height, moisture, resources, ...) draw from its own
        /// reproducible sequence without coupling to the order other layers ran in.
        /// </summary>
        public static DeterministicRng Stream(ulong seed, ulong streamId)
        {
            ulong mixed = StableHash.Combine(StableHash.Combine(StableHash.Fnv1aOffset, seed), streamId);
            return new DeterministicRng(mixed);
        }
    }
}
