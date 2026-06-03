using FoundersLands.Simulation.Construction;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.SaveLoad;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Threats;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;
using Xunit;

namespace FoundersLands.Simulation.Tests
{
    public class ThreatTests
    {
        private static readonly ulong Seed = StableHash.Fnv1a64("green-valley");

        private static SettlementConfig BaseConfig(bool threats, bool defended) => new SettlementConfig
        {
            StartingPopulation = 40,
            ForagerShare = 0.45f, WoodcutterShare = 0.15f, LoggerShare = 0.08f,
            QuarrymanShare = 0.04f, MinerShare = 0.06f, CraftsmanShare = 0.10f,
            MilitiaShare = defended ? 0.10f : 0.0f,
            StorehouseCapacity = 20000f,
            StartingFoodUnits = 400f, StartingFirewoodUnits = 350f,
            StartingWoodUnits = 60f, StartingStoneUnits = 40f,
            EnableThreats = threats
        };

        // A rich, pre-built colony — exactly the kind of fat target the Director escalates against.
        private static Settlement Colony(bool threats, bool defended)
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 128, Height = 128 }, Seed);
            Settlement s = SettlementFactory.Create(map, Seed, BaseConfig(threats, defended),
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            s.Storehouse.Add(ResourceType.IronOre, ResourceQuality.Standard, 20f);

            int gx = s.CenterX, gy = s.CenterY;
            Complete(s, BuildingType.Storehouse, gx + 1, gy);
            for (int i = 0; i < 8; i++) Complete(s, BuildingType.House, gx + 2 + i, gy);
            Complete(s, BuildingType.Sawmill, gx, gy + 1);
            Complete(s, BuildingType.Smelter, gx, gy + 2);
            Complete(s, BuildingType.Smithy, gx, gy + 3);
            Complete(s, BuildingType.Market, gx, gy + 4);
            if (defended)
            {
                Complete(s, BuildingType.Watchtower, gx - 1, gy);
                Complete(s, BuildingType.Watchtower, gx - 1, gy + 2);
                Complete(s, BuildingType.Palisade, gx - 1, gy + 4);
            }
            return s;
        }

        private static void Complete(Settlement s, BuildingType type, int x, int y)
        {
            Building b = s.PlaceBlueprint(type, x, y);
            b.WorkDone = b.WorkRequired;
            b.Complete = true;
        }

        [Fact]
        public void DisabledByDefault_LeavesTheColonyUntouched()
        {
            Settlement s = Colony(threats: false, defended: false);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            Assert.Equal(ThreatStage.Calm, s.Threat.Stage);
            Assert.Equal(0f, s.Threat.Pressure);
            Assert.Equal(0, s.Threat.TotalRaids);
            Assert.Equal(0, s.Threat.TotalThefts);
            Assert.Equal(0f, s.Threat.TotalStolen);
        }

        [Fact]
        public void RichUndefendedColony_EscalatesAndGetsRaided()
        {
            Settlement s = Colony(threats: true, defended: false);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);

            Assert.True(s.Threat.TotalRaids > 0, "a fat, open colony should be raided within a year");
            Assert.True(s.Threat.TotalStolen > 0f, "raids and thefts should remove goods");
        }

        [Fact]
        public void Defence_HoldsPressureDownAndCutsLosses()
        {
            Settlement open = Colony(threats: true, defended: false);
            Settlement guarded = Colony(threats: true, defended: true);
            SettlementSimulation.Run(open, open.Clock.DaysPerYear);
            SettlementSimulation.Run(guarded, guarded.Clock.DaysPerYear);

            Assert.True(guarded.Threat.TotalStolen < open.Threat.TotalStolen, "guards should lose less");
            Assert.True(guarded.Threat.TotalRaids <= open.Threat.TotalRaids);
            Assert.True(guarded.Threat.Pressure <= open.Threat.Pressure);
        }

        [Fact]
        public void Watchtower_ContributesBuildingDefense()
        {
            WorldMap map = WorldGenerator.Generate(new WorldGenSettings { Width = 48, Height = 48 }, Seed);
            var s = new Settlement(map, 24, 24, new SettlementConfig(),
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            Complete(s, BuildingType.Watchtower, 24, 25);
            s.RecomputeBuildingEffects();

            float expected = s.BuildingCatalog.Get(BuildingType.Watchtower).DefenseBonus;
            Assert.Equal(expected, s.BuildingDefense);
            Assert.True(expected > 0f);
        }

        [Fact]
        public void IsDeterministicWithThreatsOn()
        {
            Settlement a = Colony(threats: true, defended: false);
            Settlement b = Colony(threats: true, defended: false);
            SettlementSimulation.Run(a, a.Clock.DaysPerYear);
            SettlementSimulation.Run(b, b.Clock.DaysPerYear);
            Assert.Equal(a.StateHash(), b.StateHash());
        }

        [Fact]
        public void ThreatState_RoundTripsThroughSave()
        {
            Settlement s = Colony(threats: true, defended: false);
            SettlementSimulation.Run(s, s.Clock.DaysPerYear);
            Assert.True(s.Threat.TotalRaids > 0); // make sure there is non-trivial threat state to preserve

            var settings = new WorldGenSettings { Width = 128, Height = 128 };
            Settlement loaded = SaveGame.Load(SaveGame.Save(s, settings));

            Assert.Equal(s.StateHash(), loaded.StateHash());
            Assert.Equal(s.Threat.TotalRaids, loaded.Threat.TotalRaids);
            Assert.Equal(s.Threat.Stage, loaded.Threat.Stage);
            Assert.Equal(s.Threat.Pressure, loaded.Threat.Pressure);
            Assert.Equal(s.Threat.CampStrength, loaded.Threat.CampStrength);
        }
    }
}
