using UnityEngine;
using FoundersLands.Simulation.Settlements;

namespace FoundersLands.Presentation.Authoring
{
    /// <summary>
    /// Editor-authored wrapper around <see cref="SettlementConfig"/> (GDD §20). Exposes the most
    /// tuned balance — workforce split, starting stock, and the opt-in subsystems — and builds the
    /// plain config the simulation runs on. Fields left off the inspector keep their code defaults.
    /// </summary>
    [CreateAssetMenu(fileName = "SettlementConfig", menuName = "Founder's Lands/Settlement Config")]
    public sealed class SettlementConfigAsset : ScriptableObject
    {
        [Header("Population")]
        [Min(1)] public int startingPopulation = 20;

        [Header("Workforce split (share of population)")]
        [Range(0f, 1f)] public float foragerShare = 0.65f;
        [Range(0f, 1f)] public float woodcutterShare = 0.30f;
        [Range(0f, 1f)] public float loggerShare = 0f;
        [Range(0f, 1f)] public float quarrymanShare = 0f;
        [Range(0f, 1f)] public float builderShare = 0f;
        [Range(0f, 1f)] public float minerShare = 0f;
        [Range(0f, 1f)] public float craftsmanShare = 0f;
        [Range(0f, 1f)] public float militiaShare = 0f;
        [Range(0f, 1f)] public float farmerShare = 0f;

        [Header("Starting stock")]
        public float startingFoodUnits = 120f;
        public float startingFirewoodUnits = 150f;
        public float startingWoodUnits = 0f;
        public float startingStoneUnits = 0f;
        public float storehouseCapacity = 6000f;

        [Header("Subsystems (opt-in)")]
        public bool usePathWeightedPotential = false;
        public bool enableThreats = false;
        public bool enablePopulationDynamics = false;

        /// <summary>Builds the plain config; unset fields keep <see cref="SettlementConfig"/> defaults.</summary>
        public SettlementConfig ToConfig()
        {
            return new SettlementConfig
            {
                StartingPopulation = startingPopulation,
                ForagerShare = foragerShare,
                WoodcutterShare = woodcutterShare,
                LoggerShare = loggerShare,
                QuarrymanShare = quarrymanShare,
                BuilderShare = builderShare,
                MinerShare = minerShare,
                CraftsmanShare = craftsmanShare,
                MilitiaShare = militiaShare,
                FarmerShare = farmerShare,
                StartingFoodUnits = startingFoodUnits,
                StartingFirewoodUnits = startingFirewoodUnits,
                StartingWoodUnits = startingWoodUnits,
                StartingStoneUnits = startingStoneUnits,
                StorehouseCapacity = storehouseCapacity,
                UsePathWeightedPotential = usePathWeightedPotential,
                EnableThreats = enableThreats,
                EnablePopulationDynamics = enablePopulationDynamics
            };
        }
    }
}
