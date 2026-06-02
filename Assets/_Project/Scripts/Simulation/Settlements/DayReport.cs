using FoundersLands.Simulation.Time;

namespace FoundersLands.Simulation.Settlements
{
    /// <summary>Snapshot of the settlement at the end of a simulated day (for logging/tests).</summary>
    public readonly struct DayReport
    {
        public readonly int Day;
        public readonly int Year;
        public readonly Season Season;
        public readonly int Population;
        public readonly int DeathsToday;
        public readonly float FoodUnits;
        public readonly float FirewoodUnits;
        public readonly float StoredNutrition;
        public readonly float AvgHealth;

        public DayReport(int day, int year, Season season, int population, int deathsToday,
            float foodUnits, float firewoodUnits, float storedNutrition, float avgHealth)
        {
            Day = day;
            Year = year;
            Season = season;
            Population = population;
            DeathsToday = deathsToday;
            FoodUnits = foodUnits;
            FirewoodUnits = firewoodUnits;
            StoredNutrition = storedNutrition;
            AvgHealth = avgHealth;
        }
    }
}
