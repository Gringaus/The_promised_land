using System;
using System.Collections.Generic;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Time;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class InventoryTests
    {
        private static ResourceCatalog Catalog => ResourceCatalog.CreateDefault();

        [Fact]
        public void Add_RespectsCapacity()
        {
            var inv = new Inventory(100f);
            float a = inv.Add(ResourceType.Wood, ResourceQuality.Standard, 60f);
            float b = inv.Add(ResourceType.Stone, ResourceQuality.Standard, 60f);
            Assert.Equal(60f, a);
            Assert.Equal(40f, b); // only 40 capacity left
            Assert.Equal(100f, inv.TotalUnits);
            Assert.Equal(0f, inv.FreeSpace);
        }

        [Fact]
        public void RemoveAndCount_AggregateAcrossQualities()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Berries, ResourceQuality.Poor, 10f);
            inv.Add(ResourceType.Berries, ResourceQuality.Fine, 5f);
            Assert.Equal(15f, inv.Count(ResourceType.Berries));

            float removed = inv.Remove(ResourceType.Berries, ResourceQuality.Poor, 4f);
            Assert.Equal(4f, removed);
            Assert.Equal(11f, inv.Count(ResourceType.Berries));
        }

        [Fact]
        public void Stacks_AreInDeterministicTypeThenQualityOrder()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Fish, ResourceQuality.Fine, 1f);
            inv.Add(ResourceType.Wood, ResourceQuality.Standard, 1f);
            inv.Add(ResourceType.Fish, ResourceQuality.Poor, 1f);

            IReadOnlyList<ItemStack> stacks = inv.Stacks();
            // Wood (0) before Fish (4); within Fish, Poor (0) before Fine (2).
            Assert.Equal(ResourceType.Wood, stacks[0].Type);
            Assert.Equal(ResourceType.Fish, stacks[1].Type);
            Assert.Equal(ResourceQuality.Poor, stacks[1].Quality);
            Assert.Equal(ResourceType.Fish, stacks[2].Type);
            Assert.Equal(ResourceQuality.Fine, stacks[2].Quality);
        }

        [Fact]
        public void ConsumeNutrition_AccountsForQuality()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Berries, ResourceQuality.Fine, 10f); // 0.6 * 1.3 = 0.78 per unit
            float got = inv.ConsumeNutrition(Catalog, 0.78f);
            Assert.True(Math.Abs(got - 0.78f) < 1e-3f);
            Assert.True(Math.Abs(inv.Count(ResourceType.Berries) - 9f) < 1e-3f);
        }

        [Fact]
        public void ConsumeHeat_OnlyUsesFuel()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Firewood, ResourceQuality.Standard, 10f);
            inv.Add(ResourceType.Berries, ResourceQuality.Standard, 10f);

            float got = inv.ConsumeHeat(Catalog, 4f);
            Assert.True(Math.Abs(got - 4f) < 1e-3f);
            Assert.True(Math.Abs(inv.Count(ResourceType.Firewood) - 6f) < 1e-3f);
            Assert.Equal(10f, inv.Count(ResourceType.Berries)); // food never burned
        }

        [Fact]
        public void Spoilage_DecaysPerishablesOnly()
        {
            var inv = new Inventory(1000f);
            inv.Add(ResourceType.Firewood, ResourceQuality.Standard, 100f); // not perishable
            inv.Add(ResourceType.Fish, ResourceQuality.Standard, 100f);     // 3%/day

            inv.ApplySpoilage(Catalog);
            Assert.Equal(100f, inv.Count(ResourceType.Firewood));
            Assert.True(Math.Abs(inv.Count(ResourceType.Fish) - 97f) < 1e-3f);
        }
    }

    public class ResourceCatalogTests
    {
        [Fact]
        public void Default_DefinesEveryResourceType()
        {
            var c = ResourceCatalog.CreateDefault();
            foreach (ResourceType t in Enum.GetValues(typeof(ResourceType)))
            {
                Assert.True(c.Has(t), "missing def for " + t);
            }
        }

        [Fact]
        public void Get_ThrowsForUndefinedType()
        {
            var empty = new ResourceCatalog();
            Assert.Throws<KeyNotFoundException>(() => empty.Get(ResourceType.Wood));
        }

        [Fact]
        public void FoodAndFuel_FlagsAreConsistent()
        {
            var c = ResourceCatalog.CreateDefault();
            Assert.True(c.Get(ResourceType.Berries).IsFood);
            Assert.True(c.Get(ResourceType.Firewood).IsFuel);
            Assert.False(c.Get(ResourceType.Firewood).IsFood);
            Assert.False(c.Get(ResourceType.Stone).IsFood);
        }
    }

    public class ClockAndSeasonTests
    {
        [Fact]
        public void Clock_DerivesSeasonAndYear()
        {
            var clock = new SimulationClock(24);
            Assert.Equal(Season.Spring, clock.Season);
            Assert.Equal(0, clock.Year);

            clock.Advance(24); Assert.Equal(Season.Summer, clock.Season);
            clock.Advance(24); Assert.Equal(Season.Autumn, clock.Season);
            clock.Advance(24); Assert.Equal(Season.Winter, clock.Season);
            clock.Advance(24); Assert.Equal(Season.Spring, clock.Season);
            Assert.Equal(1, clock.Year);
            Assert.Equal(0, clock.DayOfSeason);
        }

        [Fact]
        public void SeasonDef_DefaultIsFourInSeasonOrder()
        {
            SeasonDef[] defs = SeasonDef.CreateDefault();
            Assert.Equal(4, defs.Length);
            for (int i = 0; i < defs.Length; i++)
            {
                Assert.Equal((Season)i, defs[i].Season);
            }
            // Winter is the harshest: no food gathering pressure relief, full fuel demand.
            Assert.Equal(1.0f, defs[(int)Season.Winter].FirewoodNeedMult);
            Assert.Equal(0.0f, defs[(int)Season.Summer].FirewoodNeedMult);
        }
    }
}
