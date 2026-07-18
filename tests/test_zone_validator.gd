extends GutTest


func _build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


func test_cell_valid_with_bed_and_toilet() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 4, 4)
	grid.place_object(2, 2, ObjectDef.Type.BED)
	grid.place_object(3, 2, ObjectDef.Type.TOILET)
	var rooms := RoomDetector.detect(grid)
	var cell: RoomInfo
	for r in rooms:
		if r.tiles.size() == 9:
			cell = r
	grid.set_zone(cell.tiles, ZoneValidator.Kind.CELL)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.tiles.size() == 9:
			cell = r
	assert_true(cell.sealed)
	assert_eq(cell.zone_kind, ZoneValidator.Kind.CELL)
	assert_true(cell.zone_valid)


func test_cell_missing_toilet_is_invalid() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 4, 4)
	grid.place_object(2, 2, ObjectDef.Type.BED)
	var rooms := RoomDetector.detect(grid)
	var cell: RoomInfo = rooms[1] if rooms[0].tiles.size() != 9 else rooms[0]
	grid.set_zone(cell.tiles, ZoneValidator.Kind.CELL)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.zone_kind == ZoneValidator.Kind.CELL:
			assert_false(r.zone_valid, "no toilet placed")
			return
	fail_test("cell room not found")


func test_unsealed_room_never_valid_even_with_objects() -> void:
	var grid := SimGrid.new(10, 10)
	# Three walls only — deliberately not sealed.
	grid.set_wall(2, 2, SimTile.WALL_N, true)
	grid.set_wall(2, 2, SimTile.WALL_W, true)
	grid.set_wall(3, 2, SimTile.WALL_N, true)
	grid.place_object(2, 2, ObjectDef.Type.BED)
	grid.place_object(3, 2, ObjectDef.Type.TOILET)
	var rooms := RoomDetector.detect(grid)
	var big := rooms[0]
	grid.set_zone(big.tiles, ZoneValidator.Kind.CELL)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.zone_kind == ZoneValidator.Kind.CELL:
			assert_false(r.sealed)
			assert_false(r.zone_valid)
			return
	fail_test("zoned room not found")


func test_solitary_requires_no_objects() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 3, 3)
	var rooms := RoomDetector.detect(grid)
	var solitary := rooms[0] if rooms[0].tiles.size() == 4 else rooms[1]
	grid.set_zone(solitary.tiles, ZoneValidator.Kind.SOLITARY)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.zone_kind == ZoneValidator.Kind.SOLITARY:
			assert_true(r.zone_valid)
			return
	fail_test("solitary room not found")


func test_visitation_requires_table_and_phone() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 5, 5)
	grid.place_object(2, 2, ObjectDef.Type.TABLE)
	var rooms := RoomDetector.detect(grid)
	var room := rooms[0] if rooms[0].tiles.size() == 16 else rooms[1]
	grid.set_zone(room.tiles, ZoneValidator.Kind.VISITATION)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.zone_kind == ZoneValidator.Kind.VISITATION:
			assert_false(r.zone_valid, "phone still missing")
	grid.place_object(3, 2, ObjectDef.Type.PHONE)
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.zone_kind == ZoneValidator.Kind.VISITATION:
			assert_true(r.zone_valid)
			return
	fail_test("visitation room not found")


func test_mixed_zoning_within_one_room_is_unzoned() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 5, 5)
	var rooms := RoomDetector.detect(grid)
	var room := rooms[0] if rooms[0].tiles.size() == 16 else rooms[1]
	grid.tile_at(2, 2).zone_kind = ZoneValidator.Kind.CELL
	grid.tile_at(3, 2).zone_kind = ZoneValidator.Kind.CANTEEN
	grid.grid_version += 1
	rooms = RoomDetector.detect(grid)
	for r in rooms:
		if r.tiles.size() == 16:
			assert_eq(r.zone_kind, -1)
			assert_false(r.zone_valid)
