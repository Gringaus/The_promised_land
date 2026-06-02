namespace FoundersLands.Simulation.Economy
{
    /// <summary>
    /// Data-driven definition of a resource (GDD §20). Plain C# so the simulation can be
    /// tested headlessly; in Unity a ScriptableObject authoring wrapper produces these.
    /// Rules live in code, balance lives in data.
    /// </summary>
    public sealed class ResourceDef
    {
        public ResourceType Type;
        public string Name;

        /// <summary>Food value per unit (0 if inedible).</summary>
        public float Nutrition;

        /// <summary>Heat/fuel value per unit (0 if not a fuel).</summary>
        public float HeatValue;

        /// <summary>Whether the good spoils over time (food).</summary>
        public bool Perishable;

        /// <summary>Fraction of the stock lost per day when perishable.</summary>
        public float DailySpoilFraction;

        /// <summary>Whether the good is used for construction (GDD §10).</summary>
        public bool BuildingMaterial;

        public bool IsFood { get { return Nutrition > 0f; } }
        public bool IsFuel { get { return HeatValue > 0f; } }

        public ResourceDef(ResourceType type, string name)
        {
            Type = type;
            Name = name;
        }
    }
}
