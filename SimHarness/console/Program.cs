using System;
using System.Collections.Generic;
using System.Text;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.World;

namespace FoundersLands.SimViewer
{
    /// <summary>
    /// Headless preview of procedural world generation (GDD §7). Generates a map from a
    /// seed and prints its content hash, an ASCII minimap and biome/resource histograms,
    /// so generation can be inspected and tuned without the Unity Editor.
    ///
    /// Usage: dotnet run --project SimHarness/console -- --seed green-valley --width 96 --height 48
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            int width = 96;
            int height = 48;

            for (int i = 0; i + 1 < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seed": seed = ParseSeed(args[i + 1]); break;
                    case "--width": int.TryParse(args[i + 1], out width); break;
                    case "--height": int.TryParse(args[i + 1], out height); break;
                }
            }

            var settings = new WorldGenSettings { Width = width, Height = height };
            WorldMap map = WorldGenerator.Generate(settings, seed);

            Console.WriteLine("Founder's Lands — procedural map preview (GDD §7)");
            Console.WriteLine($"seed={seed}  size={map.Width}x{map.Height}  seaLevel={map.SeaLevel:0.00}  contentHash=0x{map.ContentHash():X16}");
            Console.WriteLine();

            PrintMap(map);
            Console.WriteLine();
            Console.WriteLine("Legend: ~ water  ; marsh  , meadow  . steppe  T deciduous  Y conifer  ^ highland  M mountain");
            Console.WriteLine("        resources overlay biomes: * wood  O stone  # iron  c clay  f fish  b berries  g game  h herbs  w spring");
            Console.WriteLine();

            PrintBiomes(map);
            Console.WriteLine();
            PrintResources(map);
            return 0;
        }

        private static ulong ParseSeed(string s)
        {
            return ulong.TryParse(s, out ulong n) ? n : StableHash.Fnv1a64(s);
        }

        private static void PrintMap(WorldMap map)
        {
            for (int y = 0; y < map.Height; y++)
            {
                var sb = new StringBuilder(map.Width);
                for (int x = 0; x < map.Width; x++)
                {
                    sb.Append(Glyph(map.Get(x, y)));
                }
                Console.WriteLine(sb.ToString());
            }
        }

        private static char Glyph(Tile t)
        {
            switch (t.Resource)
            {
                case ResourceNodeKind.Wood: return '*';
                case ResourceNodeKind.Stone: return 'O';
                case ResourceNodeKind.IronOre: return '#';
                case ResourceNodeKind.Clay: return 'c';
                case ResourceNodeKind.Fish: return 'f';
                case ResourceNodeKind.Berries: return 'b';
                case ResourceNodeKind.Game: return 'g';
                case ResourceNodeKind.Herbs: return 'h';
                case ResourceNodeKind.FreshWater: return 'w';
            }
            switch (t.Biome)
            {
                case Biome.Water: return '~';
                case Biome.Marsh: return ';';
                case Biome.Meadow: return ',';
                case Biome.Steppe: return '.';
                case Biome.DeciduousForest: return 'T';
                case Biome.ConiferousForest: return 'Y';
                case Biome.RockyHighland: return '^';
                case Biome.Mountain: return 'M';
            }
            return ' ';
        }

        private static void PrintBiomes(WorldMap map)
        {
            Console.WriteLine("Biomes:");
            var hist = map.BiomeHistogram();
            float total = map.TileCount;
            foreach (var kv in Sorted(hist))
            {
                Console.WriteLine($"  {kv.Key,-18} {kv.Value,6}  {(kv.Value / total) * 100f,5:0.0}%");
            }
        }

        private static void PrintResources(WorldMap map)
        {
            Console.WriteLine("Resource nodes:");
            var hist = map.ResourceHistogram();
            if (hist.Count == 0)
            {
                Console.WriteLine("  (none)");
                return;
            }
            foreach (var kv in SortedRes(hist))
            {
                Console.WriteLine($"  {kv.Key,-12} {kv.Value,6}");
            }
        }

        private static IEnumerable<KeyValuePair<Biome, int>> Sorted(Dictionary<Biome, int> d)
        {
            var list = new List<KeyValuePair<Biome, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }

        private static IEnumerable<KeyValuePair<ResourceNodeKind, int>> SortedRes(Dictionary<ResourceNodeKind, int> d)
        {
            var list = new List<KeyValuePair<ResourceNodeKind, int>>(d);
            list.Sort((a, b) => b.Value.CompareTo(a.Value));
            return list;
        }
    }
}
