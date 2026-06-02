namespace FoundersLands.Simulation.Population
{
    /// <summary>
    /// What work a citizen performs (GDD §11 "WorkAssignment"). Module 2 covers the two
    /// jobs the survival loop needs; crafting, hauling and building professions arrive
    /// with later systems.
    /// </summary>
    public enum Profession : byte
    {
        Idle = 0,
        Forager = 1,    // собирает еду (ягоды/рыба/дичь)
        Woodcutter = 2  // заготавливает дрова
    }
}
