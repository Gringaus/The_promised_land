namespace FoundersLands.Simulation.Construction
{
    /// <summary>
    /// Visible construction stages (GDD §10): a building progresses чертёж → фундамент →
    /// каркас → стены → крыша → ввод as work is done. If materials run out, the stage
    /// simply stops advancing — the build physically stalls rather than ticking a timer.
    /// </summary>
    public enum ConstructionStage : byte
    {
        Blueprint = 0,   // чертёж/разметка
        Foundation = 1,  // фундамент
        Frame = 2,       // каркас
        Walls = 3,       // стены
        Roof = 4,        // крыша
        Complete = 5     // ввод в эксплуатацию
    }

    public static class ConstructionStages
    {
        /// <summary>Derive the visible stage from how much of the work is done (0..1).</summary>
        public static ConstructionStage FromWorkFraction(float f, bool fullyDelivered)
        {
            if (f >= 1f && fullyDelivered) return ConstructionStage.Complete;
            if (f <= 0f) return ConstructionStage.Blueprint;
            if (f < 0.20f) return ConstructionStage.Foundation;
            if (f < 0.50f) return ConstructionStage.Frame;
            if (f < 0.80f) return ConstructionStage.Walls;
            return ConstructionStage.Roof;
        }
    }
}
