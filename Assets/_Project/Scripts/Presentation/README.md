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
