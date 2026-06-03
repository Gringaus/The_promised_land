using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;

namespace FoundersLands.Simulation.Settlements
{
    /// <summary>
    /// Builds a starter colony on a generated map (GDD §28 minimal prototype). Productivity
    /// is read from the terrain around the chosen site, so the same seed always produces the
    /// same starting conditions — deterministic end to end.
    /// </summary>
    public static class SettlementFactory
    {
        private const ulong StreamSettlement = 100;

        private static readonly string[] Names =
        {
            "Aldwin", "Bera", "Cuthbert", "Dagny", "Edric", "Frida", "Godric", "Hilda",
            "Ivo", "Junia", "Kael", "Leofric", "Mabel", "Nestor", "Orla", "Penda",
            "Quenild", "Rowan", "Sigrid", "Theo", "Ulf", "Vesna", "Wystan", "Yara"
        };

        public static Settlement Create(WorldMap map, ulong seed, SettlementConfig config,
            ResourceCatalog catalog, SeasonDef[] seasons)
        {
            FindSite(map, out int cx, out int cy);

            ScanSurroundings(map, cx, cy, config.GatherRadius,
                out float berries, out float fish, out float meat, out float wood, out float stone, out float avgFertility);

            // Optionally re-weight resource potential by real path distance (GDD §3, §12).
            if (config.UsePathWeightedPotential)
            {
                ScanWeighted(map, cx, cy, config, out berries, out fish, out meat, out wood, out stone);
            }

            var settlement = new Settlement(map, cx, cy, config, catalog, seasons)
            {
                PrimaryFood = PickPrimaryFood(berries, fish, meat),
                ForageQuality = avgFertility > 0.55f ? ResourceQuality.Fine
                              : avgFertility < 0.30f ? ResourceQuality.Poor
                              : ResourceQuality.Standard,
                FoodFactor = Factor(berries + fish + meat, config.FoodPotentialForFull, config.MinFoodFactor),
                FirewoodFactor = Factor(wood, config.FirewoodPotentialForFull, config.MinFirewoodFactor),
                StoneFactor = Factor(stone, config.StonePotentialForFull, config.MinStoneFactor),
                Rng = DeterministicRng.Stream(seed, StreamSettlement)
            };

            PopulateCitizens(settlement, config);

            // Starting stock so day one is survivable (GDD §6 "стартовые ресурсы").
            settlement.Storehouse.Add(settlement.PrimaryFood, ResourceQuality.Standard, config.StartingFoodUnits);
            settlement.Storehouse.Add(ResourceType.Firewood, ResourceQuality.Standard, config.StartingFirewoodUnits);
            if (config.StartingWoodUnits > 0f) settlement.Storehouse.Add(ResourceType.Wood, ResourceQuality.Standard, config.StartingWoodUnits);
            if (config.StartingStoneUnits > 0f) settlement.Storehouse.Add(ResourceType.Stone, ResourceQuality.Standard, config.StartingStoneUnits);

            return settlement;
        }

        // Expanding-ring search from the map centre for the first dry tile.
        private static void FindSite(WorldMap map, out int cx, out int cy)
        {
            int mx = map.Width / 2;
            int my = map.Height / 2;
            int maxR = (map.Width > map.Height ? map.Width : map.Height);

            for (int r = 0; r <= maxR; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx > -r && dx < r && dy > -r && dy < r) continue; // ring perimeter only
                        int x = mx + dx, y = my + dy;
                        if (map.InBounds(x, y) && !map.Get(x, y).IsWater)
                        {
                            cx = x; cy = y; return;
                        }
                    }
                }
            }

            cx = mx; cy = my;
        }

        private static void ScanSurroundings(WorldMap map, int cx, int cy, float radius,
            out float berries, out float fish, out float meat, out float wood, out float stone, out float avgFertility)
        {
            berries = fish = meat = wood = stone = 0f;
            float fertSum = 0f;
            int land = 0;
            int r = (int)radius;
            float r2 = radius * radius;

            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (!map.InBounds(x, y)) continue;
                    float ddx = x - cx, ddy = y - cy;
                    if (ddx * ddx + ddy * ddy > r2) continue;

                    Tile t = map.Get(x, y);
                    if (!t.IsWater) { fertSum += t.Fertility; land++; }

                    switch (t.Resource)
                    {
                        case ResourceNodeKind.Berries: berries += t.ResourceAmount; break;
                        case ResourceNodeKind.Fish: fish += t.ResourceAmount; break;
                        case ResourceNodeKind.Game: meat += t.ResourceAmount; break;
                        case ResourceNodeKind.Wood: wood += t.ResourceAmount; break;
                        case ResourceNodeKind.Stone: stone += t.ResourceAmount; break;
                        case ResourceNodeKind.IronOre: stone += t.ResourceAmount * 0.5f; break;
                    }
                }
            }

            avgFertility = land > 0 ? fertSum / land : 0f;
        }

        // Weight each resource node by exp(-pathCost/scale) from the site, so far or
        // marsh-locked deposits count for less. Aquatic nodes (fish) are reached from the
        // nearest walkable shore tile, since open water is impassable.
        private static void ScanWeighted(WorldMap map, int cx, int cy, SettlementConfig config,
            out float berries, out float fish, out float meat, out float wood, out float stone)
        {
            berries = fish = meat = wood = stone = 0f;

            var cost = new MovementCost(map);
            float[] dist = DistanceField.Compute(cost, map.Width, map.Height, new Coord(cx, cy));
            float scale = config.PathPotentialScale <= 0f ? 1f : config.PathPotentialScale;

            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    if (t.Resource == ResourceNodeKind.None) continue;

                    float d = EffectiveDistance(map, dist, x, y);
                    if (float.IsPositiveInfinity(d)) continue; // unreachable contributes nothing

                    float amt = t.ResourceAmount * (float)System.Math.Exp(-d / scale);
                    switch (t.Resource)
                    {
                        case ResourceNodeKind.Berries: berries += amt; break;
                        case ResourceNodeKind.Fish: fish += amt; break;
                        case ResourceNodeKind.Game: meat += amt; break;
                        case ResourceNodeKind.Wood: wood += amt; break;
                        case ResourceNodeKind.Stone: stone += amt; break;
                        case ResourceNodeKind.IronOre: stone += amt * 0.5f; break;
                    }
                }
            }
        }

        private static float EffectiveDistance(WorldMap map, float[] dist, int x, int y)
        {
            float d = dist[y * map.Width + x];
            if (!float.IsPositiveInfinity(d)) return d;

            // Water tile (impassable): use the nearest walkable neighbour (shore access).
            float best = float.PositiveInfinity;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int nx = x + dx, ny = y + dy;
                    if (!map.InBounds(nx, ny)) continue;
                    float nd = dist[ny * map.Width + nx];
                    if (nd < best) best = nd;
                }
            }
            return best;
        }

        private static ResourceType PickPrimaryFood(float berries, float fish, float meat)
        {
            if (meat >= berries && meat >= fish && meat > 0f) return ResourceType.Meat;
            if (fish >= berries && fish > 0f) return ResourceType.Fish;
            return ResourceType.Berries; // renewable default
        }

        private static float Factor(float potential, float full, float min)
        {
            if (full <= 0f) return 1f;
            float f = potential / full;
            if (f > 1f) f = 1f;
            if (f < min) f = min;
            return f;
        }

        private static void PopulateCitizens(Settlement s, SettlementConfig config)
        {
            int pop = config.StartingPopulation;
            int foragers = (int)System.Math.Round(pop * config.ForagerShare);
            int woodcutters = (int)System.Math.Round(pop * config.WoodcutterShare);
            int loggers = (int)System.Math.Round(pop * config.LoggerShare);
            int quarrymen = (int)System.Math.Round(pop * config.QuarrymanShare);
            int builders = (int)System.Math.Round(pop * config.BuilderShare);

            // Cumulative thresholds; any remainder becomes idle.
            int tF = foragers;
            int tW = tF + woodcutters;
            int tL = tW + loggers;
            int tQ = tL + quarrymen;
            int tB = tQ + builders;

            for (int i = 0; i < pop; i++)
            {
                Profession prof = i < tF ? Profession.Forager
                                : i < tW ? Profession.Woodcutter
                                : i < tL ? Profession.Logger
                                : i < tQ ? Profession.Quarryman
                                : i < tB ? Profession.Builder
                                : Profession.Idle;

                string name = Names[s.Rng.NextInt(0, Names.Length)];
                int age = s.Rng.NextInt(16, 56);
                s.Citizens.Add(new Citizen(i, name, age, prof));
            }
        }
    }
}
