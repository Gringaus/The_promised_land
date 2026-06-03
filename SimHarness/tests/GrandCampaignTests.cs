using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Technology;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    /// <summary>
    /// Whole-game integration: one colony with every subsystem enabled at once — farming, production,
    /// trade, threats, demographics and technology — exercised over decades. Proves the modules compose
    /// (and is the holistic regression guard the per-module tests can't be on their own).
    /// </summary>
    public class GrandCampaignTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        private static Settlement Colony()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
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
            Settlement c = SettlementFactory.Create(map, Seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            for (int t = 0; t <= 6; t++) ResearchSystem.Grant(c, t); // founders' knowledge
            c.TradePolicy.Sell(ResourceType.Planks).Sell(ResourceType.IronIngot);

            int gx = c.CenterX, gy = c.CenterY;
            Complete(c, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 10; i++) Complete(c, BuildingType.House, gx + 2 + i, gy);
            Complete(c, BuildingType.ForagerHut, gx - 1, gy);
            Complete(c, BuildingType.WoodcutterCamp, gx - 2, gy);
            Complete(c, BuildingType.Sawmill, gx, gy + 1);
            Complete(c, BuildingType.Smelter, gx, gy + 2);
            Complete(c, BuildingType.Smithy, gx, gy + 3);
            Complete(c, BuildingType.Mill, gx, gy + 4);
            Complete(c, BuildingType.Bakery, gx, gy + 5);
            for (int i = 0; i < 8; i++) Complete(c, BuildingType.Field, gx - 1 - i, gy + 6);
            Complete(c, BuildingType.Market, gx + 1, gy + 1);
            for (int i = 0; i < 4; i++) Complete(c, BuildingType.Watchtower, gx + 1, gy + 2 + i);
            Complete(c, BuildingType.Palisade, gx + 2, gy + 2);
            c.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f);
            return c;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            Assert.NotNull(b); // the founding techs must have unlocked every building we place
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void EverySystemTogether_ThrivesOverDecades()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 25);

            Assert.True(s.AlivePopulation >= 35, $"colony should hold its own, was {s.AlivePopulation}");
            Assert.True(s.AverageHealth > 80f, "a thriving colony stays healthy");
            Assert.Equal(0, s.Threat.TotalRaids);                       // a well-guarded town is never sacked
            Assert.Equal(s.Techs.Count, s.UnlockedTechs.Count);         // research finishes the tree
            Assert.True(s.TradeLedger.Silver > 1000f, "exports build a treasury");
            Assert.True(s.TotalBirths > 0, "the population reproduces");
        }

        [Fact]
        public void IsDeterministic()
        {
            Settlement a = Colony();
            Settlement b = Colony();
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 15);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 15);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RoundTripsThroughSave()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 10);

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            // Continuing the loaded game stays in lockstep with continuing the original — a full
            // round-trip, not just an identical snapshot.
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            SettlementSimulation.Run(loaded, loaded.Clock.DaysPerYear);
            Assert.Equal(s.StateHash(), loaded.StateHash());
        }
    }
}
