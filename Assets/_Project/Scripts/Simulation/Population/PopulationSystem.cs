using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Settlements;

namespace FoundersLands.Simulation.Population
{
    /// <summary>
    /// Demographics (GDD §11): the colony ages, buries its elders, raises children born to it,
    /// and gains or loses settlers to migration. Births need both full bellies and room under a
    /// roof; migrants are drawn to a fed, safe, spacious town and flee a starving or dangerous one.
    /// Children don't work until they come of age, so a colony heavy with the young carries them.
    ///
    /// Births and migration use deterministic accumulators (no RNG cursor); only old-age death
    /// rolls, and it does so statelessly from (seed, day, citizen id) — so the whole system is
    /// reproducible and survives a save/load. Gated by <see cref="SettlementConfig.EnablePopulationDynamics"/>
    /// (off by default), leaving the fixed-population scenarios of earlier modules untouched.
    /// </summary>
    public static class PopulationSystem
    {
        public static void Step(Settlement s)
        {
            SettlementConfig c = s.Config;
            if (!c.EnablePopulationDynamics) return;

            int pop = s.AlivePopulation;
            if (pop == 0) return;

            // Gate growth by food-production capacity rather than the lagging stored cushion: a
            // colony grows freely below GrowthFoodHeadroom of what its foragers can feed and stops
            // at capacity, so it never breeds itself into a famine the larder briefly hid.
            float capacity = FoodCapacity(s, c);
            float popRatio = capacity > 0f ? pop / capacity : 99f;
            float foodSecurity = Clamp01((1f - popRatio) / (1f - c.GrowthFoodHeadroom));
            float housingHeadroom = HousingHeadroom(s, pop);             // 0..1 spare room
            float housingFactor = s.HousingCapacity > pop ? 1f : 0f;     // no room under a roof, no births
            float threat01 = c.EnableThreats ? s.Threat.Pressure / 100f : 0f;
            float sick01 = Clamp01(1f - s.AverageHealth / 100f);

            // Births: each fertile adult contributes a little chance daily, gated by food and housing.
            float birthMod = foodSecurity * housingFactor;
            s.BirthProgress += CountFertile(s, c) * c.DailyBirthRatePerAdult * birthMod;
            while (s.BirthProgress >= 1f) { s.BirthProgress -= 1f; SpawnChild(s); }

            // Migration: prosperity draws settlers, hardship drives them away. Prosperity needs BOTH
            // food headroom and a roof, so migrants don't pour into a colony already at its food
            // capacity (which would tip it into famine); real hardship pushes a net outflow.
            float prosperity = foodSecurity * housingHeadroom; // migrants need both food headroom and a free roof
            float attract = prosperity - 0.6f * threat01 - 0.6f * sick01 - 0.15f; // baseline: only a good place draws people
            s.MigrationProgress += attract * c.MigrationDailyRate;
            while (s.MigrationProgress >= 1f) { s.MigrationProgress -= 1f; SpawnImmigrant(s, c); }
            while (s.MigrationProgress <= -1f) { s.MigrationProgress += 1f; Emigrate(s); }

            // Once a year everyone ages; the young come of age and the old may pass.
            if (s.Clock.DayOfYear == s.Clock.DaysPerYear - 1) AnnualTick(s, c);
        }

        private static void AnnualTick(Settlement s, SettlementConfig c)
        {
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (!cz.Alive) continue;
                cz.Age += 1;
                if (cz.Age == c.WorkingAge && cz.Profession == Profession.Idle)
                    cz.Profession = PickProfession(s, c); // a child comes of age and takes up work
            }

            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (!cz.Alive || cz.Age < c.OldAgeStart) continue;
                float prob = (cz.Age - c.OldAgeStart) * c.OldAgeMortalityScale;
                if (Roll(s, cz.Id, 555) < prob)
                {
                    cz.Alive = false;
                    s.TotalDeaths++;
                    s.NaturalDeaths++;
                }
            }
        }

        private static void SpawnChild(Settlement s)
        {
            s.Citizens.Add(new Citizen(s.NextCitizenId++, "Child", 0, Profession.Idle));
            s.TotalBirths++;
        }

        private static void SpawnImmigrant(Settlement s, SettlementConfig c)
        {
            int span = c.ImmigrantMaxAge - c.ImmigrantMinAge;
            int age = c.ImmigrantMinAge + (span > 0 ? (int)(Roll(s, s.NextCitizenId, 333) * span) : 0);
            s.Citizens.Add(new Citizen(s.NextCitizenId++, "Migrant", age, PickProfession(s, c)));
            s.TotalImmigrants++;
        }

        // Only the unrooted drift away — an idle working-age adult. The colony never sheds the
        // workers it depends on, so a crowded settlement stops growing rather than spiralling as
        // emigration guts its own food production.
        private static void Emigrate(Settlement s)
        {
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (cz.Alive && cz.Profession == Profession.Idle && cz.Age >= s.Config.WorkingAge)
                {
                    cz.Alive = false;
                    s.TotalLeft++;
                    return;
                }
            }
            // Nobody idle to lose: the colony holds together.
        }

        // New adults fill whichever profession is furthest below its configured share, keeping the
        // workforce mix balanced as the colony grows.
        private static Profession PickProfession(Settlement s, SettlementConfig c)
        {
            int workers = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Age >= c.WorkingAge) workers++;

            Profession best = Profession.Forager;
            float bestDeficit = float.NegativeInfinity;
            for (int p = (int)Profession.Forager; p <= (int)Profession.Farmer; p++)
            {
                var prof = (Profession)p;
                float share = ShareOf(c, prof);
                if (share <= 0f) continue;
                float deficit = share * workers - CountProfession(s, prof);
                if (deficit > bestDeficit) { bestDeficit = deficit; best = prof; }
            }
            return best;
        }

        private static float ShareOf(SettlementConfig c, Profession p)
        {
            switch (p)
            {
                case Profession.Forager: return c.ForagerShare;
                case Profession.Woodcutter: return c.WoodcutterShare;
                case Profession.Logger: return c.LoggerShare;
                case Profession.Quarryman: return c.QuarrymanShare;
                case Profession.Builder: return c.BuilderShare;
                case Profession.Miner: return c.MinerShare;
                case Profession.Craftsman: return c.CraftsmanShare;
                case Profession.Militiaman: return c.MilitiaShare;
                case Profession.Farmer: return c.FarmerShare;
                default: return 0f;
            }
        }

        // How many mouths the colony's food workers can feed, averaged over the year. Foragers are
        // the food source in the demographics scenario; this is a heuristic the growth gate leans on,
        // not an exact production model.
        private static float FoodCapacity(Settlement s, SettlementConfig c)
        {
            int foragers = CountProfession(s, Profession.Forager);
            // The 0.42 folds in both the seasonal swing and the surplus a colony must bank in summer
            // to outlast winter, so "capacity" is a population it can actually carry year-round
            // (it matches Module 2's proven forager-to-mouths ratio).
            float dailyFood = foragers * c.ForagerNutritionPerDay * 0.42f * s.FoodFactor;
            return c.NutritionPerPersonPerDay > 0f ? dailyFood / c.NutritionPerPersonPerDay : 0f;
        }

        private static int CountProfession(Settlement s, Profession p)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == p) n++;
            return n;
        }

        private static int CountFertile(Settlement s, SettlementConfig c)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (cz.Alive && cz.Age >= c.FertileMinAge && cz.Age <= c.FertileMaxAge) n++;
            }
            return n;
        }

        private static float HousingHeadroom(Settlement s, int pop)
        {
            if (s.HousingCapacity <= pop) return 0f;
            float room = (s.HousingCapacity - pop) / (0.25f * pop + 1f);
            return room > 1f ? 1f : room;
        }

        private static float Roll(Settlement s, int citizenId, int salt)
        {
            ulong h = StableHash.Combine(StableHash.Fnv1aOffset, s.Map.Seed);
            h = StableHash.Combine(h, s.Clock.Day);
            h = StableHash.Combine(h, citizenId);
            h = StableHash.Combine(h, salt);
            return (h % 100000UL) / 100000f;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
