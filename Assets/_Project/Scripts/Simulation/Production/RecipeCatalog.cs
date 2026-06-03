using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Production
{
    /// <summary>
    /// Recipes keyed by the workshop that runs them (GDD §12, §20). <see cref="CreateDefault"/>
    /// holds Module 6's starting chains: timber -> planks, ore -> ingot -> tools.
    /// </summary>
    public sealed class RecipeCatalog
    {
        private readonly Dictionary<BuildingType, ProductionRecipe> _byBuilding = new Dictionary<BuildingType, ProductionRecipe>();

        public void Add(ProductionRecipe recipe) { _byBuilding[recipe.Building] = recipe; }

        public bool Has(BuildingType building) { return _byBuilding.ContainsKey(building); }

        public ProductionRecipe For(BuildingType building)
        {
            _byBuilding.TryGetValue(building, out ProductionRecipe r);
            return r;
        }

        public static RecipeCatalog CreateDefault()
        {
            var c = new RecipeCatalog();
            c.Add(new ProductionRecipe(BuildingType.Sawmill, "Saw planks", ResourceType.Planks, 3f, 4f)
                .In(ResourceType.Wood, 2f));
            c.Add(new ProductionRecipe(BuildingType.Smelter, "Smelt iron", ResourceType.IronIngot, 1f, 6f)
                .In(ResourceType.IronOre, 2f));
            c.Add(new ProductionRecipe(BuildingType.Smithy, "Forge tools", ResourceType.Tools, 2f, 6f)
                .In(ResourceType.IronIngot, 1f));
            return c;
        }
    }
}
