namespace FoundersLands.Simulation.Construction
{
    /// <summary>Building kinds available in Module 3 (a focused subset of GDD §10).</summary>
    public enum BuildingType : byte
    {
        Tent = 0,           // временный лагерь — дешёвое слабое укрытие
        House = 1,          // дом семьи — жильё и тепло
        Storehouse = 2,     // общий склад — вместимость
        ForagerHut = 3,     // изба сборщиков — бонус к еде
        WoodcutterCamp = 4  // дровяной двор — бонус к дровам
    }
}
