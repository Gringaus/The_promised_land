using System;
using System.Collections.Generic;
using FoundersLands.Simulation.Construction;

namespace FoundersLands.Simulation.Technology
{
    /// <summary>
    /// The technology tree (GDD §16, §20). <see cref="CreateDefault"/> gates the colony's buildings
    /// behind research so capabilities arrive in a strategic order. A handful of founding buildings
    /// (<see cref="BaseBuildings"/>) need no research, so a fresh colony can always shelter and store.
    /// </summary>
    public sealed class TechCatalog
    {
        private readonly List<Technology> _techs = new List<Technology>();

        /// <summary>Buildings available from the start, before any research.</summary>
        public readonly HashSet<BuildingType> BaseBuildings = new HashSet<BuildingType>();

        public IReadOnlyList<Technology> Techs => _techs;
        public int Count => _techs.Count;
        public Technology Get(int id) => _techs[id];

        public int Add(string name, float cost, int[] prerequisites, BuildingType[] unlocks, float workBonus)
        {
            int id = _techs.Count;
            _techs.Add(new Technology(id, name, cost, prerequisites, unlocks, workBonus));
            return id;
        }

        public static TechCatalog CreateDefault()
        {
            var c = new TechCatalog();
            c.BaseBuildings.Add(BuildingType.Tent);
            c.BaseBuildings.Add(BuildingType.House);
            c.BaseBuildings.Add(BuildingType.Storehouse);

            int forageLore = c.Add("Foraging Lore", 50f, null,
                new[] { BuildingType.ForagerHut }, 0.05f);
            int woodcraft = c.Add("Woodcraft", 60f, null,
                new[] { BuildingType.WoodcutterCamp, BuildingType.Sawmill }, 0.05f);
            int agriculture = c.Add("Agriculture", 90f, null,
                new[] { BuildingType.Field, BuildingType.Mill, BuildingType.Bakery, BuildingType.Cellar, BuildingType.Smokehouse }, 0f);
            int commerce = c.Add("Commerce", 110f, new[] { woodcraft },
                new[] { BuildingType.Market }, 0f);
            int masonry = c.Add("Masonry", 100f, new[] { woodcraft },
                new[] { BuildingType.Smelter }, 0f);
            int fortification = c.Add("Fortification", 100f, new[] { masonry },
                new[] { BuildingType.Watchtower, BuildingType.Palisade }, 0f);
            int smithing = c.Add("Smithing", 130f, new[] { masonry },
                new[] { BuildingType.Smithy }, 0.05f);
            c.Add("Engineering", 220f, new[] { smithing },
                Array.Empty<BuildingType>(), 0.15f);

            _ = forageLore; _ = agriculture; _ = commerce; _ = fortification; // ids referenced via prereqs above
            return c;
        }
    }
}
