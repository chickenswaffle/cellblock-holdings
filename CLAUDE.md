# Cellblock Holdings — agent instructions

Godot 4.4 prison-franchise management sim. Full design + milestones: `docs/cellblock-holdings-plan.md`. Work **one milestone per session**; current status is in `README.md`.

## Rules (non-negotiable)

- Run `./run-tests.sh --fast` after every change, and the full `./run-tests.sh` before committing (see "Tests: fast vs full" below). Local Godot binary: `/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot` (override with `GODOT_BIN`).
- Everything under `scripts/sim/` is **pure GDScript**: `RefCounted` only — no `Node`, no `get_tree()`, no `_process`, no `randf()`/`randi()`/`RandomNumberGenerator`, no Godot signals, no `Input`/`OS`/`Time`/`Engine`. `tests/test_sim_purity.gd` enforces this; fix the file, never the test.
- All randomness via `SimWorld.rng` (`SimRng`, seeded xorshift64*). Hex int literals must fit in signed 64-bit.
- `SimWorld.tick()` is the only mutation entry point. Fixed timestep 10 ticks/sec; speed changes ticks-per-frame in the view, never tick size. The sim never sees `delta`.
- Sim → view communication via `SimEventBus` (plain observer list). View reads sim state, never writes it.
- Every sim class implements `to_dict()`/`from_dict()`; state must be reachable from `SimWorld` or it doesn't exist. Determinism tests hash `SimWorld.to_dict()` JSON.
- Godot 4.4 APIs only: own A* not `AStarGrid2D`.
- `class_name` + typed vars everywhere.

## View layer is 3D (post-M1 pivot)

Angled orthogonal `Camera3D` (Cities Skylines-ish, ~35° pitch, no rotation yet), everything code-authored — no imported 3D models. `1 world unit = 1 tile`, sim `(x, y)` maps to 3D `(x, 0, y)` (Y is up). Key pieces:
- `terrain_renderer_3d.gd` — one ground `PlaneMesh`, one draw call. Floor type per tile is baked into a small texture (`assets/shaders/terrain.gdshader` reads it) and sampled with linear filtering, which blends smoothly across tile boundaries "for free" instead of needing per-tile geometry.
- `structures_renderer_3d.gd` — walls/doors/each object type are batched into their own `MultiMeshInstance3D` (rebuilt whenever `grid.grid_version` changes), so draw calls stay flat regardless of count. Zone tint reuses the terrain's texture-blend trick on a second, slightly-elevated plane.
- `camera_rig.gd` — pan/zoom/edge-scroll. Middle-drag panning raycasts the ground plane (`ground_point()`) so the point under the cursor stays under the cursor at any zoom — don't replace this with a hand-derived screen-to-world scale factor, the pitch's foreshortening makes that error-prone (learned the hard way: a fixed-angle guess first read as "basically top-down" until verified against the actual foreshortening ratio in a screenshot).
- Tile picking for build/zone tools also goes through `CameraRig.ground_point()` (a ray-plane intersection against y=0), not pixel math — this is what makes tile picking work correctly regardless of camera zoom/pitch.
- Gotcha already hit once: edge-scroll fired on its own before any real mouse movement, because the OS/engine's default cursor position can sit inside the edge margin at startup. `camera_rig.gd` gates edge-scroll on `_mouse_seen` (only set true by a real `InputEventMouseMotion`) — don't remove that guard.

## Agents (M2)

Movement lives in `SimAgent` (`scripts/sim/agents/sim_agent.gd`), which both `Prisoner` and `Staff` extend — pos/path/`step_along_path()`/`tile_pos()`/`set_destination()`/`place_at_tile()`. Never assign `pos` by hand; use `place_at_tile()`, which applies the +0.5 the renderers assume (see below). Subclasses override `move_speed()` — that's how staff fatigue slows people down.

`Prisoner.pos` is **tile-center convention**: pos `(5.5, 7.5)` means tile `(5,7)`, matching how `StructuresRenderer3D` places walls/objects at `tile + 0.5`. `tile_pos()` uses `floori`, not `roundi` — floori is the correct inverse of "+0.5" regardless of floating-point rounding mode; roundi is not. Every place that sets `pos` directly (`Intake.intake()`, `step_along_path()`'s waypoint target) must add the `+0.5` offset — skipping it once (a real bug hit while building this) puts the agent exactly on a tile edge, which is exactly where wall geometry is, so they render inside/behind walls and look invisible.

`UtilityAI` picks actions by scoring `need_deficit × 1/(1+distance) × RNG noise` per candidate need for the current `ScheduleSystem` block, described in `docs/cellblock-holdings-plan.md` §M2. `NeedSystem.minute_tick()` runs once per sim-minute (not per tick — decay/satisfaction rates are defined per minute); movement (`step_along_path`) and the traveling→performing transition run every tick in `SimWorld.tick()`.

`Pathfinder.find_path()` is a hand-rolled 8-way A* with its own binary min-heap (`Pathfinder._MinHeap`, an inner class) — GDScript has no built-in heap. Diagonal moves check both flanking orthogonal edges to avoid cutting through a wall corner. Room detection (`RoomDetector`) and pathfinding use *different* edge-passability rules on purpose: `grid.edge_open()` treats a door as blocking (a door still separates two rooms) while `grid.edge_passable()` treats it as passable at a cost multiplier (`Pathfinder.DOOR_COST_MULTIPLIER`) — don't unify these, they answer different questions.

## Staff (M3)

`Staff` has a role (GUARD/WORKER/SUPPORT) and a shift (DAY 06–18 / NIGHT 18–06, and the night branch wraps midnight — test any hour logic against both). Off-shift staff are parked on `world.gate_tile` with state `OFF_DUTY`; `StaffRenderer3D` skips them, so what you see on the map is who's actually covering the floor. `StaffSystem.tick()` does movement + construction work per tick, `StaffSystem.minute_tick()` does fatigue/shifts/re-decisions per minute — the same split as `NeedSystem`/`UtilityAI`, and for the same reason (rates are defined per minute; arriving at a build site has to resolve per tick).

**Construction requires workers.** `ConstructionQueue` no longer ticks itself — it's a work pool and `StaffSystem` is the only thing that calls `apply_work()`. A test that enqueues a `BuildOrder` and ticks `SimWorld` will now sit there forever unless it hires a worker: use `tests/helpers/crew.gd` (`Crew.staff_up()` + `Crew.set_hour()` to land inside the day shift, `Crew.run_until_built()`). Two traps that already bit while writing these tests: ticking long enough to cross 18:00 hands the job to the night shift, and a single wall is only 40 worker-ticks, so "tick 300 then assert they're still WORKING" fails because they already finished.

Fatigue is the cross-cutting stat — it slows `move_speed()`, cuts `work_rate()`, erodes `effective_nerve()` (M4 reads that), and past `BREAK_AT_FATIGUE` sends anyone to a staff room. `SimWorld.guard_presence(room)` is the other thing M4 is meant to consume.

## Conflict (M4)

Everything under `scripts/sim/conflict/`. The chain runs in a fixed order every sim minute and the order is the design: **conditions → `GrievanceSystem` → `TensionField` → `IncidentSystem`**. Factions and contraband tick hourly.

**Room ids are not stable — key persistent per-room state off `RoomInfo.key()`.** `RoomDetector` hands out ids in scan order, so building one wall anywhere renumbers half the map. `key()` is the room's topmost-leftmost tile. `TensionField`, faction territory and contraband stashes all use it; `room_adjacency()` is keyed by id because it's rebuilt in the same pass as the rooms.

**Two constants define the tension model's character**: `RESPONSE_RATE` (how fast a room expresses its own pressure) and `DIFFUSION_RATE` (how fast it leaks to neighbours). Their *ratio* is the largest gap two connected rooms can sustain (~0.37 today). A past bug let diffusion run into the unsealed outdoors, which touches everything and holds no tension — that pinned every room at `RESPONSE_RATE/DIFFUSION_RATE` (~0.09) regardless of conditions and nothing ever sparked. `TensionField` now only exchanges with sealed rooms, and `test_tension_field.gd` has a regression test for it. If tension ever looks suspiciously uniform or capped, suspect a sink first.

**The incident ladder is a branching graph** (`IncidentSystem._next_rung`), not `kind + 1`. The weapon rungs require contraband and are a detour, not a gate — a brawl with no weapons around escalates straight to faction war or block riot. Gating riots behind contraband breaks the M4 DoD for any facility without a visitation room.

**Don't cache per-room queries on SimWorld keyed by time.** Prisoners are added, removed and moved mid-tick, so a tick-stamped occupancy cache silently serves stale data to whatever runs next in the same tick (this shipped briefly and was caught by a unit test). `prisoners_in_room()` always scans; callers that need every room at once build `occupancy_index()` once and pass it, the same pattern as `GrievanceSystem.crowding_by_prisoner()`. Caches keyed off `grid.grid_version` (`room_capacity`, `room_center`, `room_adjacency`) are fine — only building invalidates those.

## Tests: fast vs full

`./run-tests.sh --fast` skips `tests/slow/` and takes ~45s; the bare `./run-tests.sh` includes them and takes ~6 minutes. `tests/slow/` is the M4 riot DoD (20 seeds × 5 sim-days × 2 scenarios) and the 200-agent perf test. Use `--fast` while iterating, always run the full suite before committing. The slow tests are slow because they simulate ~100 prison-days — the dominant cost is M2's A*, which scales with map area, which is why the DoD test builds its own compact 28×16 facility instead of using `FacilityBuilder`'s 80-wide one.

Two trap patterns when writing whole-sim tests, both hit for real: **ticking past a shift boundary** hands the job to the night shift and inverts your assertion, and **assuming an order/incident is still in progress** after N ticks when the underlying work takes fewer (look the entity up by id and assert it exists, don't index `[0]`).

## Verify visually

`godot --path . -- --screenshot=/tmp/shot.png` boots the game windowed, saves a PNG after 30 frames, and quits. For build-mode features that need a pre-built scene to show up in the screenshot (no way to script mouse drag headlessly), add a temporary `--demo-room`-style flag to `bootstrap.gd` that enqueues BuildOrders programmatically, screenshot, then remove the temporary code before committing — don't leave debug-only branches in.

## After editing/adding any script

New or renamed `class_name` files aren't visible to other scripts (or GUT) until Godot's global class cache is rebuilt. `run-tests.sh` always does this itself now, but if you're checking things another way, run `godot --headless --path . --import` first or you'll see spurious "Identifier not declared" parse errors.

## Web export / deploy

`./deploy-web.sh` exports the `Web` preset (single-threaded — required for GitHub Pages, which doesn't set the COOP/COEP headers threaded WASM needs) and force-pushes it to the `gh-pages` branch. Requires the 4.4.1 web export templates in `~/Library/Application Support/Godot/export_templates/4.4.1.stable/` (`web_nothreads_release.zip` / `web_nothreads_debug.zip`). Live at https://chickenswaffle.github.io/cellblock-holdings/. Headless/software-rendered browser testing (e.g. Playwright with SwiftShader) is unreliable for this project's WebGL output and produced a false-negative blank-canvas result once — verify visually in a real browser, not headless screenshots, before concluding a web-render bug is real.
