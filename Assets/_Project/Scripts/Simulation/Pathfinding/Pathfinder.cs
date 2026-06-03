using System.Collections.Generic;
using FoundersLands.Simulation.Mathematics;

namespace FoundersLands.Simulation.Pathfinding
{
    /// <summary>
    /// A* over the tile grid (GDD §20 PathfindingSystem). Eight-directional, uses each tile's
    /// <see cref="MovementCost"/> as the cost to enter it, and an admissible octile heuristic.
    /// Tie-breaks on node index so a given query is deterministic. Diagonal moves may not cut
    /// the corner between two blocked tiles (no walking across the diagonal of a river).
    /// </summary>
    public static class Pathfinder
    {
        private static readonly int[] DX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        private static readonly int[] DY = { 0, 0, 1, -1, 1, -1, 1, -1 };
        private static readonly float[] Step = { 1f, 1f, 1f, 1f, 1.4142135f, 1.4142135f, 1.4142135f, 1.4142135f };

        public static Path Find(MovementCost cost, int width, int height, Coord start, Coord goal)
        {
            if (!cost.Passable(start.X, start.Y) || !cost.Passable(goal.X, goal.Y)) return Path.None;

            int n = width * height;
            int startIdx = start.Y * width + start.X;
            int goalIdx = goal.Y * width + goal.X;
            if (startIdx == goalIdx) return new Path(true, 0f, new[] { start });

            var g = new float[n];
            var came = new int[n];
            var closed = new bool[n];
            for (int i = 0; i < n; i++) { g[i] = float.PositiveInfinity; came[i] = -1; }

            var open = new MinHeap(width + height);
            g[startIdx] = 0f;
            open.Push(Heuristic(start, goal), startIdx);

            while (open.Count > 0)
            {
                int node = open.Pop();
                if (closed[node]) continue;
                closed[node] = true;
                if (node == goalIdx) return Reconstruct(came, goalIdx, width, g[goalIdx]);

                int x = node % width;
                int y = node / width;

                for (int d = 0; d < 8; d++)
                {
                    int nx = x + DX[d];
                    int ny = y + DY[d];
                    if (!cost.Passable(nx, ny)) continue;
                    if (d >= 4 && !cost.Passable(x, ny) && !cost.Passable(nx, y)) continue; // no corner cut

                    int ni = ny * width + nx;
                    if (closed[ni]) continue;

                    float tentative = g[node] + cost.Tile(nx, ny) * Step[d];
                    if (tentative < g[ni])
                    {
                        g[ni] = tentative;
                        came[ni] = node;
                        open.Push(tentative + Heuristic(new Coord(nx, ny), goal), ni);
                    }
                }
            }

            return Path.None;
        }

        private static float Heuristic(Coord a, Coord b)
        {
            int dx = a.X - b.X; if (dx < 0) dx = -dx;
            int dy = a.Y - b.Y; if (dy < 0) dy = -dy;
            int min = dx < dy ? dx : dy;
            int max = dx < dy ? dy : dx;
            float octile = max + 0.4142135f * min;
            return octile * MovementCost.CheapestTile;
        }

        private static Path Reconstruct(int[] came, int goalIdx, int width, float cost)
        {
            var tiles = new List<Coord>();
            int cur = goalIdx;
            while (cur != -1)
            {
                tiles.Add(new Coord(cur % width, cur / width));
                cur = came[cur];
            }
            tiles.Reverse();
            return new Path(true, cost, tiles);
        }
    }
}
