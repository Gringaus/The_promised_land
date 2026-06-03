using System;
using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class ConstructionMechanicTests
    {
        private static readonly BuildingCatalog Catalog = BuildingCatalog.CreateDefault();

        [Fact]
        public void Catalog_DefinesEveryBuildingType()
        {
            foreach (BuildingType t in Enum.GetValues(typeof(BuildingType)))
            {
                Assert.True(Catalog.Has(t), "missing def for " + t);
                Assert.True(Catalog.Get(t).WorkRequired > 0f);
            }
        }

        [Fact]
        public void Stage_FollowsWorkFraction()
        {
            Assert.Equal(ConstructionStage.Blueprint, ConstructionStages.FromWorkFraction(0f, false));
            Assert.Equal(ConstructionStage.Foundation, ConstructionStages.FromWorkFraction(0.1f, false));
            Assert.Equal(ConstructionStage.Frame, ConstructionStages.FromWorkFraction(0.3f, false));
            Assert.Equal(ConstructionStage.Walls, ConstructionStages.FromWorkFraction(0.6f, false));
            Assert.Equal(ConstructionStage.Roof, ConstructionStages.FromWorkFraction(0.9f, false));
            Assert.Equal(ConstructionStage.Complete, ConstructionStages.FromWorkFraction(1f, true));
            // Work done but materials missing is not "complete".
            Assert.NotEqual(ConstructionStage.Complete, ConstructionStages.FromWorkFraction(1f, false));
        }

        [Fact]
        public void Build_DeliversMaterialsAndCompletes()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 100f);

            var b = new Building(Catalog.Get(BuildingType.Tent), 0, 0); // Wood 6, work 18
            var sites = new List<Building> { b };

            for (int day = 0; day < 10 && !b.Complete; day++)
            {
                ConstructionSystem.Step(sites, inv, 5f); // 5 work/day
            }

            Assert.True(b.Complete);
            Assert.Equal(ConstructionStage.Complete, b.Stage);
            Assert.True(Math.Abs(inv.Count(ResourceType.Wood) - 94f) < 1e-2f); // 6 consumed
        }

        [Fact]
        public void Build_StallsWithoutMaterials_ThenResumes()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 24f); // House needs Wood 24 + Stone 8
            // No stone yet.

            var b = new Building(Catalog.Get(BuildingType.House), 0, 0);
            var sites = new List<Building> { b };

            ConstructionSystem.Step(sites, inv, 1000f); // plenty of labour...
            Assert.False(b.Complete);                   // ...but the stone bottleneck stalls it
            Assert.Equal(0f, b.WorkDone);
            Assert.Equal(ConstructionStage.Blueprint, b.Stage);

            inv.Add(ResourceType.Stone, ResourceQuality.Standard, 8f);
            for (int day = 0; day < 20 && !b.Complete; day++) ConstructionSystem.Step(sites, inv, 20f);
            Assert.True(b.Complete);
        }

        [Fact]
        public void Build_PartialMaterials_CapWorkProgress()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 12f); // half the wood
            inv.Add(ResourceType.Stone, ResourceQuality.Standard, 8f); // all the stone

            var b = new Building(Catalog.Get(BuildingType.House), 0, 0); // work 80
            var sites = new List<Building> { b };

            ConstructionSystem.Step(sites, inv, 1000f);
            Assert.False(b.Complete);
            Assert.True(Math.Abs(b.WorkDone - 40f) < 1e-2f); // capped at 50% by the wood bottleneck
        }
    }

    public class BuildingEffectTests
    {
        private static WorldMap SmallMap()
        {
            return WorldGenerator.Generate(new WorldGenSettings { Width = 48, Height = 48 }, StableHash.Fnv1a64("green-valley"));
        }

        private static SettlementConfig BuildConfig()
        {
            return new SettlementConfig
            {
                StartingPopulation = 28,
                ForagerShare = 0.50f,
                WoodcutterShare = 0.20f,
                LoggerShare = 0.12f,
                QuarrymanShare = 0.06f,
                BuilderShare = 0.12f,
                StartingFoodUnits = 300f,
                StartingFirewoodUnits = 250f,
                StartingWoodUnits = 80f,
                StartingStoneUnits = 40f
            };
        }

        private static Settlement BuildColony()
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
            Settlement colony = SettlementFactory.Create(map, seed, BuildConfig(),
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
            return colony;
        }

        [Fact]
        public void BuildScenario_CompletesPlanAndSurvives()
        {
            Settlement colony = BuildColony();
            int planned = colony.Buildings.Count;

            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);

            Assert.Equal(planned, colony.BuildingsComplete);
            colony.RecomputeBuildingEffects();
            Assert.Equal(25, colony.HousingCapacity); // five houses, 5 each
            Assert.True(colony.Storehouse.Capacity >= colony.BaseStorageCapacity + 2500f - 1f); // storehouse bonus
            Assert.Equal(0, colony.TotalDeaths); // building does not starve the colony
        }

        [Fact]
        public void BuildScenario_IsDeterministic()
        {
            Settlement a = BuildColony();
            Settlement b = BuildColony();
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 2);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 2);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void Housing_ReducesWinterFirewoodBurn()
        {
            float burnNoHouses = WinterFirewoodBurn(withHouses: false);
            float burnHoused = WinterFirewoodBurn(withHouses: true);
            Assert.True(burnHoused < burnNoHouses, $"housed {burnHoused} should burn less than {burnNoHouses}");
        }

        private static float WinterFirewoodBurn(bool withHouses)
        {
            WorldMap map = SmallMap();
            var config = new SettlementConfig();
            var s = new Settlement(map, map.Width / 2, map.Height / 2, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault())
            {
                FoodFactor = 0f,
                FirewoodFactor = 0f,
                PrimaryFood = ResourceType.Berries,
                ForageQuality = ResourceQuality.Standard,
                Rng = DeterministicRng.Stream(1, 1)
            };
            for (int i = 0; i < 10; i++) s.Citizens.Add(new Citizen(i, "T", 30, Profession.Idle));
            s.Storehouse.Add(ResourceType.Firewood, ResourceQuality.Standard, 1000f);
            s.Storehouse.Add(ResourceType.Berries, ResourceQuality.Standard, 1000f);

            if (withHouses)
            {
                // Two finished houses shelter all ten settlers.
                for (int i = 0; i < 2; i++)
                {
                    Building h = s.PlaceBlueprint(BuildingType.House, s.CenterX + i, s.CenterY);
                    h.WorkDone = h.WorkRequired;
                    h.Complete = true;
                }
            }

            s.Clock.Advance(config.DaysPerSeason * 3); // into winter
            s.RecomputeBuildingEffects();

            float before = s.Storehouse.Count(ResourceType.Firewood);
            SettlementSimulation.Step(s);
            return before - s.Storehouse.Count(ResourceType.Firewood);
        }
    }
}
