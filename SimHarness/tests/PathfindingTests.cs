using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Pathfinding;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    internal static class MapFixtures
    {
        public static WorldMap GreenValley(int size = 64)
        {
            return WorldGenerator.Generate(new WorldGenSettings { Width = size, Height = size }, StableHash.Fnv1a64("green-valley"));
        }

        public static Coord LandTile(WorldMap m)
        {
            int cx = m.Width / 2, cy = m.Height / 2;
            for (int r = 0; r < m.Width; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (dx > -r && dx < r && dy > -r && dy < r) continue;
                        int x = cx + dx, y = cy + dy;
                        if (m.InBounds(x, y) && !m.Get(x, y).IsWater) return new Coord(x, y);
                    }
            return new Coord(cx, cy);
        }

        public static Coord WaterTile(WorldMap m)
        {
            for (int y = 0; y < m.Height; y++)
                for (int x = 0; x < m.Width; x++)
                    if (m.Get(x, y).IsWater) return new Coord(x, y);
            return new Coord(0, 0);
        }

        public static Coord FarthestReachable(MovementCost cost, WorldMap m, Coord from)
        {
            float[] df = DistanceField.Compute(cost, m.Width, m.Height, from);
            int best = from.Y * m.Width + from.X;
            float bd = -1f;
            for (int i = 0; i < df.Length; i++)
                if (!float.IsPositiveInfinity(df[i]) && df[i] > bd) { bd = df[i]; best = i; }
            return new Coord(best % m.Width, best / m.Width);
        }
    }

    public class RoadNetworkTests
    {
        [Fact]
        public void Traffic_WearsTrailThenRoad()
        {
            var roads = new RoadNetwork(10, 10);
            Assert.Equal(RoadLevel.None, roads.LevelAt(5, 5));

            roads.AddTraffic(5, 5, roads.TrailThreshold);
            Assert.Equal(RoadLevel.Trail, roads.LevelAt(5, 5));

            roads.AddTraffic(5, 5, roads.RoadThreshold);
            Assert.Equal(RoadLevel.Road, roads.LevelAt(5, 5));
        }

        [Fact]
        public void CostMultiplier_OrdersRoadCheaperThanTrailCheaperThanNone()
        {
            var roads = new RoadNetwork(4, 4);
            float none = roads.CostMultiplier(0, 0);
            roads.AddTraffic(1, 1, roads.TrailThreshold);
            float trail = roads.CostMultiplier(1, 1);
            roads.AddTraffic(2, 2, roads.RoadThreshold);
            float road = roads.CostMultiplier(2, 2);
            Assert.True(road < trail && trail < none);
        }

        [Fact]
        public void Decay_FadesUnusedTrail()
        {
            var roads = new RoadNetwork(4, 4);
            roads.AddTraffic(1, 1, roads.TrailThreshold);
            Assert.Equal(RoadLevel.Trail, roads.LevelAt(1, 1));
            for (int i = 0; i < 200; i++) roads.Decay();
            Assert.Equal(RoadLevel.None, roads.LevelAt(1, 1));
        }
    }

    public class MovementCostTests
    {
        [Fact]
        public void Water_IsImpassable_LandIsPassable()
        {
            WorldMap m = MapFixtures.GreenValley();
            var cost = new MovementCost(m);
            Coord land = MapFixtures.LandTile(m);
            Coord water = MapFixtures.WaterTile(m);
            Assert.True(cost.Passable(land.X, land.Y));
            Assert.False(cost.Passable(water.X, water.Y));
            Assert.True(float.IsPositiveInfinity(cost.Tile(water.X, water.Y)));
        }

        [Fact]
        public void Road_LowersTileCost()
        {
            WorldMap m = MapFixtures.GreenValley();
            var roads = new RoadNetwork(m.Width, m.Height);
            var cost = new MovementCost(m, roads);
            Coord land = MapFixtures.LandTile(m);

            float bare = cost.Tile(land.X, land.Y);
            roads.AddTraffic(land.X, land.Y, roads.RoadThreshold);
            float roaded = cost.Tile(land.X, land.Y);
            Assert.True(roaded < bare);
        }
    }

    public class PathfinderTests
    {
        [Fact]
        public void FindsContiguousLandPath()
        {
            WorldMap m = MapFixtures.GreenValley();
            var cost = new MovementCost(m);
            Coord start = MapFixtures.LandTile(m);
            Coord goal = MapFixtures.FarthestReachable(cost, m, start);

            Path p = Pathfinder.Find(cost, m.Width, m.Height, start, goal);

            Assert.True(p.Found);
            Assert.Equal(start, p.Tiles[0]);
            Assert.Equal(goal, p.Tiles[p.Length - 1]);
            for (int i = 1; i < p.Length; i++)
            {
                Assert.True(p.Tiles[i - 1].ChebyshevTo(p.Tiles[i]) == 1, "path not contiguous");
                Assert.False(m.Get(p.Tiles[i].X, p.Tiles[i].Y).IsWater, "path crosses water");
            }
        }

        [Fact]
        public void IsDeterministic()
        {
            WorldMap m = MapFixtures.GreenValley();
            var cost = new MovementCost(m);
            Coord start = MapFixtures.LandTile(m);
            Coord goal = MapFixtures.FarthestReachable(cost, m, start);

            Path a = Pathfinder.Find(cost, m.Width, m.Height, start, goal);
            Path b = Pathfinder.Find(cost, m.Width, m.Height, start, goal);
            Assert.Equal(a.Cost, b.Cost);
            Assert.Equal(a.Length, b.Length);
        }

        [Fact]
        public void UnreachableGoal_ReturnsNone()
        {
            WorldMap m = MapFixtures.GreenValley();
            var cost = new MovementCost(m);
            Coord start = MapFixtures.LandTile(m);
            Coord water = MapFixtures.WaterTile(m);
            Assert.False(Pathfinder.Find(cost, m.Width, m.Height, start, water).Found);
        }

        [Fact]
        public void Roads_MakeThePathCheaper()
        {
            WorldMap m = MapFixtures.GreenValley();
            var roads = new RoadNetwork(m.Width, m.Height);
            var cost = new MovementCost(m, roads);
            Coord start = MapFixtures.LandTile(m);
            Coord goal = MapFixtures.FarthestReachable(cost, m, start);

            Path before = Pathfinder.Find(cost, m.Width, m.Height, start, goal);
            for (int i = 0; i < before.Length; i++)
                roads.AddTraffic(before.Tiles[i].X, before.Tiles[i].Y, roads.RoadThreshold);

            Path after = Pathfinder.Find(cost, m.Width, m.Height, start, goal);
            Assert.True(after.Cost < before.Cost, $"road {after.Cost} should beat wilderness {before.Cost}");
        }
    }

    public class DistanceFieldTests
    {
        [Fact]
        public void SourceIsZero_WaterIsInfinite_LandIsFinite()
        {
            WorldMap m = MapFixtures.GreenValley();
            var cost = new MovementCost(m);
            Coord start = MapFixtures.LandTile(m);
            float[] df = DistanceField.Compute(cost, m.Width, m.Height, start);

            Assert.Equal(0f, df[start.Y * m.Width + start.X]);
            Coord water = MapFixtures.WaterTile(m);
            Assert.True(float.IsPositiveInfinity(df[water.Y * m.Width + water.X]));
            Coord far = MapFixtures.FarthestReachable(cost, m, start);
            Assert.True(df[far.Y * m.Width + far.X] > 0f && !float.IsPositiveInfinity(df[far.Y * m.Width + far.X]));
        }
    }

    public class PathWeightedPotentialTests
    {
        [Fact]
        public void PathWeighting_ChangesFactorsVersusRadius()
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 96, Height = 96 }, seed);

            SettlementConfig Make(bool weighted) => new SettlementConfig
            {
                FoodPotentialForFull = 3000f, FirewoodPotentialForFull = 3000f, StonePotentialForFull = 3000f,
                MinFoodFactor = 0f, MinFirewoodFactor = 0f, MinStoneFactor = 0f,
                UsePathWeightedPotential = weighted
            };

            Settlement radius = SettlementFactory.Create(map, seed, Make(false), ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            Settlement weight = SettlementFactory.Create(map, seed, Make(true), ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());

            bool anyDiff =
                System.Math.Abs(radius.FoodFactor - weight.FoodFactor) > 1e-4f ||
                System.Math.Abs(radius.FirewoodFactor - weight.FirewoodFactor) > 1e-4f ||
                System.Math.Abs(radius.StoneFactor - weight.StoneFactor) > 1e-4f;
            Assert.True(anyDiff, "path weighting should change the resource factors");

            Assert.InRange(weight.FoodFactor, 0f, 1f);
            Assert.InRange(weight.FirewoodFactor, 0f, 1f);
            Assert.InRange(weight.StoneFactor, 0f, 1f);
        }
    }
}
