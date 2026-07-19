class_name StaffAI
extends RefCounted
## Decides what each on-shift staffer does next. Deliberately simpler than
## UtilityAI: staff have jobs, not needs, so this is a role-driven state
## machine rather than a scoring pass.
##
##   WORKER  — claims the oldest unclaimed BuildOrder, walks to it, works.
##   GUARD   — walks an auto-derived patrol route over the facility's zoned
##             rooms, looping forever. Guard presence per room is what M4's
##             tension model reads.
##   SUPPORT — posts up in a canteen; prisoners eating in a staffed canteen
##             satisfy hunger faster (NeedSystem applies the bonus).
##
## Any staffer past BREAK_AT_FATIGUE drops what they're doing and walks to a
## staff room until they're rested. That's the intended feedback loop: too
## few staff for the site means everyone is always on the edge of a break.

## Rooms a patrol route visits, in zone-kind priority order. Staff rooms are
## excluded on purpose — a route through the break room is not a patrol.
const PATROL_ZONES := [
	ZoneValidator.Kind.CELL, ZoneValidator.Kind.CANTEEN, ZoneValidator.Kind.YARD,
	ZoneValidator.Kind.WORKSHOP, ZoneValidator.Kind.SOLITARY, ZoneValidator.Kind.VISITATION,
]
## A guard counts as "present" in a room this far from its patrol point.
const PRESENCE_RADIUS := 6.0


## Pick the next action for one on-shift staffer. Called once per sim minute
## and whenever they finish travelling.
static func reassess(world: SimWorld, s: Staff) -> void:
	if s.state == Staff.State.RESTING:
		if s.fatigue > Staff.RESUME_AT_FATIGUE:
			return
		s.state = Staff.State.IDLE

	if s.needs_break():
		_go_on_break(world, s)
		return

	# Let a walk finish before deciding again — on_arrived() picks up from
	# there. Without this a guard would re-target every sim minute and never
	# reach a waypoint.
	if s.state == Staff.State.TRAVELING:
		return

	match s.role:
		Staff.Role.WORKER:
			_work_next_job(world, s)
		Staff.Role.GUARD:
			_patrol(world, s)
		Staff.Role.SUPPORT:
			_staff_the_canteen(world, s)


## Send a staffer home: claims dropped, path cleared, parked at the gate.
static func go_off_duty(world: SimWorld, s: Staff) -> void:
	if s.state == Staff.State.OFF_DUTY:
		return
	world.construction_queue.release_claims_of(s.id)
	s.job_order_id = -1
	s.clear_path()
	s.state = Staff.State.OFF_DUTY
	s.place_at_tile(world.gate_tile)


static func _go_on_break(world: SimWorld, s: Staff) -> void:
	world.construction_queue.release_claims_of(s.id)
	s.job_order_id = -1
	var room_tile := world.nearest_zone_tile(s.pos, ZoneValidator.Kind.STAFF_ROOM)
	# No staff room built yet: they rest where they stand, just more slowly
	# than a proper break would allow. Building one is the fix.
	if room_tile.x < 0 or not s.set_destination(world.grid, room_tile):
		s.clear_path()
		s.state = Staff.State.RESTING
		return
	if s.has_arrived():
		s.state = Staff.State.RESTING
	else:
		s.state = Staff.State.TRAVELING


static func _work_next_job(world: SimWorld, s: Staff) -> void:
	var order: BuildOrder = null
	if s.job_order_id >= 0:
		order = world.construction_queue.order_by_id(s.job_order_id)
		if order == null or order.claimed_by != s.id:
			s.job_order_id = -1
	if s.job_order_id < 0:
		order = world.construction_queue.claim_next(s.id)
		if order == null:
			s.state = Staff.State.IDLE
			s.clear_path()
			return
		s.job_order_id = order.id

	if s.tile_pos() == order.tile():
		s.clear_path()
		s.state = Staff.State.WORKING
		return
	if not s.set_destination(world.grid, order.tile()):
		# Walled off with no route in. Park the order rather than spinning on
		# it — reset_blocked_claims() frees it again the moment the map changes.
		order.claimed_by = ConstructionQueue.BLOCKED_CLAIM
		s.job_order_id = -1
		s.state = Staff.State.IDLE
		return
	s.state = Staff.State.TRAVELING


static func _patrol(world: SimWorld, s: Staff) -> void:
	var route := world.patrol_route()
	if route.is_empty():
		s.state = Staff.State.IDLE
		s.clear_path()
		return
	s.patrol_index = (s.patrol_index + 1) % route.size()
	var target: Vector2i = route[s.patrol_index]
	if not s.set_destination(world.grid, target):
		s.state = Staff.State.IDLE
		return
	s.state = Staff.State.PATROLLING if s.has_arrived() else Staff.State.TRAVELING


static func _staff_the_canteen(world: SimWorld, s: Staff) -> void:
	var target := world.nearest_zone_tile(s.pos, ZoneValidator.Kind.CANTEEN)
	if target.x < 0:
		target = world.nearest_zone_tile(s.pos, ZoneValidator.Kind.STAFF_ROOM)
	if target.x < 0 or not s.set_destination(world.grid, target):
		s.state = Staff.State.IDLE
		s.clear_path()
		return
	s.state = Staff.State.WORKING if s.has_arrived() else Staff.State.TRAVELING


## What a staffer does the instant their path completes. Travelling is the
## only state that resolves per-tick rather than per-minute, so that arriving
## at a build site starts work immediately instead of up to a minute later.
static func on_arrived(world: SimWorld, s: Staff) -> void:
	s.clear_path()
	match s.role:
		Staff.Role.WORKER:
			if s.state == Staff.State.TRAVELING and s.job_order_id >= 0:
				s.state = Staff.State.WORKING
			elif s.needs_break():
				s.state = Staff.State.RESTING
			else:
				reassess(world, s)
		Staff.Role.GUARD:
			s.state = Staff.State.RESTING if s.needs_break() else Staff.State.PATROLLING
		Staff.Role.SUPPORT:
			s.state = Staff.State.RESTING if s.needs_break() else Staff.State.WORKING


## Ordered patrol waypoints: one representative tile per sealed, zoned,
## patrol-worthy room. Derived from the room list, so it re-derives itself
## whenever the player builds or rezones — there is no patrol editor yet.
static func build_patrol_route(world: SimWorld) -> Array[Vector2i]:
	var route: Array[Vector2i] = []
	for zone_kind in PATROL_ZONES:
		for r in world.rooms:
			if r.zone_kind == zone_kind and r.sealed and not r.tiles.is_empty():
				route.append(room_center(r))
	return route


## Tile nearest the room's centroid that is actually part of the room —
## an L-shaped room's centroid can fall outside it.
static func room_center(r: RoomInfo) -> Vector2i:
	var sum := Vector2.ZERO
	for t in r.tiles:
		sum += Vector2(t)
	var centroid := sum / float(r.tiles.size())
	var best: Vector2i = r.tiles[0]
	var best_dist := INF
	for t in r.tiles:
		var d: float = centroid.distance_squared_to(Vector2(t))
		if d < best_dist:
			best_dist = d
			best = t
	return best
