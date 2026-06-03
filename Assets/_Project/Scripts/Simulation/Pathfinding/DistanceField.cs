using FoundersLands.Simulation.Mathematics;

namespace FoundersLands.Simulation.Pathfinding
{
    /// <summary>
    /// Dijkstra distance field: the cheapest movement cost from a source tile to every other
    /// tile (GDD §12 logistics). Unreachable tiles stay at infinity. Used to weight how
    /// accessible a resource is from the settlement — far or marsh-locked deposits cost more
    /// to exploit, which is what makes the economy "visible" (GDD §3).
    /// </summary>
    public static class DistanceField
    {
        public static float[] Compute(MovementCost cost, int width, int height, Coord source)
        {
            int n = width * height;
            var dist = new float[n];
            for (int i = 0; i < n; i++) dist[i] = float.PositiveInfinity;
            if (!cost.Passable(source.X, source.Y)) return dist;

            var done = new bool[n];
            var heap = new MinHeap(width + height);
            int srcIdx = source.Y * width + source.X;
            dist[srcIdx] = 0f;
            heap.Push(0f, srcIdx);

            int[] dx = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] dy = { 0, 0, 1, -1, 1, -1, 1, -1 };
            float[] step = { 1f, 1f, 1f, 1f, 1.4142135f, 1.4142135f, 1.4142135f, 1.4142135f };

            while (heap.Count > 0)
            {
                int node = heap.Pop();
                if (done[node]) continue;
                done[node] = true;

                int x = node % width;
                int y = node / width;
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + dx[d];
                    int ny = y + dy[d];
                    if (!cost.Passable(nx, ny)) continue;
                    if (d >= 4 && !cost.Passable(x, ny) && !cost.Passable(nx, y)) continue;

                    int ni = ny * width + nx;
                    float nd = dist[node] + cost.Tile(nx, ny) * step[d];
                    if (nd < dist[ni])
                    {
                        dist[ni] = nd;
                        heap.Push(nd, ni);
                    }
                }
            }

            return dist;
        }
    }
}
