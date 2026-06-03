using System;
using System.Collections.Generic;
using System.Text;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;

namespace FoundersLands.SimViewer
{
    /// <summary>
    /// Headless previewer for the simulation core.
    ///   --mode map      : ASCII map + biome/resource histograms (GDD §7)
    ///   --mode survive  : run a colony through several years and report survival (GDD §9, §28)
    /// Examples:
    ///   dotnet run --project SimHarness/console -- --mode map --seed green-valley
    ///   dotnet run --project SimHarness/console -- --mode survive --seed green-valley --years 3 --pop 20
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string mode = "map";
            ulong seed = StableHash.Fnv1a64("green-valley");
            int width = 96, height = 48;
            int years = 3, pop = 20, daysPerSeason = 24;

            for (int i = 0; i + 1 < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--mode": mode = args[i + 1]; break;
                    case "--seed": seed = ParseSeed(args[i + 1]); break;
                    case "--width": int.TryParse(args[i + 1], out width); break;
                    case "--height": int.TryParse(args[i + 1], out height); break;
                    case "--years": int.TryParse(args[i + 1], out years); break;
                    case "--pop": int.TryParse(args[i + 1], out pop); break;
                    case "--daysPerSeason": int.TryParse(args[i + 1], out daysPerSeason); break;
                }
            }

            if (mode == "survive") return RunSurvival(seed, years, pop, daysPerSeason);
            if (mode == "build") return RunBuild(seed, years, pop, daysPerSeason);
            return RunMapPreview(seed, width, height);
        }

        private static ulong ParseSeed(string s)
        {
            return ulong.TryParse(s, out ulong n) ? n : StableHash.Fnv1a64(s);
        }

        // ---------------------------------------------------------------- survival

        private static int RunSurvival(ulong seed, int years, int pop, int daysPerSeason)
        {
            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            var config = new SettlementConfig { StartingPopulation = pop, DaysPerSeason = daysPerSeason };
            ResourceCatalog catalog = ResourceCatalog.CreateDefault();
            SeasonDef[] seasons = SeasonDef.CreateDefault();
            Settlement colony = SettlementFactory.Create(map, seed, config, catalog, seasons);

            Console.WriteLine("Founder's Lands — survival run (GDD §9, §28)");
            Console.WriteLine($"seed={seed}  site=({colony.CenterX},{colony.CenterY})  population={colony.AlivePopulation}");
            Console.WriteLine($"primaryFood={colony.PrimaryFood} ({colony.ForageQuality})  " +
                              $"foodFactor={colony.FoodFactor:0.00}  firewoodFactor={colony.FirewoodFactor:0.00}");
            Console.WriteLine($"start stock: food={colony.FoodUnits():0} units, firewood={colony.Storehouse.Count(ResourceType.Firewood):0} units");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Pop",4} {"Food",7} {"Firewd",7} {"Nutr",7} {"Health",7} {"Died",5}");

            int totalDays = years * daysPerSeason * 4;
            int popAfterFirstWinter = -1;
            int seasonDeaths = 0;

            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                seasonDeaths += r.DeathsToday;

                bool lastDayOfSeason = (r.Day % daysPerSeason) == (daysPerSeason - 1);
                if (lastDayOfSeason)
                {
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {r.Population,4} " +
                                      $"{r.FoodUnits,7:0} {r.FirewoodUnits,7:0} {r.StoredNutrition,7:0} " +
                                      $"{r.AvgHealth,7:0.0} {seasonDeaths,5}");
                    if (r.Year == 0 && r.Season == Season.Winter) popAfterFirstWinter = r.Population;
                    seasonDeaths = 0;
                }

                if (colony.AlivePopulation == 0)
                {
                    Console.WriteLine($"  -- colony wiped out on day {r.Day} ({r.Season}) --");
                    break;
                }
            }

            Console.WriteLine();
            int started = pop;
            int survivors = colony.AlivePopulation;
            if (popAfterFirstWinter < 0) popAfterFirstWinter = survivors;
            bool survivedFirstWinter = popAfterFirstWinter > 0;

            Console.WriteLine($"Verdict: first winter {(survivedFirstWinter ? "SURVIVED" : "FAILED")} " +
                              $"({popAfterFirstWinter}/{started} alive after winter of year 0)");
            Console.WriteLine($"After {years} years: {survivors}/{started} alive, total deaths {colony.TotalDeaths}.");
            return 0;
        }

        // ---------------------------------------------------------------- construction

        private static int RunBuild(ulong seed, int years, int pop, int daysPerSeason)
        {
            if (pop < 28) pop = 28; // need enough hands to both feed the colony and build

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            var config = new SettlementConfig
            {
                StartingPopulation = pop,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.50f,
                WoodcutterShare = 0.20f,
                LoggerShare = 0.12f,
                QuarrymanShare = 0.06f,
                BuilderShare = 0.12f,
                // Bigger buffer: building diverts hands from food/fuel, so the colony must
                // coast through the first lean spring on stored supplies (GDD §9, §10).
                StartingFoodUnits = 300f,
                StartingFirewoodUnits = 250f,
                StartingWoodUnits = 80f,
                StartingStoneUnits = 40f
            };
            ResourceCatalog catalog = ResourceCatalog.CreateDefault();
            SeasonDef[] seasons = SeasonDef.CreateDefault();
            Settlement colony = SettlementFactory.Create(map, seed, config, catalog, seasons);

            PlaceBuildPlan(colony);
            int planned = colony.Buildings.Count;

            Console.WriteLine("Founder's Lands — construction run (GDD §10)");
            Console.WriteLine($"seed={seed}  site=({colony.CenterX},{colony.CenterY})  population={colony.AlivePopulation}");
            Console.WriteLine($"factors: food={colony.FoodFactor:0.00} firewood={colony.FirewoodFactor:0.00} stone={colony.StoneFactor:0.00}  blueprints queued={planned}");
            Console.WriteLine($"start materials: wood={colony.Storehouse.Count(ResourceType.Wood):0}, stone={colony.Storehouse.Count(ResourceType.Stone):0}");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Pop",4} {"Food",6} {"Firewd",7} {"Wood",6} {"Stone",6} {"Built",6} {"WIP",4} {"Cap",6} {"House",6}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                bool lastDayOfSeason = (r.Day % daysPerSeason) == (daysPerSeason - 1);
                if (lastDayOfSeason)
                {
                    colony.RecomputeBuildingEffects();
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {r.Population,4} {r.FoodUnits,6:0} {r.FirewoodUnits,7:0} " +
                                      $"{colony.Storehouse.Count(ResourceType.Wood),6:0} {colony.Storehouse.Count(ResourceType.Stone),6:0} " +
                                      $"{r.BuildingsComplete,6} {r.BuildingsUnderConstruction,4} {colony.Storehouse.Capacity,6:0} {colony.HousingCapacity,6}");
                }
                if (colony.AlivePopulation == 0)
                {
                    Console.WriteLine($"  -- colony wiped out on day {r.Day} ({r.Season}) --");
                    break;
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Built {colony.BuildingsComplete}/{planned} buildings.  Housing for {colony.HousingCapacity} " +
                              $"(pop {colony.AlivePopulation}).  Storehouse capacity {colony.Storehouse.Capacity:0}.");
            return 0;
        }

        private static void PlaceBuildPlan(Settlement colony)
        {
            BuildingType[] plan =
            {
                BuildingType.Storehouse,
                BuildingType.House, BuildingType.House, BuildingType.House,
                BuildingType.House, BuildingType.House,
                BuildingType.ForagerHut, BuildingType.WoodcutterCamp
            };

            var spots = LandSpotsNear(colony.Map, colony.CenterX, colony.CenterY, plan.Length);
            for (int i = 0; i < plan.Length; i++)
            {
                int x = i < spots.Count ? spots[i].X : colony.CenterX;
                int y = i < spots.Count ? spots[i].Y : colony.CenterY;
                colony.PlaceBlueprint(plan[i], x, y);
            }
        }

        private static List<(int X, int Y)> LandSpotsNear(WorldMap map, int cx, int cy, int count)
        {
            var list = new List<(int X, int Y)>();
            for (int r = 1; r <= 40 && list.Count < count; r++)
            {
                for (int dy = -r; dy <= r && list.Count < count; dy++)
                {
                    for (int dx = -r; dx <= r && list.Count < count; dx++)
                    {
                        if (dx > -r && dx < r && dy > -r && dy < r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (map.InBounds(x, y) && !map.Get(x, y).IsWater) list.Add((x, y));
                    }
                }
            }
            return list;
        }

        // ---------------------------------------------------------------- map preview

        private static int RunMapPreview(ulong seed, int width, int height)
        {
            var settings = new WorldGenSettings { Width = width, Height = height };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            Console.WriteLine("Founder's Lands — procedural map preview (GDD §7)");
            Console.WriteLine($"seed={seed}  size={map.Width}x{map.Height}  seaLevel={map.SeaLevel:0.00}  contentHash=0x{map.ContentHash():X16}");
            Console.WriteLine();
            PrintMap(map);
            Console.WriteLine();
            Console.WriteLine("Legend: ~ water  ; marsh  , meadow  . steppe  T deciduous  Y conifer  ^ highland  M mountain");
            Console.WriteLine("        resources: * wood  O stone  # iron  c clay  f fish  b berries  g game  h herbs  w spring");
            Console.WriteLine();
            PrintBiomes(map);
            Console.WriteLine();
            PrintResources(map);
            return 0;
        }

        private static void PrintMap(WorldMap map)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var sb = new StringBuilder(map.Width);
                for (int x = 0; x < map.Width; x++) sb.Append(Glyph(map.Get(x, y)));
                Console.WriteLine(sb.ToString());
            }
        }

        private static char Glyph(Tile t)
        {
            switch (t.Resource)
            {
                case ResourceNodeKind.Wood: return '*';
                case ResourceNodeKind.Stone: return 'O';
                case ResourceNodeKind.IronOre: return '#';
                case ResourceNodeKind.Clay: return 'c';
                case ResourceNodeKind.Fish: return 'f';
                case ResourceNodeKind.Berries: return 'b';
                case ResourceNodeKind.Game: return 'g';
                case ResourceNodeKind.Herbs: return 'h';
                case ResourceNodeKind.FreshWater: return 'w';
            }
            switch (t.Biome)
            {
                case Biome.Water: return '~';
                case Biome.Marsh: return ';';
                case Biome.Meadow: return ',';
                case Biome.Steppe: return '.';
                case Biome.DeciduousForest: return 'T';
                case Biome.ConiferousForest: return 'Y';
                case Biome.RockyHighland: return '^';
                case Biome.Mountain: return 'M';
            }
            return ' ';
        }

        private static void PrintBiomes(WorldMap map)
        {
            Console.WriteLine("Biomes:");
            float total = map.TileCount;
            foreach (var kv in SortedBiomes(map.BiomeHistogram()))
                Console.WriteLine($"  {kv.Key,-18} {kv.Value,6}  {(kv.Value / total) * 100f,5:0.0}%");
        }

        private static void PrintResources(WorldMap map)
        {
            Console.WriteLine("Resource nodes:");
            var hist = map.ResourceHistogram();
            if (hist.Count == 0) { Console.WriteLine("  (none)"); return; }
            foreach (var kv in SortedResources(hist))
                Console.WriteLine($"  {kv.Key,-12} {kv.Value,6}");
        }

        private static IEnumerable<KeyValuePair<Biome, int>> SortedBiomes(Dictionary<Biome, int> d)
        {
            var list = new List<KeyValuePair<Biome, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        private static IEnumerable<KeyValuePair<ResourceNodeKind, int>> SortedResources(Dictionary<ResourceNodeKind, int> d)
        {
            var list = new List<KeyValuePair<ResourceNodeKind, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }
    }
}
