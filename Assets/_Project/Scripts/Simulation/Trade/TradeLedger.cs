using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.Trade
{
    public enum TradeEvent { None = 0, Traded = 1, Ambushed = 2 }

    /// <summary>
    /// The colony's trade purse and running tally (GDD §12): silver on hand, plus totals for caravan
    /// visits, lost (ambushed) caravans, goods moved and silver turned over. Persisted with the
    /// settlement; the last-event fields are transient hints for the view.
    /// </summary>
    public sealed class TradeLedger
    {
        public float Silver;

        public int CaravanVisits;
        public int Ambushes;
        public float TotalExported;   // units sold
        public float TotalImported;   // units bought
        public float SilverEarned;
        public float SilverSpent;

        public TradeEvent LastEvent;  // transient (for reporting), not hashed
        public int LastEventDay;

        public ulong Hash(ulong h)
        {
            h = StableHash.Combine(h, (int)(Silver * 100f));
            h = StableHash.Combine(h, CaravanVisits);
            h = StableHash.Combine(h, Ambushes);
            h = StableHash.Combine(h, (int)(TotalExported * 10f));
            h = StableHash.Combine(h, (int)(TotalImported * 10f));
            h = StableHash.Combine(h, (int)(SilverEarned * 100f));
            h = StableHash.Combine(h, (int)(SilverSpent * 100f));
            return h;
        }
    }
}
