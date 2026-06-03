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

            return c;
        }
    }
}
