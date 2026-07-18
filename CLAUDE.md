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

`godot --path . -- --screenshot=/tmp/shot.png` boots the game windowed, saves a PNG after 30 frames, and quits.
