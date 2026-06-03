using System.Collections.Generic;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Construction
{
    /// <summary>
    /// Lookup of <see cref="BuildingDef"/> by type. <see cref="CreateDefault"/> holds the
    /// starting balance for Module 3; later these come from authored ScriptableObjects (§20).
    /// </summary>
    public sealed class BuildingCatalog
    {
        private readonly Dictionary<BuildingType, BuildingDef> _defs = new Dictionary<BuildingType, BuildingDef>();

        public void Add(BuildingDef def) { _defs[def.Type] = def; }

        public BuildingDef Get(BuildingType type)
        {
            if (_defs.TryGetValue(type, out BuildingDef def)) return def;
            throw new KeyNotFoundException("No BuildingDef for " + type);
        }

        public bool Has(BuildingType type) { return _defs.ContainsKey(type); }

        public static BuildingCatalog CreateDefault()
        {
            var c = new BuildingCatalog();

            c.Add(new BuildingDef(BuildingType.Tent, "Tent")
                { WorkRequired = 18f, Housing = 3, ShelterQuality = 0.30f }
                .Needs(ResourceType.Wood, 6f));

            c.Add(new BuildingDef(BuildingType.House, "House")
                { WorkRequired = 80f, Housing = 5, ShelterQuality = 0.65f }
                .Needs(ResourceType.Wood, 24f).Needs(ResourceType.Stone, 8f));

            c.Add(new BuildingDef(BuildingType.Storehouse, "Storehouse")
                { WorkRequired = 110f, StorageBonus = 2500f }
                .Needs(ResourceType.Wood, 30f).Needs(ResourceType.Stone, 12f));

            c.Add(new BuildingDef(BuildingType.ForagerHut, "Forager hut")
                { WorkRequired = 50f, ForagerBonus = 0.25f }
                .Needs(ResourceType.Wood, 16f));

            c.Add(new BuildingDef(BuildingType.WoodcutterCamp, "Woodcutter camp")
                { WorkRequired = 50f, WoodcutterBonus = 0.25f }
                .Needs(ResourceType.Wood, 16f));

            // Workshops and market (Module 6, GDD §12). Their value is the recipes they run
            // (see RecipeCatalog) and the market's service bonus, not passive stats.
            c.Add(new BuildingDef(BuildingType.Sawmill, "Sawmill")
                { WorkRequired = 60f }
                .Needs(ResourceType.Wood, 18f).Needs(ResourceType.Stone, 4f));

            c.Add(new BuildingDef(BuildingType.Smelter, "Smelter")
                { WorkRequired = 90f }
                .Needs(ResourceType.Wood, 14f).Needs(ResourceType.Stone, 20f));

            c.Add(new BuildingDef(BuildingType.Smithy, "Smithy")
                { WorkRequired = 80f }
                .Needs(ResourceType.Wood, 16f).Needs(ResourceType.Stone, 12f));

            c.Add(new BuildingDef(BuildingType.Market, "Market")
                { WorkRequired = 70f }
                .Needs(ResourceType.Wood, 22f));

            // Defences (Module 7, GDD §13). A watchtower watches and shoots; a palisade slows.
            c.Add(new BuildingDef(BuildingType.Watchtower, "Watchtower")
                { WorkRequired = 60f, DefenseBonus = 6f }
                .Needs(ResourceType.Wood, 20f).Needs(ResourceType.Stone, 8f));

            c.Add(new BuildingDef(BuildingType.Palisade, "Palisade")
                { WorkRequired = 40f, DefenseBonus = 3f }
                .Needs(ResourceType.Wood, 24f));

            // Farming (Module 8, GDD §9). A field is cheap to clear; the mill and bakery are
            // workshops driven by RecipeCatalog, turning grain into flour and then bread.
            c.Add(new BuildingDef(BuildingType.Field, "Field")
                { WorkRequired = 30f }
                .Needs(ResourceType.Wood, 6f));

            c.Add(new BuildingDef(BuildingType.Mill, "Mill")
                { WorkRequired = 70f }
                .Needs(ResourceType.Wood, 22f).Needs(ResourceType.Stone, 10f));

            c.Add(new BuildingDef(BuildingType.Bakery, "Bakery")
                { WorkRequired = 60f }
                .Needs(ResourceType.Wood, 16f).Needs(ResourceType.Stone, 14f));

            return c;
        }
    }
}
