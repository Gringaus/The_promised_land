using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.World;

namespace FoundersLands.Simulation.Pathfinding
{
    /// <summary>
    /// Per-tile cost of moving across the terrain (GDD §7, §12, §20 PathfindingSystem). Water
    /// is impassable (bridges come later); slope, marsh, highland and snow all slow movement,
    /// while trails and roads speed it up. The land cost is precomputed once; the cheap,
    /// changing road multiplier is applied per query so the same instance follows a wearing
    /// road network.
    /// </summary>
    public sealed class MovementCost
    {
        public const float Impassable = float.PositiveInfinity;

        // Weights for the terrain penalties.
        public const float SlopeWeight = 10f;
        public const float SnowWeight = 1.5f;
        public const float CheapestTile = 0.35f; // road on flat ground — used as the A* heuristic floor

        private readonly RoadNetwork _roads;
        private readonly float[] _land; // base land cost, or Impassable for water
        private readonly int _width;
        private readonly int _height;

        public MovementCost(WorldMap map, RoadNetwork roads = null, float snow = 0f)
        {
            _roads = roads;
            _width = map.Width;
            _height = map.Height;
            _land = new float[map.Width * map.Height];

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    _land[y * map.Width + x] = ComputeLand(map, x, y, snow);
                }
            }
        }

        public bool InBounds(int x, int y) { return x >= 0 && y >= 0 && x < _width && y < _height; }

        public bool Passable(int x, int y)
        {
            return InBounds(x, y) && !float.IsPositiveInfinity(_land[y * _width + x]);
        }

        /// <summary>Cost to enter tile (x, y), including any road discount. Infinity if blocked.</summary>
        public float Tile(int x, int y)
        {
            float land = _land[y * _width + x];
            if (float.IsPositiveInfinity(land)) return Impassable;
            float mult = _roads != null ? _roads.CostMultiplier(x, y) : 1f;
            return land * mult;
        }

        private static float ComputeLand(WorldMap map, int x, int y, float snow)
        {
            Tile t = map.Get(x, y);
            if (t.IsWater) return Impassable;

            float c = 1f;
            c += LocalSlope(map, x, y) * SlopeWeight;
            c += snow * SnowWeight;

            switch (t.Biome)
            {
                case Biome.Marsh: c += 1.5f; break;
                case Biome.Mountain: c += 2.0f; break;
                case Biome.RockyHighland: c += 0.6f; break;
                case Biome.DeciduousForest:
                case Biome.ConiferousForest: c += 0.3f; break;
            }

            return c;
        }

        private static float LocalSlope(WorldMap map, int x, int y)
        {
            float h = map.Get(x, y).Height;
            float max = 0f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    float d = map.Get(nx, ny).Height - h;
                    if (d < 0f) d = -d;
                    if (d > max) max = d;
                }
            }
            return max;
        }
    }
}
