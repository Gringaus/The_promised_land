using UnityEngine;
using FoundersLands.Simulation.World;

namespace FoundersLands.Presentation
{
    /// <summary>
    /// Paints a <see cref="WorldMap"/> into a point-filtered <see cref="Texture2D"/> for the debug
    /// view: biome colour shaded by elevation, with resource deposits flecked in. A stand-in for the
    /// HDRP terrain to come (GDD §15, §22) — enough to see the procedural world the core produced.
    /// </summary>
    public static class WorldMapTextureBuilder
    {
        public static Texture2D Build(WorldMap map)
        {
            var tex = new Texture2D(map.Width, map.Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "WorldMap"
            };

            var pixels = new Color32[map.Width * map.Height];
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    pixels[y * map.Width + x] = Colorize(t);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            return tex;
        }

        private static Color32 Colorize(Tile t)
        {
            Color baseColor = BiomeColor(t.Biome);

            // Elevation relief: water deepens with depth, land brightens with height.
            if (t.IsWater)
                baseColor *= Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(t.Height * 3f));
            else
                baseColor *= Mathf.Lerp(0.78f, 1.12f, Mathf.Clamp01(t.Height));

            // Fleck in any resource deposit so the economy's raw inputs are visible on the map.
            if (t.Resource != ResourceNodeKind.None)
                baseColor = Color.Lerp(baseColor, ResourceColor(t.Resource), 0.4f);

            baseColor.a = 1f;
            return baseColor;
        }

        private static Color BiomeColor(Biome b)
        {
            switch (b)
            {
                case Biome.Water: return new Color(0.16f, 0.34f, 0.55f);
                case Biome.Marsh: return new Color(0.34f, 0.40f, 0.30f);
                case Biome.Meadow: return new Color(0.52f, 0.69f, 0.36f);
                case Biome.Steppe: return new Color(0.74f, 0.71f, 0.44f);
                case Biome.DeciduousForest: return new Color(0.30f, 0.50f, 0.24f);
                case Biome.ConiferousForest: return new Color(0.20f, 0.37f, 0.27f);
                case Biome.RockyHighland: return new Color(0.55f, 0.52f, 0.47f);
                case Biome.Mountain: return new Color(0.80f, 0.80f, 0.82f);
                default: return Color.magenta; // unmapped biome stands out
            }
        }

        private static Color ResourceColor(ResourceNodeKind r)
        {
            switch (r)
            {
                case ResourceNodeKind.Wood: return new Color(0.14f, 0.30f, 0.12f);
                case ResourceNodeKind.Berries: return new Color(0.78f, 0.20f, 0.28f);
                case ResourceNodeKind.Game: return new Color(0.60f, 0.40f, 0.22f);
                case ResourceNodeKind.Herbs: return new Color(0.50f, 0.74f, 0.40f);
                case ResourceNodeKind.Stone: return new Color(0.85f, 0.85f, 0.88f);
                case ResourceNodeKind.IronOre: return new Color(0.66f, 0.38f, 0.26f);
                case ResourceNodeKind.Clay: return new Color(0.74f, 0.50f, 0.36f);
                case ResourceNodeKind.Fish: return new Color(0.40f, 0.66f, 0.82f);
                case ResourceNodeKind.FreshWater: return new Color(0.45f, 0.72f, 0.90f);
                default: return Color.white;
            }
        }
    }
}
