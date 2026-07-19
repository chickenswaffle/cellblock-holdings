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
var payroll: Payroll
var schedule := ScheduleSystem.new()
var prisoners: Array[Prisoner] = []
var next_prisoner_id: int = 0
var staff: Array[Staff] = []
var next_staff_id: int = 0

## Where staff clock in and out. Off-shift staff are parked here rather than
## modelled going home; bootstrap moves it next to the facility entrance.
var gate_tile: Vector2i = Vector2i.ZERO

## Derived from grid; recomputed on grid_version change, never saved directly.
var rooms: Array[RoomInfo] = []
var _rooms_grid_version: int = -1
var _patrol_route: Array[Vector2i] = []
var _patrol_route_version: int = -1


func _init(p_seed: int = 1, grid_w: int = 100, grid_h: int = 100) -> void:
	seed_value = p_seed
	rng = SimRng.new(p_seed)
	clock = SimClock.new()
	grid = SimGrid.new(grid_w, grid_h)
	events = SimEventBus.new()
	ledger = Ledger.new(STARTING_BALANCE)
	construction_queue = ConstructionQueue.new()
	payroll = Payroll.new()
	gate_tile = Vector2i(grid_w / 2, grid_h / 2)
	_refresh_rooms()


## Advance the world by exactly one fixed tick.
func tick() -> void:
	clock.advance()
	# Future systems (tension, economy events) hook in here, in a fixed
	# order — order is part of determinism. Staff run before the rooms check
	# because their construction work is what mutates the grid.
	StaffSystem.tick(self)
	if grid.grid_version != _rooms_grid_version:
		_refresh_rooms()
		construction_queue.reset_blocked_claims()

	for p in prisoners:
		if p.action_state == Prisoner.ActionState.TRAVELING:
			p.step_along_path()
			if p.has_arrived():
				UtilityAI.start_performing(p)

	if clock.tick_count % SimClock.TICKS_PER_SIM_MINUTE == 0:
		StaffSystem.minute_tick(self)
		NeedSystem.minute_tick(self)
		events.emit("minute_passed", {"minute": clock.minute_of_day(), "day": clock.day()})
		if clock.minute_of_day() == BUS_ARRIVAL_HOUR * 60:
			_run_bus_arrival()
	if clock.minute_of_day() == 0 and clock.tick_count % (SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY) == 0:
		payroll.run_day(self)
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


## Guard patrol waypoints, re-derived whenever the room layout changes.
## Derived state, like rooms — never serialized.
func patrol_route() -> Array[Vector2i]:
	if _patrol_route_version != grid.grid_version:
		_patrol_route = StaffAI.build_patrol_route(self)
		_patrol_route_version = grid.grid_version
	return _patrol_route


## Tile of the nearest room with this zone kind, or (-1, -1) if none exists.
## Shared by UtilityAI (prisoners seeking a yard) and StaffAI (staff rooms,
## canteens) so both agree on what "nearest zone" means.
func nearest_zone_tile(from: Vector2, zone_kind: int) -> Vector2i:
	var best := Vector2i(-1, -1)
	var best_dist := INF
	for r in rooms:
		if r.zone_kind != zone_kind:
			continue
		for t in r.tiles:
			var d: float = from.distance_squared_to(Vector2(t))
			if d < best_dist:
				best_dist = d
				best = t
	return best


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


func staff_at(id: int) -> Staff:
	for s in staff:
		if s.id == id:
			return s
	return null


func staff_count(role: int) -> int:
	var n := 0
	for s in staff:
		if s.role == role:
			n += 1
	return n


## On-shift staff right now, by role. What the player actually has covering
## the floor, as opposed to how many are on the books.
func on_duty_count(role: int) -> int:
	var hour := clock.hour_of_day()
	var n := 0
	for s in staff:
		if s.role == role and s.on_shift_at_hour(hour):
			n += 1
	return n


## Remove a staffer for any reason (fired, quit). Their claimed build orders
## go back in the pool with the work they'd already done intact.
func dismiss_staff(id: int, reason: String) -> bool:
	for i in range(staff.size()):
		if staff[i].id != id:
			continue
		construction_queue.release_claims_of(id)
		var s := staff[i]
		staff.remove_at(i)
		events.emit("staff_left", {"id": id, "sname": s.sname, "role": s.role, "reason": reason})
		return true
	return false


## On-shift guards patrolling within PRESENCE_RADIUS of a room's center.
## M4's tension model reads this; M3 only displays it.
func guard_presence(room: RoomInfo) -> int:
	if room == null or room.tiles.is_empty():
		return 0
	var center := Vector2(StaffAI.room_center(room)) + Vector2(0.5, 0.5)
	var radius_sq := StaffAI.PRESENCE_RADIUS * StaffAI.PRESENCE_RADIUS
	var n := 0
	for s in staff:
		if s.role != Staff.Role.GUARD or s.state == Staff.State.OFF_DUTY:
			continue
		if s.pos.distance_squared_to(center) <= radius_sq:
			n += 1
	return n


## True when a support staffer is working the canteen containing this tile —
## NeedSystem serves meals faster there.
func canteen_is_staffed(tile: Vector2i) -> bool:
	var room := room_at(tile.x, tile.y)
	if room == null or room.zone_kind != ZoneValidator.Kind.CANTEEN:
		return false
	for s in staff:
		if s.role != Staff.Role.SUPPORT or s.state != Staff.State.WORKING:
			continue
		var st := s.tile_pos()
		if grid.in_bounds(st.x, st.y) and grid.tile_at(st.x, st.y).room_id == room.id:
			return true
	return false


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
	var staff_data: Array = []
	for s in staff:
		staff_data.append(s.to_dict())
	return {
		"seed_value": seed_value,
		"rng": rng.to_dict(),
		"clock": clock.to_dict(),
		"grid": grid.to_dict(),
		"ledger": ledger.to_dict(),
		"construction_queue": construction_queue.to_dict(),
		"payroll": payroll.to_dict(),
		"prisoners": prisoner_data,
		"next_prisoner_id": next_prisoner_id,
		"staff": staff_data,
		"next_staff_id": next_staff_id,
		"gate_tile": [gate_tile.x, gate_tile.y],
	}


func from_dict(d: Dictionary) -> void:
	seed_value = int(d.get("seed_value", 1))
	rng.from_dict(d.get("rng", {}))
	clock.from_dict(d.get("clock", {}))
	grid.from_dict(d.get("grid", {}))
	ledger.from_dict(d.get("ledger", {}))
	construction_queue.from_dict(d.get("construction_queue", {}))
	payroll.from_dict(d.get("payroll", {}))
	next_prisoner_id = int(d.get("next_prisoner_id", 0))
	prisoners.clear()
	for pd in d.get("prisoners", []):
		prisoners.append(Prisoner.from_dict(pd))
	next_staff_id = int(d.get("next_staff_id", 0))
	staff.clear()
	for sd in d.get("staff", []):
		staff.append(Staff.from_dict(sd))
	var gt: Array = d.get("gate_tile", [0, 0])
	gate_tile = Vector2i(int(gt[0]), int(gt[1]))
	_refresh_rooms()


## Stable hash of the full serialized state, for determinism tests.
func state_hash() -> String:
	return JSON.stringify(to_dict()).sha256_text()
