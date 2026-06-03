using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Farming;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class FarmingSystemTests
    {
        private static readonly BuildingCatalog Buildings = BuildingCatalog.CreateDefault();

        private static List<Building> OneField()
        {
            var b = new Building(Buildings.Get(BuildingType.Field), 0, 0);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
            return new List<Building> { b };
        }

        [Fact]
        public void Crop_IsSownInSpringAndOnlyReapedInAutumn()
        {
            var fields = OneField();
            var inv = new Inventory(10000f);
            var cfg = new SettlementConfig();

            FarmingSystem.Step(fields, inv, Season.Spring, 0.6f, 80f, cfg);
            Assert.True(fields[0].Planted);
            Assert.True(fields[0].CropGrowth > 0f);
            Assert.Equal(0f, inv.Count(ResourceType.Grain));

            FarmingSystem.Step(fields, inv, Season.Summer, 0.6f, 80f, cfg);
            Assert.Equal(0f, inv.Count(ResourceType.Grain)); // still growing

            float harvested = FarmingSystem.Step(fields, inv, Season.Autumn, 0.6f, 80f, cfg);
            Assert.True(harvested > 0f);
            Assert.True(inv.Count(ResourceType.Grain) > 0f);
            Assert.False(fields[0].Planted);     // reaped, now fallow
            Assert.Equal(0f, fields[0].CropGrowth);
        }

        [Fact]
        public void NoFields_ProduceNothing()
        {
            var none = new List<Building>();
            var inv = new Inventory(10000f);
            Assert.Equal(0f, FarmingSystem.Step(none, inv, Season.Autumn, 0.6f, 80f, new SettlementConfig()));
        }

        // Tending matters: a worked field ripens fuller and yields more than a neglected one.
        private static float SeasonYield(float labor)
        {
            var fields = OneField();
            var inv = new Inventory(100000f);
            var cfg = new SettlementConfig();
            for (int d = 0; d < cfg.DaysPerSeason; d++) FarmingSystem.Step(fields, inv, Season.Spring, 0.6f, labor, cfg);
            for (int d = 0; d < cfg.DaysPerSeason; d++) FarmingSystem.Step(fields, inv, Season.Summer, 0.6f, labor, cfg);
            FarmingSystem.Step(fields, inv, Season.Autumn, 0.6f, labor, cfg);
            return inv.Count(ResourceType.Grain);
        }

        [Fact]
        public void Tending_RaisesYield()
        {
            Assert.True(SeasonYield(labor: 8f) > SeasonYield(labor: 0f));
        }
    }

    public class FarmingColonyTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        private static Settlement Colony()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 40,
                ForagerShare = 0.25f, WoodcutterShare = 0.20f, FarmerShare = 0.33f, CraftsmanShare = 0.14f,
                StorehouseCapacity = 20000f,
                StartingFoodUnits = 2000f, StartingFirewoodUnits = 450f,
                StartingWoodUnits = 40f, StartingStoneUnits = 20f
            };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            int gx = s.CenterX, gy = s.CenterY;
            Complete(s, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) Complete(s, BuildingType.House, gx + 2 + i, gy);
            Complete(s, BuildingType.Mill, gx, gy + 1);
            Complete(s, BuildingType.Bakery, gx, gy + 2);
            for (int i = 0; i < 10; i++) Complete(s, BuildingType.Field, gx - 1 - i, gy + 4);
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void GrowsGrainAndBakesBreadAndFeedsItself()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, 60); // into the first autumn, after the harvest

            Assert.Equal(40, s.AlivePopulation);
            Assert.True(s.Storehouse.Count(ResourceType.Grain) > 0f, "no grain harvested");
            Assert.True(s.Storehouse.Count(ResourceType.Bread) > 0f, "no bread baked from the grain");
        }

        [Fact]
        public void SurvivesAFullYearOnFarming()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            Assert.Equal(40, s.AlivePopulation);
            Assert.Equal(0, s.TotalDeaths);
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
        public void CropState_RoundTripsThroughSave()
        {
            Settlement s = Colony();
            SettlementSimulation.Run(s, 40); // mid-summer: fields planted and partly grown

            // Sanity: there is live crop state worth preserving.
            bool anyGrowing = false;
            foreach (Building b in s.Buildings)
                if (b.Type == BuildingType.Field && b.Planted && b.CropGrowth > 0f) anyGrowing = true;
            Assert.True(anyGrowing);

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));
            Assert.Equal(s.StateHash(), loaded.StateHash());
        }
    }
}
