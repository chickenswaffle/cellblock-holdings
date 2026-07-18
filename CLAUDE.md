# Cellblock Holdings — agent instructions

Godot 4.4 prison-franchise management sim. Full design + milestones: `docs/cellblock-holdings-plan.md`. Work **one milestone per session**; current status is in `README.md`.

## Rules (non-negotiable)

- Run `./run-tests.sh` after every change. Local Godot binary: `/Users/trading/ClaudeWorkspace/tools/Godot.app/Contents/MacOS/Godot` (override with `GODOT_BIN`).
- Everything under `scripts/sim/` is **pure GDScript**: `RefCounted` only — no `Node`, no `get_tree()`, no `_process`, no `randf()`/`randi()`/`RandomNumberGenerator`, no Godot signals, no `Input`/`OS`/`Time`/`Engine`. `tests/test_sim_purity.gd` enforces this; fix the file, never the test.
- All randomness via `SimWorld.rng` (`SimRng`, seeded xorshift64*). Hex int literals must fit in signed 64-bit.
- `SimWorld.tick()` is the only mutation entry point. Fixed timestep 10 ticks/sec; speed changes ticks-per-frame in the view, never tick size. The sim never sees `delta`.
- Sim → view communication via `SimEventBus` (plain observer list). View reads sim state, never writes it.
- Every sim class implements `to_dict()`/`from_dict()`; state must be reachable from `SimWorld` or it doesn't exist. Determinism tests hash `SimWorld.to_dict()` JSON.
- Godot 4.4 APIs only: `TileMapLayer` not `TileMap`, own A* not `AStarGrid2D`.
- `class_name` + typed vars everywhere.

## Verify visually

`godot --path . -- --screenshot=/tmp/shot.png` boots the game windowed, saves a PNG after 30 frames, and quits. For build-mode features that need a pre-built scene to show up in the screenshot (no way to script mouse drag headlessly), add a temporary `--demo-room`-style flag to `bootstrap.gd` that enqueues BuildOrders programmatically, screenshot, then remove the temporary code before committing — don't leave debug-only branches in.

## After editing/adding any script

New or renamed `class_name` files aren't visible to other scripts (or GUT) until Godot's global class cache is rebuilt. `run-tests.sh` always does this itself now, but if you're checking things another way, run `godot --headless --path . --import` first or you'll see spurious "Identifier not declared" parse errors.

## Web export / deploy

`./deploy-web.sh` exports the `Web` preset (single-threaded — required for GitHub Pages, which doesn't set the COOP/COEP headers threaded WASM needs) and force-pushes it to the `gh-pages` branch. Requires the 4.4.1 web export templates in `~/Library/Application Support/Godot/export_templates/4.4.1.stable/` (`web_nothreads_release.zip` / `web_nothreads_debug.zip`). Live at https://chickenswaffle.github.io/cellblock-holdings/. Headless/software-rendered browser testing (e.g. Playwright with SwiftShader) is unreliable for this project's WebGL output and produced a false-negative blank-canvas result once — verify visually in a real browser, not headless screenshots, before concluding a web-render bug is real.
