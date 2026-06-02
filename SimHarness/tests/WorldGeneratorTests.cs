using System;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class WorldGeneratorTests
    {
        private static WorldGenSettings Settings(int w = 96, int h = 96)
        {
            return new WorldGenSettings { Width = w, Height = h };
        }

        [Fact]
        public void Generate_IsDeterministic_ForSameSeedAndSettings()
        {
            WorldMap a = WorldGenerator.Generate(Settings(), 777);
            WorldMap b = WorldGenerator.Generate(Settings(), 777);
            Assert.Equal(a.ContentHash(), b.ContentHash());
        }

        [Fact]
        public void Generate_DiffersBySeed()
        {
            WorldMap a = WorldGenerator.Generate(Settings(), 1);
            WorldMap b = WorldGenerator.Generate(Settings(), 2);
            Assert.NotEqual(a.ContentHash(), b.ContentHash());
        }

        [Fact]
        public void Generate_RespectsDimensions()
        {
            WorldMap map = WorldGenerator.Generate(Settings(80, 60), 5);
            Assert.Equal(80, map.Width);
            Assert.Equal(60, map.Height);
            Assert.Equal(80 * 60, map.TileCount);
        }

        [Fact]
        public void AllScalarFields_StayInUnitInterval()
        {
            WorldMap map = WorldGenerator.Generate(Settings(), 13);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    Assert.InRange(t.Height, 0f, 1f);
                    Assert.InRange(t.Temperature, 0f, 1f);
                    Assert.InRange(t.Moisture, 0f, 1f);
                    Assert.InRange(t.Fertility, 0f, 1f);
                }
            }
        }

        [Fact]
        public void WaterFlag_AlwaysMatchesWaterBiome()
        {
            WorldMap map = WorldGenerator.Generate(Settings(), 21);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    Assert.Equal(t.IsWater, t.Biome == Biome.Water);
                }
            }
        }

        [Fact]
        public void WaterTiles_HaveZeroFertility()
        {
            WorldMap map = WorldGenerator.Generate(Settings(), 34);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    if (t.IsWater) Assert.Equal(0f, t.Fertility);
                }
            }
        }

        [Fact]
        public void ResourcePlacement_RespectsLandWaterRules()
        {
            WorldMap map = WorldGenerator.Generate(Settings(), 55);
            for (int y = 0; y < map.Height; y++)
            {
                for (int x = 0; x < map.Width; x++)
                {
                    Tile t = map.Get(x, y);
                    switch (t.Resource)
                    {
                        case ResourceNodeKind.Fish:
                        case ResourceNodeKind.FreshWater:
                            Assert.True(t.IsWater, "aquatic resource on land");
                            break;
                        case ResourceNodeKind.None:
                            break;
                        default:
                            Assert.False(t.IsWater, "land resource on water");
                            break;
                    }
                    if (t.Resource != ResourceNodeKind.None)
                    {
                        Assert.True(t.ResourceAmount > 0, "placed node has no amount");
                    }
                }
            }
        }

        [Fact]
        public void GreenValleySeed_ProducesCoreResources()
        {
            ulong seed = StableHash.Fnv1a64("green-valley");
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 100, Height = 50 }, seed);
            Assert.True(map.CountResource(ResourceNodeKind.Wood) > 0, "no wood");
            Assert.True(map.CountResource(ResourceNodeKind.Stone) > 0, "no stone");
            Assert.True(map.CountResource(ResourceNodeKind.Fish) > 0, "no fish");
        }

        [Fact]
        public void DefaultMaps_CanYieldEveryKeyResource()
        {
            // Not every seed contains every deposit, but a balanced valley map must be
            // able to produce the full early economy chain on some seed (GDD §8, §12).
            bool found = false;
            for (ulong seed = 1; seed <= 30 && !found; seed++)
            {
                WorldMap map = WorldGenerator.Generate(new WorldGenSettings(), seed);
                found =
                    map.CountResource(ResourceNodeKind.Wood) > 0 &&
                    map.CountResource(ResourceNodeKind.Stone) > 0 &&
                    map.CountResource(ResourceNodeKind.IronOre) > 0 &&
                    map.CountResource(ResourceNodeKind.Clay) > 0 &&
                    map.CountResource(ResourceNodeKind.Fish) > 0;
            }
            Assert.True(found, "no seed in 1..30 produced the full key-resource set");
        }

        [Fact]
        public void Generate_RejectsInvalidArguments()
        {
            Assert.Throws<ArgumentNullException>(() => WorldGenerator.Generate(null, 1));
            Assert.Throws<ArgumentException>(() => WorldGenerator.Generate(new WorldGenSettings { Width = 0 }, 1));
            Assert.Throws<ArgumentException>(() => WorldGenerator.Generate(new WorldGenSettings { Height = -4 }, 1));
        }
    }
}
