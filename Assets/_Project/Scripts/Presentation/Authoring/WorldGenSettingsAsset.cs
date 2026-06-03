using UnityEngine;
using FoundersLands.Simulation.World;

namespace FoundersLands.Presentation.Authoring
{
    /// <summary>
    /// Editor-authored wrapper around the engine-free <see cref="WorldGenSettings"/> (GDD §20).
    /// Designers tune map size and richness here; the simulation consumes the plain-C# record
    /// built by <see cref="ToSettings"/>. Rules stay in the core — this only carries data.
    /// </summary>
    [CreateAssetMenu(fileName = "WorldGenSettings", menuName = "Founder's Lands/World Gen Settings")]
    public sealed class WorldGenSettingsAsset : ScriptableObject
    {
        [Header("Map")]
        [Min(8)] public int width = 128;
        [Min(8)] public int height = 128;
        [Range(0f, 1f)] public float seaLevel = 0.32f;
        public bool islandFalloff = true;

        [Header("Resource density (share of eligible tiles)")]
        [Range(0f, 1f)] public float woodDensity = 0.24f;
        [Range(0f, 1f)] public float berriesDensity = 0.05f;
        [Range(0f, 1f)] public float gameDensity = 0.03f;
        [Range(0f, 1f)] public float herbsDensity = 0.04f;
        [Range(0f, 1f)] public float stoneDensity = 0.14f;
        [Range(0f, 1f)] public float ironOreDensity = 0.05f;
        [Range(0f, 1f)] public float clayDensity = 0.10f;
        [Range(0f, 1f)] public float fishDensity = 0.06f;

        /// <summary>Builds the plain settings record the world generator consumes.</summary>
        public WorldGenSettings ToSettings()
        {
            return new WorldGenSettings
            {
                Width = width,
                Height = height,
                SeaLevel = seaLevel,
                IslandFalloff = islandFalloff,
                WoodDensity = woodDensity,
                BerriesDensity = berriesDensity,
                GameDensity = gameDensity,
                HerbsDensity = herbsDensity,
                StoneDensity = stoneDensity,
                IronOreDensity = ironOreDensity,
                ClayDensity = clayDensity,
                FishDensity = fishDensity
            };
        }
    }
}
