using System.Collections.Generic;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;

namespace FoundersLands.Simulation.Settlements
{
    /// <summary>
    /// Mutable state of one settlement: where it sits on the map, its people, its single
    /// storehouse (Module 2), the clock, and the map-derived productivity it can draw on.
    /// The rules that evolve this state live in <see cref="SettlementSimulation"/>.
    /// </summary>
    public sealed class Settlement
    {
        public readonly WorldMap Map;
        public readonly int CenterX;
        public readonly int CenterY;

        public readonly List<Citizen> Citizens = new List<Citizen>();
        public readonly Inventory Storehouse;
        public readonly SimulationClock Clock;
        public readonly ResourceCatalog Catalog;
        public readonly SeasonDef[] Seasons;
        public readonly SettlementConfig Config;

        // Map-derived productivity gates (set at creation). Tie Module 1's terrain to
        // how much food/firewood this valley can yield (GDD §3 "видимая экономика").
        public float FoodFactor;
        public float FirewoodFactor;
        public ResourceType PrimaryFood;
        public ResourceQuality ForageQuality;

        public DeterministicRng Rng;

        public int TotalDeaths;

        public Settlement(WorldMap map, int centerX, int centerY, SettlementConfig config,
            ResourceCatalog catalog, SeasonDef[] seasons)
        {
            Map = map;
            CenterX = centerX;
            CenterY = centerY;
            Config = config;
            Catalog = catalog;
            Seasons = seasons;
            Storehouse = new Inventory(config.StorehouseCapacity);
            Clock = new SimulationClock(config.DaysPerSeason);
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

        /// <summary>Stable hash of the whole settlement state for determinism tests.</summary>
        public ulong StateHash()
        {
            ulong h = StableHash.Fnv1aOffset;
            h = StableHash.Combine(h, Clock.Day);
            h = StableHash.Combine(h, TotalDeaths);
            for (int i = 0; i < Citizens.Count; i++) h = Citizens[i].Hash(h);
            h = Storehouse.Hash(h);
            return h;
        }
    }
}
