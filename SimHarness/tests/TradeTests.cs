using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.Trade;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class TradeSystemTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        // A tiny colony with a finished market, used to drive TradeSystem.Step directly so the
        // caravan mechanics can be checked without the surrounding survival loop.
        private static Settlement MarketColony(bool trade, bool threats = false)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 64, Height = 64 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 8,
                EnableTrade = trade,
                EnableThreats = threats,
                TradeIntervalDays = 12,
                CaravanCapacity = 250f,
                MerchantMargin = 0.25f
            };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            Building m = s.PlaceBlueprint(BuildingType.Market, s.CenterX, s.CenterY);
            m.WorkDone = m.WorkRequired;
            m.Complete = true;
            s.RecomputeBuildingEffects();
            return s;
        }

        [Fact]
        public void DisabledByDefault_NoCaravanComes()
        {
            Settlement s = MarketColony(trade: false);
            s.Storehouse.Add(ResourceType.Planks, ResourceQuality.Standard, 1000f);
            s.TradePolicy.Sell(ResourceType.Planks);
            s.Clock.Advance(12);
            TradeSystem.Step(s);

            Assert.Equal(0, s.TradeLedger.CaravanVisits);
            Assert.Equal(0f, s.TradeLedger.Silver);
            Assert.Equal(1000f, s.Storehouse.Count(ResourceType.Planks));
        }

        [Fact]
        public void Caravan_SellsSurplusAboveReserveForSilver()
        {
            Settlement s = MarketColony(trade: true);
            s.Storehouse.Add(ResourceType.Planks, ResourceQuality.Standard, 1000f);
            s.TradePolicy.Sell(ResourceType.Planks, reserve: 100f);

            s.Clock.Advance(12);
            TradeSystem.Step(s);

            Assert.Equal(1, s.TradeLedger.CaravanVisits);
            Assert.True(s.TradeLedger.Silver > 0f, "selling should earn silver");
            Assert.True(s.TradeLedger.TotalExported > 0f);
            Assert.True(s.Storehouse.Count(ResourceType.Planks) >= 100f, "the reserve must be kept");
            Assert.True(s.Storehouse.Count(ResourceType.Planks) < 1000f, "surplus should be sold");
        }

        [Fact]
        public void Caravan_BuysTowardTargetSpendingSilver()
        {
            Settlement s = MarketColony(trade: true);
            s.TradeLedger.Silver = 1000f;
            s.TradePolicy.Buy(ResourceType.Tools, target: 50f);

            s.Clock.Advance(12);
            TradeSystem.Step(s);

            Assert.True(s.Storehouse.Count(ResourceType.Tools) > 0f, "should have imported tools");
            Assert.True(s.TradeLedger.Silver < 1000f, "buying should spend silver");
            Assert.True(s.TradeLedger.TotalImported > 0f);
        }

        [Fact]
        public void NoMarket_NoTrade()
        {
            // Same as MarketColony but without the market building.
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 64, Height = 64 }, Seed);
            var config = new SettlementConfig { StartingPopulation = 8, EnableTrade = true, TradeIntervalDays = 12 };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            s.Storehouse.Add(ResourceType.Planks, ResourceQuality.Standard, 500f);
            s.TradePolicy.Sell(ResourceType.Planks);

            s.Clock.Advance(12);
            TradeSystem.Step(s);

            Assert.Equal(0, s.TradeLedger.CaravanVisits);
            Assert.Equal(500f, s.Storehouse.Count(ResourceType.Planks));
        }

        [Fact]
        public void CaravansComeOnlyOnCadence()
        {
            Settlement s = MarketColony(trade: true);
            s.Storehouse.Add(ResourceType.Planks, ResourceQuality.Standard, 1000f);
            s.TradePolicy.Sell(ResourceType.Planks);

            s.Clock.Advance(11); // day 11 — not a caravan day
            TradeSystem.Step(s);
            Assert.Equal(0, s.TradeLedger.CaravanVisits);

            s.Clock.Advance(1); // day 12 — a caravan day
            TradeSystem.Step(s);
            Assert.Equal(1, s.TradeLedger.CaravanVisits);
        }

        [Fact]
        public void UnsafeRoutesGetCaravansAmbushed()
        {
            Settlement s = MarketColony(trade: true, threats: true);
            s.Storehouse.Add(ResourceType.Planks, ResourceQuality.Standard, 5000f);
            s.TradePolicy.Sell(ResourceType.Planks);
            s.Threat.Pressure = 99f; // route is very dangerous → safety floors at 0.1

            int ambushes = 0;
            for (int visit = 0; visit < 6; visit++)
            {
                s.Clock.Advance(12);
                int before = s.TradeLedger.Ambushes;
                TradeSystem.Step(s);
                if (s.TradeLedger.Ambushes > before) ambushes++;
            }

            Assert.True(ambushes > 0, "a dangerous road should cost at least one caravan over six visits");
        }
    }

    public class TradeColonyTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        // Mirrors the trade scenario: a timber town that exports planks and imports tools.
        private static Settlement Colony()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 30,
                ForagerShare = 0.52f, WoodcutterShare = 0.16f, LoggerShare = 0.16f, CraftsmanShare = 0.13f,
                StorehouseCapacity = 20000f,
                StartingFoodUnits = 600f, StartingFirewoodUnits = 350f, StartingWoodUnits = 40f,
                EnableTrade = true
            };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            s.TradePolicy.Sell(ResourceType.Planks).Buy(ResourceType.Tools, target: 80f);
            int gx = s.CenterX, gy = s.CenterY;
            Complete(s, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 7; i++) Complete(s, BuildingType.House, gx + 2 + i, gy);
            Complete(s, BuildingType.Sawmill, gx, gy + 1);
            Complete(s, BuildingType.Market, gx, gy + 2);
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void ExportsForSilverAndImportsToolsAndSurvives()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 2);

            Assert.Equal(30, s.AlivePopulation);
            Assert.True(s.TradeLedger.CaravanVisits > 0, "caravans should have visited");
            Assert.True(s.TradeLedger.Silver > 0f, "exports should outweigh imports");
            Assert.True(s.TradeLedger.TotalExported > 0f, "no planks exported");
            Assert.True(s.Storehouse.Count(ResourceType.Tools) > 0f, "tools should have been imported");
        }

        [Fact]
        public void IsDeterministic()
        {
            Settlement a = Colony();
            Settlement b = Colony();
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 2);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 2);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RoundTripsThroughSave()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 2);
            Assert.True(s.TradeLedger.Silver > 0f);

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.TradeLedger.Silver, loaded.TradeLedger.Silver);
            Assert.Equal(s.TradeLedger.CaravanVisits, loaded.TradeLedger.CaravanVisits);
        }
    }
}
