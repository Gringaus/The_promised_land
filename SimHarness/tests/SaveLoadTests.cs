using System;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class SaveLoadTests
    {
        private static (Settlement colony, WorldGenSettings settings) RichColony(int years = 1)
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            var config = new SettlementConfig
            {
                StartingPopulation = 28,
                ForagerShare = 0.50f, WoodcutterShare = 0.20f,
                LoggerShare = 0.12f, QuarrymanShare = 0.06f, BuilderShare = 0.12f,
                StartingFoodUnits = 300f, StartingFirewoodUnits = 250f,
                StartingWoodUnits = 80f, StartingStoneUnits = 40f
            };
            Settlement colony = SettlementFactory.Create(map, seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            BuildingType[] plan =
            {
                BuildingType.Storehouse,
                BuildingType.House, BuildingType.House, BuildingType.House,
                BuildingType.House, BuildingType.House,
                BuildingType.ForagerHut, BuildingType.WoodcutterCamp
            };
            for (int i = 0; i < plan.Length; i++)
                colony.PlaceBlueprint(plan[i], colony.CenterX + 1 + i, colony.CenterY);

            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear * years);
            return (colony, settings);
        }

        [Fact]
        public void RoundTrip_RestoresMapAndState()
        {
            var (colony, settings) = RichColony();
            Settlement loaded = SaveGame.Load(SaveGame.Save(colony, settings));

            Assert.Equal(colony.Map.ContentHash(), loaded.Map.ContentHash());
            Assert.Equal(colony.StateHash(), loaded.StateHash());
            Assert.Equal(colony.AlivePopulation, loaded.AlivePopulation);
            Assert.Equal(colony.BuildingsComplete, loaded.BuildingsComplete);
            Assert.True(Math.Abs(colony.Storehouse.Capacity - loaded.Storehouse.Capacity) < 0.01f);
        }

        [Fact]
        public void RoundTrip_ContinuationStaysInLockStep()
        {
            var (colony, settings) = RichColony();
            Settlement loaded = SaveGame.Load(SaveGame.Save(colony, settings));

            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);
            SettlementSimulation.Run(loaded, loaded.Clock.DaysPerYear);

            Assert.Equal(colony.StateHash(), loaded.StateHash());
        }

        [Fact]
        public void Save_IsIdempotent()
        {
            var (colony, settings) = RichColony();
            string first = SaveGame.Save(colony, settings);
            string second = SaveGame.Save(SaveGame.Load(first), settings);
            Assert.Equal(first, second);
        }

        [Fact]
        public void FreshColony_RoundTrips()
        {
            // A just-created survival colony (no buildings, default config) must also survive
            // a round-trip, not just a rich mid-game one.
            ulong seed = StableHash.Fnv1a64("green-valley");
            var settings = new WorldGenSettings { Width = 96, Height = 96 };
            WorldMap map = WorldGenerator.Generate(settings, seed);
            Settlement colony = SettlementFactory.Create(map, seed, new SettlementConfig(),
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            Settlement loaded = SaveGame.Load(SaveGame.Save(colony, settings));
            Assert.Equal(colony.StateHash(), loaded.StateHash());
        }

        [Fact]
        public void Load_PreservesConfigBalance()
        {
            var (colony, settings) = RichColony();
            Settlement loaded = SaveGame.Load(SaveGame.Save(colony, settings));

            Assert.Equal(colony.Config.DaysPerSeason, loaded.Config.DaysPerSeason);
            Assert.Equal(colony.Config.ForagerShare, loaded.Config.ForagerShare);
            Assert.Equal(colony.Config.StartingFoodUnits, loaded.Config.StartingFoodUnits);
        }

        [Fact]
        public void Load_RejectsUnknownVersion()
        {
            Assert.Throws<FormatException>(() => SaveGame.Load("FLSAVE 999\nEND\n"));
        }

        [Fact]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => SaveGame.Save(null, new WorldGenSettings()));
            var (colony, _) = RichColony();
            Assert.Throws<ArgumentNullException>(() => SaveGame.Save(colony, null));
            Assert.Throws<ArgumentNullException>(() => SaveGame.Load(null));
        }
    }
}
