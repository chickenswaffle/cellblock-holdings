# Cellblock Holdings

[![Play in browser](https://img.shields.io/badge/%E2%96%B6%EF%B8%8E%20%20PLAY%20IN%20BROWSER-2ea44f?style=for-the-badge)](https://chickenswaffle.github.io/cellblock-holdings/)

A tile-based prison management sim where you don't play a warden — you play the **franchise owner**. Build prisons, staff them, keep the population from tearing itself apart, and answer to a board that only reads spreadsheets.

**Stack:** Godot 4.4 · GDScript · 3D (angled orthogonal camera, procedurally generated geometry — no imported 3D assets) · no external deps (GUT vendored for tests).

Full design + milestone plan: [`docs/cellblock-holdings-plan.md`](docs/cellblock-holdings-plan.md)

## Status

- ✅ **M0 — Skeleton**: pure sim core (`SimWorld`, seeded xorshift RNG, fixed-tick clock, event bus, edge-walled grid), code-built view (camera rig, `TileMapLayer` renderer with runtime placeholder tileset), headless GUT tests incl. determinism (same seed × 1000 ticks → identical state hash) and a sim-purity scan that fails if anything under `scripts/sim/` touches the engine.
- ✅ **M1 — Build mode**: walls/doors/floors via a sequential `ConstructionQueue` (money deducted on completion, not order), flood-fill `RoomDetector` (border-leak sealed check, doors block same as walls), `ZoneValidator` + 8 zone kinds with per-kind required objects, `Ledger`, drag-rectangle build tool + click-to-zone tool. 54 tests green, incl. 5 room-detection fixtures (nested rooms, door-in-corner, border-sealed) and an end-to-end SimWorld test matching the milestone's literal DoD.
- ✅ **Rendering pivot — 2D → 3D**: replaced the flat top-down tilemap with a true-3D view: one shader-blended ground mesh (soft natural terrain instead of a hard grid), an angled Cities-Skylines-style orthogonal camera, real-time lighting/shadows, and procedurally generated 3D geometry for walls/doors/zone tint/objects (all still code-authored, no imported models). Sim layer untouched — this only replaced `scripts/view/`.
- ✅ **M2 — Prisoners**: `Needs` (7 decaying needs), hand-rolled 8-way A* pathfinder (edge-wall blocking, no corner-cutting, doors passable at a cost, own binary heap), a 24-hour `ScheduleSystem`, utility-scored `UtilityAI` action selection (need deficit × availability × distance + RNG noise), `Intake` (procedural name/trait generation, bus arrivals, bed assignment), and a click-to-inspect prisoner panel. 89 tests green, incl. a full 50-prisoner day/night-cycle DoD test and a perf test — 200 agents average **0.75ms/tick**, well under the 8ms budget, so the hierarchical pathfinding the design doc worried about wasn't needed at this scale. The game now boots into a small starter facility (4 cells, a canteen, a yard) with a few prisoners already living in it.
- ✅ **M3 — Staff & jobs**: three hireable roles (`Staff`) on DAY/NIGHT shifts with `fatigue` and `nerve`. **Construction now costs worker-time**: `ConstructionQueue` became a claimable work pool denominated in worker-ticks, so a queued building only goes up while a worker stands on the site — an unstaffed prison builds nothing, and hiring more workers finishes the same queue sooner. `StaffAI` runs workers (claim → walk → build), guards (auto-derived patrol routes looping the zoned rooms, feeding a `guard_presence()` query M4's tension model will read), and support staff (a staffed canteen serves meals 1.6× faster). Anyone past 85% fatigue walks off to a staff room until they've recovered; exhausted staff move and build slower. `Payroll` debits the whole wage bill daily and can't part-pay — miss three days and your staff quit. 124 tests green, incl. all three DoD clauses.
- ✅ **M4 — Conflict** *(the game shows up here)*: `GrievanceSystem` turns lived conditions into slow-moving resentment; `TensionField` turns that into per-room pressure that **diffuses between connected rooms** like heat, so trouble spreads along the map's real topology; `IncidentSystem` sparks incidents out of tense rooms and climbs them a branching ladder (grudge → verbal → shove → fight → brawl → weapon/stabbing *or* faction war → block riot → facility riot), with five resolution options that all cost something real — force injures and spikes facility-wide grievance, solitary destroys reform and is counted for M6's oversight, negotiation is the only one that genuinely helps and needs a support staffer on duty, transfers cost money, conceding costs more. Plus procedurally-named `Faction`s with recruitment/territory/relations, a `Contraband` supply graph feeding weapon availability, searches that suppress it and raise grievance either way, informants who get killed if you lean on them, and lockdown. The **tension overlay ships with the model, not after it** — [T] in game. 194 tests, incl. the milestone's literal DoD: an understaffed, overcrowded prison riots within 5 sim-days and a well-run one doesn't, **across all 20 seeds**.
- ⬜ M5 — Franchise · M6 — Board & oversight · M7 — Save/load · M8 — Content & juice

## Architecture in one paragraph

Everything under `scripts/sim/` is pure GDScript (`RefCounted`, no `Node`, no Godot RNG, no signals). `SimWorld.tick()` is the only mutation entry point, run at a fixed 10 ticks/sec; the view (`scripts/view/`) accumulates real frame time into whole ticks and only ever *reads* sim state, reacting to sim events via a plain observer list. All randomness flows through `SimWorld.rng` (seeded xorshift64\*), so the same seed always produces the same prison. Save = serialize `SimWorld` to JSON.

## Running

Open the project in Godot 4.4 and run, or:

```sh
godot --path .        # main scene
./run-tests.sh        # all tests incl. the slow whole-sim ones (~6 min)
./run-tests.sh --fast # skips tests/slow/ (~45 s) — for tight iteration
```

`tests/slow/` holds the M4 riot DoD (20 seeds × 5 sim-days × 2 scenarios) and the 200-agent perf test. They're slow because they simulate ~100 prison-days, and they're the two tests most likely to catch a real regression in the balance of the sim — so the default run includes them.

Everything is clickable — the keyboard shortcuts below are also printed on the buttons, and any action you can't currently take is greyed out with a tooltip saying why ("No support staffer on duty").

| | |
|---|---|
| **Camera** | WASD/arrows or middle-drag to pan, wheel to zoom, **Home** to recenter. Right-drag or **,**/**.** turns the view a full 360°, **PgUp**/**PgDn** tilt. Panning follows wherever you're facing. Edge-scrolling is off by default; there's a toggle in the top bar. |
| **Build** | **1**–**5** pick a tool (wall, door, floor, object, zone), **Esc** returns to camera. **Q**/**E** cycle the active tool's sub-type. Drag out an area and it highlights green (or red if you can't afford it) while a readout tells you how many pieces, what it costs, and how long your crew will take. Walls drag as a room outline or a single run — pick which in the type palette — so you can extend and divide existing buildings. Objects drag too: pull out a row of beds in one gesture. Right-click demolishes. |
| **Time** | **Space** pauses, **−**/**+** change speed. |
| **Staff** | Hire and fire from the staff panel. Nothing you queue gets built until a worker is on shift to build it. |
| **Conflict** | **T** toggles the tension overlay. While an incident is running: **F** force, **G** solitary, **N** negotiate, **B** transfer, **K** concede, **L** lockdown. |
