class_name Crew
extends RefCounted
## Shared test fixture for M3 staffing. Hiring through Hiring.hire() would
## alternate shifts and charge fees, which most tests don't care about — this
## puts a known crew on duty at a known spot instead.

## Hire n staff of a role directly onto the DAY shift, on duty, standing at
## the gate. Bypasses the hiring fee so ledger assertions stay about the
## thing under test.
static func staff_up(world: SimWorld, role: int, n: int, at: Vector2i = Vector2i(-1, -1)) -> Array[Staff]:
	var out: Array[Staff] = []
	for i in range(n):
		var s := Staff.new()
		s.id = world.next_staff_id
		world.next_staff_id += 1
		s.role = role
		s.sname = "Test %s %d" % [Staff.Role.keys()[role], s.id]
		s.shift = Staff.Shift.DAY
		s.state = Staff.State.IDLE
		s.place_at_tile(at if at.x >= 0 else world.gate_tile)
		world.staff.append(s)
		out.append(s)
	return out


## Wind the clock to a given hour without running a full day of ticks, so a
## test can start mid-DAY-shift. Ticks nothing — call before the first tick.
static func set_hour(world: SimWorld, hour: int) -> void:
	world.clock.tick_count = hour * 60 * SimClock.TICKS_PER_SIM_MINUTE


## Double-bunk extra prisoners into cells that are already full — the
## overcrowding half of M4's DoD. Intake refuses to place anyone without a
## free bed (correctly), so overcrowding has to be constructed deliberately:
## these arrivals share someone else's bunk, which is exactly what pushes
## GrievanceSystem's crowding term up.
static func overcrowd(world: SimWorld, extra: int) -> int:
	var bunks: Array[Vector2i] = []
	for o in world.grid.objects:
		if o.object_type != ObjectDef.Type.BED:
			continue
		var room := world.room_at(o.x, o.y)
		if room != null and room.sealed and room.zone_kind == ZoneValidator.Kind.CELL:
			bunks.append(Vector2i(o.x, o.y))
	if bunks.is_empty():
		return 0

	for i in range(extra):
		var p := Intake.generate_prisoner(world)
		p.cell_bed_pos = bunks[i % bunks.size()]
		p.place_at_tile(p.cell_bed_pos)
		world.prisoners.append(p)
	return extra


## Tick until the construction queue drains or max_ticks passes. Returns the
## ticks actually spent, so tests can assert building took real time.
static func run_until_built(world: SimWorld, max_ticks: int) -> int:
	for i in range(max_ticks):
		if world.construction_queue.orders.is_empty():
			return i
		world.tick()
	return max_ticks
