using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Logistics;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class LogisticsTests
    {
        private static Path StraightPath(int x0, int y, int length)
        {
            var tiles = new Coord[length];
            for (int i = 0; i < length; i++) tiles[i] = new Coord(x0 + i, y);
            return new Path(true, length, tiles);
        }

        [Fact]
        public void Haul_RespectsStockAndCapacity()
        {
            var from = new Inventory(1000f);
            from.Add(ResourceType.Wood, ResourceQuality.Standard, 50f);
            var to = new Inventory(30f); // smaller than the load

            float moved = LogisticsSystem.Haul(from, to, ResourceType.Wood, ResourceQuality.Standard, 40f);

            Assert.Equal(30f, moved);                               // capped by destination capacity
            Assert.Equal(30f, to.Count(ResourceType.Wood));
            Assert.Equal(20f, from.Count(ResourceType.Wood));       // overflow returned to source
        }

        [Fact]
        public void Haul_MovesNothingFromEmptySource()
        {
            var from = new Inventory(100f);
            var to = new Inventory(100f);
            Assert.Equal(0f, LogisticsSystem.Haul(from, to, ResourceType.Stone, ResourceQuality.Standard, 10f));
        }

        [Fact]
        public void StampRoute_AddsTrafficAlongPath()
        {
            var roads = new RoadNetwork(20, 20);
            Path path = StraightPath(2, 5, 6);
            LogisticsSystem.StampRoute(roads, path, 4f);
            for (int i = 0; i < path.Length; i++)
            {
                Assert.Equal(4f, roads.TrafficAt(path.Tiles[i].X, path.Tiles[i].Y));
            }
        }

        [Fact]
        public void ExecuteHaul_DeliversGoodsAndWearsRoute()
        {
            var from = new Inventory(10000f);
            from.Add(ResourceType.Wood, ResourceQuality.Standard, 10000f);
            var to = new Inventory(10000f);
            var roads = new RoadNetwork(40, 40);
            Path path = StraightPath(5, 10, 8);

            for (int trip = 0; trip < 20; trip++)
            {
                var job = new HaulJob(from, to, ResourceType.Wood, ResourceQuality.Standard, 10f, path);
                Assert.Equal(10f, LogisticsSystem.ExecuteHaul(job, roads, 3f));
            }

            Assert.Equal(200f, to.Count(ResourceType.Wood)); // 20 trips * 10
            // 20 trips * 3 traffic = 60 >= RoadThreshold (40): the supply line packed into a road.
            Assert.Equal(RoadLevel.Road, roads.LevelAt(path.Tiles[3].X, path.Tiles[3].Y));
        }

        [Fact]
        public void Carrier_WalksThePathAndStampsIt()
        {
            var roads = new RoadNetwork(40, 40);
            Path path = StraightPath(5, 10, 6);
            var carrier = new Carrier(path, speed: 1f);

            int guard = 100;
            while (!carrier.Arrived && guard-- > 0) carrier.Advance(roads, 5f);

            Assert.True(carrier.Arrived);
            Assert.Equal(path.Tiles[path.Length - 1], carrier.Position);
            // The tiles it walked over picked up traffic (start tile is not stamped, the rest are).
            Assert.True(roads.TrafficAt(path.Tiles[3].X, path.Tiles[3].Y) > 0f);
        }
    }
}
