using FoundersLands.Simulation.Core;

namespace FoundersLands.Simulation.Population
{
    /// <summary>
    /// A settler (GDD §11). Module 2 models the needs that decide life and death — food
    /// and warmth — plus health. Richer attributes (family, skill, mood, inventory,
    /// personal history) layer on later without changing this loop's shape.
    /// </summary>
    public sealed class Citizen
    {
        public int Id;
        public string Name;
        public int Age;
        public Profession Profession;

        public bool Alive = true;

        /// <summary>0 = healthy, 100 = peak. Death at 0.</summary>
        public float Health = 100f;

        /// <summary>0 = well fed, 1 = starving.</summary>
        public float Hunger;

        /// <summary>0 = warm, 1 = freezing.</summary>
        public float Cold;

        public Citizen(int id, string name, int age, Profession profession)
        {
            Id = id;
            Name = name;
            Age = age;
            Profession = profession;
        }

        public ulong Hash(ulong h)
        {
            h = StableHash.Combine(h, Id);
            h = StableHash.Combine(h, Alive ? 1 : 0);
            h = StableHash.Combine(h, Age);
            h = StableHash.Combine(h, (int)(Health * 100f));
            h = StableHash.Combine(h, (int)(Hunger * 1000f));
            h = StableHash.Combine(h, (int)(Cold * 1000f));
            h = StableHash.Combine(h, (int)Profession);
            return h;
        }
    }
}
