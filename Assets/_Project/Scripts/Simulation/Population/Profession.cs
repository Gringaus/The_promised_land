namespace FoundersLands.Simulation.Population
{
    /// <summary>
    /// What work a citizen performs (GDD §11 "WorkAssignment"). Module 2 added the survival
    /// jobs (food and fuel); Module 3 adds the material and construction jobs (GDD §10).
    /// </summary>
    public enum Profession : byte
    {
        Idle = 0,
        Forager = 1,     // собирает еду (ягоды/рыба/дичь)
        Woodcutter = 2,  // заготавливает дрова
        Logger = 3,      // валит лес на строевую древесину (Wood)
        Quarryman = 4,   // добывает камень (Stone)
        Builder = 5,     // строит по чертежам (GDD §10)
        Miner = 6,       // добывает железную руду (IronOre)
        Craftsman = 7,   // работает в мастерских по рецептам (GDD §12)
        Militiaman = 8,  // ополчение/дружина — обороняет колонию (GDD §13)
        Farmer = 9       // обрабатывает поля: сев, уход, жатва (GDD §9)
    }
}
