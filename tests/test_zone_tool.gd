extends GutTest
## Designating what an area is for — by clicking a finished room, or by
## painting a rectangle before the walls exist.


func _world() -> SimWorld:
	return SimWorld.new(1, 30, 20)


func _tool(world: SimWorld, kind: int) -> ZoneTool:
	var t := ZoneTool.new(world)
	t.zone_kind = kind
	return t


func _sealed_room(world: SimWorld) -> void:
	FacilityBuilder.build_box(world.grid, 3, 3, 7, 6)
	world.grid.set_door(3, 4, SimTile.WALL_W, true)
	world.grid.place_object(4, 4, ObjectDef.Type.BED)
	world.grid.place_object(6, 4, ObjectDef.Type.TOILET)
	world.tick()


func test_clicking_inside_a_finished_room_designates_the_whole_room() -> void:
	var world := _world()
	_sealed_room(world)
	var t := _tool(world, ZoneValidator.Kind.CELL)

	t.begin_drag(Vector2i(5, 5))
	t.end_drag() # never moved -> click
	var room := world.room_at(5, 5)
	assert_eq(room.zone_kind, ZoneValidator.Kind.CELL)
	assert_true(room.zone_valid, "sealed, with a bed and toilet, so it's a valid cell")
	# Every tile, not just the one clicked.
	assert_eq(world.grid.tile_at(4, 6).zone_kind, ZoneValidator.Kind.CELL)


func test_dragging_paints_an_arbitrary_area_with_no_walls_needed() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.CANTEEN)
	t.begin_drag(Vector2i(10, 10))
	t.update_drag(Vector2i(13, 12))
	t.end_drag()

	for y in range(10, 13):
		for x in range(10, 14):
			assert_eq(
				world.grid.tile_at(x, y).zone_kind, ZoneValidator.Kind.CANTEEN,
				"tile %d,%d should be designated" % [x, y]
			)
	assert_eq(world.grid.tile_at(14, 10).zone_kind, -1, "and nothing outside the box")


## The point of painting on open ground: mark out the layout first, then put
## walls around it and have it resolve into a real zoned room.
func test_an_area_painted_first_becomes_a_valid_room_once_enclosed() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.CELL)
	t.begin_drag(Vector2i(4, 4))
	t.update_drag(Vector2i(6, 5))
	t.end_drag()

	FacilityBuilder.build_box(world.grid, 4, 4, 6, 5)
	world.grid.set_door(4, 4, SimTile.WALL_W, true)
	world.grid.place_object(5, 4, ObjectDef.Type.BED)
	world.grid.place_object(6, 5, ObjectDef.Type.TOILET)
	world.tick()

	var room := world.room_at(5, 5)
	assert_eq(room.zone_kind, ZoneValidator.Kind.CELL, "the paint resolved into the room")
	assert_true(room.zone_valid)


func test_repainting_replaces_the_previous_designation() -> void:
	var world := _world()
	var cell := _tool(world, ZoneValidator.Kind.CELL)
	cell.begin_drag(Vector2i(10, 10))
	cell.update_drag(Vector2i(12, 11))
	cell.end_drag()

	var yard := _tool(world, ZoneValidator.Kind.YARD)
	yard.begin_drag(Vector2i(10, 10))
	yard.update_drag(Vector2i(12, 11))
	yard.end_drag()

	assert_eq(world.grid.tile_at(11, 10).zone_kind, ZoneValidator.Kind.YARD)


func test_preview_reports_the_room_for_a_click_and_the_box_for_a_drag() -> void:
	var world := _world()
	_sealed_room(world)
	var t := _tool(world, ZoneValidator.Kind.CELL)

	t.begin_drag(Vector2i(5, 5))
	var room := world.room_at(5, 5)
	assert_eq(t.preview_tiles().size(), room.tiles.size(), "a click highlights the whole room")

	t.update_drag(Vector2i(9, 8))
	assert_eq(t.preview_tiles().size(), 5 * 4, "a drag highlights just the box")


func test_painting_partly_off_the_map_keeps_what_is_on_it() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.YARD)
	t.begin_drag(Vector2i(-3, -3))
	t.update_drag(Vector2i(1, 1))
	t.end_drag()
	assert_eq(world.grid.tile_at(0, 0).zone_kind, ZoneValidator.Kind.YARD)
	assert_eq(world.grid.tile_at(1, 1).zone_kind, ZoneValidator.Kind.YARD)


func test_cancelling_a_drag_designates_nothing() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.CELL)
	t.begin_drag(Vector2i(10, 10))
	t.update_drag(Vector2i(12, 12))
	t.cancel_drag()
	t.end_drag()
	assert_eq(world.grid.tile_at(11, 11).zone_kind, -1, "zoning is free, but not accidental")


## A room whose tiles disagree resolves to "mixed" and counts as unzoned, so
## a sloppy drag across most of a finished block would otherwise silently
## un-designate it. A drag wholly inside one sealed room takes the whole room.
func test_a_partial_drag_inside_a_sealed_room_designates_the_whole_room() -> void:
	var world := _world()
	_sealed_room(world)
	var t := _tool(world, ZoneValidator.Kind.CELL)
	t.begin_drag(Vector2i(4, 4))
	t.update_drag(Vector2i(6, 5)) # only part of the 5x4 interior
	t.end_drag()

	var room := world.room_at(5, 5)
	assert_eq(room.zone_kind, ZoneValidator.Kind.CELL, "not left in a mixed, unzoned state")
	assert_eq(world.grid.tile_at(3, 6).zone_kind, ZoneValidator.Kind.CELL, "corner picked up too")


func test_open_ground_is_not_snapped_to_the_giant_outdoor_region() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.YARD)
	t.begin_drag(Vector2i(10, 10))
	t.update_drag(Vector2i(12, 11))
	t.end_drag()
	assert_eq(world.grid.tile_at(11, 10).zone_kind, ZoneValidator.Kind.YARD)
	assert_eq(world.grid.tile_at(20, 15).zone_kind, -1, "the rest of the map is untouched")


func test_rooms_update_immediately_rather_than_next_tick() -> void:
	var world := _world()
	var t := _tool(world, ZoneValidator.Kind.CELL)
	t.begin_drag(Vector2i(10, 10))
	t.update_drag(Vector2i(12, 11))
	t.end_drag()
	# No world.tick() here — the player has to see the result of their drag.
	assert_eq(world.grid.tile_at(11, 11).zone_kind, ZoneValidator.Kind.CELL)
