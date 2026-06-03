using System.Collections.Generic;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Trade
{
    public enum TradeMode
    {
        Sell, // sell whatever sits above the reserve
        Buy   // buy up to the target stock
    }

    /// <summary>One standing instruction in a <see cref="TradePolicy"/> (GDD §12).</summary>
    public readonly struct TradeOrder
    {
        public readonly ResourceType Type;
        public readonly TradeMode Mode;
        public readonly float Threshold; // Sell: keep this much in reserve; Buy: stock up to this

        public TradeOrder(ResourceType type, TradeMode mode, float threshold)
        {
            Type = type;
            Mode = mode;
            Threshold = threshold;
        }
    }

    /// <summary>
    /// What a colony offers to and wants from passing merchants (GDD §12). Data-driven: sell orders
    /// export the surplus above a reserve, buy orders top stock up to a target. Sells are settled
    /// before buys each visit, so a colony can fund its imports from that day's exports.
    /// </summary>
    public sealed class TradePolicy
    {
        public readonly List<TradeOrder> Orders = new List<TradeOrder>();

        public TradePolicy Sell(ResourceType type, float reserve = 0f)
        {
            Orders.Add(new TradeOrder(type, TradeMode.Sell, reserve < 0f ? 0f : reserve));
            return this;
        }

        public TradePolicy Buy(ResourceType type, float target)
        {
            Orders.Add(new TradeOrder(type, TradeMode.Buy, target < 0f ? 0f : target));
            return this;
        }
    }
}
