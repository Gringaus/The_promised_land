using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Logistics;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Technology;
using FoundersLands.Simulation.Threats;
using FoundersLands.Simulation.Trade;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Path = FoundersLands.Simulation.Pathfinding.Path;

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
            string? commands = null;

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
                    case "--commands": commands = args[i + 1]; break;
                }
            }

            if (mode == "survive") return RunSurvival(seed, years, pop, daysPerSeason);
            if (mode == "build") return RunBuild(seed, years, pop, daysPerSeason);
            if (mode == "saveload") return RunSaveLoad(seed, daysPerSeason);
            if (mode == "logistics") return RunLogistics(seed);
            if (mode == "production") return RunProduction(seed, years, daysPerSeason);
            if (mode == "raiders") return RunRaiders(seed, years, daysPerSeason);
            if (mode == "farming") return RunFarming(seed, years, daysPerSeason);
            if (mode == "demography") return RunDemography(seed, years, daysPerSeason);
            if (mode == "trade") return RunTrade(seed, years, daysPerSeason);
            if (mode == "tech") return RunTech(seed, years, daysPerSeason);
            if (mode == "grand") return RunGrand(seed, years, daysPerSeason);
            if (mode == "preserve") return RunPreserve(seed, years, daysPerSeason);
            if (mode == "play") return RunPlay(seed, daysPerSeason, commands);
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

            SettlementConfig config = BuildScenarioConfig(pop, daysPerSeason);
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

        private static SettlementConfig BuildScenarioConfig(int pop, int daysPerSeason)
        {
            return new SettlementConfig
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
        }

        // ---------------------------------------------------------------- save / load

        private static int RunSaveLoad(ulong seed, int daysPerSeason)
        {
            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            SettlementConfig config = BuildScenarioConfig(28, daysPerSeason);
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            PlaceBuildPlan(colony);

            // Play a year so the save captures a rich, mid-game state (buildings, stocks).
            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);

            string text = SaveGame.Save(colony, settings);
            const string path = "founders_save.fls";
            File.WriteAllText(path, text);

            Settlement loaded = SaveGame.Load(File.ReadAllText(path));

            Console.WriteLine("Founder's Lands — save / load round-trip (GDD §21)");
            Console.WriteLine($"saved to {path}  ({text.Length} bytes)");
            Console.WriteLine($"state: day={colony.Clock.Day}  pop={colony.AlivePopulation}  buildings={colony.BuildingsComplete}  food={colony.FoodUnits():0}");
            Console.WriteLine();

            bool mapMatch = colony.Map.ContentHash() == loaded.Map.ContentHash();
            bool stateMatch = colony.StateHash() == loaded.StateHash();
            Console.WriteLine($"map regenerated from seed:  {(mapMatch ? "MATCH" : "MISMATCH")}  (0x{loaded.Map.ContentHash():X16})");
            Console.WriteLine($"settlement state restored:  {(stateMatch ? "MATCH" : "MISMATCH")}  (0x{loaded.StateHash():X16})");

            // Continue both another year and confirm they stay in lock-step.
            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);
            SettlementSimulation.Run(loaded, loaded.Clock.DaysPerYear);
            bool contMatch = colony.StateHash() == loaded.StateHash();
            Console.WriteLine($"identical after replaying a year: {(contMatch ? "MATCH" : "MISMATCH")}");

            return (mapMatch && stateMatch && contMatch) ? 0 : 1;
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

        // ---------------------------------------------------------------- production

        private static int RunProduction(ulong seed, int years, int daysPerSeason)
        {
            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.50f, WoodcutterShare = 0.16f, LoggerShare = 0.09f,
                QuarrymanShare = 0.03f, MinerShare = 0.08f, CraftsmanShare = 0.10f,
                StorehouseCapacity = 20000f, // ample so this demo isn't dominated by a full store
                StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
                StartingWoodUnits = 60f, StartingStoneUnits = 40f
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            colony.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f); // prime the smelter

            // Pre-build the infrastructure so this run highlights the production chains
            // themselves (construction is demonstrated by --mode build).
            int gx = colony.CenterX, gy = colony.CenterY;
            PlaceComplete(colony, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) PlaceComplete(colony, BuildingType.House, gx + 2 + i, gy);
            PlaceComplete(colony, BuildingType.Sawmill, gx, gy + 1);
            PlaceComplete(colony, BuildingType.Smelter, gx, gy + 2);
            PlaceComplete(colony, BuildingType.Smithy, gx, gy + 3);
            PlaceComplete(colony, BuildingType.Market, gx, gy + 4);

            Console.WriteLine("Founder's Lands — production chains & market (GDD §12)");
            Console.WriteLine($"seed={seed}  pop={colony.AlivePopulation}  foodFactor={colony.FoodFactor:0.00}  firewoodFactor={colony.FirewoodFactor:0.00}  ironFactor={colony.IronFactor:0.00}");
            Console.WriteLine("chains: Wood->Planks (sawmill), IronOre->Ingot (smelter), Ingot->Tools (smithy); market + tools boost work");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Pop",4} {"Planks",7} {"Tools",6} {"Food",6} {"Firewd",7} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if ((r.Day % daysPerSeason) == (daysPerSeason - 1))
                {
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {r.Population,4} " +
                                      $"{colony.Storehouse.Count(ResourceType.Planks),7:0} " +
                                      $"{colony.Storehouse.Count(ResourceType.Tools),6:0} {r.FoodUnits,6:0} {r.FirewoodUnits,7:0} {r.AvgHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- wiped out day {r.Day} --"); break; }
            }

            Console.WriteLine();
            Console.WriteLine($"Final stock: planks={colony.Storehouse.Count(ResourceType.Planks):0}, " +
                              $"ingots={colony.Storehouse.Count(ResourceType.IronIngot):0}, tools={colony.Storehouse.Count(ResourceType.Tools):0}.");
            return 0;
        }

        private static void PlaceComplete(Settlement colony, BuildingType type, int x, int y)
        {
            Building b = colony.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        // ================================================================ PLAYABLE COLONY (§28)
        // A first playable build of the colony: a real-time loop you steer with simple commands.
        // Works two ways — live single-key control in a real terminal, or a deterministic command
        // script (piped stdin or --commands "...") for reproducible runs, demos and testing.

        private const int PlayWinPopulation = 30;
        private const int PlayEndureYears = 30;

        private static int RunPlay(ulong seed, int daysPerSeason, string? commands)
        {
            Settlement s = CreatePlayColony(seed, daysPerSeason, out WorldGenSettings settings);

            // Input source: explicit --commands, else piped stdin (scripted), else live keyboard.
            List<string>? script = null;
            if (!string.IsNullOrEmpty(commands)) script = SplitCommands(commands);
            else if (Console.IsInputRedirected) script = ReadStdinCommands();

            Console.WriteLine("=== FOUNDER'S LANDS — playable colony (GDD §28) ===");
            Console.WriteLine($"seed={seed}.  Lead your settlers through the seasons: keep them fed and warm,");
            Console.WriteLine($"raise buildings, balance the workforce, and grow the colony to {PlayWinPopulation}.");
            Console.WriteLine(PlayHelp());

            float speed = 3f; bool running = true; string msg = "A new colony takes root by the river.";

            if (script != null)
            {
                RenderDashboard(s, speed, running, msg, false);
                foreach (string raw in script)
                {
                    string line = (raw ?? string.Empty).Trim();
                    if (line.Length == 0 || line[0] == '#') continue;
                    Console.WriteLine();
                    Console.WriteLine($"> {line}");
                    bool keepGoing = HandleCommand(ref s, settings, line, ref speed, ref running, ref msg);
                    RenderDashboard(s, speed, running, msg, false);
                    string? outcome = PlayOutcome(s);
                    if (outcome != null) { Console.WriteLine(); Console.WriteLine(outcome); return 0; }
                    if (!keepGoing) { Console.WriteLine(); Console.WriteLine("You step back; the colony carries on without you."); return 0; }
                }
                Console.WriteLine();
                Console.WriteLine("(end of script — the colony stands paused, awaiting your next command)");
                return 0;
            }

            // Live terminal: time flows in real time; keys steer it.
            var clock = System.Diagnostics.Stopwatch.StartNew();
            double accumDays = 0;
            while (true)
            {
                RenderDashboard(s, speed, running, msg, true);

                double frameStart = clock.Elapsed.TotalSeconds;
                while (clock.Elapsed.TotalSeconds - frameStart < 0.12)
                {
                    if (TryReadKeyCommand(out string keyLine))
                    {
                        if (keyLine == "\0pause") { running = !running; msg = running ? "Resumed." : "Paused."; }
                        else if (!HandleCommand(ref s, settings, keyLine, ref speed, ref running, ref msg))
                        { Console.WriteLine(); Console.WriteLine("Farewell, founder."); return 0; }
                    }
                    System.Threading.Thread.Sleep(15);
                }

                if (running)
                {
                    accumDays += speed * (clock.Elapsed.TotalSeconds - frameStart);
                    int steps = (int)accumDays;
                    if (steps > 0) { accumDays -= steps; msg = AdvanceDays(s, steps); }
                }

                string? outcome = PlayOutcome(s);
                if (outcome != null) { RenderDashboard(s, speed, running, msg, true); Console.WriteLine(); Console.WriteLine(outcome); return 0; }
            }
        }

        private static Settlement CreatePlayColony(ulong seed, int daysPerSeason, out WorldGenSettings settings)
        {
            settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 16,
                DaysPerSeason = daysPerSeason,
                EnablePopulationDynamics = true,
                EnableThreats = false, // a peaceful first build: survive the seasons, build, and grow
                EnableTrade = true,
                // Food-and-warmth-first mix (close to the proven Module 2 survival ratio) so the colony
                // holds together on its own; the player retasks labour to loggers/quarrymen/builders
                // when they want to raise something, trading a little food security for progress.
                ForagerShare = 0.60f, WoodcutterShare = 0.30f, LoggerShare = 0.05f,
                QuarrymanShare = 0.03f, BuilderShare = 0.02f,
                StartingFoodUnits = 350f, StartingFirewoodUnits = 400f,
                StartingWoodUnits = 140f, StartingStoneUnits = 90f,
                // Forgiving yields for a first colony: a fed, warm, housed town grows steadily instead of
                // merely holding on. Generous on BOTH food (so it grows — growth keys off forager
                // capacity) and firewood (so a growing town still survives winter rather than freezing).
                ForagerNutritionPerDay = 8.0f,
                WoodcutterFirewoodPerDay = 9.0f,
                // A gentler raid curve than the headless threat demo: a first colony gets years to
                // raise a watchtower and a militia before bandits become a real danger (GDD §13).
                ThreatBaseGrowth = 0.12f
            };
            Settlement s = SettlementFactory.Create(map, seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            // The founders arrive to a small established camp, not bare ground: room to live, somewhere
            // to store the harvest, and two forager huts feeding a food surplus to grow on.
            int cx = s.CenterX, cy = s.CenterY;
            PlaceComplete(s, BuildingType.House, cx - 1, cy);
            PlaceComplete(s, BuildingType.House, cx + 1, cy);
            PlaceComplete(s, BuildingType.House, cx, cy - 1);
            PlaceComplete(s, BuildingType.House, cx - 2, cy);
            PlaceComplete(s, BuildingType.House, cx + 2, cy);
            PlaceComplete(s, BuildingType.Storehouse, cx, cy + 1);
            PlaceComplete(s, BuildingType.ForagerHut, cx - 1, cy - 1);
            PlaceComplete(s, BuildingType.ForagerHut, cx + 1, cy + 1);
            s.RecomputeBuildingEffects();
            return s;
        }

        private static string PlayHelp() =>
            "Commands: step [n] · season · year · build <type> [x y] · labor <job> <0..1> · speed <n> · save · load · status · help · quit\n" +
            "  buildings: house storehouse forager woodcutter sawmill smelter smithy market watchtower palisade field mill bakery cellar smokehouse tent\n" +
            "  jobs: forager woodcutter logger quarryman builder miner craftsman militia farmer scholar";

        // Advance the simulation a number of days, returning a short summary of what happened.
        private static string AdvanceDays(Settlement s, int days)
        {
            if (s.AlivePopulation == 0) return "The colony is gone.";
            int b0 = s.TotalBirths, d0 = s.TotalDeaths, n0 = s.NaturalDeaths, l0 = s.TotalLeft, im0 = s.TotalImmigrants, r0 = s.Threat.TotalRaids;
            float stolen0 = s.Threat.TotalStolen;

            int done = 0;
            for (int i = 0; i < days; i++) { if (s.AlivePopulation == 0) break; SettlementSimulation.Step(s); done++; }

            var sb = new StringBuilder($"+{done}d: ");
            int before = sb.Length;
            AppendCount(sb, "born", s.TotalBirths - b0);
            AppendCount(sb, "arrived", s.TotalImmigrants - im0);
            AppendCount(sb, "starved/froze", s.TotalDeaths - d0);
            AppendCount(sb, "old age", s.NaturalDeaths - n0);
            AppendCount(sb, "left", s.TotalLeft - l0);
            int raids = s.Threat.TotalRaids - r0;
            if (raids > 0) sb.Append($"{raids} raid(s) (−{s.Threat.TotalStolen - stolen0:0} stolen); ");
            if (sb.Length == before) sb.Append("a quiet stretch.");
            return sb.ToString();
        }

        private static void AppendCount(StringBuilder sb, string label, int n)
        {
            if (n > 0) sb.Append($"{n} {label}; ");
        }

        private static bool HandleCommand(ref Settlement s, WorldGenSettings settings, string line,
                                          ref float speed, ref bool running, ref string msg)
        {
            string[] t = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (t.Length == 0) return true;
            switch (t[0].ToLowerInvariant())
            {
                case "quit": case "q": case "exit": return false;
                case "help": case "h": case "?": msg = PlayHelp(); return true;
                case "status": case "": msg = "—"; return true;
                case "pause": running = false; msg = "Paused."; return true;
                case "resume": case "go": running = true; msg = "Resumed."; return true;
                case "speed":
                    if (t.Length > 1 && TryF(t[1], out float sp)) { speed = sp < 0f ? 0f : (sp > 64f ? 64f : sp); msg = $"Speed set to {speed:0.#} days/s."; }
                    else msg = "Usage: speed <days per second>";
                    return true;
                case "step": { int n = 1; if (t.Length > 1) int.TryParse(t[1], out n); msg = AdvanceDays(s, n < 1 ? 1 : n); return true; }
                case "season": msg = AdvanceDays(s, s.Clock.DaysPerSeason); return true;
                case "year": msg = AdvanceDays(s, s.Clock.DaysPerYear); return true;
                case "labor": case "jobs": msg = DoLabor(s, t); return true;
                case "build": msg = DoBuild(s, t); return true;
                case "save": msg = DoSave(s, settings, t); return true;
                case "load": { Settlement? loaded = DoLoad(t, out string r); if (loaded != null) s = loaded; msg = r; return true; }
                default: msg = $"Unknown command '{t[0]}'. Type 'help'."; return true;
            }
        }

        private static string DoLabor(Settlement s, string[] t)
        {
            if (t.Length < 3 || !TryF(t[2], out float share))
                return "Usage: labor <job> <0..1>   e.g. labor builder 0.25";
            share = share < 0f ? 0f : (share > 1f ? 1f : share);
            SettlementConfig c = s.Config;
            switch (t[1].ToLowerInvariant())
            {
                case "forager": c.ForagerShare = share; break;
                case "woodcutter": c.WoodcutterShare = share; break;
                case "logger": c.LoggerShare = share; break;
                case "quarryman": c.QuarrymanShare = share; break;
                case "builder": c.BuilderShare = share; break;
                case "miner": c.MinerShare = share; break;
                case "craftsman": c.CraftsmanShare = share; break;
                case "militia": case "militiaman": c.MilitiaShare = share; break;
                case "farmer": c.FarmerShare = share; break;
                case "scholar": c.ScholarShare = share; break;
                default: return $"Unknown job '{t[1]}'.";
            }
            PopulationSystem.ReassignWorkforce(s);
            return $"{t[1].ToLowerInvariant()} share set to {share:0.00}; workforce re-tasked.";
        }

        private static string DoBuild(Settlement s, string[] t)
        {
            if (t.Length < 2 || !TryParseBuildingType(t[1], out BuildingType type))
                return "Usage: build <type> [x y]   (try 'help' for the list)";
            if (!s.IsBuildingUnlocked(type)) return $"{s.BuildingCatalog.Get(type).Name} is not researched yet.";

            int x, y;
            if (t.Length >= 4 && int.TryParse(t[2], out x) && int.TryParse(t[3], out y))
            {
                if (!s.Map.InBounds(x, y)) return "That spot is off the map.";
                if (OccupiedAt(s, x, y)) return "That tile is already taken.";
            }
            else if (!FindBuildTile(s, out x, out y)) return "No free building spot near the centre.";

            Building b = s.PlaceBlueprint(type, x, y);
            if (b == null) return $"Cannot place {type} there.";
            BuildingDef def = s.BuildingCatalog.Get(type);
            return $"Blueprint placed: {def.Name} at ({x},{y}). Builders will raise it as materials arrive.";
        }

        private static string DoSave(Settlement s, WorldGenSettings settings, string[] t)
        {
            string path = t.Length > 1 ? t[1] : "founderslands-save.json";
            try { File.WriteAllText(path, SaveGame.Save(s, settings)); return $"Saved to {path}."; }
            catch (Exception e) { return $"Save failed: {e.Message}"; }
        }

        private static Settlement? DoLoad(string[] t, out string msg)
        {
            string path = t.Length > 1 ? t[1] : "founderslands-save.json";
            try
            {
                if (!File.Exists(path)) { msg = $"No save at {path}."; return null; }
                Settlement loaded = SaveGame.Load(File.ReadAllText(path));
                msg = $"Loaded {path} — {loaded.AlivePopulation} settlers, year {loaded.Clock.Year}.";
                return loaded;
            }
            catch (Exception e) { msg = $"Load failed: {e.Message}"; return null; }
        }

        private static string? PlayOutcome(Settlement s)
        {
            if (s.AlivePopulation == 0)
                return "DEFEAT — the last settler is gone. The land reclaims the clearing. (try again with a steadier hand on food and firewood)";
            if (s.AlivePopulation >= PlayWinPopulation)
                return $"VICTORY — the colony has grown to {s.AlivePopulation} in year {s.Clock.Year}. Word spreads of a town that endures.";
            if (s.Clock.Year >= PlayEndureYears)
                return $"The chronicle closes after {PlayEndureYears} years with {s.AlivePopulation} settlers — a colony that endured.";
            return null;
        }

        private static void RenderDashboard(Settlement s, float speed, bool running, string msg, bool live)
        {
            if (live) { try { Console.Clear(); } catch { /* no console buffer */ } }
            var clk = s.Clock;
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine($"--- {clk.Season,-6} day {clk.DayOfSeason + 1,2}/{clk.DaysPerSeason} · year {clk.Year}    {(running ? $"RUN {speed:0.#}/s" : "PAUSED")} ---");
            sb.AppendLine($"  People {s.AlivePopulation,3}   health {s.AverageHealth,5:0.0}   housing {s.HousingCapacity,3}   (goal {PlayWinPopulation})");

            float pop = s.AlivePopulation < 1 ? 1 : s.AlivePopulation;
            float foodDays = s.StoredNutrition() / (pop * s.Config.NutritionPerPersonPerDay);
            sb.AppendLine($"  Food {s.FoodUnits(),6:0}  (~{foodDays,4:0}d)   Firewood {s.Storehouse.Count(ResourceType.Firewood),6:0}");
            sb.AppendLine($"  Wood {s.Storehouse.Count(ResourceType.Wood),5:0}  Stone {s.Storehouse.Count(ResourceType.Stone),5:0}  " +
                          $"Planks {s.Storehouse.Count(ResourceType.Planks),4:0}  Tools {s.Storehouse.Count(ResourceType.Tools),4:0}  " +
                          $"Bread {s.Storehouse.Count(ResourceType.Bread),4:0}");
            sb.AppendLine($"  Buildings {s.BuildingsComplete} done, {s.BuildingsUnderConstruction} rising" +
                          $"{(s.SpoilageReductionFactor > 0f ? $"   spoilage -{s.SpoilageReductionFactor * 100f:0}%" : "")}" +
                          $"{(s.Config.EnableTrade ? $"   silver {s.TradeLedger.Silver:0}" : "")}");
            if (s.Config.EnableThreats)
                sb.AppendLine($"  Threat {s.Threat.Stage} (pressure {s.Threat.Pressure:0})   raids survived {s.Threat.TotalRaids}");
            sb.AppendLine($"  Labor  {LaborSummary(s)}");
            sb.AppendLine($"  > {msg}");
            Console.Write(sb.ToString());
        }

        private static string LaborSummary(Settlement s)
        {
            var sb = new StringBuilder();
            AppendJob(sb, s, "for", Profession.Forager);
            AppendJob(sb, s, "wood", Profession.Woodcutter);
            AppendJob(sb, s, "log", Profession.Logger);
            AppendJob(sb, s, "quarry", Profession.Quarryman);
            AppendJob(sb, s, "build", Profession.Builder);
            AppendJob(sb, s, "craft", Profession.Craftsman);
            AppendJob(sb, s, "farm", Profession.Farmer);
            AppendJob(sb, s, "militia", Profession.Militiaman);
            AppendJob(sb, s, "scholar", Profession.Scholar);
            AppendJob(sb, s, "idle", Profession.Idle);
            return sb.ToString().TrimEnd();
        }

        private static void AppendJob(StringBuilder sb, Settlement s, string label, Profession p)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == p &&
                    (p == Profession.Idle || s.Citizens[i].Age >= s.Config.WorkingAge)) n++;
            if (n > 0) sb.Append($"{label}:{n}  ");
        }

        private static bool TryParseBuildingType(string token, out BuildingType type)
        {
            switch (token.ToLowerInvariant())
            {
                case "forager": case "foragerhut": type = BuildingType.ForagerHut; return true;
                case "woodcutter": case "woodcuttercamp": type = BuildingType.WoodcutterCamp; return true;
                case "store": case "storehouse": type = BuildingType.Storehouse; return true;
                case "tower": case "watchtower": type = BuildingType.Watchtower; return true;
                default: return Enum.TryParse(token, true, out type) && Enum.IsDefined(typeof(BuildingType), type);
            }
        }

        private static bool OccupiedAt(Settlement s, int x, int y)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
                if (s.Buildings[i].X == x && s.Buildings[i].Y == y) return true;
            return false;
        }

        private static bool FindBuildTile(Settlement s, out int x, out int y)
        {
            for (int r = 0; r <= 24; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue; // walk outward ring by ring
                        int tx = s.CenterX + dx, ty = s.CenterY + dy;
                        if (!s.Map.InBounds(tx, ty) || s.Map.Get(tx, ty).IsWater || OccupiedAt(s, tx, ty)) continue;
                        x = tx; y = ty; return true;
                    }
            x = s.CenterX; y = s.CenterY; return false;
        }

        private static bool TryF(string s, out float v) =>
            float.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v);

        private static List<string> SplitCommands(string commands) =>
            new List<string>(commands.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));

        private static List<string> ReadStdinCommands()
        {
            var lines = new List<string>();
            string? line;
            while ((line = Console.In.ReadLine()) != null) lines.Add(line);
            return lines;
        }

        // Live single-key control, translated into the same command strings the script path uses.
        private static bool TryReadKeyCommand(out string line)
        {
            line = string.Empty;
            try { if (!Console.KeyAvailable) return false; }
            catch { return false; } // input redirected / no console
            ConsoleKeyInfo k = Console.ReadKey(true);
            switch (char.ToLowerInvariant(k.KeyChar))
            {
                case ' ': line = "\0pause"; return true;
                case '+': case '=': line = "speed 8"; return true;
                case '-': line = "speed 1"; return true;
                case '.': line = "step 1"; return true;
                case 'b': Console.Write("\nbuild> "); line = "build " + (Console.ReadLine() ?? ""); return true;
                case 'l': Console.Write("\nlabor> "); line = "labor " + (Console.ReadLine() ?? ""); return true;
                case 's': line = "save"; return true;
                case 'o': line = "load"; return true;
                case 'h': case '?': line = "help"; return true;
                case 'q': line = "quit"; return true;
                default: return false;
            }
        }

        // ---------------------------------------------------------------- storage & preserving (§8)

        private static Settlement FoundForagerColony(ulong seed, int daysPerSeason)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 20,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.62f, WoodcutterShare = 0.30f,
                StartingFoodUnits = 220f, StartingFirewoodUnits = 220f
            };
            Settlement c = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            int gx = c.CenterX, gy = c.CenterY;
            PlaceComplete(c, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 4; i++) PlaceComplete(c, BuildingType.House, gx + 2 + i, gy);
            return c;
        }

        private static int RunPreserve(ulong seed, int years, int daysPerSeason)
        {
            if (years < 3) years = 6;

            // Two identical forager colonies; only the second keeps a cellar and a smokehouse, so
            // the difference in food kept and food lost is purely the preservation buildings (§8).
            Settlement plain = FoundForagerColony(seed, daysPerSeason);
            Settlement kept = FoundForagerColony(seed, daysPerSeason);
            PlaceComplete(kept, BuildingType.Cellar, kept.CenterX, kept.CenterY + 1);
            PlaceComplete(kept, BuildingType.Smokehouse, kept.CenterX, kept.CenterY + 2);
            kept.RecomputeBuildingEffects();

            Console.WriteLine("Founder's Lands — storage & preserving (GDD §8)");
            Console.WriteLine($"seed={seed}  two identical forager camps; the second adds a root cellar + smokehouse " +
                              $"(spoilage −{kept.SpoilageReductionFactor * 100f:0}%)");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} | {"plain food",10} {"lost",7} | {"preserved food",14} {"lost",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                SettlementSimulation.Step(plain);
                DayReport r = SettlementSimulation.Step(kept);
                if ((r.Day % daysPerSeason) == (daysPerSeason - 1))
                {
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} | {plain.FoodUnits(),10:0} {plain.TotalSpoiled,7:0} | " +
                                      $"{kept.FoodUnits(),14:0} {kept.TotalSpoiled,7:0}");
                }
            }

            float saved = plain.TotalSpoiled - kept.TotalSpoiled;
            Console.WriteLine();
            Console.WriteLine($"Over {years} years the cellar + smokehouse saved {saved:0} units from spoiling " +
                              $"({plain.TotalSpoiled:0} → {kept.TotalSpoiled:0}); food on hand {plain.FoodUnits():0} → {kept.FoodUnits():0}.");
            return 0;
        }

        // ---------------------------------------------------------------- grand campaign (all systems at once)

        // Builds the all-systems colony shared by the console demo and the integration test.
        private static Settlement FoundGrandColony(ulong seed)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                // A bountiful valley so food never knife-edges the many specialists this colony fields.
                ForagerNutritionPerDay = 7.0f, WoodcutterFirewoodPerDay = 9.0f,
                ForagerShare = 0.40f, FarmerShare = 0.10f, WoodcutterShare = 0.10f, LoggerShare = 0.06f,
                MinerShare = 0.05f, CraftsmanShare = 0.11f, MilitiaShare = 0.10f, ScholarShare = 0.05f,
                StorehouseCapacity = 30000f,
                StartingFoodUnits = 800f, StartingFirewoodUnits = 500f,
                StartingWoodUnits = 80f, StartingStoneUnits = 60f,
                EnableThreats = true,
                EnablePopulationDynamics = true,
                EnableTrade = true,
                EnableTechnology = true
            };
            Settlement c = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            // The founders arrive knowing the basics (techs 0..6); scholars will research the rest.
            for (int t = 0; t <= 6; t++) ResearchSystem.Grant(c, t);

            // Export the manufacturing surplus for silver.
            c.TradePolicy.Sell(ResourceType.Planks).Sell(ResourceType.IronIngot);

            int gx = c.CenterX, gy = c.CenterY;
            PlaceComplete(c, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 10; i++) PlaceComplete(c, BuildingType.House, gx + 2 + i, gy);
            PlaceComplete(c, BuildingType.ForagerHut, gx - 1, gy);
            PlaceComplete(c, BuildingType.WoodcutterCamp, gx - 2, gy);
            PlaceComplete(c, BuildingType.Sawmill, gx, gy + 1);
            PlaceComplete(c, BuildingType.Smelter, gx, gy + 2);
            PlaceComplete(c, BuildingType.Smithy, gx, gy + 3);
            PlaceComplete(c, BuildingType.Mill, gx, gy + 4);
            PlaceComplete(c, BuildingType.Bakery, gx, gy + 5);
            for (int i = 0; i < 8; i++) PlaceComplete(c, BuildingType.Field, gx - 1 - i, gy + 6);
            PlaceComplete(c, BuildingType.Market, gx + 1, gy + 1);
            for (int i = 0; i < 4; i++) PlaceComplete(c, BuildingType.Watchtower, gx + 1, gy + 2 + i); // a well-guarded town
            PlaceComplete(c, BuildingType.Palisade, gx + 2, gy + 2);
            c.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f); // prime the smelter
            return c;
        }

        private static int RunGrand(ulong seed, int years, int daysPerSeason)
        {
            if (years < 10) years = 25; // a full campaign plays out over decades
            Settlement colony = FoundGrandColony(seed);

            Console.WriteLine("Founder's Lands — grand campaign: every system at once (GDD, all modules)");
            Console.WriteLine($"seed={seed}  start pop={colony.AlivePopulation}  housing={colony.HousingCapacity}  " +
                              $"farming · production · trade · threats · demography · technology");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Pop",4} {"Food",6} {"Bread",6} {"Silver",7} {"Tech",5} {"Bonus",6} {"Threat",-7} {"Raids",6} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if (r.Day % (daysPerSeason * 4) == (daysPerSeason * 4 - 1))
                {
                    Console.WriteLine($"{r.Year,4} {colony.AlivePopulation,4} {r.FoodUnits,6:0} " +
                                      $"{colony.Storehouse.Count(ResourceType.Bread),6:0} {colony.TradeLedger.Silver,7:0} " +
                                      $"{colony.UnlockedTechs.Count + "/" + colony.Techs.Count,5} {colony.TechWorkBonus * 100f,5:0}% " +
                                      $"{colony.Threat.Stage,-7} {colony.Threat.TotalRaids,6} {r.AvgHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- the colony fell on day {r.Day} --"); break; }
            }

            Console.WriteLine();
            Console.WriteLine($"After {years} years: {colony.AlivePopulation} settlers, {colony.TradeLedger.Silver:0} silver, " +
                              $"{colony.UnlockedTechs.Count}/{colony.Techs.Count} techs (+{colony.TechWorkBonus * 100f:0}% work), " +
                              $"{colony.Threat.TotalRaids} raids survived; born {colony.TotalBirths}, arrived {colony.TotalImmigrants}.");
            return 0;
        }

        // ---------------------------------------------------------------- technology (research tree)

        private static int RunTech(ulong seed, int years, int daysPerSeason)
        {
            if (years < 4) years = 6; // the tree fills over a few years
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 24,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.58f, WoodcutterShare = 0.25f, ScholarShare = 0.13f,
                StartingFoodUnits = 320f, StartingFirewoodUnits = 260f,
                EnableTechnology = true,
                ResearchPerScholarPerDay = 1.5f
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            int scholars = 0; foreach (var cz in colony.Citizens) if (cz.Profession == Profession.Scholar) scholars++;

            Console.WriteLine("Founder's Lands — technology & progression (GDD §16)");
            Console.WriteLine($"seed={seed}  pop={colony.AlivePopulation}  scholars={scholars}  tech tree: {colony.Techs.Count} technologies");
            Console.WriteLine("scholars accrue research; each tech unlocks buildings and lifts the work bonus.");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Techs",6} {"Researching",-16} {"Progress",-10} {"Bonus",6} {"Pop",4} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            int lastCount = -1;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                bool unlockedThisStep = colony.UnlockedTechs.Count != lastCount && lastCount >= 0;
                lastCount = colony.UnlockedTechs.Count;

                if ((r.Day % daysPerSeason) == (daysPerSeason - 1) || unlockedThisStep)
                {
                    Technology next = ResearchSystem.NextTarget(colony);
                    string researching = next != null ? next.Name : "(complete)";
                    string progress = next != null ? $"{colony.ResearchProgress:0}/{next.Cost:0}" : "—";
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {colony.UnlockedTechs.Count + "/" + colony.Techs.Count,6} " +
                                      $"{researching,-16} {progress,-10} {colony.TechWorkBonus * 100f,5:0}% {r.Population,4} {r.AvgHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- wiped out day {r.Day} --"); break; }
            }

            Console.WriteLine();
            Console.WriteLine($"Researched {colony.UnlockedTechs.Count}/{colony.Techs.Count} technologies; " +
                              $"work bonus +{colony.TechWorkBonus * 100f:0}%. Buildings unlocked: {colony.UnlockedBuildings.Count} beyond the founding set.");
            return 0;
        }

        // ---------------------------------------------------------------- trade (caravans & contracts)

        private static int RunTrade(ulong seed, int years, int daysPerSeason)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 30,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.52f, WoodcutterShare = 0.16f, LoggerShare = 0.16f, CraftsmanShare = 0.13f,
                StorehouseCapacity = 20000f,
                StartingFoodUnits = 600f, StartingFirewoodUnits = 350f, StartingWoodUnits = 40f,
                EnableTrade = true
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            // A timber-and-sawmill town: it exports planks and imports the tools it can't forge.
            colony.TradePolicy.Sell(ResourceType.Planks, reserve: 0f).Buy(ResourceType.Tools, target: 80f);

            int gx = colony.CenterX, gy = colony.CenterY;
            PlaceComplete(colony, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 7; i++) PlaceComplete(colony, BuildingType.House, gx + 2 + i, gy);
            PlaceComplete(colony, BuildingType.Sawmill, gx, gy + 1);
            PlaceComplete(colony, BuildingType.Market, gx, gy + 2);

            Console.WriteLine("Founder's Lands — trade & contracts (GDD §12)");
            Console.WriteLine($"seed={seed}  pop={colony.AlivePopulation}  market+sawmill; sells Planks, buys Tools; caravan every {config.TradeIntervalDays} days");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Pop",4} {"Planks",7} {"Tools",6} {"Silver",8} {"Visits",7} {"Exp",6} {"Imp",5} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if ((r.Day % daysPerSeason) == (daysPerSeason - 1))
                {
                    TradeLedger t = colony.TradeLedger;
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {r.Population,4} " +
                                      $"{colony.Storehouse.Count(ResourceType.Planks),7:0} {colony.Storehouse.Count(ResourceType.Tools),6:0} " +
                                      $"{t.Silver,8:0} {t.CaravanVisits,7} {t.TotalExported,6:0} {t.TotalImported,5:0} {r.AvgHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- wiped out day {r.Day} --"); break; }
            }

            TradeLedger led = colony.TradeLedger;
            Console.WriteLine();
            Console.WriteLine($"Trade over {years} years: {led.CaravanVisits} caravans, " +
                              $"{led.TotalExported:0} planks exported for {led.SilverEarned:0} silver, " +
                              $"{led.TotalImported:0} tools imported for {led.SilverSpent:0}; purse {led.Silver:0} silver.");
            return 0;
        }

        // ---------------------------------------------------------------- demography (births/aging/migration)

        private static int RunDemography(ulong seed, int years, int daysPerSeason)
        {
            if (years < 12) years = 25; // growth and the stable plateau show over a generation
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 16,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.62f, WoodcutterShare = 0.30f,
                // A bountiful valley: food and fuel are plentiful per worker, so housing — not hunger
                // or cold — is what caps growth, keeping the spotlight on the demographics.
                ForagerNutritionPerDay = 7.0f, WoodcutterFirewoodPerDay = 9.0f,
                StorehouseCapacity = 8000f,
                StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
                EnablePopulationDynamics = true
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            int gx = colony.CenterX, gy = colony.CenterY;
            PlaceComplete(colony, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 7; i++) PlaceComplete(colony, BuildingType.House, gx + 2 + i, gy); // room to grow into
            colony.RecomputeBuildingEffects(); // so the header shows housing before day one

            Console.WriteLine("Founder's Lands — demographics & growth (GDD §11)");
            Console.WriteLine($"seed={seed}  start pop={colony.AlivePopulation}  housing={colony.HousingCapacity}");
            Console.WriteLine("births need food + housing; migrants come for prosperity; the young work only at 16; elders pass.");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Pop",4} {"Births",7} {"Immig",6} {"OldAge",7} {"Left",5} {"Starved",8} {"AvgAge",7} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if (r.Day % (daysPerSeason * 4) == (daysPerSeason * 4 - 1))
                {
                    Console.WriteLine($"{r.Year,4} {colony.AlivePopulation,4} {colony.TotalBirths,7} {colony.TotalImmigrants,6} " +
                                      $"{colony.NaturalDeaths,7} {colony.TotalLeft,5} {colony.TotalDeaths - colony.NaturalDeaths,8} " +
                                      $"{AverageAge(colony),7:0.0} {colony.AverageHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- died out year {r.Year} --"); break; }
            }

            Console.WriteLine();
            Console.WriteLine($"From {config.StartingPopulation} settlers to {colony.AlivePopulation}: " +
                              $"{colony.TotalBirths} born, {colony.TotalImmigrants} arrived, " +
                              $"{colony.NaturalDeaths} died of old age, {colony.TotalLeft} moved on.");
            return 0;
        }

        private static float AverageAge(Settlement colony)
        {
            float sum = 0f; int n = 0;
            foreach (var cz in colony.Citizens) if (cz.Alive) { sum += cz.Age; n++; }
            return n == 0 ? 0f : sum / n;
        }

        // ---------------------------------------------------------------- farming (seasonal fields)

        private static int RunFarming(ulong seed, int years, int daysPerSeason)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.25f, WoodcutterShare = 0.20f, FarmerShare = 0.33f, CraftsmanShare = 0.14f,
                StorehouseCapacity = 20000f,
                // A new colony must bridge its first spring and summer on stores until the first
                // autumn harvest — only then does home-grown bread carry it through winter.
                StartingFoodUnits = 2000f, StartingFirewoodUnits = 450f,
                StartingWoodUnits = 40f, StartingStoneUnits = 20f
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            int gx = colony.CenterX, gy = colony.CenterY;
            PlaceComplete(colony, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) PlaceComplete(colony, BuildingType.House, gx + 2 + i, gy);
            PlaceComplete(colony, BuildingType.Mill, gx, gy + 1);
            PlaceComplete(colony, BuildingType.Bakery, gx, gy + 2);
            for (int i = 0; i < 10; i++) PlaceComplete(colony, BuildingType.Field, gx - 1 - i, gy + 4);

            int farmers = 0; foreach (var cz in colony.Citizens) if (cz.Profession == Profession.Farmer) farmers++;

            Console.WriteLine("Founder's Lands — seasonal farming (GDD §9)");
            Console.WriteLine($"seed={seed}  pop={colony.AlivePopulation}  farmers={farmers}  fields=10  soilFertility={colony.SoilFertility:0.00}");
            Console.WriteLine("cycle: sow in spring, ripen through summer, reap grain in autumn; mill -> flour -> bread (bakery burns firewood)");
            Console.WriteLine();
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Pop",4} {"Grain",6} {"Flour",6} {"Bread",6} {"Food",6} {"Firewd",7} {"Health",7}");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if ((r.Day % daysPerSeason) == (daysPerSeason - 1))
                {
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {r.Population,4} " +
                                      $"{colony.Storehouse.Count(ResourceType.Grain),6:0} {colony.Storehouse.Count(ResourceType.Flour),6:0} " +
                                      $"{colony.Storehouse.Count(ResourceType.Bread),6:0} {r.FoodUnits,6:0} {r.FirewoodUnits,7:0} {r.AvgHealth,7:0.0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- wiped out day {r.Day} --"); break; }
            }

            Console.WriteLine();
            Console.WriteLine($"Survivors {colony.AlivePopulation}/40, total deaths {colony.TotalDeaths}. " +
                              $"A year's bread baked from home-grown grain feeds the colony through winter.");
            return 0;
        }

        // ---------------------------------------------------------------- raiders (AI Director)

        private static int RunRaiders(ulong seed, int years, int daysPerSeason)
        {
            Console.WriteLine("Founder's Lands — bandits & the AI Director (GDD §13)");
            Console.WriteLine("Two equally rich colonies on the same seed: one left open, one kept guarded.");
            RunOneRaider(seed, years, daysPerSeason, defended: false);
            RunOneRaider(seed, years, daysPerSeason, defended: true);
            return 0;
        }

        private static void RunOneRaider(ulong seed, int years, int daysPerSeason, bool defended)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                DaysPerSeason = daysPerSeason,
                ForagerShare = 0.45f, WoodcutterShare = 0.15f, LoggerShare = 0.08f,
                QuarrymanShare = 0.04f, MinerShare = 0.06f, CraftsmanShare = 0.10f,
                MilitiaShare = defended ? 0.10f : 0.0f,
                StorehouseCapacity = 20000f,
                StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
                StartingWoodUnits = 60f, StartingStoneUnits = 40f,
                EnableThreats = true
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            colony.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f);

            int gx = colony.CenterX, gy = colony.CenterY;
            PlaceComplete(colony, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) PlaceComplete(colony, BuildingType.House, gx + 2 + i, gy);
            PlaceComplete(colony, BuildingType.Sawmill, gx, gy + 1);
            PlaceComplete(colony, BuildingType.Smelter, gx, gy + 2);
            PlaceComplete(colony, BuildingType.Smithy, gx, gy + 3);
            PlaceComplete(colony, BuildingType.Market, gx, gy + 4);
            if (defended)
            {
                PlaceComplete(colony, BuildingType.Watchtower, gx - 1, gy);
                PlaceComplete(colony, BuildingType.Watchtower, gx - 1, gy + 2);
                PlaceComplete(colony, BuildingType.Palisade, gx - 1, gy + 4);
            }

            int militia = 0;
            foreach (var cz in colony.Citizens) if (cz.Profession == Profession.Militiaman) militia++;

            Console.WriteLine();
            Console.WriteLine(defended
                ? $"== GUARDED colony ({militia} militia + 2 towers + palisade) =="
                : "== OPEN colony (no militia, no walls) ==");
            Console.WriteLine($"{"Year",4} {"Season",-7} {"Stage",-7} {"Press",6} {"Camp",5} {"Pop",4} {"Food",6} {"Tools",6} {"Health",7}  raids/stolen");

            int totalDays = years * daysPerSeason * 4;
            for (int d = 0; d < totalDays; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                var th = colony.Threat;
                if ((r.Day % daysPerSeason) == (daysPerSeason - 1))
                {
                    Console.WriteLine($"{r.Year,4} {r.Season,-7} {th.Stage,-7} {th.Pressure,6:0.0} {th.CampStrength,5:0} " +
                                      $"{r.Population,4} {r.FoodUnits,6:0} {colony.Storehouse.Count(ResourceType.Tools),6:0} {r.AvgHealth,7:0.0}  " +
                                      $"{th.TotalRaids}/{th.TotalStolen:0}");
                }
                if (colony.AlivePopulation == 0) { Console.WriteLine($"  -- wiped out day {r.Day} --"); break; }
            }

            ThreatState f = colony.Threat;
            Console.WriteLine($"  totals: raids={f.TotalRaids}, thefts={f.TotalThefts}, goods stolen={f.TotalStolen:0}, " +
                              $"raid casualties={f.TotalCasualties}, survivors={colony.AlivePopulation}/40.");
        }

        // ---------------------------------------------------------------- logistics

        private static int RunLogistics(ulong seed)
        {
            var settings = new WorldGenSettings { Width = 96, Height = 96 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            Coord site = FindLand(map, map.Width / 2, map.Height / 2);
            Coord target = FindResourceFar(map, site, ResourceNodeKind.Wood, 18);

            var roads = new RoadNetwork(map.Width, map.Height);
            var cost = new MovementCost(map, roads, snow: 0f);

            Path first = Pathfinder.Find(cost, map.Width, map.Height, site, target);
            if (!first.Found)
            {
                Console.WriteLine("No land route between site and target on this seed.");
                return 1;
            }

            Console.WriteLine("Founder's Lands — logistics & living roads (GDD §7, §12)");
            Console.WriteLine($"seed={seed}  site={site}  target={target}  pathTiles={first.Length}");
            Console.WriteLine($"initial route cost (wilderness): {first.Cost:0.0}");
            Console.WriteLine();
            Console.WriteLine($"{"trip",4} {"routeCost",10} {"trails",7} {"roads",6}");

            const float trafficPerTrip = 3f;
            for (int trip = 1; trip <= 30; trip++)
            {
                Path path = Pathfinder.Find(cost, map.Width, map.Height, site, target);
                LogisticsSystem.StampRoute(roads, path, trafficPerTrip);

                if (trip == 1 || trip == 3 || trip == 5 || trip == 10 || trip == 20 || trip == 30)
                {
                    CountRoads(roads, out int trails, out int builtRoads);
                    Console.WriteLine($"{trip,4} {path.Cost,10:0.0} {trails,7} {builtRoads,6}");
                }
            }

            Path final = Pathfinder.Find(cost, map.Width, map.Height, site, target);
            Console.WriteLine();
            Console.WriteLine($"final route cost (worn road): {final.Cost:0.0}  " +
                              $"({100f * (1f - final.Cost / first.Cost):0}% cheaper than wilderness)");
            Console.WriteLine();
            DrawRoute(map, roads, site, target);
            return 0;
        }

        private static Coord FindLand(WorldMap map, int cx, int cy)
        {
            for (int r = 0; r < map.Width; r++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx > -r && dx < r && dy > -r && dy < r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (map.InBounds(x, y) && !map.Get(x, y).IsWater) return new Coord(x, y);
                    }
                }
            }
            return new Coord(cx, cy);
        }

        private static Coord FindResourceFar(WorldMap map, Coord from, ResourceNodeKind kind, int minDist)
        {
            Coord best = from;
            int bestScore = int.MaxValue;
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    if (map.Get(x, y).Resource != kind) continue;
                    int dist = new Coord(x, y).ChebyshevTo(from);
                    if (dist < minDist) continue;
                    int score = dist; // prefer the closest node beyond the minimum distance
                    if (score < bestScore) { bestScore = score; best = new Coord(x, y); }
                }
            }
            return best;
        }

        private static void CountRoads(RoadNetwork roads, out int trails, out int builtRoads)
        {
            trails = 0; builtRoads = 0;
            for (int y = 0; y < roads.Height; y++)
            {
                for (int x = 0; x < roads.Width; x++)
                {
                    RoadLevel l = roads.LevelAt(x, y);
                    if (l == RoadLevel.Trail) trails++;
                    else if (l == RoadLevel.Road) builtRoads++;
                }
            }
        }

        private static void DrawRoute(WorldMap map, RoadNetwork roads, Coord site, Coord target)
        {
            int minX = Math.Max(0, Math.Min(site.X, target.X) - 2);
            int maxX = Math.Min(map.Width - 1, Math.Max(site.X, target.X) + 2);
            int minY = Math.Max(0, Math.Min(site.Y, target.Y) - 2);
            int maxY = Math.Min(map.Height - 1, Math.Max(site.Y, target.Y) + 2);
            if (maxX - minX > 78) return;

            Console.WriteLine("Worn route (S=site, X=target, ==road, --trail):");
            for (int y = minY; y <= maxY; y++)
            {
                var sb = new StringBuilder();
                for (int x = minX; x <= maxX; x++)
                {
                    if (x == site.X && y == site.Y) { sb.Append('S'); continue; }
                    if (x == target.X && y == target.Y) { sb.Append('X'); continue; }
                    RoadLevel l = roads.LevelAt(x, y);
                    if (l == RoadLevel.Road) { sb.Append('='); continue; }
                    if (l == RoadLevel.Trail) { sb.Append('-'); continue; }
                    sb.Append(map.Get(x, y).IsWater ? '~' : '.');
                }
                Console.WriteLine(sb.ToString());
            }
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
