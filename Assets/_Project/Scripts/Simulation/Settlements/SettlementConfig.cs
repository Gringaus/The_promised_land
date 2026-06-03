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
        public float MinerShare = 0.0f;
        public float CraftsmanShare = 0.0f;
        public float MilitiaShare = 0.0f;
        public float FarmerShare = 0.0f;

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
        public float MinerIronPerDay = 3.0f;      // железная руда
        public float CraftsmanWorkPerDay = 8.0f;  // единиц работы в мастерских (GDD §12)
        public float FarmerWorkPerDay = 8.0f;     // единиц труда на полях (GDD §9)

        // Farming (Module 8, GDD §9). With no fields/farmers these are inert.
        public float FarmGrowthPerDay = 0.03f;       // crop maturity gained per tended day
        public float FarmTendLaborPerField = 8.0f;   // farmer labour to fully tend one field
        public float FarmMinGrowthFraction = 0.3f;   // growth an untended field still manages
        public float FieldBaseYield = 110f;          // grain from one fully-grown field at avg soil

        // How map richness near the settlement gates output.
        public float GatherRadius = 24f;
        public float FoodPotentialForFull = 250f;
        public float FirewoodPotentialForFull = 250f;
        public float StonePotentialForFull = 250f;
        public float IronPotentialForFull = 120f;
        public float MinFoodFactor = 0.2f;     // wild foraging even with no nodes
        public float MinFirewoodFactor = 0.1f;
        public float MinStoneFactor = 0.05f;
        public float MinIronFactor = 0.0f;     // no iron without a deposit nearby

        // Tools (GDD §12 "инструменты ускоряют работы") and market service. With none of
        // these present the work multiplier is 1, so earlier modules are unaffected.
        public float ToolsBonusMax = 0.5f;             // fully tooled -> +50% output
        public float ToolsWearPerWorkerPerDay = 0.02f;
        public float MarketProductivityBonus = 0.1f;   // a stocked market frees chore time

        // Opt-in: weight resource potential by real path distance from the site (Dijkstra)
        // instead of a flat radius, so rivers, marsh and distance shape the economy
        // (GDD §3, §12). Default off keeps the Module 2/3 balance unchanged.
        public bool UsePathWeightedPotential = false;
        public float PathPotentialScale = 12f;

        // Sheltered citizens need less firewood. Fully housed in good shelter cuts the
        // winter fuel demand by up to this fraction (GDD §10 housing/warmth).
        public float WarmthReductionMax = 0.5f;

        // Threat layer / AI Director (GDD §13). Off by default so Modules 2-6 are unchanged;
        // the "raiders" scenario turns it on. Wealth and a nearby camp drive pressure up the
        // escalation ladder; militia and defensive buildings push it back down and soften blows.
        public bool EnableThreats = false;
        public float MilitiaDefense = 2.0f;       // defence points per militiaman
        public float ThreatBaseGrowth = 0.5f;
        public float ThreatWealthGrowth = 2.5f;   // scaled by wealth / ThreatWealthScale
        public float ThreatWealthScale = 6000f;
        public float ThreatDefenseDecay = 0.5f;   // pressure suppressed per defence point
        public float CampStartStrength = 8f;
        public float CampMaxStrength = 30f;
        public float CampRegenPerDay = 0.3f;
        public float TheftChance = 0.35f;         // per day while in the Thefts stage
        public float TheftUnits = 25f;
        public float AmbushChance = 0.5f;         // per day while in the Ambush stage
        public float AmbushUnits = 45f;
        public float AmbushInjury = 5f;           // health hit on a successful ambush
        public int RaidIntervalDays = 15;         // raids recur while pressure stays at raid level
        public float RaidFraction = 0.15f;        // fraction of stored goods looted in a raid
        public float RaidEssentialCapFraction = 0.3f; // raiders skim at most this share of food/fuel
        public float RaidInjury = 16f;            // health hit to everyone in a raid
        public float RaidPressureRelief = 45f;    // pressure drop once raiders leave with loot
        public float RaidCampCost = 6f;           // camp strength spent mounting a raid

        // Population dynamics / demographics (GDD §11). Off by default so the fixed-population
        // scenarios of Modules 2-8 are unchanged; the demographics scenario turns it on.
        public bool EnablePopulationDynamics = false;
        public int WorkingAge = 16;                   // children below this don't work
        public float ChildFoodFraction = 0.5f;        // a child eats this share of an adult's ration
        public int FertileMinAge = 18;
        public int FertileMaxAge = 45;
        public float DailyBirthRatePerAdult = 0.0016f; // births per fertile adult per day, before modifiers
        public int OldAgeStart = 66;
        public float OldAgeMortalityScale = 0.02f;     // yearly death chance per year of age past OldAgeStart
        public float MigrationDailyRate = 0.04f;       // scales net migration by attractiveness
        public int ImmigrantMinAge = 18;
        public int ImmigrantMaxAge = 35;
        // Growth is gated by food-production capacity, not the (lagging) stored cushion, so a
        // colony grows within its means instead of overshooting into famine. It grows freely while
        // its population sits below this fraction of what its food workers can feed, and stops at
        // capacity. As the workforce expands (migrants, children come of age) capacity rises.
        public float GrowthFoodHeadroom = 0.70f;

        // Trade (Module 11, GDD §12). Off by default so colonies that own a market in earlier
        // modules (production, raiders) keep their behaviour; the trade scenario turns it on.
        public bool EnableTrade = false;
        public int TradeIntervalDays = 12;       // a caravan visits on this cadence
        public float MerchantMargin = 0.25f;     // spread between buy and sell prices
        public float CaravanCapacity = 250f;     // max units moved per visit (scaled by route safety)
        public float StartingSilver = 0f;

        // Health dynamics.
        public float StarveHealthLossPerDay = 16f;
        public float ColdHealthLossPerDay = 16f;
        public float HealthRecoverPerDay = 6f;
        public float NeedHealthThreshold = 0.5f; // hunger/cold above this damages health
    }
}
