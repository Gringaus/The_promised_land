namespace FoundersLands.Simulation.Construction
{
    /// <summary>Building kinds available in Module 3 (a focused subset of GDD §10).</summary>
    public enum BuildingType : byte
    {
        Tent = 0,           // временный лагерь — дешёвое слабое укрытие
        House = 1,          // дом семьи — жильё и тепло
        Storehouse = 2,     // общий склад — вместимость
        ForagerHut = 3,     // изба сборщиков — бонус к еде
        WoodcutterCamp = 4, // дровяной двор — бонус к дровам
        Sawmill = 5,        // лесопилка: дерево -> доски (GDD §12)
        Smelter = 6,        // плавильня: руда -> слиток
        Smithy = 7,         // кузница: слиток -> инструменты
        Market = 8,         // рынок: раздаёт товары, снижает бытовые издержки
        Watchtower = 9,     // дозорная башня — оборона и раннее предупреждение (GDD §13)
        Palisade = 10,      // частокол — оборона
        Field = 11,         // поле: сев весной, жатва осенью -> зерно (GDD §9)
        Mill = 12,          // мельница: зерно -> мука
        Bakery = 13,        // пекарня: мука + дрова -> хлеб
        Cellar = 14,        // погреб/ледник: замедляет порчу запасов (GDD §8)
        Smokehouse = 15     // коптильня: заготовка мяса/рыбы, замедляет порчу (GDD §8)
    }
}
