namespace FoundersLands.Simulation.World
{
    /// <summary>
    /// A single map cell. Kept as a compact value type stored in a flat array so the
    /// world can be swept cheaply in batch passes — the performance posture the GDD
    /// calls for with hundreds of agents and large maps (§4).
    /// </summary>
    public struct Tile
    {
        public float Height;       // [0,1] normalized elevation
        public float Temperature;  // [0,1] cold..warm
        public float Moisture;     // [0,1] dry..wet
        public float Fertility;    // [0,1] soil fertility (GDD soil layer §3, §7)
        public bool IsWater;
        public Biome Biome;
        public ResourceNodeKind Resource;
        public ushort ResourceAmount;
    }
}
