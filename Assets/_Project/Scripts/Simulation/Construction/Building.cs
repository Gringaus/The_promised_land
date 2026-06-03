using System.Collections.Generic;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Construction
{
    /// <summary>
    /// A placed building, from blueprint to finished structure (GDD §10). Holds a map
    /// position for future spatial use; in Module 3 the simulation is still aggregate, so
    /// position is recorded but does not yet affect logistics.
    /// </summary>
    public sealed class Building
    {
        public readonly BuildingType Type;
        public readonly int X;
        public readonly int Y;
        public readonly float WorkRequired;

        public float WorkDone;
        public bool Complete;

        private readonly List<MaterialCost> _cost;
        private readonly Dictionary<ResourceType, float> _delivered = new Dictionary<ResourceType, float>();

        public Building(BuildingDef def, int x, int y)
        {
            Type = def.Type;
            X = x;
            Y = y;
            WorkRequired = def.WorkRequired;
            _cost = def.Cost;
        }

        public IReadOnlyList<MaterialCost> Cost { get { return _cost; } }

        public float Delivered(ResourceType type)
        {
            _delivered.TryGetValue(type, out float v);
            return v;
        }

        public void Deliver(ResourceType type, float amount)
        {
            _delivered.TryGetValue(type, out float v);
            _delivered[type] = v + amount;
        }

        public float Remaining(ResourceType type, float required)
        {
            float r = required - Delivered(type);
            return r < 0f ? 0f : r;
        }

        /// <summary>True once every required material has been delivered in full.</summary>
        public bool FullyDelivered
        {
            get
            {
                for (int i = 0; i < _cost.Count; i++)
                {
                    if (Delivered(_cost[i].Type) + 1e-4f < _cost[i].Amount) return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Work cannot outrun materials: the fraction of work that may be done is bounded by
        /// the least-delivered required material (the bottleneck). No materials, no progress.
        /// </summary>
        public float DeliveredFraction
        {
            get
            {
                if (_cost.Count == 0) return 1f;
                float min = 1f;
                for (int i = 0; i < _cost.Count; i++)
                {
                    float req = _cost[i].Amount;
                    float frac = req <= 0f ? 1f : Delivered(_cost[i].Type) / req;
                    if (frac < min) min = frac;
                }
                return min;
            }
        }

        public float WorkFraction { get { return WorkRequired <= 0f ? 1f : WorkDone / WorkRequired; } }

        public ConstructionStage Stage { get { return ConstructionStages.FromWorkFraction(WorkFraction, FullyDelivered); } }

        public ulong Hash(ulong h)
        {
            h = StableHash.Combine(h, (int)Type);
            h = StableHash.Combine(h, X);
            h = StableHash.Combine(h, Y);
            h = StableHash.Combine(h, (int)(WorkDone * 100f));
            h = StableHash.Combine(h, Complete ? 1 : 0);
            for (int i = 0; i < _cost.Count; i++)
            {
                h = StableHash.Combine(h, (int)(Delivered(_cost[i].Type) * 100f));
            }
            return h;
        }
    }
}
