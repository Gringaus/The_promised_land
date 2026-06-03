using System.Collections.Generic;
using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Production
{
    /// <summary>One input requirement of a recipe.</summary>
    public readonly struct RecipeInput
    {
        public readonly ResourceType Type;
        public readonly float Amount;

        public RecipeInput(ResourceType type, float amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// A production recipe run by a workshop (GDD §12 "Производственные цепочки"). Data-driven:
    /// inputs are consumed from the storehouse and one output is produced, costing a fixed amount
    /// of craftsman labour per batch. Rules in code, balance in data (§20).
    /// </summary>
    public sealed class ProductionRecipe
    {
        public readonly BuildingType Building;
        public readonly string Name;
        public readonly List<RecipeInput> Inputs = new List<RecipeInput>();
        public readonly ResourceType Output;
        public readonly float OutputAmount;
        public readonly float WorkPerBatch;

        public ProductionRecipe(BuildingType building, string name, ResourceType output, float outputAmount, float workPerBatch)
        {
            Building = building;
            Name = name;
            Output = output;
            OutputAmount = outputAmount;
            WorkPerBatch = workPerBatch;
        }

        public ProductionRecipe In(ResourceType type, float amount)
        {
            Inputs.Add(new RecipeInput(type, amount));
            return this;
        }
    }
}
