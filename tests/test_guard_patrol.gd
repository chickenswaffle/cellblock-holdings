extends GutTest
## M3 DoD, second clause: "Guards patrol routes."
##
## The route is derived from the room layout rather than drawn by the player,
## so these tests pin down both halves: that the derivation picks the right
## rooms, and that a guard actually walks the thing over sim-time.


## Two sealed, zoned rooms side by side with a door each, plus a staff room.
func _facility(world: SimWorld) -> void:
	var g := world.grid
	FacilityBuilder.build_box(g, 2, 2, 6, 5)
	g.set_door(2, 3, SimTile.WALL_W, true)
	g.place_object(3, 3, ObjectDef.Type.BED)
	g.place_object(4, 3, ObjectDef.Type.TOILET)

	FacilityBuilder.build_box(g, 10, 2, 15, 6)
	g.set_door(10, 4, SimTile.WALL_W, true)
	g.place_object(12, 4, ObjectDef.Type.TABLE)

	world.tick()
	world.zone_room(world.room_at(4, 3).id, ZoneValidator.Kind.CELL)
	world.zone_room(world.room_at(12, 4).id, ZoneValidator.Kind.CANTEEN)


func test_route_covers_zoned_rooms_in_priority_order() -> void:
	var world := SimWorld.new(1, 24, 24)
	_facility(world)
	var route := world.patrol_route()
	assert_eq(route.size(), 2, "one waypoint per zoned room")
	assert_true(world.grid.tile_at(route[0].x, route[0].y).zone_kind == ZoneValidator.Kind.CELL,
		"cells come first in PATROL_ZONES")
	assert_true(world.grid.tile_at(route[1].x, route[1].y).zone_kind == ZoneValidator.Kind.CANTEEN)


func test_unzoned_facility_has_no_route() -> void:
	var world := SimWorld.new(2, 24, 24)
	FacilityBuilder.build_box(world.grid, 2, 2, 6, 5)
	world.grid.set_door(2, 3, SimTile.WALL_W, true)
	world.tick()
	assert_eq(world.patrol_route().size(), 0, "nothing zoned, nothing to patrol")


func test_route_redderives_when_the_player_zones_a_new_room() -> void:
	var world := SimWorld.new(3, 24, 24)
	_facility(world)
	assert_eq(world.patrol_route().size(), 2)

	FacilityBuilder.build_box(world.grid, 2, 10, 7, 14)
	world.grid.set_door(2, 12, SimTile.WALL_W, true)
	world.grid.place_object(4, 12, ObjectDef.Type.WEIGHT_BENCH)
	world.tick()
	world.zone_room(world.room_at(4, 12).id, ZoneValidator.Kind.YARD)
	assert_eq(world.patrol_route().size(), 3, "route picks up the new yard on its own")


func test_a_guard_walks_the_route_and_loops() -> void:
	var world := SimWorld.new(4, 24, 24)
	_facility(world)
	world.gate_tile = Vector2i(1, 3)
	Crew.set_hour(world, 8)
	var guard := Crew.staff_up(world, Staff.Role.GUARD, 1, Vector2i(1, 3))[0]

	var visited := {}
	var route := world.patrol_route()
	# ~4 sim-hours: comfortably several laps of a two-stop route, and short
	# of the fatigue threshold where they'd break off for a rest.
	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 60 * 4):
		world.tick()
		for wp in route:
			if guard.tile_pos() == wp:
				visited[wp] = int(visited.get(wp, 0)) + 1

	assert_eq(visited.size(), route.size(), "guard reached every waypoint")
	for wp in visited:
		assert_gt(int(visited[wp]), 1, "and came back around to %s" % wp)


func test_guard_presence_reports_who_is_covering_a_room() -> void:
	var world := SimWorld.new(5, 24, 24)
	_facility(world)
	var cell := world.room_at(4, 3)
	assert_eq(world.guard_presence(cell), 0, "nobody hired yet")

	var guard := Crew.staff_up(world, Staff.Role.GUARD, 1, StaffAI.room_center(cell))[0]
	guard.state = Staff.State.PATROLLING
	assert_eq(world.guard_presence(cell), 1)

	guard.place_at_tile(Vector2i(22, 22))
	assert_eq(world.guard_presence(cell), 0, "too far away to count")

	guard.place_at_tile(StaffAI.room_center(cell))
	guard.state = Staff.State.OFF_DUTY
	assert_eq(world.guard_presence(cell), 0, "off-duty guards aren't cover")


func test_guards_without_a_route_stay_put_instead_of_erroring() -> void:
	var world := SimWorld.new(6, 24, 24)
	Crew.set_hour(world, 8)
	var guard := Crew.staff_up(world, Staff.Role.GUARD, 1, Vector2i(5, 5))[0]
	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 30):
		world.tick()
	assert_eq(guard.state, Staff.State.IDLE)
	assert_eq(guard.tile_pos(), Vector2i(5, 5))
