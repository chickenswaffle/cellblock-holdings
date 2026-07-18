extends GutTest


func test_default_schedule_has_24_entries() -> void:
	var s := ScheduleSystem.new()
	assert_eq(s.schedule.size(), 24)


func test_block_at_hour() -> void:
	var s := ScheduleSystem.new()
	assert_eq(s.block_at_hour(2), ScheduleSystem.Block.SLEEP)
	assert_eq(s.block_at_hour(7), ScheduleSystem.Block.WORK)


func test_block_at_hour_clamps_out_of_range() -> void:
	var s := ScheduleSystem.new()
	assert_eq(s.block_at_hour(30), s.block_at_hour(23))
	assert_eq(s.block_at_hour(-5), s.block_at_hour(0))


func _build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


func _make_cell(world: SimWorld, x0: int, y0: int) -> void:
	_build_box(world.grid, x0, y0, x0 + 2, y0 + 2)
	world.grid.set_door(x0 + 1, y0 + 2, SimTile.WALL_S, true)
	world.grid.place_object(x0, y0, ObjectDef.Type.BED)
	world.grid.place_object(x0 + 1, y0, ObjectDef.Type.TOILET)
	world.tick()
	var room := world.room_at(x0 + 1, y0 + 1)
	world.zone_room(room.id, ZoneValidator.Kind.CELL)


func test_intake_fails_with_no_beds() -> void:
	var world := SimWorld.new(1, 20, 20)
	assert_false(Intake.intake(world))
	assert_eq(world.prisoners.size(), 0)


func test_intake_assigns_a_bed() -> void:
	var world := SimWorld.new(1, 20, 20)
	_make_cell(world, 2, 2)
	var ok := Intake.intake(world)
	assert_true(ok)
	assert_eq(world.prisoners.size(), 1)
	var p := world.prisoners[0]
	assert_ne(p.cell_bed_pos, Vector2i(-1, -1))
	var bed := world.grid.object_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
	assert_eq(bed.owner_id, p.id)


func test_intake_stops_when_beds_run_out() -> void:
	var world := SimWorld.new(1, 20, 20)
	_make_cell(world, 2, 2)
	assert_true(Intake.intake(world))
	assert_false(Intake.intake(world), "only one bed exists")
	assert_eq(world.prisoners.size(), 1)


func test_intake_only_uses_sealed_cell_beds() -> void:
	var world := SimWorld.new(1, 20, 20)
	# A bed sitting in the open, not inside any Cell room.
	world.grid.place_object(10, 10, ObjectDef.Type.BED)
	assert_false(Intake.intake(world))


func test_generated_prisoners_are_unique_ids() -> void:
	var world := SimWorld.new(1, 20, 20)
	_make_cell(world, 2, 2)
	_make_cell(world, 8, 2)
	Intake.intake(world)
	Intake.intake(world)
	assert_ne(world.prisoners[0].id, world.prisoners[1].id)


func test_bus_arrival_fills_cells_over_time() -> void:
	var world := SimWorld.new(7, 20, 20)
	for i in range(3):
		_make_cell(world, 2 + i * 4, 2)
	var ticks_per_day := SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY
	for i in range(ticks_per_day * 3):
		world.tick()
	assert_gt(world.prisoners.size(), 0, "bus arrivals should have intaken someone within 3 days")
	assert_lte(world.prisoners.size(), 3, "never more prisoners than beds")
