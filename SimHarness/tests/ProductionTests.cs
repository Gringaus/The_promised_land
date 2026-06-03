using System;
using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Production;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class ProductionSystemTests
    {
        private static readonly BuildingCatalog Buildings = BuildingCatalog.CreateDefault();
        private static readonly RecipeCatalog Recipes = RecipeCatalog.CreateDefault();

        private static Building Workshop(BuildingType type)
        {
            var b = new Building(Buildings.Get(type), 0, 0);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
            return b;
        }

        [Fact]
        public void Recipes_CoverEveryWorkshop()
        {
            Assert.True(Recipes.Has(BuildingType.Sawmill));
            Assert.True(Recipes.Has(BuildingType.Smelter));
            Assert.True(Recipes.Has(BuildingType.Smithy));
            Assert.False(Recipes.Has(BuildingType.House));
        }

        [Fact]
        public void Workshop_ConvertsInputsToOutputs()
        {
            var sites = new List<Building> { Workshop(BuildingType.Sawmill) }; // 2 Wood -> 3 Planks
            var inv = new Inventory(10000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 10f);

            ProductionSystem.Step(sites, inv, Recipes, 1000f); // labour is not the limit here

            Assert.Equal(0f, inv.Count(ResourceType.Wood));     // 5 batches consumed all 10 wood
            Assert.Equal(15f, inv.Count(ResourceType.Planks));  // 5 * 3
        }

        [Fact]
        public void Workshop_StallsWithoutInputs()
        {
            var sites = new List<Building> { Workshop(BuildingType.Sawmill) };
            var inv = new Inventory(10000f); // no wood
            int batches = ProductionSystem.Step(sites, inv, Recipes, 1000f);
            Assert.Equal(0, batches);
            Assert.Equal(0f, inv.Count(ResourceType.Planks));
        }

        [Fact]
        public void Workshop_IsLimitedByLabour()
        {
            var sites = new List<Building> { Workshop(BuildingType.Sawmill) }; // work 4 per batch
            var inv = new Inventory(10000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 100f);

            ProductionSystem.Step(sites, inv, Recipes, 8f); // only 2 batches' worth of labour

            Assert.Equal(6f, inv.Count(ResourceType.Planks)); // 2 * 3
            Assert.Equal(96f, inv.Count(ResourceType.Wood));  // 2 * 2 consumed
        }

        [Fact]
        public void IncompleteWorkshop_ProducesNothing()
        {
            var b = new Building(Buildings.Get(BuildingType.Sawmill), 0, 0); // not complete
            var sites = new List<Building> { b };
            var inv = new Inventory(10000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 50f);
            Assert.Equal(0, ProductionSystem.Step(sites, inv, Recipes, 1000f));
        }
    }

    public class ProductivityAndMarketTests
    {
        private static WorldMap Map() => WorldGenerator.Generate(new WorldGenSettings { Width = 48, Height = 48 }, StableHash.Fnv1a64("green-valley"));

        private static Settlement Foragers(bool tools, bool market)
        {
            WorldMap map = Map();
            var s = new Settlement(map, map.Width / 2, map.Height / 2, new SettlementConfig(),
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault())
            {
                FoodFactor = 1f,
                PrimaryFood = ResourceType.Berries,
                ForageQuality = ResourceQuality.Standard,
                Rng = DeterministicRng.Stream(1, 1)
            };
            for (int i = 0; i < 10; i++) s.Citizens.Add(new Citizen(i, "F", 30, Profession.Forager));
            s.Storehouse.Add(ResourceType.Berries, ResourceQuality.Standard, 500f); // buffer so nobody starves
            if (tools) s.Storehouse.Add(ResourceType.Tools, ResourceQuality.Standard, 100f);
            if (market)
            {
                Building m = s.PlaceBlueprint(BuildingType.Market, s.CenterX, s.CenterY);
                m.WorkDone = m.WorkRequired;
                m.Complete = true;
            }
            s.Clock.Advance(s.Clock.DaysPerSeason); // summer
            return s;
        }

        private static float FoodGainOneDay(Settlement s)
        {
            float before = s.FoodUnits();
            SettlementSimulation.Step(s);
            return s.FoodUnits() - before;
        }

        [Fact]
        public void Tools_RaiseGatherOutput()
        {
            Assert.True(FoodGainOneDay(Foragers(tools: true, market: false)) >
                        FoodGainOneDay(Foragers(tools: false, market: false)));
        }

        [Fact]
        public void StockedMarket_RaisesGatherOutput()
        {
            Assert.True(FoodGainOneDay(Foragers(tools: false, market: true)) >
                        FoodGainOneDay(Foragers(tools: false, market: false)));
        }
    }

    public class ProductionColonyTests
    {
        private static Settlement Colony()
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                ForagerShare = 0.50f, WoodcutterShare = 0.16f, LoggerShare = 0.09f,
                QuarrymanShare = 0.03f, MinerShare = 0.08f, CraftsmanShare = 0.10f,
                StorehouseCapacity = 20000f,
                StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
                StartingWoodUnits = 60f, StartingStoneUnits = 40f
            };
            Settlement s = SettlementFactory.Create(map, seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            s.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f);

            int gx = s.CenterX, gy = s.CenterY;
            Complete(s, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) Complete(s, BuildingType.House, gx + 2 + i, gy);
            Complete(s, BuildingType.Sawmill, gx, gy + 1);
            Complete(s, BuildingType.Smelter, gx, gy + 2);
            Complete(s, BuildingType.Smithy, gx, gy + 3);
            Complete(s, BuildingType.Market, gx, gy + 4);
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void ProducesChainGoodsAndSurvives()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            Assert.Equal(40, s.AlivePopulation);
            Assert.Equal(0, s.TotalDeaths);
            Assert.True(s.Storehouse.Count(ResourceType.Planks) > 0f, "no planks produced");
            Assert.True(s.Storehouse.Count(ResourceType.Tools) > 0f, "no tools produced");
        }

        [Fact]
        public void IsDeterministic()
        {
            Settlement a = Colony();
            Settlement b = Colony();
            SettlementSimulation.Run(a, a.Clock.DaysPerYear);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RoundTripsThroughSave()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.IronFactor, loaded.IronFactor); // manufactured goods + iron access survive the round-trip
            Assert.True(Math.Abs(s.Storehouse.Count(ResourceType.Tools) - loaded.Storehouse.Count(ResourceType.Tools)) < 0.01f);
        }
    }
}
