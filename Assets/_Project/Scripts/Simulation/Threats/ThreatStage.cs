namespace FoundersLands.Simulation.Threats
{
    /// <summary>
    /// Escalation ladder of the soft military threat (GDD §13). Pressure climbs the rungs as a
    /// rich, poorly-guarded colony draws attention; defence pushes it back down.
    /// </summary>
    public enum ThreatStage
    {
        Calm = 0,
        Rumors = 1,   // слухи — early warning, no damage
        Scouts = 2,   // разведчики — probing, no damage
        Thefts = 3,   // кражи — petty theft from the storehouse
        Ambush = 4,   // засада — harder theft, workers get hurt
        Raid = 5      // рейд — looting, building damage, casualties if undefended
    }
}
