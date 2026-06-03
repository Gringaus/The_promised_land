using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;

namespace FoundersLands.Simulation.Trade
{
    /// <summary>
    /// Travelling merchants (GDD §12). A colony with a complete market is visited on a fixed cadence;
    /// each visit it sells its surplus and buys toward its targets, settling sells first so exports
    /// fund imports. Prices move with the season (food dearer in the lean months) and with route
    /// safety — and when bandits are abroad an unsafe road may see the caravan ambushed and the visit
    /// lost entirely (ties to Module 7). Deterministic: cadence is by day, the ambush roll is a
    /// stateless hash of (seed, day), so trade reproduces and survives save/load.
    ///
    /// Gated by <see cref="SettlementConfig.EnableTrade"/> (off by default), so colonies in earlier
    /// modules that happen to own a market are unaffected.
    /// </summary>
    public static class TradeSystem
    {
        public static void Step(Settlement s)
        {
            SettlementConfig c = s.Config;
            if (!c.EnableTrade || c.TradeIntervalDays <= 0) return;

            int day = s.Clock.Day;
            if (day <= 0 || day % c.TradeIntervalDays != 0) return; // a caravan only comes on the cadence
            if (!HasCompleteMarket(s)) return;

            TradeLedger ledger = s.TradeLedger;
            float safety = c.EnableThreats ? Clamp(1f - s.Threat.Pressure / 100f, 0.1f, 1f) : 1f;

            // An unsafe road risks the caravan: the merchant is waylaid and the visit is lost.
            if (c.EnableThreats && Roll(s, 991) > safety)
            {
                ledger.Ambushes++;
                ledger.LastEvent = TradeEvent.Ambushed;
                ledger.LastEventDay = day;
                return;
            }

            ledger.CaravanVisits++;
            ledger.LastEvent = TradeEvent.Traded;
            ledger.LastEventDay = day;

            Season season = s.Clock.Season;
            // A caravan can both take goods away and bring goods in — separate cargo legs, so a big
            // export doesn't crowd out imports.
            float sellBudget = c.CaravanCapacity * safety;
            float buyBudget = c.CaravanCapacity * safety;

            // Sells first so the day's exports can fund the day's imports.
            var orders = s.TradePolicy.Orders;
            for (int i = 0; i < orders.Count && sellBudget > 0f; i++)
            {
                if (orders[i].Mode != TradeMode.Sell) continue;
                float surplus = s.Storehouse.Count(orders[i].Type) - orders[i].Threshold;
                float qty = Min(surplus, sellBudget);
                if (qty <= 0f) continue;

                qty = Drain(s.Storehouse, orders[i].Type, qty);
                if (qty <= 0f) continue;

                float gain = qty * SellPrice(s.Catalog, orders[i].Type, season, safety, c.MerchantMargin);
                ledger.Silver += gain;
                ledger.SilverEarned += gain;
                ledger.TotalExported += qty;
                sellBudget -= qty;
            }

            for (int i = 0; i < orders.Count && buyBudget > 0f; i++)
            {
                if (orders[i].Mode != TradeMode.Buy) continue;
                float need = orders[i].Threshold - s.Storehouse.Count(orders[i].Type);
                if (need <= 0f) continue;

                float price = BuyPrice(s.Catalog, orders[i].Type, season, safety, c.MerchantMargin);
                if (price <= 0f) continue;

                float qty = Min(Min(need, buyBudget), ledger.Silver / price);
                if (qty <= 0f) continue;

                s.Storehouse.Add(orders[i].Type, ResourceQuality.Standard, qty);
                float cost = qty * price;
                ledger.Silver -= cost;
                ledger.SilverSpent += cost;
                ledger.TotalImported += qty;
                buyBudget -= qty;
            }
        }

        private static float SellPrice(ResourceCatalog catalog, ResourceType type, Season season, float safety, float margin)
        {
            // Merchants pay below market, and less still when the road home is dangerous.
            return UnitPrice(catalog, type, season) * (1f - margin) * (0.5f + 0.5f * safety);
        }

        private static float BuyPrice(ResourceCatalog catalog, ResourceType type, Season season, float safety, float margin)
        {
            // Goods cost above market, and dearer when the road in is dangerous.
            float safetyFactor = 0.5f + 0.5f * safety;
            return safetyFactor <= 0f ? 0f : UnitPrice(catalog, type, season) * (1f + margin) / safetyFactor;
        }

        private static float UnitPrice(ResourceCatalog catalog, ResourceType type, Season season)
        {
            ResourceDef def = catalog.Get(type);
            float price = def.BasePrice;
            if (def.IsFood)
            {
                // Food is dear in the lean seasons and cheap right after the harvest.
                if (season == Season.Winter || season == Season.Spring) price *= 1.25f;
                else if (season == Season.Autumn) price *= 0.8f;
            }
            return price;
        }

        private static readonly ResourceQuality[] Qualities =
            { ResourceQuality.Fine, ResourceQuality.Standard, ResourceQuality.Poor };

        private static float Drain(Inventory store, ResourceType type, float want)
        {
            float taken = 0f;
            for (int q = 0; q < Qualities.Length && want - taken > 0.0001f; q++)
                taken += store.Remove(type, Qualities[q], want - taken);
            return taken;
        }

        private static bool HasCompleteMarket(Settlement s)
        {
            for (int i = 0; i < s.Buildings.Count; i++)
                if (s.Buildings[i].Complete && s.Buildings[i].Type == BuildingType.Market) return true;
            return false;
        }

        private static float Roll(Settlement s, int salt)
        {
            ulong h = StableHash.Combine(StableHash.Fnv1aOffset, s.Map.Seed);
            h = StableHash.Combine(h, s.Clock.Day);
            h = StableHash.Combine(h, salt);
            return (h % 100000UL) / 100000f;
        }

        private static float Min(float a, float b) => a < b ? a : b;
        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
