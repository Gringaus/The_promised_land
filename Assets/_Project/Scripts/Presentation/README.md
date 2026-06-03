# Presentation layer (Unity-side)

This assembly is the **thin "view"** over the engine-free `FoundersLands.Simulation`
core. It is where `MonoBehaviour`s, HDRP materials/shaders, Cinemachine cameras, UI
and ScriptableObject wrappers live (GDD §14, §15, §18, §22).

Rules:

- The simulation never references this assembly; this assembly references the
  simulation (one-way dependency). See the Simulation–Presentation split in
  `/README.md` and GDD §4 / §20.
- ScriptableObjects here are **authoring wrappers**: they hold serialized fields and
  build the plain-C# config/records the simulation consumes (e.g. `WorldGenSettings`).
  Game rules stay in the simulation, not in `MonoBehaviour`s.

Code in this folder is added during the Unity-Editor stages (it needs a GPU and the
Editor, which the headless build environment does not have).

## What's here (Module 10 scaffold + Module 15 interactive view)

A first runnable slice that makes the core visible in the Editor without any HDRP/scene
authoring. It uses IMGUI + a generated texture, so it works in any render pipeline.

- `Authoring/WorldGenSettingsAsset`, `Authoring/SettlementConfigAsset` — `ScriptableObject`
  wrappers (menu **Founder's Lands ▸ …**). Tune map/colony in the Inspector; `ToSettings()` /
  `ToConfig()` build the plain records the core consumes.
- `Runtime/ColonyHost` — the bridge: generates the world + colony from a seed (and optional
  assets) and advances `SettlementSimulation` in step with real time (`days/second`).
- `Runtime/WorldMapTextureBuilder` — paints a `WorldMap` into a `Texture2D` (biome colour,
  elevation relief, resource flecks).
- `Runtime/ColonyDebugOverlay` — an IMGUI overlay drawing the map and a live read-out
  (season, population, food/fuel, goods, buildings, and — when enabled — threat and
  demographics), plus pause/speed/regenerate controls.
- `Runtime/ColonyMapView` (Module 15) — an **interactive** IMGUI view: pan (middle/right-drag
  or arrow keys) and zoom-to-cursor (scroll wheel); left-click to select a tile and read its
  details; a build palette (respecting tech locks) places blueprints on click, which the
  colony's builders then raise. Buildings show as markers (amber building, blue done). Use this
  *or* `ColonyDebugOverlay`.
- `Runtime/ColonyAutoBootstrap` — optional: with the scripting define `FOUNDERSLANDS_AUTORUN`,
  spawns the host + interactive view automatically so any scene runs the sim.

### Run it

1. Open the project in Unity (HDRP 6.x). Let it import and compile the assemblies.
2. In any scene, create an empty GameObject and add **Colony Map View** — it pulls in **Colony
   Host** automatically via `[RequireComponent]`. Optionally assign authored config assets and
   set the seed (e.g. `green-valley` — the same seed reproduces the CLI world exactly).
3. Press **Play**. The procedural map appears; pan/zoom, click a tile to inspect it, pick a
   building and click to place it, and use the panel to pause/change speed/regenerate.

Alternatively, add `FOUNDERSLANDS_AUTORUN` to *Project Settings ▸ Player ▸ Scripting Define
Symbols* and just press Play in an empty scene.

The same balance runs headlessly in `/SimHarness` (see `/README.md`), so behaviour can be
verified on the CLI before it is ever drawn.
