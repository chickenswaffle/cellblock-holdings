class_name SimWorld
extends RefCounted
## Root of the simulation. tick() is the ONLY mutation entry point; the view
## calls it on a fixed timestep and never writes state directly.
## Everything savable is reachable from here.

const STARTING_BALANCE := 50000

var seed_value: int
var rng: SimRng
var clock: SimClock
var grid: SimGrid
var events: SimEventBus
var ledger: Ledger
var construction_queue: ConstructionQueue

## Derived from grid; recomputed on grid_version change, never saved directly.
var rooms: Array[RoomInfo] = []
var _rooms_grid_version: int = -1


func _init(p_seed: int = 1, grid_w: int = 100, grid_h: int = 100) -> void:
	seed_value = p_seed
	rng = SimRng.new(p_seed)
	clock = SimClock.new()
	grid = SimGrid.new(grid_w, grid_h)
	events = SimEventBus.new()
	ledger = Ledger.new(STARTING_BALANCE)
	construction_queue = ConstructionQueue.new()
	_refresh_rooms()


## Advance the world by exactly one fixed tick.
func tick() -> void:
	clock.advance()
	# Future systems (needs, schedule, tension, economy) hook in here, in a
	# fixed order — order is part of determinism.
	construction_queue.tick(grid, ledger, events)
	if grid.grid_version != _rooms_grid_version:
		_refresh_rooms()
	if clock.tick_count % SimClock.TICKS_PER_SIM_MINUTE == 0:
		events.emit("minute_passed", {"minute": clock.minute_of_day(), "day": clock.day()})
	if clock.minute_of_day() == 0 and clock.tick_count % (SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY) == 0:
		events.emit("day_passed", {"day": clock.day()})


func _refresh_rooms() -> void:
	rooms = RoomDetector.detect(grid)
	_rooms_grid_version = grid.grid_version
	events.emit("rooms_changed", {"count": rooms.size()})


func room_at(x: int, y: int) -> RoomInfo:
	var id := grid.tile_at(x, y).room_id
	for r in rooms:
		if r.id == id:
			return r
	return null


## Assigns a zone kind to every tile of the given room, then re-derives
## rooms immediately so callers see updated validity without waiting a tick.
func zone_room(room_id: int, zone_kind: int) -> bool:
	for r in rooms:
		if r.id == room_id:
			grid.set_zone(r.tiles, zone_kind)
			_refresh_rooms()
			return true
	return false


## Serialize the full world state. Event subscribers and the derived rooms
## cache are runtime-only and intentionally excluded.
func to_dict() -> Dictionary:
	return {
		"seed_value": seed_value,
		"rng": rng.to_dict(),
		"clock": clock.to_dict(),
		"grid": grid.to_dict(),
		"ledger": ledger.to_dict(),
		"construction_queue": construction_queue.to_dict(),
	}


func from_dict(d: Dictionary) -> void:
	seed_value = int(d.get("seed_value", 1))
	rng.from_dict(d.get("rng", {}))
	clock.from_dict(d.get("clock", {}))
	grid.from_dict(d.get("grid", {}))
	ledger.from_dict(d.get("ledger", {}))
	construction_queue.from_dict(d.get("construction_queue", {}))
	_refresh_rooms()


## Stable hash of the full serialized state, for determinism tests.
func state_hash() -> String:
	return JSON.stringify(to_dict()).sha256_text()
