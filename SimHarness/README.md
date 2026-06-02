# SimHarness — headless build & test of the simulation core

This folder exists so the **engine-free simulation** (everything under
`Assets/_Project/Scripts/Simulation/`) can be compiled, unit-tested and previewed
**without the Unity Editor or a GPU** — in CI, or on a server, or in a sandbox.

It does not duplicate code: `src/FoundersLands.Simulation.csproj` compiles the *same*
`.cs` files Unity does, via a `<Compile Include="../../Assets/.../Simulation/**/*.cs" />`
glob. It targets `netstandard2.1` + C# 9, which is the subset Unity 6 accepts, so
anything that builds here also builds in the Editor.

## Requirements

- .NET SDK 8.0+ (`dotnet --version`)

## Commands

Run from this folder:

```bash
# Build just the engine-free simulation assembly
dotnet build src/FoundersLands.Simulation.csproj -c Release

# Run the unit tests (determinism, value ranges, biome/resource rules)
dotnet test tests/FoundersLands.Simulation.Tests.csproj -c Release

# Preview a generated map as ASCII + biome/resource histograms
dotnet run --project console -c Release -- --seed green-valley --width 100 --height 50
```

`--seed` accepts either a number or any text (text is hashed with a platform-stable
FNV-1a so the same word always yields the same world).

## Why this matters

The simulation is the part of the game that is hardest to get right and most
performance-sensitive (GDD §4, §26). Keeping it engine-free means we can prove the
economic loop in numbers and lock behaviour with tests before any of it touches HDRP,
shaders or the Editor.
