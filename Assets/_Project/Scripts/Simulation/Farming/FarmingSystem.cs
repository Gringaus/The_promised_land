using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;

namespace FoundersLands.Simulation.Farming
{
    /// <summary>
    /// The seasonal crop cycle (GDD §9). Fields are sown in spring, tended through spring and
    /// summer — well-tended, fertile plots ripen fully, neglected ones only part-way — then reaped
    /// in autumn into grain, and lie fallow in winter. Farmer labour is shared across all fields,
    /// so a colony that plants more ground than it can work gets thinner harvests from each plot.
    /// Grain is later milled and baked into bread by the Module 6 production chain.
    /// </summary>
    public static class FarmingSystem
    {
        /// <summary>Returns the grain harvested this day (0 except at harvest).</summary>
        public static float Step(IReadOnlyList<Building> buildings, Inventory store, Season season,
            float fertility, float farmerLabor, SettlementConfig config)
        {
            int fieldCount = 0;
            for (int i = 0; i < buildings.Count; i++)
                if (buildings[i].Complete && buildings[i].Type == BuildingType.Field) fieldCount++;
            if (fieldCount == 0) return 0f;

            float laborPerField = farmerLabor / fieldCount;
            float harvested = 0f;

            for (int i = 0; i < buildings.Count; i++)
            {
                Building f = buildings[i];
                if (!f.Complete || f.Type != BuildingType.Field) continue;

                switch (season)
                {
                    case Season.Spring:
                        f.Planted = true; // sow
                        Grow(f, laborPerField, config);
                        break;
                    case Season.Summer:
                        if (f.Planted) Grow(f, laborPerField, config);
                        break;
                    case Season.Autumn:
                        if (f.Planted)
                        {
                            float yield = config.FieldBaseYield * f.CropGrowth * (0.5f + fertility);
                            if (yield > 0f)
                            {
                                store.Add(ResourceType.Grain, ResourceQuality.Standard, yield);
                                harvested += yield;
                            }
                            f.Planted = false;
                            f.CropGrowth = 0f;
                        }
                        break;
                    // Winter: fallow.
                }
            }

            return harvested;
        }

        private static void Grow(Building f, float labor, SettlementConfig c)
        {
            if (f.CropGrowth >= 1f) return;
            float tend = c.FarmTendLaborPerField > 0f ? labor / c.FarmTendLaborPerField : 1f;
            if (tend > 1f) tend = 1f;
            float inc = c.FarmGrowthPerDay * (c.FarmMinGrowthFraction + (1f - c.FarmMinGrowthFraction) * tend);
            f.CropGrowth += inc;
            if (f.CropGrowth > 1f) f.CropGrowth = 1f;
        }
    }
}
