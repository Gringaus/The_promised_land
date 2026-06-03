using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Threats;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.Trade;
using FoundersLands.Simulation.World;

namespace FoundersLands.Simulation.SaveLoad
{
    /// <summary>
    /// Serializes a game to a compact, versioned, dependency-free text format and back
    /// (GDD §21). Because world generation is deterministic, a save stores only the seed and
    /// the generation settings — the map is regenerated on load — plus the full settlement
    /// state (people, storehouse, buildings, clock). The simulation assembly owns only the
    /// string form; file I/O is left to the caller so this stays engine-independent.
    /// </summary>
    public static class SaveGame
    {
        public const int Version = 1;

        public static string Save(Settlement s, WorldGenSettings settings)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var sb = new StringBuilder(4096);
            sb.Append("FLSAVE ").Append(Version).Append('\n');
            sb.Append("SEED ").Append(s.Map.Seed.ToString(CultureInfo.InvariantCulture)).Append('\n');

            FieldSerializer.Write(sb, "WGS", settings);
            FieldSerializer.Write(sb, "CFG", s.Config);

            sb.Append("SET ").Append(s.CenterX).Append(' ').Append(s.CenterY).Append(' ')
              .Append((int)s.PrimaryFood).Append(' ').Append((int)s.ForageQuality).Append(' ')
              .Append(F(s.FoodFactor)).Append(' ').Append(F(s.FirewoodFactor)).Append(' ')
              .Append(F(s.StoneFactor)).Append(' ').Append(F(s.IronFactor)).Append(' ')
              .Append(F(s.SoilFertility)).Append('\n');
            sb.Append("CLK ").Append(s.Clock.Day).Append('\n');
            sb.Append("RNG ").Append(s.Rng.State.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("DTH ").Append(s.TotalDeaths).Append('\n');

            ThreatState th = s.Threat;
            sb.Append("THR ").Append(F(th.Pressure)).Append(' ').Append((int)th.Stage).Append(' ')
              .Append(th.DaysInStage).Append(' ').Append(F(th.CampStrength)).Append(' ')
              .Append(th.TotalRaids).Append(' ').Append(th.TotalThefts).Append(' ')
              .Append(F(th.TotalStolen)).Append(' ').Append(th.TotalCasualties).Append('\n');

            sb.Append("POP ").Append(F(s.BirthProgress)).Append(' ').Append(F(s.MigrationProgress)).Append(' ')
              .Append(s.NextCitizenId).Append(' ').Append(s.TotalBirths).Append(' ')
              .Append(s.TotalImmigrants).Append(' ').Append(s.TotalLeft).Append(' ').Append(s.NaturalDeaths).Append('\n');

            TradeLedger tl = s.TradeLedger;
            sb.Append("TRD ").Append(F(tl.Silver)).Append(' ').Append(tl.CaravanVisits).Append(' ')
              .Append(tl.Ambushes).Append(' ').Append(F(tl.TotalExported)).Append(' ').Append(F(tl.TotalImported)).Append(' ')
              .Append(F(tl.SilverEarned)).Append(' ').Append(F(tl.SilverSpent)).Append('\n');

            sb.Append("TEC ").Append(F(s.ResearchProgress)).Append(' ').Append(s.UnlockedTechs.Count);
            for (int i = 0; i < s.Techs.Count; i++) if (s.UnlockedTechs.Contains(i)) sb.Append(' ').Append(i);
            sb.Append('\n');

            // Standing trade orders (the player's policy) so a loaded game keeps trading the same way.
            var orders = s.TradePolicy.Orders;
            sb.Append("TPO ").Append(orders.Count);
            for (int i = 0; i < orders.Count; i++)
                sb.Append(' ').Append((int)orders[i].Type).Append(' ').Append((int)orders[i].Mode).Append(' ').Append(F(orders[i].Threshold));
            sb.Append('\n');

            sb.Append("CIT ").Append(s.Citizens.Count).Append('\n');
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen c = s.Citizens[i];
                sb.Append("C ").Append(c.Id).Append(' ').Append((int)c.Profession).Append(' ')
                  .Append(c.Alive ? 1 : 0).Append(' ').Append(F(c.Health)).Append(' ')
                  .Append(F(c.Hunger)).Append(' ').Append(F(c.Cold)).Append(' ')
                  .Append(c.Age).Append(' ').Append(Sanitize(c.Name)).Append('\n');
            }

            IReadOnlyList<ItemStack> stacks = s.Storehouse.Stacks();
            sb.Append("STO ").Append(F(s.Storehouse.Capacity)).Append(' ').Append(stacks.Count).Append('\n');
            for (int i = 0; i < stacks.Count; i++)
            {
                ItemStack st = stacks[i];
                sb.Append("S ").Append((int)st.Type).Append(' ').Append((int)st.Quality).Append(' ').Append(F(st.Amount)).Append('\n');
            }

            sb.Append("BLD ").Append(s.Buildings.Count).Append('\n');
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                Building b = s.Buildings[i];
                IReadOnlyList<MaterialCost> cost = b.Cost;
                sb.Append("B ").Append((int)b.Type).Append(' ').Append(b.X).Append(' ').Append(b.Y).Append(' ')
                  .Append(F(b.WorkDone)).Append(' ').Append(b.Complete ? 1 : 0).Append(' ').Append(cost.Count);
                for (int j = 0; j < cost.Count; j++)
                {
                    sb.Append(' ').Append((int)cost[j].Type).Append(' ').Append(F(b.Delivered(cost[j].Type)));
                }
                sb.Append(' ').Append(b.Planted ? 1 : 0).Append(' ').Append(F(b.CropGrowth)); // field crop state
                sb.Append('\n');
            }

            sb.Append("END\n");
            return sb.ToString();
        }

        public static Settlement Load(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));

            ulong seed = 0;
            var settings = new WorldGenSettings();
            var config = new SettlementConfig();
            int cx = 0, cy = 0, day = 0, deaths = 0;
            ResourceType primaryFood = ResourceType.Berries;
            ResourceQuality forage = ResourceQuality.Standard;
            float foodFactor = 0f, firewoodFactor = 0f, stoneFactor = 0f, ironFactor = 0f, soilFertility = 0f, capacity = 0f;
            ulong rngState = 0;
            float thrPressure = 0f, thrCamp = 0f, thrStolen = 0f;
            int thrStage = 0, thrDays = 0, thrRaids = 0, thrThefts = 0, thrCasualties = 0;
            float birthProgress = 0f, migrationProgress = 0f;
            int nextId = 0, totalBirths = 0, totalImmigrants = 0, totalLeft = 0, naturalDeaths = 0;
            float silver = 0f, tradeExported = 0f, tradeImported = 0f, silverEarned = 0f, silverSpent = 0f;
            int caravanVisits = 0, ambushes = 0;
            float researchProgress = 0f;
            var unlockedTechIds = new List<int>();
            string[] tradeOrderTokens = null;

            var citizens = new List<string[]>();
            var stacks = new List<string[]>();
            var buildings = new List<string[]>();

            string[] lines = text.Replace("\r", "").Split('\n');
            for (int li = 0; li < lines.Length; li++)
            {
                string line = lines[li];
                if (line.Length == 0) continue;
                string[] t = line.Split(' ');
                switch (t[0])
                {
                    case "FLSAVE":
                        if (int.Parse(t[1], CultureInfo.InvariantCulture) != Version)
                            throw new FormatException("Unsupported save version: " + t[1]);
                        break;
                    case "SEED": seed = ulong.Parse(t[1], CultureInfo.InvariantCulture); break;
                    case "WGS": FieldSerializer.Apply(settings, t[1], t[2]); break;
                    case "CFG": FieldSerializer.Apply(config, t[1], t[2]); break;
                    case "SET":
                        cx = Int(t[1]); cy = Int(t[2]);
                        primaryFood = (ResourceType)Int(t[3]); forage = (ResourceQuality)Int(t[4]);
                        foodFactor = Flt(t[5]); firewoodFactor = Flt(t[6]); stoneFactor = Flt(t[7]);
                        if (t.Length > 8) ironFactor = Flt(t[8]);
                        if (t.Length > 9) soilFertility = Flt(t[9]);
                        break;
                    case "CLK": day = Int(t[1]); break;
                    case "RNG": rngState = ulong.Parse(t[1], CultureInfo.InvariantCulture); break;
                    case "DTH": deaths = Int(t[1]); break;
                    case "THR":
                        thrPressure = Flt(t[1]); thrStage = Int(t[2]); thrDays = Int(t[3]); thrCamp = Flt(t[4]);
                        thrRaids = Int(t[5]); thrThefts = Int(t[6]); thrStolen = Flt(t[7]); thrCasualties = Int(t[8]);
                        break;
                    case "POP":
                        birthProgress = Flt(t[1]); migrationProgress = Flt(t[2]); nextId = Int(t[3]);
                        totalBirths = Int(t[4]); totalImmigrants = Int(t[5]); totalLeft = Int(t[6]); naturalDeaths = Int(t[7]);
                        break;
                    case "TRD":
                        silver = Flt(t[1]); caravanVisits = Int(t[2]); ambushes = Int(t[3]);
                        tradeExported = Flt(t[4]); tradeImported = Flt(t[5]); silverEarned = Flt(t[6]); silverSpent = Flt(t[7]);
                        break;
                    case "TEC":
                        researchProgress = Flt(t[1]);
                        for (int j = 3; j < t.Length; j++) unlockedTechIds.Add(Int(t[j])); // t[2] = count
                        break;
                    case "TPO": tradeOrderTokens = t; break;
                    case "STO": capacity = Flt(t[1]); break;
                    case "C": citizens.Add(t); break;
                    case "S": stacks.Add(t); break;
                    case "B": buildings.Add(t); break;
                    // CIT / BLD counts and END are informational; lists are read directly.
                }
            }

            WorldMap map = WorldGenerator.Generate(settings, seed);
            var s = new Settlement(map, cx, cy, config, ResourceCatalog.CreateDefault(),
                SeasonDef.CreateDefault(), BuildingCatalog.CreateDefault())
            {
                PrimaryFood = primaryFood,
                ForageQuality = forage,
                FoodFactor = foodFactor,
                FirewoodFactor = firewoodFactor,
                StoneFactor = stoneFactor,
                IronFactor = ironFactor,
                SoilFertility = soilFertility,
                TotalDeaths = deaths,
                Rng = new DeterministicRng(rngState)
            };
            s.Clock.Advance(day);

            s.Threat.Pressure = thrPressure;
            s.Threat.Stage = (ThreatStage)thrStage;
            s.Threat.DaysInStage = thrDays;
            s.Threat.CampStrength = thrCamp;
            s.Threat.TotalRaids = thrRaids;
            s.Threat.TotalThefts = thrThefts;
            s.Threat.TotalStolen = thrStolen;
            s.Threat.TotalCasualties = thrCasualties;

            // Restore tech state before buildings load, so unlocked types pass the placement gate.
            s.ResearchProgress = researchProgress;
            for (int i = 0; i < unlockedTechIds.Count; i++)
            {
                int id = unlockedTechIds[i];
                if (!s.UnlockedTechs.Add(id)) continue;
                var tech = s.Techs.Get(id);
                for (int u = 0; u < tech.Unlocks.Length; u++) s.UnlockedBuildings.Add(tech.Unlocks[u]);
                s.TechWorkBonus += tech.WorkBonus;
            }

            // Set the effective capacity before adding goods so a full storehouse is not
            // clamped on load (a built storehouse may have raised capacity above the base).
            s.Storehouse.Capacity = capacity;

            for (int i = 0; i < citizens.Count; i++)
            {
                string[] t = citizens[i];
                var c = new Citizen(Int(t[1]), t.Length > 8 ? t[8] : "?", Int(t[7]), (Profession)Int(t[2]))
                {
                    Alive = t[3] == "1",
                    Health = Flt(t[4]),
                    Hunger = Flt(t[5]),
                    Cold = Flt(t[6])
                };
                s.Citizens.Add(c);
            }

            for (int i = 0; i < stacks.Count; i++)
            {
                string[] t = stacks[i];
                s.Storehouse.Add((ResourceType)Int(t[1]), (ResourceQuality)Int(t[2]), Flt(t[3]));
            }

            for (int i = 0; i < buildings.Count; i++)
            {
                string[] t = buildings[i];
                Building b = s.PlaceBlueprint((BuildingType)Int(t[1]), Int(t[2]), Int(t[3]));
                b.WorkDone = Flt(t[4]);
                b.Complete = t[5] == "1";
                int nCost = Int(t[6]);
                for (int j = 0; j < nCost; j++)
                {
                    ResourceType rt = (ResourceType)Int(t[7 + j * 2]);
                    float amt = Flt(t[8 + j * 2]);
                    if (amt > 0f) b.Deliver(rt, amt);
                }
                int cropIdx = 7 + nCost * 2;
                if (t.Length > cropIdx + 1)
                {
                    b.Planted = t[cropIdx] == "1";
                    b.CropGrowth = Flt(t[cropIdx + 1]);
                }
            }

            s.BirthProgress = birthProgress;
            s.MigrationProgress = migrationProgress;
            s.NextCitizenId = nextId > 0 ? nextId : s.Citizens.Count;
            s.TotalBirths = totalBirths;
            s.TotalImmigrants = totalImmigrants;
            s.TotalLeft = totalLeft;
            s.NaturalDeaths = naturalDeaths;

            s.TradeLedger.Silver = silver;
            s.TradeLedger.CaravanVisits = caravanVisits;
            s.TradeLedger.Ambushes = ambushes;
            s.TradeLedger.TotalExported = tradeExported;
            s.TradeLedger.TotalImported = tradeImported;
            s.TradeLedger.SilverEarned = silverEarned;
            s.TradeLedger.SilverSpent = silverSpent;

            if (tradeOrderTokens != null)
            {
                int n = Int(tradeOrderTokens[1]);
                for (int j = 0; j < n; j++)
                {
                    var type = (ResourceType)Int(tradeOrderTokens[2 + j * 3]);
                    int mode = Int(tradeOrderTokens[3 + j * 3]);
                    float threshold = Flt(tradeOrderTokens[4 + j * 3]);
                    if (mode == (int)TradeMode.Buy) s.TradePolicy.Buy(type, threshold);
                    else s.TradePolicy.Sell(type, threshold);
                }
            }

            s.RecomputeBuildingEffects();
            return s;
        }

        private static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }
        private static int Int(string s) { return int.Parse(s, CultureInfo.InvariantCulture); }
        private static float Flt(string s) { return float.Parse(s, CultureInfo.InvariantCulture); }

        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return "?";
            return name.Replace(' ', '_').Replace('\n', '_').Replace('\r', '_');
        }
    }
}
