using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.Threats
{
    /// <summary>
    /// The AI Director's running state for one settlement (GDD §13): how much pressure has built
    /// up, which escalation stage that puts it in, and the strength of the bandit camp feeding it.
    /// Evolved by <see cref="ThreatSystem"/>; persisted with the settlement.
    /// </summary>
    public sealed class ThreatState
    {
        public float Pressure;       // 0..100
        public ThreatStage Stage;
        public int DaysInStage;
        public float CampStrength;   // a nearby camp regenerates and presses harder

        // Running totals (persisted) and last-day summary (transient, for reporting).
        public int TotalRaids;
        public int TotalThefts;
        public float TotalStolen;
        public int TotalCasualties;

        public float LastStolen;
        public bool LastWasRaid;

        public ulong Hash(ulong h)
        {
            h = StableHash.Combine(h, (int)(Pressure * 100f));
            h = StableHash.Combine(h, (int)Stage);
            h = StableHash.Combine(h, DaysInStage);
            h = StableHash.Combine(h, (int)(CampStrength * 100f));
            h = StableHash.Combine(h, TotalRaids);
            h = StableHash.Combine(h, TotalThefts);
            h = StableHash.Combine(h, (int)(TotalStolen * 10f));
            h = StableHash.Combine(h, TotalCasualties);
            return h;
        }
    }
}
