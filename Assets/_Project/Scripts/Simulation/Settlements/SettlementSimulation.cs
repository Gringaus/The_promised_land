using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Time;

namespace FoundersLands.Simulation.Settlements
{
    /// <summary>
    /// Advances a <see cref="Settlement"/> by whole days (GDD §9, §11, §28). Each day:
    /// citizens work (gather into the storehouse), perishable food spoils, then everyone
    /// eats and — in cold seasons — burns firewood for warmth. Unmet needs erode health
    /// and eventually kill. Deterministic: identical input state yields identical output.
    /// </summary>
    public static class SettlementSimulation
    {
        public static DayReport Step(Settlement s)
        {
            SeasonDef season = s.Seasons[(int)s.Clock.Season];

            Gather(s, season);
            s.Storehouse.ApplySpoilage(s.Catalog);
            int deaths = ConsumeAndAge(s, season);

            DayReport report = new DayReport(
                s.Clock.Day, s.Clock.Year, s.Clock.Season,
                s.AlivePopulation, deaths,
                s.FoodUnits(), s.Storehouse.Count(ResourceType.Firewood),
                s.StoredNutrition(), s.AverageHealth);

            s.Clock.Advance(1);
            return report;
        }

        /// <summary>Run <paramref name="days"/> days and return the final day's report.</summary>
        public static DayReport Run(Settlement s, int days)
        {
            DayReport last = default;
            for (int i = 0; i < days; i++) last = Step(s);
            return last;
        }

        private static void Gather(Settlement s, SeasonDef season)
        {
            SettlementConfig c = s.Config;
            float foodQualityMult = s.ForageQuality.Multiplier();
            ResourceDef foodDef = s.Catalog.Get(s.PrimaryFood);

            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (!cz.Alive) continue;

                switch (cz.Profession)
                {
                    case Profession.Forager:
                    {
                        float nutrition = c.ForagerNutritionPerDay * season.FoodGatherMult * season.WorkSpeedMult * s.FoodFactor;
                        if (nutrition > 0f && foodDef.Nutrition > 0f)
                        {
                            float units = nutrition / (foodDef.Nutrition * foodQualityMult);
                            s.Storehouse.Add(s.PrimaryFood, s.ForageQuality, units);
                        }
                        break;
                    }
                    case Profession.Woodcutter:
                    {
                        float firewood = c.WoodcutterFirewoodPerDay * season.FirewoodGatherMult * season.WorkSpeedMult * s.FirewoodFactor;
                        if (firewood > 0f) s.Storehouse.Add(ResourceType.Firewood, ResourceQuality.Standard, firewood);
                        break;
                    }
                }
            }
        }

        private static int ConsumeAndAge(Settlement s, SeasonDef season)
        {
            SettlementConfig c = s.Config;
            int alive = s.AlivePopulation;
            if (alive == 0) return 0;

            // Ration the whole settlement at once, then share any shortage equally — this
            // avoids the storehouse favouring whichever citizen happens to be processed
            // first. Differential survival comes from age, not list order.
            float foodDemand = alive * c.NutritionPerPersonPerDay;
            float foodGot = s.Storehouse.ConsumeNutrition(s.Catalog, foodDemand);
            float foodDeficit = foodDemand > 0f ? M.Clamp01(1f - foodGot / foodDemand) : 0f;

            float heatNeedPer = c.FirewoodPerPersonPerDayWinter * season.FirewoodNeedMult;
            float heatDemand = alive * heatNeedPer;
            float heatGot = heatDemand > 0f ? s.Storehouse.ConsumeHeat(s.Catalog, heatDemand) : 0f;
            float heatDeficit = heatDemand > 0f ? M.Clamp01(1f - heatGot / heatDemand) : 0f;

            float t = c.NeedHealthThreshold;
            int deaths = 0;

            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (!cz.Alive) continue;

                cz.Hunger = foodDeficit <= 1e-4f ? Dec(cz.Hunger, 0.5f) : M.Clamp01(cz.Hunger + foodDeficit);
                cz.Cold = (heatNeedPer <= 0f || heatDeficit <= 1e-4f) ? Dec(cz.Cold, 0.5f) : M.Clamp01(cz.Cold + heatDeficit);

                float hungerOver = cz.Hunger > t ? (cz.Hunger - t) / (1f - t) : 0f;
                float coldOver = cz.Cold > t ? (cz.Cold - t) / (1f - t) : 0f;
                float damage = (hungerOver * c.StarveHealthLossPerDay + coldOver * c.ColdHealthLossPerDay) * AgeVulnerability(cz.Age);

                if (damage > 0f)
                {
                    cz.Health -= damage;
                    if (cz.Health <= 0f)
                    {
                        cz.Health = 0f;
                        cz.Alive = false;
                        deaths++;
                        s.TotalDeaths++;
                    }
                }
                else
                {
                    cz.Health += c.HealthRecoverPerDay;
                    if (cz.Health > 100f) cz.Health = 100f;
                }
            }

            return deaths;
        }

        // The very young and the elderly suffer privation harder (GDD §11 age matters),
        // so under a shortage they decline first instead of everyone dying at once.
        private static float AgeVulnerability(int age)
        {
            if (age >= 50) { float v = 1f + (age - 50) * 0.03f; return v > 1.6f ? 1.6f : v; }
            if (age < 18) return 1.2f;
            return 1.0f;
        }

        private static float Dec(float v, float by)
        {
            v -= by;
            return v < 0f ? 0f : v;
        }
    }
}
