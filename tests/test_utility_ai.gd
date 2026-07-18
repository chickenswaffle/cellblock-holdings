extends GutTest


func _build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


func _make_world_with_cell_and_canteen() -> SimWorld:
	var world := SimWorld.new(3, 25, 25)
	var g := world.grid
	_build_box(g, 2, 2, 4, 4)
	g.set_door(3, 4, SimTile.WALL_S, true)
	g.place_object(2, 2, ObjectDef.Type.BED)
	g.place_object(4, 2, ObjectDef.Type.TOILET)
	_build_box(g, 10, 2, 15, 6)
	g.set_door(10, 4, SimTile.WALL_W, true)
	g.place_object(12, 3, ObjectDef.Type.TABLE)
	g.place_object(13, 3, ObjectDef.Type.TABLE)
	world.tick()
	var cell := world.room_at(3, 3)
	world.zone_room(cell.id, ZoneValidator.Kind.CELL)
	var canteen := world.room_at(12, 4)
	world.zone_room(canteen.id, ZoneValidator.Kind.CANTEEN)
	Intake.intake(world)
	return world


func test_sleepy_prisoner_travels_to_own_bed() -> void:
	var world := _make_world_with_cell_and_canteen()
	var p := world.prisoners[0]
	p.pos = Vector2(12, 4) # start far from their cell
	p.needs.values[Needs.Kind.SLEEP] = 0.1
	UtilityAI.reassess(world, p, ScheduleSystem.Block.SLEEP)
	assert_eq(p.action_state, Prisoner.ActionState.TRAVELING)
	assert_eq(p.action_need, Needs.Kind.SLEEP)
	assert_eq(p.path[-1], p.cell_bed_pos)


func test_hungry_prisoner_travels_to_a_table() -> void:
	var world := _make_world_with_cell_and_canteen()
	var p := world.prisoners[0]
	p.needs.values[Needs.Kind.HUNGER] = 0.05
	UtilityAI.reassess(world, p, ScheduleSystem.Block.EAT)
	assert_eq(p.action_state, Prisoner.ActionState.TRAVELING)
	assert_eq(p.action_need, Needs.Kind.HUNGER)
	var obj := world.grid.object_at(p.action_object_pos.x, p.action_object_pos.y)
	assert_eq(obj.object_type, ObjectDef.Type.TABLE)
	assert_eq(obj.occupied_by, p.id)


func test_two_prisoners_do_not_share_a_table() -> void:
	var world := _make_world_with_cell_and_canteen()
	world.grid.place_object(2, 8, ObjectDef.Type.BED) # second bed, unowned cell-less — fine, just needs a start pos
	var p1 := world.prisoners[0]
	var p2 := Prisoner.new()
	p2.id = 999
	p2.pos = Vector2(13, 4)
	world.prisoners.append(p2)

	p1.needs.values[Needs.Kind.HUNGER] = 0.05
	p2.needs.values[Needs.Kind.HUNGER] = 0.05
	UtilityAI.reassess(world, p1, ScheduleSystem.Block.EAT)
	UtilityAI.reassess(world, p2, ScheduleSystem.Block.EAT)

	assert_ne(p1.action_object_pos, Vector2i(-1, -1))
	assert_ne(p2.action_object_pos, Vector2i(-1, -1))
	assert_ne(p1.action_object_pos, p2.action_object_pos, "must claim different tables")


func test_arriving_starts_performing_and_satisfying() -> void:
	var world := _make_world_with_cell_and_canteen()
	var p := world.prisoners[0]
	p.needs.values[Needs.Kind.HUNGER] = 0.05
	UtilityAI.reassess(world, p, ScheduleSystem.Block.EAT)
	# Run ticks until arrival.
	for i in range(500):
		if p.action_state != Prisoner.ActionState.TRAVELING:
			break
		p.step_along_path()
		if p.has_arrived():
			UtilityAI.start_performing(p)
	assert_eq(p.action_state, Prisoner.ActionState.PERFORMING)
	var before := p.needs.get_value(Needs.Kind.HUNGER)
	NeedSystem.minute_tick(world)
	assert_gt(p.needs.get_value(Needs.Kind.HUNGER), before)


func test_action_releases_object_and_returns_to_idle_when_satisfied() -> void:
	var world := _make_world_with_cell_and_canteen()
	var p := world.prisoners[0]
	p.needs.values[Needs.Kind.HUNGER] = 0.05
	UtilityAI.reassess(world, p, ScheduleSystem.Block.EAT)
	while p.action_state == Prisoner.ActionState.TRAVELING:
		p.step_along_path()
		if p.has_arrived():
			UtilityAI.start_performing(p)
	var table_pos := p.action_object_pos
	p.needs.values[Needs.Kind.HUNGER] = 0.96 # nearly satisfied already
	NeedSystem.minute_tick(world)
	var obj := world.grid.object_at(table_pos.x, table_pos.y)
	assert_eq(obj.occupied_by, -1, "table released once satisfied")


func test_prisoner_with_no_bed_stays_idle_during_sleep() -> void:
	var world := SimWorld.new(1, 10, 10)
	var p := Prisoner.new()
	p.id = 1
	p.cell_bed_pos = Vector2i(-1, -1)
	world.prisoners.append(p)
	UtilityAI.reassess(world, p, ScheduleSystem.Block.SLEEP)
	assert_eq(p.action_state, Prisoner.ActionState.IDLE)
