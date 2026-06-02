namespace FoundersLands.Simulation.World
{
    /// <summary>Biome classification produced by world generation (GDD §7).</summary>
    public enum Biome : byte
    {
        Water = 0,
        Marsh = 1,             // болото
        Meadow = 2,            // луга
        Steppe = 3,            // степь / лесостепь
        DeciduousForest = 4,   // лиственный лес
        ConiferousForest = 5,  // хвойный лес
        RockyHighland = 6,     // каменистые холмы
        Mountain = 7           // горы
    }
}
