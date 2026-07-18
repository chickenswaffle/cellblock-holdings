class_name FacilityBuilder
extends RefCounted
## Shared test fixture: a row of 5-bed dorms, a canteen, and a small yard,
## built directly via grid mutation (no construction queue — tests want it
## built instantly). Size scales with bed_capacity so the same helper
## serves both the 50-prisoner DoD test and the 200-agent perf test.

const BEDS_PER_DORM := 5


## grid should be at least (2 + dorm_count*8 + 5) wide and ~25 tall.
static func build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


## dorm_count dorms of BEDS_PER_DORM beds each = dorm_count*5 total capacity.
static func build(world: SimWorld, dorm_count: int) -> void:
	var g := world.grid
	for i in range(dorm_count):
		var x0 := 2 + i * 8
		var x1 := x0 + 5
		build_box(g, x0, 2, x1, 3)
		g.set_door(x0 + 2, 3, SimTile.WALL_S, true)
		for b in range(BEDS_PER_DORM):
			g.place_object(x0 + b, 2, ObjectDef.Type.BED)
		g.place_object(x1, 2, ObjectDef.Type.TOILET)

	var canteen_width := maxi(40, dorm_count * 2)
	build_box(g, 2, 6, 2 + canteen_width, 10)
	g.set_door(2 + canteen_width / 2, 6, SimTile.WALL_N, true)
	var table_count := maxi(10, dorm_count / 2)
	for t in range(table_count):
		g.place_object(4 + t * 4, 8, ObjectDef.Type.TABLE)

	var yard_x0 := 2 + canteen_width + 4
	build_box(g, yard_x0, 6, yard_x0 + 20, 15)
	g.set_door(yard_x0, 10, SimTile.WALL_W, true)
	for b in range(maxi(3, dorm_count / 8)):
		g.place_object(yard_x0 + 3 + b * 3, 9, ObjectDef.Type.WEIGHT_BENCH)

	world.tick()

	for i in range(dorm_count):
		var x0 := 2 + i * 8
		var room := world.room_at(x0 + 2, 2)
		if room != null:
			world.zone_room(room.id, ZoneValidator.Kind.CELL)
	var canteen := world.room_at(4, 8)
	if canteen != null:
		world.zone_room(canteen.id, ZoneValidator.Kind.CANTEEN)
	var yard := world.room_at(yard_x0 + 3, 9)
	if yard != null:
		world.zone_room(yard.id, ZoneValidator.Kind.YARD)


## Calls Intake.intake() exactly n times (stops early if beds run out).
static func intake_n(world: SimWorld, n: int) -> void:
	for i in range(n):
		if not Intake.intake(world):
			break


static func fill_all_beds(world: SimWorld) -> void:
	while Intake.intake(world):
		pass
