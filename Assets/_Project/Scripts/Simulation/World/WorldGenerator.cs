using System;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Mathematics;

namespace FoundersLands.Simulation.World
{
    /// <summary>
    /// Layered procedural world generation (GDD §7): height → temperature → moisture →
    /// hydrology → rivers → soil fertility → biomes → resources. Fully deterministic:
    /// the same (settings, seed) always produces an identical <see cref="WorldMap"/>,
    /// which is the contract the save system depends on (§21).
    /// </summary>
    public static class WorldGenerator
    {
        // Independent RNG streams keep each layer reproducible and de-correlated.
        private const ulong StreamHeight = 1;
        private const ulong StreamMoisture = 2;
        private const ulong StreamTemperature = 3;
        private const ulong StreamRivers = 4;
        private const ulong StreamResources = 5;

        public static WorldMap Generate(WorldGenSettings s, ulong seed)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (s.Width <= 0 || s.Height <= 0) throw new ArgumentException("World dimensions must be positive.");

            var map = new WorldMap(s.Width, s.Height, seed, s.SeaLevel);

            var heightNoise = new ValueNoise(DeterministicRng.Stream(seed, StreamHeight).NextULong());
            var moistureNoise = new ValueNoise(DeterministicRng.Stream(seed, StreamMoisture).NextULong());
            var tempNoise = new ValueNoise(DeterministicRng.Stream(seed, StreamTemperature).NextULong());

            GenerateHeight(map, s, heightNoise);
            GenerateClimate(map, s, moistureNoise, tempNoise);
            ApplyHydrology(map, s);
            CarveRivers(map, s, seed);
            ComputeFertility(map, s);
            ClassifyBiomes(map, s);
            ScatterResources(map, s, seed);

            return map;
        }

        private static void GenerateHeight(WorldMap map, WorldGenSettings s, ValueNoise noise)
        {
            float invW = 1f / map.Width;
            float invH = 1f / map.Height;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float nx = x * invW;
                    float ny = y * invH;
                    float h = noise.Fbm(nx, ny, s.HeightOctaves, s.HeightFrequency, s.HeightLacunarity, s.HeightPersistence);

                    // Contrast around the mean widens the elevation distribution so the
                    // map gets real lowlands and real peaks (value-noise fBm otherwise
                    // clusters tightly around 0.5).
                    h = M.Clamp01(0.5f + (h - 0.5f) * s.HeightContrast);

                    if (s.IslandFalloff)
                    {
                        // Chebyshev distance from centre: 0 at centre, 1 at any map edge.
                        // Land is preserved within IslandInner and fades to water by
                        // IslandOuter, giving a contained valley framed by water.
                        float dx = M.Abs((nx - 0.5f) * 2f);
                        float dy = M.Abs((ny - 0.5f) * 2f);
                        float d = dx > dy ? dx : dy;
                        float mask = 1f - M.SmoothStep(M.InverseLerp(s.IslandInner, s.IslandOuter, d));
                        h *= mask;
                    }

                    map.RefAt(x, y).Height = M.Clamp01(h);
                }
            }
        }

        private static void GenerateClimate(WorldMap map, WorldGenSettings s, ValueNoise moistureNoise, ValueNoise tempNoise)
        {
            float invW = 1f / map.Width;
            float invH = 1f / map.Height;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    float nx = x * invW;
                    float ny = y * invH;
                    ref Tile t = ref map.RefAt(x, y);

                    // Warmer toward the south (y = 0), colder north and colder with altitude.
                    float latitude = 1f - ny;
                    float baseTemp = M.Lerp(1f - s.TemperatureNorthSouth, 1f, latitude);
                    float altitudePenalty = M.Clamp01(t.Height) * s.TemperatureAltitudeFalloff;
                    float tempJitter = tempNoise.Fbm(nx, ny, 3, 2f, 2f, 0.5f) * 0.15f - 0.075f;
                    t.Temperature = M.Clamp01(baseTemp - altitudePenalty + tempJitter);

                    t.Moisture = M.Clamp01(moistureNoise.Fbm(nx, ny, s.MoistureOctaves, s.MoistureFrequency, 2f, 0.5f));
                }
            }
        }

        private static void ApplyHydrology(WorldMap map, WorldGenSettings s)
        {
            // Everything at or below sea level becomes open water.
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ref Tile t = ref map.RefAt(x, y);
                    if (t.Height <= s.SeaLevel) t.IsWater = true;
                }
            }

            // Land next to water reads as wetter (riparian moisture bonus).
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ref Tile t = ref map.RefAt(x, y);
                    if (t.IsWater) continue;
                    if (HasWaterNeighbour(map, x, y))
                    {
                        t.Moisture = M.Clamp01(t.Moisture + 0.15f);
                    }
                }
            }
        }

        private static void CarveRivers(WorldMap map, WorldGenSettings s, ulong seed)
        {
            var rng = DeterministicRng.Stream(seed, StreamRivers);
            int riverCount = 2 + (map.Width * map.Height) / 8000;

            for (int r = 0; r < riverCount; r++)
            {
                int cx = rng.NextInt(0, map.Width);
                int cy = rng.NextInt(0, map.Height);
                Tile start = map.Get(cx, cy);
                // Springs start on reasonably high, dry ground.
                if (start.IsWater || start.Height < s.SeaLevel + 0.25f) continue;

                int guard = map.Width + map.Height;
                while (guard-- > 0)
                {
                    map.RefAt(cx, cy).IsWater = true;

                    int bestX = cx, bestY = cy;
                    float bestH = map.Get(cx, cy).Height;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            int nxp = cx + dx, nyp = cy + dy;
                            if (!map.InBounds(nxp, nyp)) continue;
                            float hh = map.Get(nxp, nyp).Height;
                            if (hh < bestH) { bestH = hh; bestX = nxp; bestY = nyp; }
                        }
                    }

                    if (bestX == cx && bestY == cy) break; // local minimum, river pools here
                    cx = bestX;
                    cy = bestY;
                    if (map.Get(cx, cy).Height <= s.SeaLevel) { map.RefAt(cx, cy).IsWater = true; break; }
                }
            }
        }

        private static void ComputeFertility(WorldMap map, WorldGenSettings s)
        {
            float seaToPeak = 1f - s.SeaLevel;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ref Tile t = ref map.RefAt(x, y);
                    if (t.IsWater) { t.Fertility = 0f; continue; }

                    float slope = LocalSlope(map, x, y);
                    float flat = 1f - M.Clamp01(slope * 6f);
                    float tempComfort = M.Clamp01(1f - M.Abs(t.Temperature - 0.6f) * 1.6f);
                    float lowland = seaToPeak <= 0f ? 0f : 1f - M.Clamp01((t.Height - s.SeaLevel) / seaToPeak);

                    float fert = t.Moisture * 0.45f + flat * 0.30f + tempComfort * 0.15f + lowland * 0.10f;
                    t.Fertility = M.Clamp01(fert);
                }
            }
        }

        private static void ClassifyBiomes(WorldMap map, WorldGenSettings s)
        {
            float seaToPeak = 1f - s.SeaLevel;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ref Tile t = ref map.RefAt(x, y);
                    if (t.IsWater) { t.Biome = Biome.Water; continue; }

                    float aboveSea = seaToPeak <= 0f ? 0f : M.Clamp01((t.Height - s.SeaLevel) / seaToPeak);

                    if (aboveSea > s.MountainThreshold) { t.Biome = Biome.Mountain; continue; }
                    if (aboveSea > s.HighlandThreshold) { t.Biome = Biome.RockyHighland; continue; }
                    if (aboveSea < s.MarshMaxAltitude && t.Moisture > s.ForestMoisture) { t.Biome = Biome.Marsh; continue; }

                    if (t.Moisture > s.ForestMoisture)
                    {
                        t.Biome = t.Temperature < s.ConiferTemperature ? Biome.ConiferousForest : Biome.DeciduousForest;
                    }
                    else if (t.Moisture > s.MeadowMoisture)
                    {
                        t.Biome = Biome.Meadow;
                    }
                    else
                    {
                        t.Biome = Biome.Steppe;
                    }
                }
            }
        }

        private static void ScatterResources(WorldMap map, WorldGenSettings s, ulong seed)
        {
            var rng = DeterministicRng.Stream(seed, StreamResources);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    ref Tile t = ref map.RefAt(x, y);
                    t.Resource = ResourceNodeKind.None;
                    t.ResourceAmount = 0;

                    if (t.IsWater)
                    {
                        if (rng.NextBool(s.FishDensity)) Place(ref t, ResourceNodeKind.Fish, rng.NextInt(40, 120));
                        else if (rng.NextBool(0.02f)) Place(ref t, ResourceNodeKind.FreshWater, 1000);
                        continue;
                    }

                    switch (t.Biome)
                    {
                        case Biome.DeciduousForest:
                        case Biome.ConiferousForest:
                            if (rng.NextBool(s.WoodDensity)) Place(ref t, ResourceNodeKind.Wood, rng.NextInt(60, 200));
                            else if (rng.NextBool(s.GameDensity)) Place(ref t, ResourceNodeKind.Game, rng.NextInt(10, 40));
                            else if (rng.NextBool(s.BerriesDensity)) Place(ref t, ResourceNodeKind.Berries, rng.NextInt(20, 60));
                            else if (rng.NextBool(s.HerbsDensity)) Place(ref t, ResourceNodeKind.Herbs, rng.NextInt(10, 30));
                            break;

                        case Biome.Meadow:
                            if (rng.NextBool(s.BerriesDensity)) Place(ref t, ResourceNodeKind.Berries, rng.NextInt(20, 60));
                            else if (rng.NextBool(s.HerbsDensity)) Place(ref t, ResourceNodeKind.Herbs, rng.NextInt(10, 30));
                            else if (rng.NextBool(s.GameDensity)) Place(ref t, ResourceNodeKind.Game, rng.NextInt(10, 40));
                            break;

                        case Biome.Steppe:
                            if (rng.NextBool(s.HerbsDensity * 0.5f)) Place(ref t, ResourceNodeKind.Herbs, rng.NextInt(5, 20));
                            else if (rng.NextBool(s.GameDensity * 0.5f)) Place(ref t, ResourceNodeKind.Game, rng.NextInt(5, 20));
                            break;

                        case Biome.RockyHighland:
                            if (rng.NextBool(s.StoneDensity)) Place(ref t, ResourceNodeKind.Stone, rng.NextInt(80, 250));
                            else if (rng.NextBool(s.IronOreDensity)) Place(ref t, ResourceNodeKind.IronOre, rng.NextInt(30, 90));
                            break;

                        case Biome.Mountain:
                            if (rng.NextBool(s.StoneDensity * 1.2f)) Place(ref t, ResourceNodeKind.Stone, rng.NextInt(100, 300));
                            else if (rng.NextBool(s.IronOreDensity * 1.5f)) Place(ref t, ResourceNodeKind.IronOre, rng.NextInt(40, 120));
                            break;

                        case Biome.Marsh:
                            if (rng.NextBool(s.ClayDensity)) Place(ref t, ResourceNodeKind.Clay, rng.NextInt(40, 120));
                            else if (rng.NextBool(s.HerbsDensity)) Place(ref t, ResourceNodeKind.Herbs, rng.NextInt(10, 30));
                            break;
                    }
                }
            }
        }

        private static void Place(ref Tile t, ResourceNodeKind kind, int amount)
        {
            t.Resource = kind;
            t.ResourceAmount = (ushort)M.Clamp(amount, 0, ushort.MaxValue);
        }

        private static bool HasWaterNeighbour(WorldMap map, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (map.InBounds(nx, ny) && map.Get(nx, ny).IsWater) return true;
                }
            }
            return false;
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
                    float d = M.Abs(map.Get(nx, ny).Height - h);
                    if (d > max) max = d;
                }
            }
            return max;
        }
    }
}
