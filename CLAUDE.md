# Cellblock Holdings — agent instructions

Godot 4.4 prison-franchise management sim. Full design + milestones: `docs/cellblock-holdings-plan.md`. Work **one milestone per session**; current status is in `README.md`.

## Rules (non-negotiable)

- Run `./run-tests.sh` after every change. Local Godot binary: `/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot` (override with `GODOT_BIN`).
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

## Verify visually

`godot --path . -- --screenshot=/tmp/shot.png` boots the game windowed, saves a PNG after 30 frames, and quits. For build-mode features that need a pre-built scene to show up in the screenshot (no way to script mouse drag headlessly), add a temporary `--demo-room`-style flag to `bootstrap.gd` that enqueues BuildOrders programmatically, screenshot, then remove the temporary code before committing — don't leave debug-only branches in.

## After editing/adding any script

New or renamed `class_name` files aren't visible to other scripts (or GUT) until Godot's global class cache is rebuilt. `run-tests.sh` always does this itself now, but if you're checking things another way, run `godot --headless --path . --import` first or you'll see spurious "Identifier not declared" parse errors.

## Web export / deploy

`./deploy-web.sh` exports the `Web` preset (single-threaded — required for GitHub Pages, which doesn't set the COOP/COEP headers threaded WASM needs) and force-pushes it to the `gh-pages` branch. Requires the 4.4.1 web export templates in `~/Library/Application Support/Godot/export_templates/4.4.1.stable/` (`web_nothreads_release.zip` / `web_nothreads_debug.zip`). Live at https://chickenswaffle.github.io/cellblock-holdings/. Headless/software-rendered browser testing (e.g. Playwright with SwiftShader) is unreliable for this project's WebGL output and produced a false-negative blank-canvas result once — verify visually in a real browser, not headless screenshots, before concluding a web-render bug is real.
