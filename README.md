# OpenApparatus

[![dotnet](https://github.com/OpenApparatus/core/actions/workflows/dotnet.yml/badge.svg)](https://github.com/OpenApparatus/core/actions/workflows/dotnet.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

Engine-agnostic .NET library for procedurally generating reproducible navigation environments — multi-room floor plans, mazes, and other behavioral-research apparatuses — with deterministic output suitable for replication across studies.

This is the **core** library. Other repos in the `OpenApparatus` org depend on it:

- **[OpenApparatus/studio](https://github.com/OpenApparatus/studio)** — cross-platform desktop app (Avalonia) for authoring and previewing floor plans
- **[OpenApparatus/unity](https://github.com/OpenApparatus/unity)** — Unity package that consumes this library to spawn environments in scenes

## Status

🚧 Pre-release — under active early development. Public API is unstable until v0.1.

| Milestone | Description | Status |
|---|---|---|
| A0 | Repo scaffold + sentinel test | ✅ |
| A1 | Topology data model + grid+domino generator + spanning-tree passage assigner | ⏳ |
| A2 | Engine-agnostic mesh builder for a single rectangular cell | ⏳ |
| A3 | Doorway tunnel geometry through shared walls | ⏳ |
| A4 | Full floor-plan mesh assembler | ⏳ |

## Architecture

Three layers, one-way dependencies:

1. **Topology** (`src/OpenApparatus.Core/Topology/`) — pure data: `FloorPlan`, `Cell`, `Adjacency`, `Passage`, `IFloorPlanGenerator`. Pure C#, no engine assumptions.
2. **Geometry** (`src/OpenApparatus.Core/Geometry/`) — turns topology into engine-agnostic `MeshData` POCOs. Studio and Unity packages convert these to their respective renderable types.
3. **Math / utilities** — `SeededRandom`, vector helpers, etc.

## Building and testing

Requires .NET 8 SDK or newer.

```bash
git clone https://github.com/OpenApparatus/core.git
cd core
dotnet test
```

The library multi-targets `net8.0` and `netstandard2.1` — the latter for Unity 2021.2+ compatibility.

## Determinism

Every generator and assigner takes a `SeededRandom` instance. Same seed + same parameters always produce the same `FloorPlan`. This is the central reproducibility guarantee — methods sections only need to record the seed and parameter values for a result to be reconstructable.

## License

MIT — see [LICENSE](LICENSE).
