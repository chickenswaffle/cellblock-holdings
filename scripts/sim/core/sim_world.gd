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

## Conflict layer (M4).
var tension: TensionField
var contraband: Contraband
var factions: Array[Faction] = []
var next_faction_id: int = 0
var incidents: Array[Incident] = []
var next_incident_id: int = 0
## Sim minutes of lockdown left. While > 0 everyone is confined to cells.
var lockdown_minutes: int = 0
## Running count for M6's oversight/scrutiny model to consume.
var solitary_uses: int = 0
## Incidents sparked today — reset by contract on the day rollover.
var incidents_today: int = 0

## Where staff clock in and out. Off-shift staff are parked here rather than
## modelled going home; bootstrap moves it next to the facility entrance.
var gate_tile: Vector2i = Vector2i.ZERO

## Contract / game-over state.
var contract: Contract
var game_over: bool = false
var game_over_reason: String = ""

## Derived from grid; recomputed on grid_version change, never saved directly.
var rooms: Array[RoomInfo] = []
var _rooms_grid_version: int = -1
var _patrol_route: Array[Vector2i] = []
var _patrol_route_version: int = -1
var _adjacency: Dictionary = {}
var _adjacency_version: int = -1
var _capacity: Dictionary = {}
var _capacity_version: int = -1
var _centers: Dictionary = {}
var _centers_version: int = -1


func _init(p_seed: int = 1, grid_w: int = 100, grid_h: int = 100) -> void:
	seed_value = p_seed
	rng = SimRng.new(p_seed)
	clock = SimClock.new()
	grid = SimGrid.new(grid_w, grid_h)
	events = SimEventBus.new()
	ledger = Ledger.new(STARTING_BALANCE)
	construction_queue = ConstructionQueue.new()
	payroll = Payroll.new()
	contract = Contract.new()
	tension = TensionField.new()
	contraband = Contraband.new()
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
		# Conflict runs in causal order: conditions set grievance, grievance
		# drives tension, tension sparks and escalates incidents. Changing
		# this order changes the game, not just the frame it lands on.
		if clock.minute_of_day() % 60 == 0:
			FactionSystem.hour_tick(self)
			contraband.hour_tick(self)
		GrievanceSystem.minute_tick(self)
		tension.minute_tick(self)
		IncidentSystem.minute_tick(self)
		events.emit("minute_passed", {"minute": clock.minute_of_day(), "day": clock.day()})
		if clock.minute_of_day() == BUS_ARRIVAL_HOUR * 60:
			_run_bus_arrival()
	if clock.minute_of_day() == 0 and clock.tick_count % (SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY) == 0:
		payroll.run_day(self)
		var prev_incidents := incidents_today
		contract.day_incidents = prev_incidents
		contract.run_day(self)
		incidents_today = 0
		Snitches.day_tick(self)
		events.emit("day_passed", {"day": clock.day()})
		if contract.breached:
			game_over = true
			game_over_reason = "The state pulled your contract — " + contract.last_day_reason

	if not game_over:
		_check_riot_game_over()


func _run_bus_arrival() -> void:
	var count := rng.randi_between(BUS_ARRIVAL_MIN, BUS_ARRIVAL_MAX)
	for i in range(count):
		if not Intake.intake(self):
			break


## Re-derive rooms right now. The view calls this after painting zones so the
## player sees the result immediately rather than on the next tick.
func refresh_rooms() -> void:
	_refresh_rooms()


func _refresh_rooms() -> void:
	rooms = RoomDetector.detect(grid)
	_rooms_grid_version = grid.grid_version
	events.emit("rooms_changed", {"count": rooms.size()})


## Rooms that share a blocked edge, with the weight tension diffuses across:
## a door leaks pressure between rooms far more readily than a solid wall.
## room id -> Array of {"id": int, "weight": float}. Derived, never saved.
func room_adjacency() -> Dictionary:
	if _adjacency_version != grid.grid_version:
		_adjacency = _build_adjacency()
		_adjacency_version = grid.grid_version
	return _adjacency


func _build_adjacency() -> Dictionary:
	var adj := {}
	var weights := {}
	# Only E and S neighbours: every pair is then visited exactly once, and
	# both directions get recorded below.
	for y in range(grid.height):
		for x in range(grid.width):
			var here := grid.tile_at(x, y).room_id
			for d in [Vector2i(1, 0), Vector2i(0, 1)]:
				var n := Vector2i(x + d.x, y + d.y)
				if not grid.in_bounds(n.x, n.y):
					continue
				var there := grid.tile_at(n.x, n.y).room_id
				if there == here or grid.edge_open(x, y, d.x, d.y):
					continue
				var w: float = TensionField.DOOR_WEIGHT if grid.edge_is_door(x, y, d.x, d.y) else TensionField.WALL_WEIGHT
				# Rooms joined by several doors leak more than rooms sharing
				# one — take the strongest connection, not the sum, so a long
				# shared wall doesn't out-conduct an actual doorway.
				var pair_key := [mini(here, there), maxi(here, there)]
				weights[pair_key] = maxf(float(weights.get(pair_key, 0.0)), w)

	for pair_key: Array in weights:
		var a: int = pair_key[0]
		var b: int = pair_key[1]
		var w: float = weights[pair_key]
		if not adj.has(a):
			adj[a] = []
		if not adj.has(b):
			adj[b] = []
		adj[a].append({"id": b, "weight": w})
		adj[b].append({"id": a, "weight": w})
	return adj


## Prisoners currently standing inside a room. Always accurate — it scans.
##
## Callers that need this for *every* room in the same instant (the conflict
## layer does, every sim minute) should build occupancy_index() once and read
## that instead; scanning per room is O(rooms x population).
##
## An earlier version cached this on SimWorld keyed by tick count. Don't do
## that again: prisoners are added, removed and moved mid-tick, so a cache
## keyed by time silently serves stale occupancy to whatever runs next. An
## index the caller builds and owns can't go stale behind anyone's back.
func prisoners_in_room(room: RoomInfo) -> Array[Prisoner]:
	var out: Array[Prisoner] = []
	if room == null:
		return out
	for p in prisoners:
		var t := p.tile_pos()
		if grid.in_bounds(t.x, t.y) and grid.tile_at(t.x, t.y).room_id == room.id:
			out.append(p)
	return out


## room id -> prisoners standing in it, built in one pass over the roster.
func occupancy_index() -> Dictionary:
	var index := {}
	for p in prisoners:
		var t := p.tile_pos()
		if not grid.in_bounds(t.x, t.y):
			continue
		var room_id := grid.tile_at(t.x, t.y).room_id
		if not index.has(room_id):
			index[room_id] = [] as Array[Prisoner]
		index[room_id].append(p)
	return index


## Occupants of one room out of a prebuilt index, or a live scan if the
## caller didn't bring one.
func occupants_of(room: RoomInfo, index: Dictionary) -> Array[Prisoner]:
	if room == null:
		return [] as Array[Prisoner]
	if index.is_empty():
		return prisoners_in_room(room)
	return index.get(room.id, [] as Array[Prisoner])


## Beds in a room — its designed capacity. Overcrowding is measured against
## this, so a dorm with 4 beds holding 8 bodies reads as 2.0. Cached per
## grid version; only building changes it.
func room_capacity(room: RoomInfo) -> int:
	if room == null:
		return 0
	if _capacity_version != grid.grid_version:
		_capacity = _build_capacity()
		_capacity_version = grid.grid_version
	return int(_capacity.get(room.id, 0))


func _build_capacity() -> Dictionary:
	var out := {}
	for o in grid.objects:
		if o.object_type != ObjectDef.Type.BED or not grid.in_bounds(o.x, o.y):
			continue
		var room_id := grid.tile_at(o.x, o.y).room_id
		out[room_id] = int(out.get(room_id, 0)) + 1
	return out


func room_at_id(room_id: int) -> RoomInfo:
	for r in rooms:
		if r.id == room_id:
			return r
	return null


func room_by_key(room_key: Vector2i) -> RoomInfo:
	for r in rooms:
		if r.key() == room_key:
			return r
	return null


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
## Representative tile per room, cached — StaffAI.room_center() scans every
## tile in the room twice, and a 200-tile canteen recomputing that on every
## guard_presence() call was the single most expensive thing in the conflict
## layer. Only building changes the answer.
func room_center(room: RoomInfo) -> Vector2i:
	if room == null or room.tiles.is_empty():
		return Vector2i(-1, -1)
	if _centers_version != grid.grid_version:
		_centers = {}
		_centers_version = grid.grid_version
	if not _centers.has(room.id):
		_centers[room.id] = StaffAI.room_center(room)
	return _centers[room.id]


func guard_presence(room: RoomInfo) -> int:
	if room == null or room.tiles.is_empty():
		return 0
	var center := Vector2(room_center(room)) + Vector2(0.5, 0.5)
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


## Remove a prisoner (transferred out, released). Frees their bed so intake
## can reuse it — a transfer that left the bed owned would slowly starve the
## facility of capacity.
func remove_prisoner(id: int, reason: String) -> bool:
	for i in range(prisoners.size()):
		if prisoners[i].id != id:
			continue
		var p := prisoners[i]
		if p.cell_bed_pos.x >= 0:
			var bed := grid.object_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
			if bed != null and bed.owner_id == id:
				bed.owner_id = -1
		prisoners.remove_at(i)
		events.emit("prisoner_left", {"id": id, "pname": p.pname, "reason": reason})
		return true
	return false


func is_locked_down() -> bool:
	return lockdown_minutes > 0


## The schedule block in force right now. Lockdown overrides the timetable —
## that's what a lockdown *is*.
func current_block() -> int:
	if is_locked_down():
		return ScheduleSystem.Block.LOCKUP
	return schedule.block_at_hour(clock.hour_of_day())


## Highest-severity open incident anywhere, or null.
func worst_incident() -> Incident:
	return IncidentSystem.worst_open(self)


## Facility riot lasting more than 2 sim-days = game over.
func _check_riot_game_over() -> void:
	var MIN_FACILITY_RIOT_AGE := 60 * 48
	for inc in incidents:
		if inc.is_open() and inc.kind == Incident.Kind.FACILITY_RIOT and inc.age_minutes >= MIN_FACILITY_RIOT_AGE:
			game_over = true
			game_over_reason = "Facility lost to riot — the state seized control"
			return


func has_active_riot() -> bool:
	for inc in incidents:
		if inc.is_open() and inc.is_riot():
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
	var faction_data: Array = []
	for f in factions:
		faction_data.append(f.to_dict())
	var incident_data: Array = []
	for inc in incidents:
		incident_data.append(inc.to_dict())
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
		"tension": tension.to_dict(),
		"contraband": contraband.to_dict(),
		"factions": faction_data,
		"next_faction_id": next_faction_id,
		"incidents": incident_data,
		"next_incident_id": next_incident_id,
		"lockdown_minutes": lockdown_minutes,
		"solitary_uses": solitary_uses,
		"incidents_today": incidents_today,
		"contract": contract.to_dict(),
		"game_over": game_over,
		"game_over_reason": game_over_reason,
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
	tension.from_dict(d.get("tension", {}))
	contraband.from_dict(d.get("contraband", {}))
	factions.clear()
	for fd in d.get("factions", []):
		factions.append(Faction.from_dict(fd))
	next_faction_id = int(d.get("next_faction_id", 0))
	incidents.clear()
	for incident_data in d.get("incidents", []):
		incidents.append(Incident.from_dict(incident_data))
	next_incident_id = int(d.get("next_incident_id", 0))
	lockdown_minutes = int(d.get("lockdown_minutes", 0))
	solitary_uses = int(d.get("solitary_uses", 0))
	incidents_today = int(d.get("incidents_today", 0))
	contract.from_dict(d.get("contract", {}))
	game_over = bool(d.get("game_over", false))
	game_over_reason = String(d.get("game_over_reason", ""))
	_refresh_rooms()


## Stable hash of the full serialized state, for determinism tests.
func state_hash() -> String:
	return JSON.stringify(to_dict()).sha256_text()
