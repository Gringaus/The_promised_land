using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Settlements;

namespace FoundersLands.Simulation.Technology
{
    /// <summary>
    /// Drives research (GDD §16): scholars accrue points each day, and whenever enough has built up
    /// the next available technology (the first in catalogue order whose prerequisites are met) is
    /// unlocked, opening its buildings and adding its work bonus. Fully deterministic — a simple
    /// accumulator over the fixed tree — so progression reproduces and survives save/load.
    ///
    /// Gated by <see cref="SettlementConfig.EnableTechnology"/> (off by default): with it off every
    /// building is available from the start, exactly as in the earlier modules.
    /// </summary>
    public static class ResearchSystem
    {
        public static void Step(Settlement s)
        {
            if (!s.Config.EnableTechnology) return;

            s.ResearchProgress += CountScholars(s) * s.Config.ResearchPerScholarPerDay;

            // A burst of accrued research may complete several cheap techs in one day.
            while (true)
            {
                Technology next = NextTarget(s);
                if (next == null || s.ResearchProgress < next.Cost) break;
                s.ResearchProgress -= next.Cost;
                Grant(s, next);
            }
        }

        /// <summary>
        /// Unlock a technology outright (no research cost) — for founders who arrive already knowing a
        /// craft, or for scenario/test setup. Idempotent: granting an already-known tech does nothing.
        /// </summary>
        public static void Grant(Settlement s, int techId)
        {
            if (techId < 0 || techId >= s.Techs.Count) return;
            Grant(s, s.Techs.Get(techId));
        }

        /// <summary>The next tech to research: first un-unlocked one whose prerequisites are all met.</summary>
        public static Technology NextTarget(Settlement s)
        {
            var techs = s.Techs.Techs;
            for (int i = 0; i < techs.Count; i++)
            {
                Technology t = techs[i];
                if (s.UnlockedTechs.Contains(t.Id)) continue;
                if (PrerequisitesMet(s, t)) return t;
            }
            return null;
        }

        private static bool PrerequisitesMet(Settlement s, Technology t)
        {
            for (int i = 0; i < t.Prerequisites.Length; i++)
                if (!s.UnlockedTechs.Contains(t.Prerequisites[i])) return false;
            return true;
        }

        private static void Grant(Settlement s, Technology t)
        {
            if (!s.UnlockedTechs.Add(t.Id)) return; // already known

            for (int i = 0; i < t.Unlocks.Length; i++) s.UnlockedBuildings.Add(t.Unlocks[i]);
            s.TechWorkBonus += t.WorkBonus;
            s.LastUnlockedTech = t.Name;
        }

        private static int CountScholars(Settlement s)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == Profession.Scholar) n++;
            return n;
        }
    }
}
