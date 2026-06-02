namespace FoundersLands.Simulation.World
{
    /// <summary>
    /// Plain-C# parameters for world generation. In the Unity layer these are authored
    /// through a ScriptableObject wrapper (data-driven design, GDD §20), but the
    /// simulation core stays engine-independent so it can be unit-tested headlessly.
    /// Defaults approximate the balanced "Зелёная долина" starter map (GDD §6).
    /// </summary>
    public sealed class WorldGenSettings
    {
        public int Width = 128;
        public int Height = 128;

        public float SeaLevel = 0.32f;

        // Height field (fBm).
        public int HeightOctaves = 5;
        public float HeightFrequency = 3.0f;
        public float HeightLacunarity = 2.0f;
        public float HeightPersistence = 0.5f;

        // Moisture field (fBm).
        public int MoistureOctaves = 4;
        public float MoistureFrequency = 2.5f;

        // Temperature shaping.
        public float TemperatureNorthSouth = 0.35f;     // strength of the warm-south gradient
        public float TemperatureAltitudeFalloff = 0.6f; // higher peaks get colder

        // Height redistribution: widens the elevation spread so the map has both broad
        // lowlands and genuine highlands/peaks instead of everything hugging the mean.
        public float HeightContrast = 1.7f;

        // Island falloff pushes map edges below sea level for a contained valley.
        // Land is untouched within IslandInner and fades to water by IslandOuter
        // (distance is 0 at the centre, 1 at the map edge).
        public bool IslandFalloff = true;
        public float IslandInner = 0.58f;
        public float IslandOuter = 1.0f;

        // Biome thresholds, expressed as height above sea level (0..1).
        public float MountainThreshold = 0.80f;
        public float HighlandThreshold = 0.55f;
        public float MarshMaxAltitude = 0.14f;
        public float ForestMoisture = 0.52f;
        public float MeadowMoisture = 0.32f;
        public float ConiferTemperature = 0.45f;

        // Resource densities (probability per eligible tile).
        public float WoodDensity = 0.24f;
        public float BerriesDensity = 0.05f;
        public float GameDensity = 0.03f;
        public float HerbsDensity = 0.04f;
        public float StoneDensity = 0.14f;
        public float IronOreDensity = 0.05f;
        public float ClayDensity = 0.10f;
        public float FishDensity = 0.06f;

        public WorldGenSettings Clone()
        {
            return (WorldGenSettings)MemberwiseClone();
        }
    }
}
