# Cellblock Holdings

A tile-based prison management sim where you don't play a warden — you play the **franchise owner**. Build prisons, staff them, keep the population from tearing itself apart, and answer to a board that only reads spreadsheets.

**Stack:** Godot 4.4 · GDScript · 2D · no external deps (GUT vendored for tests).

Full design + milestone plan: [`docs/cellblock-holdings-plan.md`](docs/cellblock-holdings-plan.md)

## Status

- ✅ **M0 — Skeleton**: pure sim core (`SimWorld`, seeded xorshift RNG, fixed-tick clock, event bus, edge-walled grid), code-built view (camera rig, `TileMapLayer` renderer with runtime placeholder tileset), headless GUT tests incl. determinism (same seed × 1000 ticks → identical state hash) and a sim-purity scan that fails if anything under `scripts/sim/` touches the engine.
- ⬜ M1 — Build mode · M2 — Prisoners · M3 — Staff · M4 — Conflict · M5 — Franchise · M6 — Board & oversight · M7 — Save/load · M8 — Content & juice

## Architecture in one paragraph

Everything under `scripts/sim/` is pure GDScript (`RefCounted`, no `Node`, no Godot RNG, no signals). `SimWorld.tick()` is the only mutation entry point, run at a fixed 10 ticks/sec; the view (`scripts/view/`) accumulates real frame time into whole ticks and only ever *reads* sim state, reacting to sim events via a plain observer list. All randomness flows through `SimWorld.rng` (seeded xorshift64\*), so the same seed always produces the same prison. Save = serialize `SimWorld` to JSON.

## Running

Open the project in Godot 4.4 and run, or:

```sh
godot --path .        # main scene
./run-tests.sh        # headless GUT tests (set GODOT_BIN if godot isn't on PATH)
```

Controls: WASD/arrows/middle-drag to pan, wheel to zoom, Space pause, 1/2/3 speed.
