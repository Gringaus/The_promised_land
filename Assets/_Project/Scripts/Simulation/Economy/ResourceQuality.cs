namespace FoundersLands.Simulation.Economy
{
    /// <summary>
    /// Quality tier of a good (GDD §8 "Качество ресурсов"). Quality scales how effective
    /// a unit is — better food nourishes more, better firewood burns warmer.
    /// </summary>
    public enum ResourceQuality : byte
    {
        Poor = 0,
        Standard = 1,
        Fine = 2
    }

    public static class ResourceQualityExtensions
    {
        /// <summary>Effectiveness multiplier applied to nutrition / heat / build value.</summary>
        public static float Multiplier(this ResourceQuality q)
        {
            switch (q)
            {
                case ResourceQuality.Poor: return 0.7f;
                case ResourceQuality.Fine: return 1.3f;
                default: return 1.0f;
            }
        }
    }
}
