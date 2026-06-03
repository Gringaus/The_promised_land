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
    public class TechnologyTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        private static Settlement Colony(bool enableTech)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 24,
                ForagerShare = 0.58f, WoodcutterShare = 0.25f, ScholarShare = 0.13f,
                StartingFoodUnits = 320f, StartingFirewoodUnits = 260f,
                EnableTechnology = enableTech,
                ResearchPerScholarPerDay = 1.5f
            };
            return SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
        }

        [Fact]
        public void DisabledByDefault_EverythingAvailableNoResearch()
        {
            Settlement s = Colony(enableTech: false);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            Assert.Empty(s.UnlockedTechs);
            Assert.Equal(0f, s.ResearchProgress);
            Assert.Equal(0f, s.TechWorkBonus);
            Assert.NotNull(s.PlaceBlueprint(BuildingType.Sawmill, s.CenterX, s.CenterY)); // no gating
        }

        [Fact]
        public void FreshColony_ResearchesThePrerequisiteFreeTechFirst()
        {
            Settlement s = Colony(enableTech: true);
            var next = ResearchSystem.NextTarget(s);
            Assert.NotNull(next);
            Assert.Equal("Foraging Lore", next.Name);
        }

        [Fact]
        public void LockedBuildingsCannotBePlaced_BaseOnesCan()
        {
            Settlement s = Colony(enableTech: true); // nothing researched yet
            Assert.NotNull(s.PlaceBlueprint(BuildingType.House, s.CenterX, s.CenterY)); // founding building
            Assert.Null(s.PlaceBlueprint(BuildingType.Sawmill, s.CenterX, s.CenterY));   // needs Woodcraft
        }

        [Fact]
        public void ResearchUnlocksTechsBuildingsAndRaisesBonus()
        {
            Settlement s = Colony(enableTech: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 2);

            Assert.True(s.UnlockedTechs.Count >= 2, "scholars should unlock several techs over two years");
            Assert.True(s.TechWorkBonus > 0f, "techs grant a work bonus");
            Assert.NotNull(s.PlaceBlueprint(BuildingType.Sawmill, s.CenterX, s.CenterY)); // Woodcraft unlocked it
        }

        [Fact]
        public void IsDeterministic()
        {
            Settlement a = Colony(enableTech: true);
            Settlement b = Colony(enableTech: true);
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 2);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 2);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RoundTripsThroughSave()
        {
            Settlement s = Colony(enableTech: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            Assert.True(s.UnlockedTechs.Count > 0); // there is tech state worth preserving

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.UnlockedTechs.Count, loaded.UnlockedTechs.Count);
            Assert.Equal(s.ResearchProgress, loaded.ResearchProgress);
            // Unlocked buildings are restored, so a researched building is still placeable after load.
            Assert.NotNull(loaded.PlaceBlueprint(BuildingType.Sawmill, loaded.CenterX, loaded.CenterY));
        }
    }
}
