# Cellblock Holdings

[![Play in browser](https://img.shields.io/badge/%E2%96%B6%EF%B8%8E%20%20PLAY%20IN%20BROWSER-2ea44f?style=for-the-badge)](https://chickenswaffle.github.io/cellblock-holdings/)

A tile-based prison management sim where you don't play a warden — you play the **franchise owner**. Build prisons, staff them, keep the population from tearing itself apart, and answer to a board that only reads spreadsheets.

**Stack:** Godot 4.4 · GDScript · 3D (angled orthogonal camera, procedurally generated geometry — no imported 3D assets) · no external deps (GUT vendored for tests).

Full design + milestone plan: [`docs/cellblock-holdings-plan.md`](docs/cellblock-holdings-plan.md)

## Status

- ✅ **M0 — Skeleton**: pure sim core (`SimWorld`, seeded xorshift RNG, fixed-tick clock, event bus, edge-walled grid), code-built view (camera rig, `TileMapLayer` renderer with runtime placeholder tileset), headless GUT tests incl. determinism (same seed × 1000 ticks → identical state hash) and a sim-purity scan that fails if anything under `scripts/sim/` touches the engine.
- ✅ **M1 — Build mode**: walls/doors/floors via a sequential `ConstructionQueue` (money deducted on completion, not order), flood-fill `RoomDetector` (border-leak sealed check, doors block same as walls), `ZoneValidator` + 8 zone kinds with per-kind required objects, `Ledger`, drag-rectangle build tool + click-to-zone tool. 54 tests green, incl. 5 room-detection fixtures (nested rooms, door-in-corner, border-sealed) and an end-to-end SimWorld test matching the milestone's literal DoD.
- ✅ **Rendering pivot — 2D → 3D**: replaced the flat top-down tilemap with a true-3D view: one shader-blended ground mesh (soft natural terrain instead of a hard grid), an angled Cities-Skylines-style orthogonal camera, real-time lighting/shadows, and procedurally generated 3D geometry for walls/doors/zone tint/objects (all still code-authored, no imported models). Sim layer untouched — this only replaced `scripts/view/`.
- ⬜ M2 — Prisoners · M3 — Staff · M4 — Conflict · M5 — Franchise · M6 — Board & oversight · M7 — Save/load · M8 — Content & juice

## Architecture in one paragraph

Everything under `scripts/sim/` is pure GDScript (`RefCounted`, no `Node`, no Godot RNG, no signals). `SimWorld.tick()` is the only mutation entry point, run at a fixed 10 ticks/sec; the view (`scripts/view/`) accumulates real frame time into whole ticks and only ever *reads* sim state, reacting to sim events via a plain observer list. All randomness flows through `SimWorld.rng` (seeded xorshift64\*), so the same seed always produces the same prison. Save = serialize `SimWorld` to JSON.

## Running

Open the project in Godot 4.4 and run, or:

```sh
godot --path .        # main scene
./run-tests.sh        # headless GUT tests (set GODOT_BIN if godot isn't on PATH)
```

Controls: WASD/arrows/middle-drag to pan, wheel to zoom, Space pause, 1/2/3 speed (camera mode).
Building: **Q/E** cycle tool (camera → wall → door → floor → object → zone), **Esc** back to camera-only. Left-drag to build a wall perimeter or paint floor, left-click to place a door/object/zone, right-click to demolish. Number keys pick the sub-type for whichever tool is active (floor 1-4, object 1-9, zone 1-8).
