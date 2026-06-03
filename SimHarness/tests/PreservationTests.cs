using System;
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
    public class PreservationTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        // ---- Inventory: spoilage scales with the reduction factor ----

        [Fact]
        public void Spoilage_ScalesWithReductionFactor()
        {
            ResourceCatalog catalog = ResourceCatalog.CreateDefault();

            float full = SpoilOnce(catalog, 0f);
            float half = SpoilOnce(catalog, 0.5f);
            float none = SpoilOnce(catalog, 1f);

            Assert.True(full > 0f, "berries should spoil with no preservation");
            Assert.Equal(full * 0.5f, half, 3);
            Assert.Equal(0f, none, 3); // full preservation stops spoilage entirely
        }

        private static float SpoilOnce(ResourceCatalog catalog, float reduction)
        {
            var inv = new Inventory(10000f);
            inv.Add(ResourceType.Berries, ResourceQuality.Standard, 1000f);
            return inv.ApplySpoilage(catalog, reduction);
        }

        // ---- Settlement: preservation buildings set the factor ----

        [Fact]
        public void Cellar_SetsSpoilageReductionFactor()
        {
            Settlement s = ForagerColony();
            Complete(s, BuildingType.Cellar, s.CenterX, s.CenterY + 1);
            s.RecomputeBuildingEffects();
            Assert.Equal(0.40f, s.SpoilageReductionFactor, 3);
        }

        [Fact]
        public void CellarAndSmokehouse_StackWithDiminishingReturns()
        {
            Settlement s = ForagerColony();
            Complete(s, BuildingType.Cellar, s.CenterX, s.CenterY + 1);
            Complete(s, BuildingType.Smokehouse, s.CenterX, s.CenterY + 2);
            s.RecomputeBuildingEffects();
            // 1 - (1-0.40)(1-0.30) = 0.58, not 0.70 — diminishing, never simply additive.
            Assert.Equal(0.58f, s.SpoilageReductionFactor, 3);
        }

        [Fact]
        public void ManyPreservers_AreCappedBelowTotalPreservation()
        {
            Settlement s = ForagerColony();
            for (int i = 0; i < 6; i++) Complete(s, BuildingType.Cellar, s.CenterX + i, s.CenterY + 3);
            s.RecomputeBuildingEffects();
            Assert.True(s.SpoilageReductionFactor <= 0.85f, "spoilage can never be fully eliminated");
            Assert.True(s.SpoilageReductionFactor > 0.8f, "many cellars should approach the cap");
        }

        [Fact]
        public void NoPreservationBuildings_FactorIsZero()
        {
            Settlement s = ForagerColony();
            s.RecomputeBuildingEffects();
            Assert.Equal(0f, s.SpoilageReductionFactor);
        }

        // ---- Behaviour: a preserving colony keeps more food ----

        [Fact]
        public void PreservingColony_KeepsMoreFoodAndLosesLess()
        {
            Settlement plain = ForagerColony();
            Settlement kept = ForagerColony();
            Complete(kept, BuildingType.Cellar, kept.CenterX, kept.CenterY + 1);
            Complete(kept, BuildingType.Smokehouse, kept.CenterX, kept.CenterY + 2);
            kept.RecomputeBuildingEffects();

            SettlementSimulation.Run(plain, plain.Clock.DaysPerYear);
            SettlementSimulation.Run(kept, kept.Clock.DaysPerYear);

            Assert.True(kept.TotalSpoiled < plain.TotalSpoiled, "preservation must reduce spoilage");
            Assert.True(kept.FoodUnits() > plain.FoodUnits(), "preservation must leave more food on hand");
        }

        // ---- Technology gates the preservation buildings ----

        [Fact]
        public void PreservationBuildings_AreGatedByAgriculture()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 64, Height = 64 }, Seed);
            var config = new SettlementConfig { StartingPopulation = 8, EnableTechnology = true };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            Assert.Null(s.PlaceBlueprint(BuildingType.Cellar, s.CenterX, s.CenterY)); // locked
            ResearchSystem.Grant(s, 2); // Agriculture
            Assert.NotNull(s.PlaceBlueprint(BuildingType.Cellar, s.CenterX, s.CenterY)); // now buildable
        }

        // ---- Save: the recomputed factor keeps a loaded colony in lockstep ----

        [Fact]
        public void PreservingColony_RoundTripsThroughSave()
        {
            Settlement s = ForagerColony();
            Complete(s, BuildingType.Cellar, s.CenterX, s.CenterY + 1);
            Complete(s, BuildingType.Smokehouse, s.CenterX, s.CenterY + 2);
            s.RecomputeBuildingEffects();
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));
            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.SpoilageReductionFactor, loaded.SpoilageReductionFactor, 3); // recomputed from saved buildings

            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            SettlementSimulation.Run(loaded, loaded.Clock.DaysPerYear);
            Assert.Equal(s.StateHash(), loaded.StateHash());
        }

        // ---- helpers ----

        private static Settlement ForagerColony()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            var config = new SettlementConfig
            {
                StartingPopulation = 20,
                ForagerShare = 0.62f, WoodcutterShare = 0.30f,
                StartingFoodUnits = 220f, StartingFirewoodUnits = 220f
            };
            Settlement s = SettlementFactory.Create(map, Seed, config, ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            Complete(s, BuildingType.Storehouse, s.CenterX + 1, s.CenterY);
            for (int i = 0; i < 4; i++) Complete(s, BuildingType.House, s.CenterX + 2 + i, s.CenterY);
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }
    }
}
