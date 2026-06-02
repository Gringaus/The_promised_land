namespace FoundersLands.Simulation.Time
{
    /// <summary>
    /// How a season modifies the economy (GDD §9). Winter is meant to be the dominant
    /// strategic pressure — gathering nearly stops while fuel demand peaks — so players
    /// must stockpile in the warm seasons.
    /// </summary>
    public sealed class SeasonDef
    {
        public Season Season;

        /// <summary>Multiplier on food gathering output.</summary>
        public float FoodGatherMult;

        /// <summary>Multiplier on firewood gathering output.</summary>
        public float FirewoodGatherMult;

        /// <summary>Multiplier on per-person daily firewood demand for warmth.</summary>
        public float FirewoodNeedMult;

        /// <summary>General work-speed multiplier (mud, snow, daylight).</summary>
        public float WorkSpeedMult;

        public SeasonDef(Season season)
        {
            Season = season;
        }

        public static SeasonDef[] CreateDefault()
        {
            return new[]
            {
                new SeasonDef(Season.Spring) { FoodGatherMult = 0.6f, FirewoodGatherMult = 0.8f, FirewoodNeedMult = 0.5f, WorkSpeedMult = 0.8f },
                new SeasonDef(Season.Summer) { FoodGatherMult = 1.2f, FirewoodGatherMult = 1.0f, FirewoodNeedMult = 0.0f, WorkSpeedMult = 1.0f },
                new SeasonDef(Season.Autumn) { FoodGatherMult = 1.0f, FirewoodGatherMult = 1.2f, FirewoodNeedMult = 0.3f, WorkSpeedMult = 1.0f },
                new SeasonDef(Season.Winter) { FoodGatherMult = 0.1f, FirewoodGatherMult = 0.3f, FirewoodNeedMult = 1.0f, WorkSpeedMult = 0.6f }
            };
        }
    }
}
