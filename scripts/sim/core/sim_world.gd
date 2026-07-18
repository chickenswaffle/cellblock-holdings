class_name SimWorld
extends RefCounted
## Root of the simulation. tick() is the ONLY mutation entry point; the view
## calls it on a fixed timestep and never writes state directly.
## Everything savable is reachable from here.

const STARTING_BALANCE := 50000
const BUS_ARRIVAL_HOUR := 9
const BUS_ARRIVAL_MIN := 1
const BUS_ARRIVAL_MAX := 3

var seed_value: int
var rng: SimRng
var clock: SimClock
var grid: SimGrid
var events: SimEventBus
var ledger: Ledger
var construction_queue: ConstructionQueue
var schedule := ScheduleSystem.new()
var prisoners: Array[Prisoner] = []
var next_prisoner_id: int = 0

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
	# Future systems (tension, economy events) hook in here, in a fixed
	# order — order is part of determinism.
	construction_queue.tick(grid, ledger, events)
	if grid.grid_version != _rooms_grid_version:
		_refresh_rooms()

	for p in prisoners:
		if p.action_state == Prisoner.ActionState.TRAVELING:
			p.step_along_path()
			if p.has_arrived():
				UtilityAI.start_performing(p)

	if clock.tick_count % SimClock.TICKS_PER_SIM_MINUTE == 0:
		NeedSystem.minute_tick(self)
		events.emit("minute_passed", {"minute": clock.minute_of_day(), "day": clock.day()})
		if clock.minute_of_day() == BUS_ARRIVAL_HOUR * 60:
			_run_bus_arrival()
	if clock.minute_of_day() == 0 and clock.tick_count % (SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY) == 0:
		events.emit("day_passed", {"day": clock.day()})


func _run_bus_arrival() -> void:
	var count := rng.randi_between(BUS_ARRIVAL_MIN, BUS_ARRIVAL_MAX)
	for i in range(count):
		if not Intake.intake(self):
			break


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


func prisoner_at(id: int) -> Prisoner:
	for p in prisoners:
		if p.id == id:
			return p
	return null


func nearest_prisoner(world_pos: Vector2, max_dist: float) -> Prisoner:
	var best: Prisoner = null
	var best_dist := max_dist * max_dist
	for p in prisoners:
		var d := p.pos.distance_squared_to(world_pos)
		if d < best_dist:
			best_dist = d
			best = p
	return best


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
	var prisoner_data: Array = []
	for p in prisoners:
		prisoner_data.append(p.to_dict())
	return {
		"seed_value": seed_value,
		"rng": rng.to_dict(),
		"clock": clock.to_dict(),
		"grid": grid.to_dict(),
		"ledger": ledger.to_dict(),
		"construction_queue": construction_queue.to_dict(),
		"prisoners": prisoner_data,
		"next_prisoner_id": next_prisoner_id,
	}


func from_dict(d: Dictionary) -> void:
	seed_value = int(d.get("seed_value", 1))
	rng.from_dict(d.get("rng", {}))
	clock.from_dict(d.get("clock", {}))
	grid.from_dict(d.get("grid", {}))
	ledger.from_dict(d.get("ledger", {}))
	construction_queue.from_dict(d.get("construction_queue", {}))
	next_prisoner_id = int(d.get("next_prisoner_id", 0))
	prisoners.clear()
	for pd in d.get("prisoners", []):
		prisoners.append(Prisoner.from_dict(pd))
	_refresh_rooms()


## Stable hash of the full serialized state, for determinism tests.
func state_hash() -> String:
	return JSON.stringify(to_dict()).sha256_text()
