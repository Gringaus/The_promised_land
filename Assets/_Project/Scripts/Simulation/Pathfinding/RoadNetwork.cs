using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.Pathfinding
{
    public enum RoadLevel : byte
    {
        None = 0,
        Trail = 1, // тропа, протоптанная движением
        Road = 2   // дорога
    }

    /// <summary>
    /// A runtime overlay over the map that records where people and carts move (GDD §7
    /// "живая дорожная сеть", §20 RuntimeMaskSystem). Frequent traffic wears a trail, heavy
    /// traffic packs it into a road; unused trails fade back to wilderness. Roads lower the
    /// cost of crossing a tile, so the settlement's own movement gradually optimises itself.
    /// </summary>
    public sealed class RoadNetwork
    {
        public readonly int Width;
        public readonly int Height;

        private readonly byte[] _level;
        private readonly float[] _traffic;

        // Tuning (GDD §7: тропы появляются от частого движения, затем улучшаются до дорог).
        public float TrailThreshold = 8f;
        public float RoadThreshold = 40f;
        public float TrailFadeBelow = 3f;
        public float DecayPerTick = 0.97f;

        public RoadNetwork(int width, int height)
        {
            Width = width;
            Height = height;
            _level = new byte[width * height];
            _traffic = new float[width * height];
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < Width && y < Height; }

        public int Index(int x, int y) { return y * Width + x; }

        public RoadLevel LevelAt(int x, int y) { return (RoadLevel)_level[Index(x, y)]; }

        public float TrafficAt(int x, int y) { return _traffic[Index(x, y)]; }

        /// <summary>Multiplier applied to a tile's movement cost (roads are cheaper to cross).</summary>
        public float CostMultiplier(int x, int y)
        {
            switch ((RoadLevel)_level[Index(x, y)])
            {
                case RoadLevel.Road: return 0.35f;
                case RoadLevel.Trail: return 0.6f;
                default: return 1f;
            }
        }

        /// <summary>Record passage over a tile; wears trails into roads over time.</summary>
        public void AddTraffic(int x, int y, float amount)
        {
            if (!InBounds(x, y) || amount <= 0f) return;
            int i = Index(x, y);
            float t = _traffic[i] + amount;
            _traffic[i] = t;
            if (t >= RoadThreshold) _level[i] = (byte)RoadLevel.Road;
            else if (t >= TrailThreshold && _level[i] < (byte)RoadLevel.Trail) _level[i] = (byte)RoadLevel.Trail;
        }

        /// <summary>One tick of decay. Unused trails fade; built roads persist.</summary>
        public void Decay()
        {
            for (int i = 0; i < _traffic.Length; i++)
            {
                _traffic[i] *= DecayPerTick;
                if (_level[i] == (byte)RoadLevel.Trail && _traffic[i] < TrailFadeBelow)
                {
                    _level[i] = (byte)RoadLevel.None;
                }
            }
        }

        public ulong Hash(ulong h)
        {
            for (int i = 0; i < _level.Length; i++)
            {
                if (_level[i] != 0) h = StableHash.Combine(StableHash.Combine(h, i), _level[i]);
            }
            return h;
        }
    }
}
