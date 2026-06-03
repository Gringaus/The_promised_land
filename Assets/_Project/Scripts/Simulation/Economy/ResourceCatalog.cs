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

            c.Add(new ResourceDef(ResourceType.Wood, "Wood") { BuildingMaterial = true, BasePrice = 1.0f });
            c.Add(new ResourceDef(ResourceType.Firewood, "Firewood") { HeatValue = 1.0f, BasePrice = 0.8f });
            c.Add(new ResourceDef(ResourceType.Stone, "Stone") { BuildingMaterial = true, BasePrice = 1.2f });
            c.Add(new ResourceDef(ResourceType.Clay, "Clay") { BuildingMaterial = true, BasePrice = 1.0f });
            c.Add(new ResourceDef(ResourceType.IronOre, "Iron ore") { BuildingMaterial = true, BasePrice = 2.0f });
            c.Add(new ResourceDef(ResourceType.Herbs, "Herbs") { BasePrice = 2.5f });

            // Manufactured goods (Module 6 production chains, GDD §12) — worth more than raw inputs.
            c.Add(new ResourceDef(ResourceType.Planks, "Planks") { BuildingMaterial = true, BasePrice = 3.0f });
            c.Add(new ResourceDef(ResourceType.IronIngot, "Iron ingot") { BasePrice = 5.0f });
            c.Add(new ResourceDef(ResourceType.Tools, "Tools") { BasePrice = 9.0f });

            // Food: nutrition per unit, with mild spoilage. Spoilage is gentle enough that
            // a winter stockpile is viable; food preservation/drying (GDD §8) is a later
            // refinement that will let some food keep far longer.
            c.Add(new ResourceDef(ResourceType.Berries, "Berries") { Nutrition = 0.6f, Perishable = true, DailySpoilFraction = 0.020f, BasePrice = 0.6f });
            c.Add(new ResourceDef(ResourceType.Fish, "Fish") { Nutrition = 1.0f, Perishable = true, DailySpoilFraction = 0.030f, BasePrice = 1.0f });
            c.Add(new ResourceDef(ResourceType.Meat, "Meat") { Nutrition = 1.5f, Perishable = true, DailySpoilFraction = 0.015f, BasePrice = 1.6f });

            // Farmed food (Module 8, GDD §9). Grain keeps through winter; milling and baking
            // turn it into bread, far more nourishing per unit than raw grain.
            c.Add(new ResourceDef(ResourceType.Grain, "Grain") { Nutrition = 0.5f, Perishable = true, DailySpoilFraction = 0.003f, BasePrice = 0.8f });
            c.Add(new ResourceDef(ResourceType.Flour, "Flour") { BasePrice = 1.6f }); // intermediate, not eaten directly
            c.Add(new ResourceDef(ResourceType.Bread, "Bread") { Nutrition = 1.8f, Perishable = true, DailySpoilFraction = 0.010f, BasePrice = 2.6f });

            return c;
        }
    }
}
