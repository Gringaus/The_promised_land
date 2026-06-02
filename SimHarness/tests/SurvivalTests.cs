using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class SurvivalTests
    {
        private static WorldMap GreenValley()
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            return WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, seed);
        }

        private static Settlement NewColony(SettlementConfig? config = null)
        {
            config ??= new SettlementConfig();
            ulong seed = StableHash.Fnv1a64("green-valley");
            return SettlementFactory.Create(GreenValley(), seed, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
        }

        [Fact]
        public void Simulation_IsDeterministic()
        {
            Settlement a = NewColony();
            Settlement b = NewColony();
            SettlementSimulation.Run(a, a.Clock.DaysPerYear * 2);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear * 2);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void RichValley_SurvivesFirstYearAtFullHealth()
        {
            Settlement colony = NewColony();
            int start = colony.AlivePopulation;
            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);

            Assert.Equal(start, colony.AlivePopulation); // nobody died
            Assert.True(colony.AverageHealth >= 90f, "health collapsed: " + colony.AverageHealth);
            Assert.Equal(0, colony.TotalDeaths);
        }

        [Fact]
        public void SeasonalStockpile_PeaksInAutumnAndDrawsDownInWinter()
        {
            Settlement colony = NewColony();

            // End of autumn (day index: 3 seasons in, last day).
            float autumnFood = 0f, winterFood = 0f;
            int daysPerSeason = colony.Clock.DaysPerSeason;
            for (int d = 0; d < daysPerSeason * 4; d++)
            {
                DayReport r = SettlementSimulation.Step(colony);
                if (r.Season == Season.Autumn && r.Day % daysPerSeason == daysPerSeason - 1) autumnFood = r.StoredNutrition;
                if (r.Season == Season.Winter && r.Day % daysPerSeason == daysPerSeason - 1) winterFood = r.StoredNutrition;
            }
            Assert.True(autumnFood > winterFood, $"expected autumn stock {autumnFood} > winter stock {winterFood}");
        }

        [Fact]
        public void DeprivedColony_CollapsesWithinAYear()
        {
            // No starting stock and no productive land: a colony that cannot feed or heat
            // itself must die — the survival loop has real stakes (GDD §28).
            var config = new SettlementConfig { StartingFoodUnits = 0f, StartingFirewoodUnits = 0f };
            Settlement colony = NewColony(config);
            colony.FoodFactor = 0f;
            colony.FirewoodFactor = 0f;

            int start = colony.AlivePopulation;
            SettlementSimulation.Run(colony, colony.Clock.DaysPerYear);

            Assert.True(colony.AlivePopulation < start, "deprived colony should lose people");
            Assert.True(colony.TotalDeaths > 0);
        }

        [Fact]
        public void Firewood_BurnsInWinterButNotSummer()
        {
            WorldMap map = GreenValley();
            var config = new SettlementConfig();
            ResourceCatalog catalog = ResourceCatalog.CreateDefault();
            SeasonDef[] seasons = SeasonDef.CreateDefault();

            // Idle colony (no gathering) so only consumption moves the stockpile.
            Settlement summer = Idle(map, config, catalog, seasons, advanceToSeasonDays: config.DaysPerSeason);     // Summer
            Settlement winter = Idle(map, config, catalog, seasons, advanceToSeasonDays: config.DaysPerSeason * 3); // Winter

            float summerBefore = summer.Storehouse.Count(ResourceType.Firewood);
            SettlementSimulation.Step(summer);
            Assert.Equal(summerBefore, summer.Storehouse.Count(ResourceType.Firewood)); // no warmth demand in summer

            float winterBefore = winter.Storehouse.Count(ResourceType.Firewood);
            SettlementSimulation.Step(winter);
            Assert.True(winter.Storehouse.Count(ResourceType.Firewood) < winterBefore, "winter should burn firewood");
        }

        private static Settlement Idle(WorldMap map, SettlementConfig config, ResourceCatalog catalog,
            SeasonDef[] seasons, int advanceToSeasonDays)
        {
            var s = new Settlement(map, map.Width / 2, map.Height / 2, config, catalog, seasons)
            {
                PrimaryFood = ResourceType.Berries,
                ForageQuality = ResourceQuality.Standard,
                FoodFactor = 0f,
                FirewoodFactor = 0f,
                Rng = DeterministicRng.Stream(1, 1)
            };
            for (int i = 0; i < 10; i++) s.Citizens.Add(new Citizen(i, "Test", 30, Profession.Idle));
            s.Storehouse.Add(ResourceType.Firewood, ResourceQuality.Standard, 1000f);
            s.Storehouse.Add(ResourceType.Berries, ResourceQuality.Standard, 1000f); // so hunger never confounds
            s.Clock.Advance(advanceToSeasonDays);
            return s;
        }
    }
}
