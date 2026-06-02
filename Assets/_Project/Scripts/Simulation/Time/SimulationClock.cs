namespace FoundersLands.Simulation.Time
{
    /// <summary>
    /// Tracks game time in whole days and derives the season/year (GDD §9). A year is
    /// four equal seasons; the season length is configurable so balance runs can use
    /// short years.
    /// </summary>
    public sealed class SimulationClock
    {
        public readonly int DaysPerSeason;

        /// <summary>Total elapsed days since the start of the game.</summary>
        public int Day { get; private set; }

        public SimulationClock(int daysPerSeason = 24, int startDay = 0)
        {
            DaysPerSeason = daysPerSeason < 1 ? 1 : daysPerSeason;
            Day = startDay;
        }

        public int DaysPerYear { get { return DaysPerSeason * 4; } }

        public int Year { get { return Day / DaysPerYear; } }

        public int DayOfYear { get { return Day % DaysPerYear; } }

        public int DayOfSeason { get { return DayOfYear % DaysPerSeason; } }

        public Season Season { get { return (Season)((DayOfYear / DaysPerSeason) % 4); } }

        public void Advance(int days)
        {
            if (days > 0) Day += days;
        }
    }
}
