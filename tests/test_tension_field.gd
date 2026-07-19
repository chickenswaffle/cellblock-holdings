extends GutTest
## The tension model in isolation: what drives local pressure, and how it
## spreads. The DoD test proves the whole thing behaves; these pin down why.


## Two rooms sharing a wall, with a door between them, plus a corridor out.
func _two_room_world() -> SimWorld:
	var world := SimWorld.new(1, 24, 16)
	var g := world.grid
	FacilityBuilder.build_box(g, 2, 2, 6, 5)
	FacilityBuilder.build_box(g, 7, 2, 11, 5)
	g.set_door(6, 3, SimTile.WALL_E, true) # room A <-> room B
	g.set_door(2, 4, SimTile.WALL_W, true) # room A <-> outside
	for b in range(2):
		g.place_object(3 + b, 3, ObjectDef.Type.BED)
		g.place_object(8 + b, 3, ObjectDef.Type.BED)
	world.tick()
	return world


func _room_a(world: SimWorld) -> RoomInfo:
	return world.room_at(4, 3)


func _room_b(world: SimWorld) -> RoomInfo:
	return world.room_at(9, 3)


func _put_angry_prisoners_in(world: SimWorld, tile: Vector2i, count: int, grievance: float) -> void:
	for i in range(count):
		var p := Intake.generate_prisoner(world)
		p.place_at_tile(tile)
		p.grievance = grievance
		world.prisoners.append(p)


func test_rooms_are_detected_as_neighbours_through_a_door() -> void:
	var world := _two_room_world()
	var a := _room_a(world)
	var b := _room_b(world)
	assert_ne(a.id, b.id, "the door should not merge them into one room")
	var links: Array = world.room_adjacency().get(a.id, [])
	var found := false
	for link: Dictionary in links:
		if int(link["id"]) == b.id:
			found = true
			assert_almost_eq(float(link["weight"]), TensionField.DOOR_WEIGHT, 0.001,
				"a door conducts more than a wall")
	assert_true(found, "room A should be adjacent to room B")


func test_empty_rooms_generate_no_pressure() -> void:
	var world := _two_room_world()
	assert_almost_eq(world.tension.local_pressure(world, _room_a(world)), 0.0, 0.001)


func test_grievance_and_crowding_both_raise_local_pressure() -> void:
	var world := _two_room_world()
	var room := _room_a(world)

	_put_angry_prisoners_in(world, Vector2i(4, 3), 2, 0.0)
	var calm := world.tension.local_pressure(world, room)

	world.prisoners.clear()
	_put_angry_prisoners_in(world, Vector2i(4, 3), 2, 0.9)
	var angry := world.tension.local_pressure(world, room)
	assert_gt(angry, calm, "angry occupants raise pressure")

	world.prisoners.clear()
	_put_angry_prisoners_in(world, Vector2i(4, 3), 6, 0.9) # 6 bodies, 2 beds
	assert_gt(world.tension.local_pressure(world, room), angry, "and overcrowding raises it further")


func test_guards_reduce_local_pressure() -> void:
	var world := _two_room_world()
	var room := _room_a(world)
	_put_angry_prisoners_in(world, Vector2i(4, 3), 4, 0.9)
	var unguarded := world.tension.local_pressure(world, room)

	var guard := Crew.staff_up(world, Staff.Role.GUARD, 1, world.room_center(room))[0]
	guard.state = Staff.State.PATROLLING
	var guarded := world.tension.local_pressure(world, room)
	assert_lt(guarded, unguarded, "a guard on the block takes the edge off")
	assert_almost_eq(unguarded - guarded, TensionField.GUARD_CALM, 0.001)


## A single sealed room with nothing adjacent — no diffusion, so tension has
## to converge on exactly the local pressure.
func test_an_isolated_room_settles_at_exactly_its_local_pressure() -> void:
	var world := SimWorld.new(1, 20, 12)
	FacilityBuilder.build_box(world.grid, 2, 2, 6, 5)
	world.grid.set_door(2, 4, SimTile.WALL_W, true)
	for b in range(2):
		world.grid.place_object(3 + b, 3, ObjectDef.Type.BED)
	world.tick()
	var room := world.room_at(4, 3)

	_put_angry_prisoners_in(world, Vector2i(4, 3), 4, 0.8)
	var target := world.tension.local_pressure(world, room)
	assert_gt(target, 0.2, "fixture should generate real pressure")

	for i in range(900): # 15 sim hours of minutes
		world.tension.minute_tick(world)
	assert_almost_eq(world.tension.value_for(room), target, 0.02,
		"nothing to bleed into, so it lands on its own pressure")


func test_tension_spreads_to_the_room_next_door() -> void:
	var world := _two_room_world()
	_put_angry_prisoners_in(world, Vector2i(4, 3), 4, 0.9)
	for i in range(600):
		world.tension.minute_tick(world)

	var hot := world.tension.value_for(_room_a(world))
	var next_door := world.tension.value_for(_room_b(world))
	assert_gt(hot, 0.2, "the occupied room is tense")
	assert_gt(next_door, 0.0, "and it bleeds through the door")
	assert_lt(next_door, hot, "but the empty room stays calmer than the source")


## Regression guard for the bug that made the whole field meaningless: the
## unsealed outdoors is adjacent to nearly every room and holds no tension,
## so letting it diffuse pinned every room at ~0.09 no matter how bad things
## got. Tension must move between built rooms only.
## The bug pinned every room at RESPONSE_RATE/DIFFUSION_RATE regardless of
## how bad conditions got — about 0.09 with the constants of the day, and it
## made the whole field a constant. Any room this angry must end up far above
## that, whatever else diffusion does.
func test_the_outdoors_is_not_an_infinite_heat_sink() -> void:
	var world := _two_room_world()
	var room := _room_a(world)
	_put_angry_prisoners_in(world, Vector2i(4, 3), 6, 1.0)
	assert_gt(world.tension.local_pressure(world, room), 0.5, "fixture should be dire")
	for i in range(900):
		world.tension.minute_tick(world)
	assert_gt(world.tension.value_for(room), 0.3,
		"room has a door to the outside; tension must not drain away through it")


func test_violence_memory_decays() -> void:
	var world := _two_room_world()
	var room_key := _room_a(world).key()
	world.tension.add_violence(room_key, 1.0)
	assert_almost_eq(float(world.tension.violence[room_key]), 1.0, 0.001)
	for i in range(int(TensionField.VIOLENCE_DECAY_MINUTES) + 1):
		world.tension.minute_tick(world)
	assert_false(world.tension.violence.has(room_key), "it should fade entirely")


func test_demolished_rooms_stop_counting_toward_peak() -> void:
	var world := _two_room_world()
	_put_angry_prisoners_in(world, Vector2i(4, 3), 4, 0.9)
	for i in range(300):
		world.tension.minute_tick(world)
	assert_gt(world.tension.peak(), 0.0)

	world.prisoners.clear()
	for x in range(2, 7):
		world.grid.set_wall(x, 2, SimTile.WALL_N, false)
		world.grid.set_wall(x, 5, SimTile.WALL_S, false)
	world.tick()
	world.tension.minute_tick(world)
	assert_almost_eq(world.tension.peak(), 0.0, 0.05, "stale room keys must be pruned")


func test_serialization_roundtrip() -> void:
	var world := _two_room_world()
	_put_angry_prisoners_in(world, Vector2i(4, 3), 4, 0.9)
	for i in range(120):
		world.tension.minute_tick(world)
	world.tension.add_violence(_room_a(world).key(), 0.4)

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_almost_eq(restored.tension.peak(), world.tension.peak(), 0.0001)
	assert_eq(restored.tension.violence.size(), world.tension.violence.size())
