using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Production
{
    /// <summary>
    /// Runs workshop recipes for one day (GDD §12). Craftsman labour is shared across all active
    /// workshops; each turns inputs from the storehouse into outputs, capped by both the labour
    /// available and the inputs in stock — so a workshop idles when its supply runs dry, just as
    /// a build site stalls without materials.
    /// </summary>
    public static class ProductionSystem
    {
        /// <summary>Returns the total number of batches produced across all workshops.</summary>
        public static int Step(IReadOnlyList<Building> buildings, Inventory storehouse, RecipeCatalog recipes, float totalLabor)
        {
            if (totalLabor <= 0f) return 0;

            var workshops = new List<Building>();
            for (int i = 0; i < buildings.Count; i++)
            {
                Building b = buildings[i];
                if (b.Complete && recipes.Has(b.Type)) workshops.Add(b);
            }
            if (workshops.Count == 0) return 0;

            float per = totalLabor / workshops.Count;
            int produced = 0;

            for (int i = 0; i < workshops.Count; i++)
            {
                ProductionRecipe recipe = recipes.For(workshops[i].Type);
                int byLabor = (int)(per / recipe.WorkPerBatch);
                if (byLabor <= 0) continue;

                int byInput = MaxBatchesByInputs(storehouse, recipe);
                int batches = byLabor < byInput ? byLabor : byInput;
                if (batches <= 0) continue;

                for (int k = 0; k < recipe.Inputs.Count; k++)
                {
                    RecipeInput input = recipe.Inputs[k];
                    storehouse.Remove(input.Type, ResourceQuality.Standard, input.Amount * batches);
                }
                storehouse.Add(recipe.Output, ResourceQuality.Standard, recipe.OutputAmount * batches);
                produced += batches;
            }

            return produced;
        }

        private static int MaxBatchesByInputs(Inventory storehouse, ProductionRecipe recipe)
        {
            int min = int.MaxValue;
            for (int k = 0; k < recipe.Inputs.Count; k++)
            {
                RecipeInput input = recipe.Inputs[k];
                int can = input.Amount <= 0f ? int.MaxValue : (int)(storehouse.Count(input.Type) / input.Amount);
                if (can < min) min = can;
            }
            return min == int.MaxValue ? 0 : min;
        }
    }
}
