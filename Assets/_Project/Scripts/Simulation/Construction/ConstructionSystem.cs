using System.Collections.Generic;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Construction
{
    /// <summary>
    /// Drives construction for one day (GDD §10). Materials are pulled from a storehouse and
    /// a shared pool of builder labour is spent on the oldest unfinished blueprint first.
    /// Aspatial for now (no hauling distance); the logistics system adds that later.
    /// </summary>
    public static class ConstructionSystem
    {
        /// <summary>
        /// Deliver materials to active sites and spend <paramref name="builderWork"/> on them.
        /// Returns the number of buildings completed this step.
        /// </summary>
        public static int Step(IReadOnlyList<Building> buildings, Inventory storehouse, float builderWork)
        {
            float workLeft = builderWork;
            int completed = 0;

            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                if (b.Complete) continue;

                DeliverMaterials(b, storehouse);

                // Work is capped by how much material has arrived (the build stalls otherwise).
                float workCap = b.WorkRequired * b.DeliveredFraction;
                if (workLeft > 0f && b.WorkDone < workCap)
                {
                    float add = workCap - b.WorkDone;
                    if (add > workLeft) add = workLeft;
                    b.WorkDone += add;
                    workLeft -= add;
                }

                if (!b.Complete && b.WorkDone >= b.WorkRequired - 1e-4f && b.FullyDelivered)
                {
                    b.WorkDone = b.WorkRequired;
                    b.Complete = true;
                    completed++;
                }

                if (workLeft <= 0f) break; // labour spent; remaining sites wait for tomorrow
            }

            return completed;
        }

        private static void DeliverMaterials(Building b, Inventory storehouse)
        {
            IReadOnlyList<MaterialCost> cost = b.Cost;
            for (int i = 0; i < cost.Count; i++)
            {
                float remaining = b.Remaining(cost[i].Type, cost[i].Amount);
                if (remaining <= 0f) continue;
                float taken = storehouse.Remove(cost[i].Type, ResourceQuality.Standard, remaining);
                if (taken > 0f) b.Deliver(cost[i].Type, taken);
            }
        }
    }
}
