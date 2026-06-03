using UnityEngine;
using FoundersLands.Presentation.Authoring;
using FoundersLands.Simulation.Core;
using FoundersLands.Simulation.Economy;
using FoundersLands.Simulation.Settlements;
using FoundersLands.Simulation.Time;
using FoundersLands.Simulation.World;

namespace FoundersLands.Presentation
{
    /// <summary>
    /// The bridge between Unity and the engine-free core: it generates a world and a colony from
    /// authored assets (or sensible defaults) and advances the simulation in step with real time.
    /// Game rules live in <see cref="SettlementSimulation"/>; this only drives the clock and exposes
    /// the current state for views to read (GDD §4, §20). Add it to an empty GameObject and press Play.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ColonyHost : MonoBehaviour
    {
        [Tooltip("World seed. A number is used directly; any other text is hashed (stable across runs).")]
        [SerializeField] private string seed = "green-valley";

        [Tooltip("Optional authored map settings; defaults are used when empty.")]
        [SerializeField] private WorldGenSettingsAsset worldSettings;

        [Tooltip("Optional authored colony config; defaults are used when empty.")]
        [SerializeField] private SettlementConfigAsset colonyConfig;

        [Tooltip("Simulated days per real second.")]
        [SerializeField, Min(0f)] private float daysPerSecond = 4f;

        [SerializeField] private bool autoStart = true;

        public WorldMap Map { get; private set; }
        public Settlement Colony { get; private set; }
        public DayReport LastReport { get; private set; }
        public bool Running { get; private set; }
        public float DaysPerSecond { get => daysPerSecond; set => daysPerSecond = Mathf.Max(0f, value); }

        private float _dayAccumulator;

        private void Start()
        {
            if (autoStart) Begin();
        }

        /// <summary>(Re)generate the world and colony from the current seed/assets and start ticking.</summary>
        public void Begin()
        {
            ulong seedValue = SeedToValue(seed);
            WorldGenSettings settings = worldSettings != null ? worldSettings.ToSettings() : new WorldGenSettings();
            SettlementConfig config = colonyConfig != null ? colonyConfig.ToConfig() : new SettlementConfig();

            Map = WorldGenerator.Generate(settings, seedValue);
            Colony = SettlementFactory.Create(Map, seedValue, config,
                ResourceCatalog.CreateDefault(), SeasonDef.CreateDefault());
            LastReport = default;
            _dayAccumulator = 0f;
            Running = true;

            Debug.Log($"[ColonyHost] World {Map.Width}x{Map.Height} (seed '{seed}' = {seedValue}); " +
                      $"{Colony.AlivePopulation} settlers at ({Colony.CenterX},{Colony.CenterY}).");
        }

        public void SetRunning(bool running) => Running = running;
        public void ToggleRunning() => Running = !Running;

        private void Update()
        {
            if (!Running || Colony == null || daysPerSecond <= 0f) return;

            _dayAccumulator += Time.deltaTime * daysPerSecond;
            int steps = (int)_dayAccumulator;
            if (steps <= 0) return;
            _dayAccumulator -= steps;
            if (steps > 60) steps = 60; // never let a frame hitch trigger an unbounded catch-up

            for (int i = 0; i < steps; i++)
            {
                if (Colony.AlivePopulation == 0) { Running = false; break; }
                LastReport = SettlementSimulation.Step(Colony);
            }
        }

        // A bare number is taken as the seed; any other text is hashed the same way the headless
        // tools do, so "green-valley" reproduces the exact same world in the Editor and on the CLI.
        private static ulong SeedToValue(string text)
        {
            if (string.IsNullOrEmpty(text)) return 1UL;
            return ulong.TryParse(text, out ulong n) ? n : StableHash.Fnv1a64(text);
        }
    }
}
