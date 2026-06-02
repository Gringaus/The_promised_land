using System;
using System.Collections.Generic;

namespace FoundersLands.Simulation.Economy
{
    /// <summary>
    /// Lookup table of <see cref="ResourceDef"/> by type. <see cref="CreateDefault"/>
    /// holds the starting balance values for Module 2's survival loop; later these come
    /// from authored ScriptableObject data (GDD §20).
    /// </summary>
    public sealed class ResourceCatalog
    {
        private readonly Dictionary<ResourceType, ResourceDef> _defs = new Dictionary<ResourceType, ResourceDef>();

        public void Add(ResourceDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            _defs[def.Type] = def;
        }

        public ResourceDef Get(ResourceType type)
        {
            if (_defs.TryGetValue(type, out ResourceDef def)) return def;
            throw new KeyNotFoundException("No ResourceDef for " + type);
        }

        public bool Has(ResourceType type) { return _defs.ContainsKey(type); }

        public static ResourceCatalog CreateDefault()
        {
            var c = new ResourceCatalog();

            c.Add(new ResourceDef(ResourceType.Wood, "Wood") { BuildingMaterial = true });
            c.Add(new ResourceDef(ResourceType.Firewood, "Firewood") { HeatValue = 1.0f });
            c.Add(new ResourceDef(ResourceType.Stone, "Stone") { BuildingMaterial = true });
            c.Add(new ResourceDef(ResourceType.Clay, "Clay") { BuildingMaterial = true });
            c.Add(new ResourceDef(ResourceType.IronOre, "Iron ore") { BuildingMaterial = true });
            c.Add(new ResourceDef(ResourceType.Herbs, "Herbs"));

            // Food: nutrition per unit, with mild spoilage. Spoilage is gentle enough that
            // a winter stockpile is viable; food preservation/drying (GDD §8) is a later
            // refinement that will let some food keep far longer.
            c.Add(new ResourceDef(ResourceType.Berries, "Berries") { Nutrition = 0.6f, Perishable = true, DailySpoilFraction = 0.020f });
            c.Add(new ResourceDef(ResourceType.Fish, "Fish") { Nutrition = 1.0f, Perishable = true, DailySpoilFraction = 0.030f });
            c.Add(new ResourceDef(ResourceType.Meat, "Meat") { Nutrition = 1.5f, Perishable = true, DailySpoilFraction = 0.015f });

            return c;
        }
    }
}
