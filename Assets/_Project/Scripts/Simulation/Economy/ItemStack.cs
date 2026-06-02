namespace FoundersLands.Simulation.Economy
{
    /// <summary>A quantity of one good at one quality tier.</summary>
    public readonly struct ItemStack
    {
        public readonly ResourceType Type;
        public readonly ResourceQuality Quality;
        public readonly float Amount;

        public ItemStack(ResourceType type, ResourceQuality quality, float amount)
        {
            Type = type;
            Quality = quality;
            Amount = amount;
        }
    }
}
