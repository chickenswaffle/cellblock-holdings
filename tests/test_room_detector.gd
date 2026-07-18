extends GutTest


func _build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


func _find_by_size(rooms: Array[RoomInfo], size: int) -> RoomInfo:
	for r in rooms:
		if r.tiles.size() == size:
			return r
	return null


## Fixture 1: nothing built yet — the whole map is one unsealed region.
func test_fixture_fully_open_map_is_one_unsealed_room() -> void:
	var grid := SimGrid.new(10, 10)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 1)
	assert_eq(rooms[0].tiles.size(), 100)
	assert_false(rooms[0].sealed)


## Fixture 2: a sealed box with a door in the middle of the map.
func test_fixture_sealed_box_with_door() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 5, 5)
	grid.set_door(3, 2, SimTile.WALL_N, true)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 2)
	var inside := _find_by_size(rooms, 16)
	var outside := _find_by_size(rooms, 84)
	assert_not_null(inside)
	assert_not_null(outside)
	assert_true(inside.sealed)
	assert_false(outside.sealed)


## Fixture 3: nested rooms — inner box inside an outer box's interior, plus
## the ring between them, plus the unsealed exterior. Four regions total.
func test_fixture_nested_rooms() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 1, 1, 8, 8)
	_build_box(grid, 3, 3, 5, 5)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 3)
	var innermost := _find_by_size(rooms, 9)
	var ring := _find_by_size(rooms, 55)
	var exterior := _find_by_size(rooms, 36)
	assert_not_null(innermost, "inner 3x3 room")
	assert_not_null(ring, "ring between outer and inner walls")
	assert_not_null(exterior, "unsealed exterior")
	assert_true(innermost.sealed)
	assert_true(ring.sealed)
	assert_false(exterior.sealed)


## Fixture 4: a room built flush against the map edge, walled on the border
## side too — must still count as sealed (the border-leak check only trips
## on an *unwalled* edge, not on the mere absence of a neighbor tile).
func test_fixture_room_sealed_against_map_border() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 0, 0, 2, 2)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 2)
	var corner := _find_by_size(rooms, 9)
	var rest := _find_by_size(rooms, 91)
	assert_not_null(corner)
	assert_not_null(rest)
	assert_true(corner.sealed, "walled on the border edge too, so it doesn't leak")
	assert_false(rest.sealed)


## Fixture 5: a door sitting exactly on a room's corner-adjacent edge must
## not break flood-fill or the sealed check.
func test_fixture_door_in_corner() -> void:
	var grid := SimGrid.new(10, 10)
	_build_box(grid, 2, 2, 4, 4)
	grid.set_door(2, 2, SimTile.WALL_N, true)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 2)
	var inside := _find_by_size(rooms, 9)
	assert_not_null(inside)
	assert_true(inside.sealed)
	assert_true(grid.tile_at(2, 2).has_door(SimTile.WALL_N))
	assert_true(grid.tile_at(2, 2).has_wall(SimTile.WALL_N), "door implies a wall")


func test_room_id_assigned_on_tiles() -> void:
	var grid := SimGrid.new(6, 6)
	_build_box(grid, 1, 1, 3, 3)
	var rooms := RoomDetector.detect(grid)
	var inside := _find_by_size(rooms, 9)
	for t in inside.tiles:
		assert_eq(grid.tile_at(t.x, t.y).room_id, inside.id)


func test_rerun_after_wall_removed_merges_rooms() -> void:
	var grid := SimGrid.new(8, 8)
	_build_box(grid, 2, 2, 4, 4)
	assert_eq(RoomDetector.detect(grid).size(), 2)
	grid.set_wall(2, 2, SimTile.WALL_N, false)
	var rooms := RoomDetector.detect(grid)
	assert_eq(rooms.size(), 1, "opening a wall merges the two regions back into one")
