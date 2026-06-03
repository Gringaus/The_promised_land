using System.Collections.Generic;
using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.Economy
{
    /// <summary>
    /// A storehouse's contents (GDD §12 "Склады"). Not a magic shared inventory: it has a
    /// finite capacity and tracks goods per (type, quality). Perishable food decays over
    /// time. All iteration is in a deterministic order so the simulation is reproducible.
    /// </summary>
    public sealed class Inventory
    {
        // Key packs type and quality: (type << 2) | quality. Quality is 0..2.
        private readonly Dictionary<int, float> _amounts = new Dictionary<int, float>();

        public float Capacity;

        public Inventory(float capacity)
        {
            Capacity = capacity;
        }

        private static int Key(ResourceType type, ResourceQuality quality)
        {
            return ((int)type << 2) | (int)quality;
        }

        public float TotalUnits
        {
            get
            {
                // Sum in fixed (type, quality) order, never dictionary order: float addition is not
                // associative, so a storehouse rebuilt from a save must add its stacks in the same
                // order as one grown in play, or the totals (and capacity clamping) would drift.
                float sum = 0f;
                for (int t = 0; t <= (int)ResourceType.Bread; t++)
                    for (int q = 0; q <= (int)ResourceQuality.Fine; q++)
                        if (_amounts.TryGetValue(Key((ResourceType)t, (ResourceQuality)q), out float v)) sum += v;
                return sum;
            }
        }

        public float FreeSpace { get { float f = Capacity - TotalUnits; return f < 0f ? 0f : f; } }

        /// <summary>Adds goods, clamped to remaining capacity. Returns the amount accepted.</summary>
        public float Add(ResourceType type, ResourceQuality quality, float amount)
        {
            if (amount <= 0f) return 0f;
            float accepted = amount;
            float free = FreeSpace;
            if (accepted > free) accepted = free;
            if (accepted <= 0f) return 0f;

            int k = Key(type, quality);
            _amounts.TryGetValue(k, out float current);
            _amounts[k] = current + accepted;
            return accepted;
        }

        /// <summary>Removes up to <paramref name="amount"/> of a specific stack. Returns the amount removed.</summary>
        public float Remove(ResourceType type, ResourceQuality quality, float amount)
        {
            if (amount <= 0f) return 0f;
            int k = Key(type, quality);
            if (!_amounts.TryGetValue(k, out float current)) return 0f;
            float removed = amount < current ? amount : current;
            float left = current - removed;
            if (left <= 1e-5f) _amounts.Remove(k);
            else _amounts[k] = left;
            return removed;
        }

        public float Count(ResourceType type)
        {
            float sum = 0f;
            for (int q = 0; q <= (int)ResourceQuality.Fine; q++)
            {
                if (_amounts.TryGetValue(Key(type, (ResourceQuality)q), out float v)) sum += v;
            }
            return sum;
        }

        /// <summary>All stacks in a deterministic order (by type, then quality).</summary>
        public IReadOnlyList<ItemStack> Stacks()
        {
            var list = new List<ItemStack>();
            // Iterate types/qualities in fixed numeric order, not dictionary order.
            // NOTE: keep the upper bound at the last ResourceType value.
            for (int t = 0; t <= (int)ResourceType.Bread; t++)
            {
                for (int q = 0; q <= (int)ResourceQuality.Fine; q++)
                {
                    var type = (ResourceType)t;
                    var qual = (ResourceQuality)q;
                    if (_amounts.TryGetValue(Key(type, qual), out float v) && v > 0f)
                    {
                        list.Add(new ItemStack(type, qual, v));
                    }
                }
            }
            return list;
        }

        /// <summary>
        /// Consume up to <paramref name="wantedNutrition"/> of food value, eating the most
        /// perishable stocks first so they are not wasted. Returns nutrition satisfied.
        /// </summary>
        public float ConsumeNutrition(ResourceCatalog catalog, float wantedNutrition)
        {
            return ConsumeBy(catalog, wantedNutrition, fuel: false);
        }

        /// <summary>Consume up to <paramref name="wantedHeat"/> of fuel value. Returns heat satisfied.</summary>
        public float ConsumeHeat(ResourceCatalog catalog, float wantedHeat)
        {
            return ConsumeBy(catalog, wantedHeat, fuel: true);
        }

        private float ConsumeBy(ResourceCatalog catalog, float wanted, bool fuel)
        {
            if (wanted <= 0f) return 0f;
            float remaining = wanted;

            foreach (ItemStack stack in OrderedForConsumption(catalog, fuel))
            {
                ResourceDef def = catalog.Get(stack.Type);
                float perUnit = (fuel ? def.HeatValue : def.Nutrition) * stack.Quality.Multiplier();
                if (perUnit <= 0f) continue;

                float unitsNeeded = remaining / perUnit;
                float take = unitsNeeded < stack.Amount ? unitsNeeded : stack.Amount;
                if (take <= 0f) continue;

                Remove(stack.Type, stack.Quality, take);
                remaining -= take * perUnit;
                if (remaining <= 1e-4f) break;
            }

            return wanted - (remaining < 0f ? 0f : remaining);
        }

        // Eat perishable goods first (highest spoil fraction), then by value.
        private IReadOnlyList<ItemStack> OrderedForConsumption(ResourceCatalog catalog, bool fuel)
        {
            var list = new List<ItemStack>();
            foreach (ItemStack s in Stacks())
            {
                ResourceDef def = catalog.Get(s.Type);
                bool usable = fuel ? def.IsFuel : def.IsFood;
                if (usable) list.Add(s);
            }
            list.Sort((a, b) =>
            {
                ResourceDef da = catalog.Get(a.Type);
                ResourceDef db = catalog.Get(b.Type);
                int bySpoil = db.DailySpoilFraction.CompareTo(da.DailySpoilFraction);
                if (bySpoil != 0) return bySpoil;
                int byType = ((int)a.Type).CompareTo((int)b.Type);
                if (byType != 0) return byType;
                return ((int)a.Quality).CompareTo((int)b.Quality);
            });
            return list;
        }

        /// <summary>
        /// Apply one day of spoilage to perishable goods, slowed by <paramref name="spoilageReduction"/>
        /// (0 = none, 1 = food never rots — see GDD §8 cellars/smokehouses). Returns units lost.
        /// </summary>
        public float ApplySpoilage(ResourceCatalog catalog, float spoilageReduction = 0f)
        {
            float mult = 1f - spoilageReduction;
            if (mult < 0f) mult = 0f; else if (mult > 1f) mult = 1f;

            float spoiled = 0f;
            foreach (ItemStack s in Stacks())
            {
                ResourceDef def = catalog.Get(s.Type);
                if (!def.Perishable || def.DailySpoilFraction <= 0f) continue;
                float lost = s.Amount * def.DailySpoilFraction * mult;
                if (lost > 0f) { Remove(s.Type, s.Quality, lost); spoiled += lost; }
            }
            return spoiled;
        }

        /// <summary>Fold inventory contents into a stable hash for determinism tests.</summary>
        public ulong Hash(ulong h)
        {
            foreach (ItemStack s in Stacks())
            {
                h = StableHash.Combine(h, (int)s.Type);
                h = StableHash.Combine(h, (int)s.Quality);
                h = StableHash.Combine(h, (int)(s.Amount * 1000f));
            }
            return h;
        }
    }
}
