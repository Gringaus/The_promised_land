using System.Text;

namespace FoundersLands.Simulation.Core
{
    /// <summary>
    /// Platform-stable hashing. Unlike <see cref="string.GetHashCode()"/>, these
    /// results are guaranteed identical across runs, machines and .NET versions.
    /// That stability is what lets a saved map seed reproduce the exact same world
    /// (GDD §7, §21) and is what the determinism tests rely on.
    /// </summary>
    public static class StableHash
    {
        public const ulong Fnv1aOffset = 14695981039346656037UL;
        public const ulong Fnv1aPrime = 1099511628211UL;

        /// <summary>FNV-1a 64-bit hash over the UTF-8 bytes of <paramref name="text"/>.</summary>
        public static ulong Fnv1a64(string text)
        {
            ulong hash = Fnv1aOffset;
            if (text != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                for (int i = 0; i < bytes.Length; i++)
                {
                    hash ^= bytes[i];
                    unchecked { hash *= Fnv1aPrime; }
                }
            }
            return hash;
        }

        /// <summary>Fold a 64-bit value into an existing FNV-1a stream.</summary>
        public static ulong Combine(ulong hash, ulong value)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    hash ^= (byte)(value & 0xFF);
                    hash *= Fnv1aPrime;
                    value >>= 8;
                }
            }
            return hash;
        }

        public static ulong Combine(ulong hash, int value)
        {
            return Combine(hash, (ulong)(uint)value);
        }
    }
}
