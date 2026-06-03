using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Population;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;

namespace FoundersLands.Simulation.Threats
{
    /// <summary>
    /// The AI Director (GDD §13). Each day it raises threat pressure from the colony's wealth and
    /// the strength of a nearby bandit camp, lowers it with the colony's defence, and resolves the
    /// stage that pressure lands on — from harmless rumours up to a looting raid. Defence (militia,
    /// watchtowers, palisade) both holds pressure down and softens any blow that lands, so a
    /// well-guarded town is harassed where a fat, open one is sacked.
    ///
    /// All randomness is derived statelessly from (map seed, day, salt), so threat events are
    /// deterministic and survive a save/load without persisting any RNG cursor. The whole system
    /// is gated by <see cref="SettlementConfig.EnableThreats"/> (off by default), leaving the
    /// earlier modules' balance untouched.
    /// </summary>
    public static class ThreatSystem
    {
        private static readonly ResourceQuality[] Qualities =
            { ResourceQuality.Fine, ResourceQuality.Standard, ResourceQuality.Poor };

        public static void Step(Settlement s, SeasonDef season)
        {
            SettlementConfig c = s.Config;
            if (!c.EnableThreats) return;

            ThreatState th = s.Threat;
            th.LastStolen = 0f;
            th.LastWasRaid = false;

            float wealth = s.Storehouse.TotalUnits;
            float defense = CountMilitia(s) * c.MilitiaDefense + s.BuildingDefense;

            // The camp regenerates toward its cap, then feeds the pressure it drives.
            if (th.CampStrength < c.CampMaxStrength)
            {
                th.CampStrength += c.CampRegenPerDay;
                if (th.CampStrength > c.CampMaxStrength) th.CampStrength = c.CampMaxStrength;
            }

            float campFactor = c.CampMaxStrength > 0f ? th.CampStrength / c.CampMaxStrength : 0f;
            float gain = (c.ThreatBaseGrowth + c.ThreatWealthGrowth * (wealth / c.ThreatWealthScale)) * campFactor;
            float suppress = c.ThreatDefenseDecay * defense;
            th.Pressure = Clamp(th.Pressure + gain - suppress, 0f, 100f);

            ThreatStage stage = StageFor(th.Pressure);
            if (stage != th.Stage) { th.Stage = stage; th.DaysInStage = 0; }
            else th.DaysInStage++;

            float mit = defense / (defense + th.CampStrength + 0.001f);
            if (mit > 1f) mit = 1f;
            float severity = 1f - mit; // 0 = fully held off, 1 = wide open

            switch (stage)
            {
                case ThreatStage.Thefts:
                    if (Roll(s, 101) < c.TheftChance) Steal(s, c.TheftUnits * severity, raid: false);
                    break;
                case ThreatStage.Ambush:
                    if (Roll(s, 102) < c.AmbushChance)
                    {
                        Steal(s, c.AmbushUnits * severity, raid: false);
                        HurtCitizens(s, c.AmbushInjury * severity);
                    }
                    break;
                case ThreatStage.Raid:
                    if ((th.DaysInStage % c.RaidIntervalDays) == 0) Raid(s, severity);
                    break;
            }
        }

        private static void Raid(Settlement s, float severity)
        {
            SettlementConfig c = s.Config;
            ThreatState th = s.Threat;
            th.LastWasRaid = true;
            th.TotalRaids++;

            Steal(s, s.Storehouse.TotalUnits * c.RaidFraction * severity, raid: true);
            HurtCitizens(s, c.RaidInjury * severity);
            if (severity > 0.6f) WreckBuilding(s); // an unguarded raid wrecks infrastructure

            th.Pressure = Clamp(th.Pressure - c.RaidPressureRelief, 0f, 100f); // they leave with the loot
            th.CampStrength -= c.RaidCampCost;
            if (th.CampStrength < 0f) th.CampStrength = 0f;
        }

        private static void Steal(Settlement s, float target, bool raid)
        {
            float stolen = StealValue(s, target);
            if (stolen <= 0f) return;
            ThreatState th = s.Threat;
            th.LastStolen += stolen;
            th.TotalStolen += stolen;
            if (!raid) th.TotalThefts++;
        }

        // Bandits grab the most portable valuables first, draining each quality tier in turn.
        // Essentials (food, fuel) are only skimmed — raiders haul loot, not a whole supply train —
        // so a single hit impoverishes a town rather than starving it out outright.
        private static float StealValue(Settlement s, float target)
        {
            if (target <= 0f) return 0f;
            float left = target;
            left -= Grab(s, ResourceType.Tools, left);
            left -= Grab(s, ResourceType.IronIngot, left);
            left -= Grab(s, ResourceType.Planks, left);

            float cap = s.Config.RaidEssentialCapFraction;
            left -= Grab(s, s.PrimaryFood, Min(left, cap * s.Storehouse.Count(s.PrimaryFood)));
            left -= Grab(s, ResourceType.Firewood, Min(left, cap * s.Storehouse.Count(ResourceType.Firewood)));
            return target - left;
        }

        private static float Min(float a, float b) => a < b ? a : b;

        private static float Grab(Settlement s, ResourceType type, float want)
        {
            if (want <= 0f) return 0f;
            float taken = 0f;
            for (int q = 0; q < Qualities.Length && want - taken > 0.0001f; q++)
                taken += s.Storehouse.Remove(type, Qualities[q], want - taken);
            return taken;
        }

        private static void HurtCitizens(Settlement s, float damage)
        {
            if (damage <= 0f) return;
            for (int i = 0; i < s.Citizens.Count; i++)
            {
                Citizen cz = s.Citizens[i];
                if (!cz.Alive) continue;
                cz.Health -= damage;
                if (cz.Health <= 0f)
                {
                    cz.Health = 0f;
                    cz.Alive = false;
                    s.TotalDeaths++;
                    s.Threat.TotalCasualties++;
                }
            }
        }

        private static void WreckBuilding(Settlement s)
        {
            if (s.Buildings.Count == 0) return;
            int idx = (int)(Roll(s, 777) * s.Buildings.Count);
            if (idx >= s.Buildings.Count) idx = s.Buildings.Count - 1;
            Building b = s.Buildings[idx];
            b.WorkDone *= 0.4f;
            if (b.WorkDone < b.WorkRequired) b.Complete = false; // back to a damaged site; needs rebuilding
        }

        private static int CountMilitia(Settlement s)
        {
            int n = 0;
            for (int i = 0; i < s.Citizens.Count; i++)
                if (s.Citizens[i].Alive && s.Citizens[i].Profession == Profession.Militiaman) n++;
            return n;
        }

        private static ThreatStage StageFor(float p)
        {
            if (p >= 90f) return ThreatStage.Raid;
            if (p >= 70f) return ThreatStage.Ambush;
            if (p >= 50f) return ThreatStage.Thefts;
            if (p >= 30f) return ThreatStage.Scouts;
            if (p >= 12f) return ThreatStage.Rumors;
            return ThreatStage.Calm;
        }

        // Stateless [0,1) draw from the seed, the day and a salt — reproducible across save/load.
        private static float Roll(Settlement s, int salt)
        {
            ulong h = StableHash.Combine(StableHash.Fnv1aOffset, s.Map.Seed);
            h = StableHash.Combine(h, s.Clock.Day);
            h = StableHash.Combine(h, salt);
            return (h % 100000UL) / 100000f;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
