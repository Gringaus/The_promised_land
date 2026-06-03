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

            // Farming chain (Module 8, GDD §9): grain -> flour -> bread. Baking burns firewood.
            c.Add(new ProductionRecipe(BuildingType.Mill, "Mill flour", ResourceType.Flour, 1f, 3f)
                .In(ResourceType.Grain, 1f));
            c.Add(new ProductionRecipe(BuildingType.Bakery, "Bake bread", ResourceType.Bread, 2f, 4f)
                .In(ResourceType.Flour, 1f).In(ResourceType.Firewood, 1f));
            return c;
        }
    }
}
