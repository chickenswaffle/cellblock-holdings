extends GutTest
## M4 DoD, and the hardest claim in the plan: "An understaffed, overcrowded
## prison reliably riots within 5 sim-days; a well-run one doesn't. Both true
## across 20 seeds."
##
## This is the test that says the conflict model is a *model* and not a
## random number generator. Two facilities differing only in staffing and
## crowding have to produce opposite outcomes, every seed, no exceptions.

const SEEDS := 20
const DAYS := 5
const TICKS_PER_DAY := SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY

## 20 seeds x 5 sim-days x 2 scenarios is 100 simulated days, and the
## dominant cost is A* — which scales with map area, not with anything this
## test is actually about. Hence a purpose-built compact facility rather than
## FacilityBuilder's 80-wide one: same dynamics, a fraction of the runtime.
## Population still clears FactionSystem.MIN_POPULATION comfortably.
const POPULATION := 12
const OVERCROWD_EXTRA := 10
const BLOCKS := 3
const BEDS_PER_BLOCK := 4


## Three cell blocks, a canteen and a yard packed into 28x16.
func _base_world(seed_value: int) -> SimWorld:
	var world := SimWorld.new(seed_value, 28, 16)
	var g := world.grid

	for i in range(BLOCKS):
		var x0 := 1 + i * 6
		var x1 := x0 + 4
		FacilityBuilder.build_box(g, x0, 1, x1, 2)
		g.set_door(x0 + 2, 2, SimTile.WALL_S, true)
		for b in range(BEDS_PER_BLOCK):
			g.place_object(x0 + b, 1, ObjectDef.Type.BED)
		g.place_object(x1, 1, ObjectDef.Type.TOILET)

	FacilityBuilder.build_box(g, 1, 5, 12, 8)
	g.set_door(6, 5, SimTile.WALL_N, true)
	for t in range(3):
		g.place_object(2 + t * 4, 7, ObjectDef.Type.TABLE)

	FacilityBuilder.build_box(g, 15, 5, 25, 12)
	g.set_door(15, 8, SimTile.WALL_W, true)
	g.place_object(18, 8, ObjectDef.Type.WEIGHT_BENCH)
	g.place_object(21, 8, ObjectDef.Type.WEIGHT_BENCH)

	world.tick() # one room-detection pass before zoning
	for i in range(BLOCKS):
		world.zone_room(world.room_at(1 + i * 6 + 1, 1).id, ZoneValidator.Kind.CELL)
	world.zone_room(world.room_at(3, 7).id, ZoneValidator.Kind.CANTEEN)
	world.zone_room(world.room_at(18, 8).id, ZoneValidator.Kind.YARD)

	world.gate_tile = Vector2i(26, 14)
	return world


## Packed in past capacity, one guard on paper who is never where the trouble
## is, and nobody to talk anyone down.
func _failing_prison(seed_value: int) -> SimWorld:
	var world := _base_world(seed_value)
	FacilityBuilder.intake_n(world, POPULATION)
	Crew.overcrowd(world, OVERCROWD_EXTRA)
	return world


## Same building, same population, properly run: everyone has their own bed,
## guards on both shifts, and support staff on hand.
func _well_run_prison(seed_value: int) -> SimWorld:
	var world := _base_world(seed_value)
	FacilityBuilder.intake_n(world, POPULATION)
	Crew.staff_up(world, Staff.Role.GUARD, 4, world.gate_tile)
	Crew.staff_up(world, Staff.Role.SUPPORT, 2, world.gate_tile)
	for s in world.staff:
		s.shift = Staff.Shift.NIGHT if s.id % 2 == 1 else Staff.Shift.DAY
	world.ledger.deposit(200000, "well-funded operator")
	return world


## Runs up to `days`, stopping the moment a riot breaks out. Returns the sim
## day the riot started, or -1 if it never did.
func _day_of_first_riot(world: SimWorld, days: int) -> int:
	for i in range(TICKS_PER_DAY * days):
		world.tick()
		if world.has_active_riot():
			return world.clock.day()
	return -1


func test_understaffed_overcrowded_prison_riots_within_five_days() -> void:
	var failures: Array[String] = []
	for s in range(SEEDS):
		var world := _failing_prison(1000 + s)
		var day := _day_of_first_riot(world, DAYS)
		if day < 0:
			failures.append("seed %d: no riot in %d days (peak tension %.2f)" % [
				1000 + s, DAYS, world.tension.peak(),
			])
	assert_eq(failures.size(), 0, "every seed must riot:\n" + "\n".join(failures))


func test_well_run_prison_does_not_riot_in_five_days() -> void:
	var failures: Array[String] = []
	for s in range(SEEDS):
		var world := _well_run_prison(1000 + s)
		var day := _day_of_first_riot(world, DAYS)
		if day >= 0:
			failures.append("seed %d: rioted on day %d" % [1000 + s, day])
	assert_eq(failures.size(), 0, "no seed may riot:\n" + "\n".join(failures))


## The two scenarios must differ because of *staffing and crowding*, not
## because one of them happens to be quiet. If the failing prison never even
## gets tense, the test above would pass for the wrong reason.
func test_the_two_scenarios_actually_diverge() -> void:
	var failing := _failing_prison(7)
	var well_run := _well_run_prison(7)
	for i in range(TICKS_PER_DAY * 2):
		failing.tick()
		well_run.tick()

	assert_gt(failing.tension.peak(), 0.5, "the failing prison should be visibly tense by day 2")
	assert_lt(well_run.tension.peak(), 0.4, "the well-run one should be under the spark threshold")
	assert_gt(
		_avg_grievance(failing), _avg_grievance(well_run) + 0.2,
		"grievance is what separates them, and it should be a wide gap"
	)


func _avg_grievance(world: SimWorld) -> float:
	if world.prisoners.is_empty():
		return 0.0
	var total := 0.0
	for p in world.prisoners:
		total += p.grievance
	return total / float(world.prisoners.size())
