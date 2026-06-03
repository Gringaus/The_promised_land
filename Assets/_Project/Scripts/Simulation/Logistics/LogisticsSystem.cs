using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;

namespace FoundersLands.Simulation.Logistics
{
    /// <summary>
    /// Moves goods between inventories and wears the route they travel (GDD §12, §7). Repeating
    /// a haul stamps traffic onto the road network, so a busy supply line packs itself into a
    /// trail and then a road — which in turn lowers the cost of every future trip along it.
    /// </summary>
    public static class LogisticsSystem
    {
        /// <summary>Move up to <paramref name="amount"/> of one stack from -> to. Returns delivered.</summary>
        public static float Haul(Inventory from, Inventory to, ResourceType type, ResourceQuality quality, float amount)
        {
            float removed = from.Remove(type, quality, amount);
            if (removed <= 0f) return 0f;

            float accepted = to.Add(type, quality, removed);
            if (accepted < removed) from.Add(type, quality, removed - accepted); // destination full: return the overflow
            return accepted;
        }

        /// <summary>Stamp traffic along every tile of a path.</summary>
        public static void StampRoute(RoadNetwork roads, Path path, float trafficPerTile)
        {
            if (roads == null || path == null || !path.Found) return;
            for (int i = 0; i < path.Tiles.Count; i++)
            {
                Coord c = path.Tiles[i];
                roads.AddTraffic(c.X, c.Y, trafficPerTile);
            }
        }

        /// <summary>Run a haul end to end: move the goods and wear the route. Returns delivered amount.</summary>
        public static float ExecuteHaul(HaulJob job, RoadNetwork roads, float trafficPerTile)
        {
            float moved = Haul(job.From, job.To, job.Type, job.Quality, job.Amount);
            if (moved > 0f) StampRoute(roads, job.Path, trafficPerTile);
            return moved;
        }
    }
}
