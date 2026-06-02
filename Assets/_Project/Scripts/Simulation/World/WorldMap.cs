using System.Collections.Generic;
using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.World
{
    /// <summary>
    /// The generated world grid together with the seed that produced it. Because
    /// generation is deterministic, a save only needs the seed plus later deltas
    /// rather than the full grid (GDD §21).
    /// </summary>
    public sealed class WorldMap
    {
        public readonly int Width;
        public readonly int Height;
        public readonly ulong Seed;
        public readonly float SeaLevel;

        private readonly Tile[] _tiles;

        public WorldMap(int width, int height, ulong seed, float seaLevel)
        {
            Width = width;
            Height = height;
            Seed = seed;
            SeaLevel = seaLevel;
            _tiles = new Tile[width * height];
        }

        public int TileCount { get { return _tiles.Length; } }

        public IReadOnlyList<Tile> Tiles { get { return _tiles; } }

        public bool InBounds(int x, int y)
        {
            return x >= 0 && y >= 0 && x < Width && y < Height;
        }

        public int Index(int x, int y) { return y * Width + x; }

        public Tile Get(int x, int y) { return _tiles[Index(x, y)]; }

        public void Set(int x, int y, Tile t) { _tiles[Index(x, y)] = t; }

        /// <summary>Direct reference into the backing array for in-place generation passes.</summary>
        public ref Tile RefAt(int x, int y) { return ref _tiles[Index(x, y)]; }

        public Dictionary<Biome, int> BiomeHistogram()
        {
            var dict = new Dictionary<Biome, int>();
            for (int i = 0; i < _tiles.Length; i++)
            {
                Biome b = _tiles[i].Biome;
                dict.TryGetValue(b, out int c);
                dict[b] = c + 1;
            }
            return dict;
        }

        public Dictionary<ResourceNodeKind, int> ResourceHistogram()
        {
            var dict = new Dictionary<ResourceNodeKind, int>();
            for (int i = 0; i < _tiles.Length; i++)
            {
                ResourceNodeKind r = _tiles[i].Resource;
                if (r == ResourceNodeKind.None) continue;
                dict.TryGetValue(r, out int c);
                dict[r] = c + 1;
            }
            return dict;
        }

        public int CountResource(ResourceNodeKind kind)
        {
            int count = 0;
            for (int i = 0; i < _tiles.Length; i++)
            {
                if (_tiles[i].Resource == kind) count++;
            }
            return count;
        }

        /// <summary>
        /// Platform-stable hash of all tile data. Two maps with identical content hash
        /// to the same value; the determinism tests assert on this.
        /// </summary>
        public ulong ContentHash()
        {
            ulong h = StableHash.Fnv1aOffset;
            h = StableHash.Combine(h, Width);
            h = StableHash.Combine(h, Height);
            h = StableHash.Combine(h, Seed);
            for (int i = 0; i < _tiles.Length; i++)
            {
                Tile t = _tiles[i];
                h = StableHash.Combine(h, Quantize(t.Height));
                h = StableHash.Combine(h, Quantize(t.Temperature));
                h = StableHash.Combine(h, Quantize(t.Moisture));
                h = StableHash.Combine(h, Quantize(t.Fertility));
                h = StableHash.Combine(h, t.IsWater ? 1 : 0);
                h = StableHash.Combine(h, (int)t.Biome);
                h = StableHash.Combine(h, (int)t.Resource);
                h = StableHash.Combine(h, t.ResourceAmount);
            }
            return h;
        }

        private static int Quantize(float v)
        {
            return (int)(v * 10000f);
        }
    }
}
