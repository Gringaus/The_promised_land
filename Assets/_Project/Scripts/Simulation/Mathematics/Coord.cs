namespace FoundersLands.Simulation.Mathematics
{
    /// <summary>Integer grid coordinate. Compact value type for tile addressing.</summary>
    public readonly struct Coord
    {
        public readonly int X;
        public readonly int Y;

        public Coord(int x, int y)
        {
            X = x;
            Y = y;
        }

        public static Coord operator +(Coord a, Coord b) { return new Coord(a.X + b.X, a.Y + b.Y); }
        public static Coord operator -(Coord a, Coord b) { return new Coord(a.X - b.X, a.Y - b.Y); }

        public int ManhattanTo(Coord o)
        {
            int dx = X - o.X; if (dx < 0) dx = -dx;
            int dy = Y - o.Y; if (dy < 0) dy = -dy;
            return dx + dy;
        }

        public int ChebyshevTo(Coord o)
        {
            int dx = X - o.X; if (dx < 0) dx = -dx;
            int dy = Y - o.Y; if (dy < 0) dy = -dy;
            return dx > dy ? dx : dy;
        }

        public override string ToString() { return "(" + X + "," + Y + ")"; }
    }
}
