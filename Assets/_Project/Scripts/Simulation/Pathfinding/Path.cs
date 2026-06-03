using System.Collections.Generic;
using FoundersLands.Simulation.Mathematics;

namespace FoundersLands.Simulation.Pathfinding
{
    /// <summary>Result of a path query: the tiles from start to goal (inclusive) and total cost.</summary>
    public sealed class Path
    {
        public readonly bool Found;
        public readonly float Cost;
        public readonly IReadOnlyList<Coord> Tiles;

        public Path(bool found, float cost, IReadOnlyList<Coord> tiles)
        {
            Found = found;
            Cost = cost;
            Tiles = tiles;
        }

        public int Length { get { return Tiles == null ? 0 : Tiles.Count; } }

        public static readonly Path None = new Path(false, float.PositiveInfinity, new Coord[0]);
    }
}
