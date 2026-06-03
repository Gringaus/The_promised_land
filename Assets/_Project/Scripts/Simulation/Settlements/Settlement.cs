using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Production;
using FoundersLands.Simulation.Technology;
using FoundersLands.Simulation.Threats;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.Trade;
using FoundersLands.Simulation.World;

namespace FoundersLands.Simulation.Settlements
{
    /// <summary>
    /// Mutable state of one settlement: where it sits on the map, its people, its storehouse,
    /// its buildings, the clock, and the map-derived productivity it can draw on. The rules
    /// that evolve this state live in <see cref="SettlementSimulation"/>.
    /// </summary>
    public sealed class Settlement
    {
        public readonly WorldMap Map;
        public readonly int CenterX;
        public readonly int CenterY;

        public readonly List<Citizen> Citizens = new List<Citizen>();
        public readonly List<Building> Buildings = new List<Building>();
        public readonly Inventory Storehouse;
        public readonly SimulationClock Clock;
        public readonly ResourceCatalog Catalog;
        public readonly BuildingCatalog BuildingCatalog;
        public readonly RecipeCatalog Recipes;
        public readonly TechCatalog Techs;
        public readonly SeasonDef[] Seasons;
        public readonly SettlementConfig Config;

        // Map-derived productivity gates (set at creation). Tie Module 1's terrain to how
        // much food / firewood / timber / stone this valley can yield (GDD §3).
        public float FoodFactor;
        public float FirewoodFactor;
        public float StoneFactor;
        public float IronFactor;
        public float SoilFertility; // avg fertility around the site, drives field yield (GDD §9)
        public ResourceType PrimaryFood;
        public ResourceQuality ForageQuality;

        // Aggregate effects of completed buildings, refreshed each day (GDD §10).
        public int HousingCapacity;
        public float AvgShelterQuality;
        public float ForagerBonus;
        public float WoodcutterBonus;
        public float BuildingDefense; // defence from completed towers/palisade (GDD §13)
        public float SpoilageReductionFactor; // 0..~0.85, from cellars/smokehouses (GDD §8)
        public readonly float BaseStorageCapacity;

        public float TotalSpoiled; // running total of food lost to spoilage (reporting stat)

        public DeterministicRng Rng;

        public int TotalDeaths;

        // Demographics (GDD §11); inert while Config.EnablePopulationDynamics is false.
        public int NextCitizenId;
        public float BirthProgress;
        public float MigrationProgress;
        public int TotalBirths;
        public int TotalImmigrants;
        public int TotalLeft;       // emigrated away
        public int NaturalDeaths;   // died of old age

        // AI Director state (GDD §13); inert while Config.EnableThreats is false.
        public readonly ThreatState Threat = new ThreatState();

        // Trade (GDD §12); inert while Config.EnableTrade is false. Policy is the colony's standing
        // buy/sell orders; the ledger holds its silver and tally.
        public readonly TradePolicy TradePolicy = new TradePolicy();
        public readonly TradeLedger TradeLedger = new TradeLedger();

        // Technology (GDD §16); inert while Config.EnableTechnology is false. Research accrues toward
        // the next tech; unlocked techs open buildings and add a work bonus.
        public float ResearchProgress;
        public float TechWorkBonus;
        public string LastUnlockedTech;
        public readonly HashSet<int> UnlockedTechs = new HashSet<int>();
        public readonly HashSet<BuildingType> UnlockedBuildings = new HashSet<BuildingType>();

        public Settlement(WorldMap map, int centerX, int centerY, SettlementConfig config,
            ResourceCatalog catalog, SeasonDef[] seasons, BuildingCatalog buildingCatalog = null,
            RecipeCatalog recipes = null, TechCatalog techs = null)
        {
            Map = map;
            CenterX = centerX;
            CenterY = centerY;
            Config = config;
            Catalog = catalog;
            Seasons = seasons;
            BuildingCatalog = buildingCatalog ?? BuildingCatalog.CreateDefault();
            Recipes = recipes ?? RecipeCatalog.CreateDefault();
            Techs = techs ?? TechCatalog.CreateDefault();
            Storehouse = new Inventory(config.StorehouseCapacity);
            BaseStorageCapacity = config.StorehouseCapacity;
            Clock = new SimulationClock(config.DaysPerSeason);
            TradeLedger.Silver = config.StartingSilver;
        }

        public int AlivePopulation
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Citizens.Count; i++) if (Citizens[i].Alive) n++;
                return n;
            }
        }

        public float AverageHealth
        {
            get
            {
                float sum = 0f; int n = 0;
                for (int i = 0; i < Citizens.Count; i++)
                {
                    if (!Citizens[i].Alive) continue;
                    sum += Citizens[i].Health; n++;
                }
                return n == 0 ? 0f : sum / n;
            }
        }

        public int BuildingsComplete
        {
            get { int n = 0; for (int i = 0; i < Buildings.Count; i++) if (Buildings[i].Complete) n++; return n; }
        }

        public int BuildingsUnderConstruction
        {
            get { int n = 0; for (int i = 0; i < Buildings.Count; i++) if (!Buildings[i].Complete) n++; return n; }
        }

        public float StoredNutrition()
        {
            float sum = 0f;
            foreach (ItemStack s in Storehouse.Stacks())
            {
                ResourceDef def = Catalog.Get(s.Type);
                if (def.IsFood) sum += s.Amount * def.Nutrition * s.Quality.Multiplier();
            }
            return sum;
        }

        public float FoodUnits()
        {
            float sum = 0f;
            foreach (ItemStack s in Storehouse.Stacks())
            {
                if (Catalog.Get(s.Type).IsFood) sum += s.Amount;
            }
            return sum;
        }

        /// <summary>
        /// Place a blueprint to be built (GDD §10). Returns the new building, or null if its type is
        /// still locked behind research (only possible when technology is enabled — see GDD §16).
        /// </summary>
        public Building PlaceBlueprint(BuildingType type, int x, int y)
        {
            if (!IsBuildingUnlocked(type)) return null;
            Building b = new Building(BuildingCatalog.Get(type), x, y);
            Buildings.Add(b);
            return b;
        }

        /// <summary>Whether a building type may be placed: always true unless technology gates it.</summary>
        public bool IsBuildingUnlocked(BuildingType type)
        {
            if (!Config.EnableTechnology) return true;
            return Techs.BaseBuildings.Contains(type) || UnlockedBuildings.Contains(type);
        }

        /// <summary>Refresh the aggregate effects of completed buildings.</summary>
        public void RecomputeBuildingEffects()
        {
            int housing = 0;
            float shelterWeighted = 0f, storage = 0f, forager = 0f, woodcutter = 0f, defense = 0f;
            float keptFraction = 1f; // multiplied down by each preserver (diminishing returns)

            for (int i = 0; i < Buildings.Count; i++)
            {
                Building b = Buildings[i];
                if (!b.Complete) continue;
                BuildingDef def = BuildingCatalog.Get(b.Type);
                housing += def.Housing;
                shelterWeighted += def.Housing * def.ShelterQuality;
                storage += def.StorageBonus;
                forager += def.ForagerBonus;
                woodcutter += def.WoodcutterBonus;
                defense += def.DefenseBonus;
                if (def.SpoilageReduction > 0f) keptFraction *= 1f - def.SpoilageReduction;
            }

            HousingCapacity = housing;
            AvgShelterQuality = housing > 0 ? shelterWeighted / housing : 0f;
            ForagerBonus = forager;
            WoodcutterBonus = woodcutter;
            BuildingDefense = defense;
            // Combined preservation, capped: even a cellar + smokehouse can't stop spoilage entirely.
            SpoilageReductionFactor = (1f - keptFraction) > 0.85f ? 0.85f : 1f - keptFraction;
            Storehouse.Capacity = BaseStorageCapacity + storage;
        }

        /// <summary>Stable hash of the whole settlement state for determinism tests.</summary>
        public ulong StateHash()
        {
            ulong h = StableHash.Fnv1aOffset;
            h = StableHash.Combine(h, Clock.Day);
            h = StableHash.Combine(h, TotalDeaths);
            for (int i = 0; i < Citizens.Count; i++) h = Citizens[i].Hash(h);
            h = Storehouse.Hash(h);
            h = StableHash.Combine(h, Buildings.Count);
            for (int i = 0; i < Buildings.Count; i++) h = Buildings[i].Hash(h);
            h = Threat.Hash(h);
            h = TradeLedger.Hash(h);
            h = StableHash.Combine(h, (int)(ResearchProgress * 100f));
            for (int i = 0; i < Techs.Count; i++)
                h = StableHash.Combine(h, UnlockedTechs.Contains(i) ? i + 1 : 0); // tech tree state, in catalog order
            h = StableHash.Combine(h, (int)(BirthProgress * 1000f));
            h = StableHash.Combine(h, (int)(MigrationProgress * 1000f));
            h = StableHash.Combine(h, TotalBirths);
            h = StableHash.Combine(h, TotalImmigrants);
            h = StableHash.Combine(h, TotalLeft);
            h = StableHash.Combine(h, NaturalDeaths);
            return h;
        }
    }
}
