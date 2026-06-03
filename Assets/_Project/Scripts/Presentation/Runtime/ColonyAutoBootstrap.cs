using UnityEngine;

namespace FoundersLands.Presentation
{
    /// <summary>
    /// Convenience for the dev harness: when the scripting define <c>FOUNDERSLANDS_AUTORUN</c> is set
    /// (Project Settings ▸ Player ▸ Scripting Define Symbols), this spawns a <see cref="ColonyHost"/>
    /// and an interactive <see cref="ColonyMapView"/> on play if the scene has none — so any scene runs
    /// the simulation, and lets you pan, select and build, with no manual setup. Off by default;
    /// without the define nothing happens.
    /// </summary>
    public static class ColonyAutoBootstrap
    {
#if FOUNDERSLANDS_AUTORUN
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void SpawnIfAbsent()
        {
            if (Object.FindFirstObjectByType<ColonyHost>() != null) return;

            var go = new GameObject("Founder's Lands (auto)");
            go.AddComponent<ColonyHost>();
            go.AddComponent<ColonyMapView>();
            Object.DontDestroyOnLoad(go);
        }
#endif
    }
}
