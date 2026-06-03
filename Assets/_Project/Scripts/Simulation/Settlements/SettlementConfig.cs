namespace FoundersLands.Simulation.Settlements
{
    /// <summary>
    /// Tunable balance for the survival loop (GDD §9, §11, §28). Plain data so balance
    /// can be swept headlessly; authored via ScriptableObject in Unity later (§20).
    /// </summary>
    public sealed class SettlementConfig
    {
        public int DaysPerSeason = 24;

        public int StartingPopulation = 20;
        // Workforce split. Logger/Quarryman/Builder default to 0 so the survival scenario
        // (Module 2) is unchanged; the construction scenario raises them.
        public float ForagerShare = 0.65f;
        public float WoodcutterShare = 0.30f;
        public float LoggerShare = 0.0f;
        public float QuarrymanShare = 0.0f;
        public float BuilderShare = 0.0f;

        public float StorehouseCapacity = 6000f;

        // Starting stock to bridge the first lean weeks while production ramps up.
        public float StartingFoodUnits = 120f;
        public float StartingFirewoodUnits = 150f;
        public float StartingWoodUnits = 0f;
        public float StartingStoneUnits = 0f;

        // Per-person daily demand.
        public float NutritionPerPersonPerDay = 1.0f;
        public float FirewoodPerPersonPerDayWinter = 2.0f; // scaled by season FirewoodNeedMult

        // Per-worker daily output at full season/potential. Forager yield is set so a
        // resource-rich valley runs an annual food surplus and can stockpile for winter.
        public float ForagerNutritionPerDay = 3.8f;
        public float WoodcutterFirewoodPerDay = 5.0f;
        public float LoggerWoodPerDay = 4.0f;     // строевая древесина
        public float QuarrymanStonePerDay = 3.0f; // камень
        public float BuilderWorkPerDay = 8.0f;    // единиц работы на стройке (GDD §10)

        // How map richness near the settlement gates output.
        public float GatherRadius = 24f;
        public float FoodPotentialForFull = 250f;
        public float FirewoodPotentialForFull = 250f;
        public float StonePotentialForFull = 250f;
        public float MinFoodFactor = 0.2f;     // wild foraging even with no nodes
        public float MinFirewoodFactor = 0.1f;
        public float MinStoneFactor = 0.05f;

        // Sheltered citizens need less firewood. Fully housed in good shelter cuts the
        // winter fuel demand by up to this fraction (GDD §10 housing/warmth).
        public float WarmthReductionMax = 0.5f;

        // Health dynamics.
        public float StarveHealthLossPerDay = 16f;
        public float ColdHealthLossPerDay = 16f;
        public float HealthRecoverPerDay = 6f;
        public float NeedHealthThreshold = 0.5f; // hunger/cold above this damages health
    }
}
