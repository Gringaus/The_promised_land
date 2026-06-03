using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class PopulationDynamicsTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        // Mirrors the demography scenario: a bountiful valley where housing, not hunger, caps growth.
        private static Settlement Colony(bool dynamics)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 16,
                ForagerShare = 0.62f, WoodcutterShare = 0.30f,
                ForagerNutritionPerDay = 7.0f, WoodcutterFirewoodPerDay = 9.0f,
                StorehouseCapacity = 8000f,
                StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
                EnablePopulationDynamics = dynamics
            };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            int gx = s.CenterX, gy = s.CenterY;
            Complete(s, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 7; i++) Complete(s, BuildingType.House, gx + 2 + i, gy);
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void DisabledByDefault_PopulationIsStatic()
        {
            Settlement s = Colony(dynamics: false);
            int age0 = s.Citizens[0].Age;
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 3);

            Assert.Equal(16, s.AlivePopulation);
            Assert.Equal(0, s.TotalBirths);
            Assert.Equal(0, s.TotalImmigrants);
            Assert.Equal(0, s.NaturalDeaths);
            Assert.Equal(age0, s.Citizens[0].Age); // no aging either
        }

        [Fact]
        public void EveryoneAgesEachYear()
        {
            Settlement s = Colony(dynamics: true);
            int age0 = s.Citizens[0].Age;
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            Assert.True(s.Citizens[0].Alive);
            Assert.Equal(age0 + 1, s.Citizens[0].Age);
        }

        [Fact]
        public void GrowsThroughBirthsAndMigration()
        {
            Settlement s = Colony(dynamics: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 5);

            Assert.True(s.AlivePopulation > 16, "colony should have grown");
            Assert.True(s.TotalBirths > 0, "no births");
            Assert.True(s.TotalImmigrants > 0, "no migrants drawn in");
        }

        [Fact]
        public void EldersEventuallyDieOfOldAge()
        {
            Settlement s = Colony(dynamics: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 18);
            Assert.True(s.NaturalDeaths > 0, "the founding generation should age out over 18 years");
        }

        [Fact]
        public void ThrivesToAStablePlateauWithoutFamine()
        {
            Settlement s = Colony(dynamics: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 20);

            Assert.True(s.AlivePopulation >= 30, "should grow to a healthy size");
            Assert.Equal(s.NaturalDeaths, s.TotalDeaths); // every death was old age — nobody starved or froze
        }

        [Fact]
        public void IsDeterministic()
        {
            Settlement a = Colony(dynamics: true);
            Settlement b = Colony(dynamics: true);
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 10);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 10);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RoundTripsThroughSave()
        {
            Settlement s = Colony(dynamics: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 6);
            Assert.True(s.TotalBirths > 0); // there is dynamic state worth preserving

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.TotalBirths, loaded.TotalBirths);
            Assert.Equal(s.TotalImmigrants, loaded.TotalImmigrants);
            Assert.Equal(s.NextCitizenId, loaded.NextCitizenId);
            Assert.Equal(s.AlivePopulation, loaded.AlivePopulation);
        }

        // ---- ReassignWorkforce: re-task the whole workforce on demand (used by the playable build) ----

        [Fact]
        public void ReassignWorkforce_RetasksByShares()
        {
            Settlement s = Colony(dynamics: true); // 16 working-age adults, all forager/woodcutter at first
            ZeroShares(s.Config);
            s.Config.ForagerShare = 0.5f; s.Config.WoodcutterShare = 0.25f; s.Config.BuilderShare = 0.25f;

            PopulationSystem.ReassignWorkforce(s);

            int workers = WorkingAdults(s);
            Assert.Equal((int)System.Math.Round(workers * 0.5f), Count(s, Profession.Forager));
            Assert.Equal((int)System.Math.Round(workers * 0.25f), Count(s, Profession.Woodcutter));
            Assert.Equal((int)System.Math.Round(workers * 0.25f), Count(s, Profession.Builder));
            Assert.Equal(0, Count(s, Profession.Logger)); // a zero share gets nobody
        }

        [Fact]
        public void ReassignWorkforce_LeavesRemainderIdle()
        {
            Settlement s = Colony(dynamics: true);
            ZeroShares(s.Config);
            s.Config.ForagerShare = 0.5f; // only half the workforce has a job to do

            PopulationSystem.ReassignWorkforce(s);

            int workers = WorkingAdults(s);
            int foragers = (int)System.Math.Round(workers * 0.5f);
            Assert.Equal(foragers, Count(s, Profession.Forager));
            Assert.Equal(workers - foragers, Count(s, Profession.Idle)); // the rest stand idle
        }

        [Fact]
        public void ReassignWorkforce_DoesNotPutChildrenToWork()
        {
            Settlement s = Colony(dynamics: true);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear * 4); // raise a generation of children
            int children = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Age < s.Config.WorkingAge) children++;
            Assert.True(children > 0, "the colony should have borne children to test against");

            PopulationSystem.ReassignWorkforce(s);

            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Age < s.Config.WorkingAge)
                    Assert.Equal(Profession.Idle, s.Citizens[i].Profession);
        }

        private static void ZeroShares(SettlementConfig c)
        {
            c.ForagerShare = c.WoodcutterShare = c.LoggerShare = c.QuarrymanShare = c.BuilderShare =
                c.MinerShare = c.CraftsmanShare = c.MilitiaShare = c.FarmerShare = c.ScholarShare = 0f;
        }

        private static int WorkingAdults(Settlement s)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Age >= s.Config.WorkingAge) n++;
            return n;
        }

        private static int Count(Settlement s, Profession p)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == p) n++;
            return n;
        }
    }
}
