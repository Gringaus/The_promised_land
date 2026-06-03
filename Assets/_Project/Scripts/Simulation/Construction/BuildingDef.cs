using System.Collections.Generic;
using FoundersLands.Simulation.Economy;

namespace FoundersLands.Simulation.Construction
{
    /// <summary>A material requirement for a building.</summary>
    public readonly struct MaterialCost
    {
        public readonly ResourceType Type;
        public readonly float Amount;

        public MaterialCost(ResourceType type, float amount)
        {
            Type = type;
            Amount = amount;
        }
    }

    /// <summary>
    /// Data-driven definition of a building (GDD §10, §20): what it costs, how much work it
    /// takes, and what it does for the settlement once complete. Rules live in code, balance
    /// in data; a Unity ScriptableObject will author these later.
    /// </summary>
    public sealed class BuildingDef
    {
        public BuildingType Type;
        public string Name;

        public readonly List<MaterialCost> Cost = new List<MaterialCost>();
        public float WorkRequired = 50f;

        // Effects applied while complete.
        public int Housing;             // how many citizens it shelters
        public float ShelterQuality;    // 0..1 warmth quality of that shelter
        public float StorageBonus;      // added storehouse capacity
        public float ForagerBonus;      // +fraction to forager output
        public float WoodcutterBonus;   // +fraction to woodcutter output
        public float DefenseBonus;      // defence points vs bandits (GDD §13)

        public BuildingDef(BuildingType type, string name)
        {
            Type = type;
            Name = name;
        }

        public BuildingDef Needs(ResourceType type, float amount)
        {
            Cost.Add(new MaterialCost(type, amount));
            return this;
        }
    }
}
