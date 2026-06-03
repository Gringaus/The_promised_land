using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;

namespace FoundersLands.Simulation.Logistics
{
    /// <summary>
    /// A porter walking a path over time (GDD §12). Advancing leaves traffic on the tiles it
    /// crosses, so movement itself shapes the road network (§7). Tick-based so the presentation
    /// layer can animate it, while the economy can also just run a whole haul at once.
    /// </summary>
    public sealed class Carrier
    {
        public readonly Path Path;

        /// <summary>Tiles advanced per tick.</summary>
        public float Speed;

        /// <summary>Fractional index along the path.</summary>
        public float Progress;

        private int _stampedUpTo;

        public Carrier(Path path, float speed = 1f)
        {
            Path = path;
            Speed = speed;
            Progress = 0f;
            _stampedUpTo = 0;
        }

        public bool Arrived { get { return Path == null || !Path.Found || Progress >= Path.Length - 1; } }

        public Coord Position
        {
            get
            {
                int i = (int)Progress;
                if (i < 0) i = 0;
                if (i >= Path.Length) i = Path.Length - 1;
                return Path.Tiles[i];
            }
        }

        /// <summary>Advance along the path, stamping newly entered tiles onto the road network.</summary>
        public void Advance(RoadNetwork roads, float trafficPerTile)
        {
            if (Path == null || !Path.Found) return;

            Progress += Speed;
            float max = Path.Length - 1;
            if (Progress > max) Progress = max;

            int idx = (int)Progress;
            while (_stampedUpTo < idx)
            {
                _stampedUpTo++;
                if (roads != null)
                {
                    Coord c = Path.Tiles[_stampedUpTo];
                    roads.AddTraffic(c.X, c.Y, trafficPerTile);
                }
            }
        }
    }
}
