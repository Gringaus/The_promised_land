using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Pathfinding;

namespace FoundersLands.Simulation.Logistics
{
    /// <summary>
    /// A request to carry goods from one inventory to another along a path (GDD §12). The path
    /// is what ties logistics to terrain: a haul over a worn road is the same job as one across
    /// a marsh, but cheaper to run and faster to repeat.
    /// </summary>
    public sealed class HaulJob
    {
        public readonly Inventory From;
        public readonly Inventory To;
        public readonly ResourceType Type;
        public readonly ResourceQuality Quality;
        public readonly float Amount;
        public readonly Path Path;

        public HaulJob(Inventory from, Inventory to, ResourceType type, ResourceQuality quality, float amount, Path path)
        {
            From = from;
            To = to;
            Type = type;
            Quality = quality;
            Amount = amount;
            Path = path;
        }
    }
}
