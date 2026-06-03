using System;
using FoundersLands.Simulation.Construction;

namespace FoundersLands.Simulation.Technology
{
    /// <summary>
    /// One node of the tech tree (GDD §16). Costs research points, may require earlier techs, and on
    /// completion unlocks buildings and/or grants a settlement-wide work bonus. Data-driven: the tree
    /// is content (<see cref="TechCatalog"/>), the progression rules are code (<c>ResearchSystem</c>).
    /// </summary>
    public sealed class Technology
    {
        public readonly int Id;
        public readonly string Name;
        public readonly float Cost;
        public readonly int[] Prerequisites;
        public readonly BuildingType[] Unlocks;
        public readonly float WorkBonus; // added to the settlement work multiplier when unlocked

        public Technology(int id, string name, float cost, int[] prerequisites, BuildingType[] unlocks, float workBonus)
        {
            Id = id;
            Name = name;
            Cost = cost;
            Prerequisites = prerequisites ?? Array.Empty<int>();
            Unlocks = unlocks ?? Array.Empty<BuildingType>();
            WorkBonus = workBonus;
        }
    }
}
