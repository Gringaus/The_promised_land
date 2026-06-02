namespace FoundersLands.Simulation.World
{
    /// <summary>
    /// Natural resource node sitting on a tile (GDD §7 "Ресурсы", §8). These are the
    /// raw deposits on the map; refined goods and production chains (GDD §12) are
    /// modelled separately in the economy layer.
    /// </summary>
    public enum ResourceNodeKind : byte
    {
        None = 0,
        Wood = 1,        // дерево
        Berries = 2,     // ягоды
        Game = 3,        // дичь
        Herbs = 4,       // травы
        Stone = 5,       // камень
        IronOre = 6,     // железная руда
        Clay = 7,        // глина
        Fish = 8,        // рыба
        FreshWater = 9   // источник пресной воды
    }
}
