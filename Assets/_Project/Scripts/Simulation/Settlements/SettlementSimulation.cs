using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Farming;
using FoundersLands.Simulation.Mathematics;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Production;
using FoundersLands.Simulation.Threats;
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

            s.RecomputeBuildingEffects();
            float efficiency = ComputeWorkEfficiency(s);

            Gather(s, season, efficiency);
            Farm(s, season, efficiency);
            Produce(s, season, efficiency);
            Construct(s, season, efficiency);
            WearTools(s);
            ThreatSystem.Step(s, season); // bandits may steal/raid before the day's spoilage and meals
            s.Storehouse.ApplySpoilage(s.Catalog);
            int deaths = ConsumeAndAge(s, season);
            PopulationSystem.Step(s); // births, migration, aging — uses today's hunger and health

            DayReport report = new DayReport(
                s.Clock.Day, s.Clock.Year, s.Clock.Season,
                s.AlivePopulation, deaths,
                s.FoodUnits(), s.Storehouse.Count(ResourceType.Firewood),
                s.StoredNutrition(), s.AverageHealth,
                s.BuildingsComplete, s.BuildingsUnderConstruction);

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

        private static void Gather(Settlement s, SeasonDef season, float efficiency)
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
                        float nutrition = c.ForagerNutritionPerDay * (1f + s.ForagerBonus) * season.FoodGatherMult * season.WorkSpeedMult * s.FoodFactor * efficiency;
                        if (nutrition > 0f && foodDef.Nutrition > 0f)
                        {
                            float units = nutrition / (foodDef.Nutrition * foodQualityMult);
                            s.Storehouse.Add(s.PrimaryFood, s.ForageQuality, units);
                        }
                        break;
                    }
                    case Profession.Woodcutter:
                    {
                        float firewood = c.WoodcutterFirewoodPerDay * (1f + s.WoodcutterBonus) * season.FirewoodGatherMult * season.WorkSpeedMult * s.FirewoodFactor * efficiency;
                        if (firewood > 0f) s.Storehouse.Add(ResourceType.Firewood, ResourceQuality.Standard, firewood);
                        break;
                    }
                    case Profession.Logger:
                    {
                        // Timber comes from forest, like firewood, so it shares the forest factor.
                        float wood = c.LoggerWoodPerDay * season.FirewoodGatherMult * season.WorkSpeedMult * s.FirewoodFactor * efficiency;
                        if (wood > 0f) s.Storehouse.Add(ResourceType.Wood, ResourceQuality.Standard, wood);
                        break;
                    }
                    case Profession.Quarryman:
                    {
                        float stone = c.QuarrymanStonePerDay * season.WorkSpeedMult * s.StoneFactor * efficiency;
                        if (stone > 0f) s.Storehouse.Add(ResourceType.Stone, ResourceQuality.Standard, stone);
                        break;
                    }
                    case Profession.Miner:
                    {
                        float ironOre = c.MinerIronPerDay * season.WorkSpeedMult * s.IronFactor * efficiency;
                        if (ironOre > 0f) s.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, ironOre);
                        break;
                    }
                }
            }
        }

        private static void Produce(Settlement s, SeasonDef season, float efficiency)
        {
            int craftsmen = CountProfession(s, Profession.Craftsman);
            if (craftsmen == 0) return;
            float labor = craftsmen * s.Config.CraftsmanWorkPerDay * season.WorkSpeedMult * efficiency;
            ProductionSystem.Step(s.Buildings, s.Storehouse, s.Recipes, labor);
        }

        private static void Farm(Settlement s, SeasonDef season, float efficiency)
        {
            float labor = CountProfession(s, Profession.Farmer) * s.Config.FarmerWorkPerDay * season.WorkSpeedMult * efficiency;
            FarmingSystem.Step(s.Buildings, s.Storehouse, s.Clock.Season, s.SoilFertility, labor, s.Config);
        }

        private static void Construct(Settlement s, SeasonDef season, float efficiency)
        {
            if (s.Buildings.Count == 0) return;

            int builders = CountProfession(s, Profession.Builder);

            // Construction slows in winter and speeds in summer (GDD §9). No builders, no work.
            float work = builders * s.Config.BuilderWorkPerDay * season.WorkSpeedMult * efficiency;
            if (work <= 0f) return;

            ConstructionSystem.Step(s.Buildings, s.Storehouse, work);
        }

        // Tools and a stocked market raise output (GDD §12). With neither present this is 1,
        // so the earlier modules' balance is untouched.
        private static float ComputeWorkEfficiency(Settlement s)
        {
            SettlementConfig c = s.Config;
            int pop = s.AlivePopulation;

            float toolsBonus = 0f;
            if (pop > 0 && c.ToolsBonusMax > 0f)
            {
                float frac = s.Storehouse.Count(ResourceType.Tools) / pop;
                if (frac > 1f) frac = 1f;
                toolsBonus = frac * c.ToolsBonusMax;
            }

            float marketBonus = (c.MarketProductivityBonus > 0f && HasCompleteMarket(s) && s.FoodUnits() > 0f)
                ? c.MarketProductivityBonus : 0f;

            return 1f + toolsBonus + marketBonus;
        }

        private static void WearTools(Settlement s)
        {
            SettlementConfig c = s.Config;
            if (c.ToolsWearPerWorkerPerDay <= 0f || s.Storehouse.Count(ResourceType.Tools) <= 0f) return;

            int workers = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (cz.Alive && cz.Profession != Profession.Idle) workers++;
            }
            float wear = workers * c.ToolsWearPerWorkerPerDay;
            if (wear > 0f) s.Storehouse.Remove(ResourceType.Tools, ResourceQuality.Standard, wear);
        }

        private static bool HasCompleteMarket(Settlement s)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
            {
                if (s.Buildings[i].Complete && s.Buildings[i].Type == BuildingType.Market) return true;
            }
            return false;
        }

        private static int CountProfession(Settlement s, Profession p)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == p) n++;
            }
            return n;
        }

        private static int ConsumeAndAge(Settlement s, SeasonDef season)
        {
            SettlementConfig c = s.Config;
            int alive = s.AlivePopulation;
            if (alive == 0) return 0;

            // Ration the whole settlement at once, then share any shortage equally — this
            // avoids the storehouse favouring whichever citizen happens to be processed
            // first. Differential survival comes from age, not list order. Children eat less
            // (GDD §11); with population dynamics off there are no children, so this is just `alive`.
            float foodMouths = 0f;
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (cz.Alive) foodMouths += cz.Age < c.WorkingAge ? c.ChildFoodFraction : 1f;
            }
            float foodDemand = foodMouths * c.NutritionPerPersonPerDay;
            float foodGot = s.Storehouse.ConsumeNutrition(s.Catalog, foodDemand);
            float foodDeficit = foodDemand > 0f ? M.Clamp01(1f - foodGot / foodDemand) : 0f;

            float heatNeedPer = c.FirewoodPerPersonPerDayWinter * season.FirewoodNeedMult;
            // Completed housing shelters citizens and cuts the fuel they need (GDD §10).
            if (heatNeedPer > 0f && s.HousingCapacity > 0)
            {
                float sheltered = s.HousingCapacity >= alive ? 1f : (float)s.HousingCapacity / alive;
                heatNeedPer *= 1f - c.WarmthReductionMax * sheltered * s.AvgShelterQuality;
            }
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
