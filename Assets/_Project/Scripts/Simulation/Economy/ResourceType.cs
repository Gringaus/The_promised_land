namespace FoundersLands.Simulation.Economy
{
    /// <summary>
    /// A carried/stored good in the economy (GDD §8). These are distinct from the raw
    /// <see cref="World.ResourceNodeKind"/> deposits on the map: gathering a deposit
    /// yields one of these goods (e.g. a Game node yields <see cref="Meat"/>).
    /// </summary>
    public enum ResourceType : byte
    {
        Wood = 0,      // строительная древесина
        Firewood = 1,  // дрова (топливо для зимы)
        Stone = 2,
        Berries = 3,   // ягоды (еда)
        Fish = 4,      // рыба (еда)
        Meat = 5,      // мясо (еда)
        Herbs = 6,     // травы
        Clay = 7,
        IronOre = 8,
        Planks = 9,     // доски (дерево -> доски)
        IronIngot = 10, // слиток (руда -> слиток)
        Tools = 11      // инструменты (слиток -> инструменты; ускоряют работу)
    }
}
