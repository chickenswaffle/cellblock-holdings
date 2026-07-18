class_name SimWorld
extends RefCounted
## Root of the simulation. tick() is the ONLY mutation entry point; the view
## calls it on a fixed timestep and never writes state directly.
## Everything savable is reachable from here.

var seed_value: int
var rng: SimRng
var clock: SimClock
var grid: SimGrid
var events: SimEventBus


func _init(p_seed: int = 1, grid_w: int = 100, grid_h: int = 100) -> void:
	seed_value = p_seed
	rng = SimRng.new(p_seed)
	clock = SimClock.new()
	grid = SimGrid.new(grid_w, grid_h)
	events = SimEventBus.new()


## Advance the world by exactly one fixed tick.
func tick() -> void:
	clock.advance()
	# Future systems (needs, schedule, tension, economy) hook in here, in a
	# fixed order — order is part of determinism.
	if clock.tick_count % SimClock.TICKS_PER_SIM_MINUTE == 0:
		events.emit("minute_passed", {"minute": clock.minute_of_day(), "day": clock.day()})
	if clock.minute_of_day() == 0 and clock.tick_count % (SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY) == 0:
		events.emit("day_passed", {"day": clock.day()})


## Serialize the full world state. Event subscribers are runtime-only and
## intentionally excluded.
func to_dict() -> Dictionary:
	return {
		"seed_value": seed_value,
		"rng": rng.to_dict(),
		"clock": clock.to_dict(),
		"grid": grid.to_dict(),
	}


func from_dict(d: Dictionary) -> void:
	seed_value = int(d.get("seed_value", 1))
	rng.from_dict(d.get("rng", {}))
	clock.from_dict(d.get("clock", {}))
	grid.from_dict(d.get("grid", {}))


## Stable hash of the full serialized state, for determinism tests.
func state_hash() -> String:
	return JSON.stringify(to_dict()).sha256_text()
