extends GutTest


func test_dimensions_and_defaults() -> void:
	var g := SimGrid.new(10, 8)
	assert_eq(g.width, 10)
	assert_eq(g.height, 8)
	assert_eq(g.tiles.size(), 80)
	var t := g.tile_at(3, 4)
	assert_eq(t.floor_type, SimTile.FloorType.DIRT)
	assert_eq(t.wall_flags, 0)
	assert_eq(t.room_id, -1)
	assert_true(t.is_outdoor)


func test_in_bounds() -> void:
	var g := SimGrid.new(10, 8)
	assert_true(g.in_bounds(0, 0))
	assert_true(g.in_bounds(9, 7))
	assert_false(g.in_bounds(10, 0))
	assert_false(g.in_bounds(0, 8))
	assert_false(g.in_bounds(-1, 0))


func test_wall_mirrors_to_neighbor() -> void:
	var g := SimGrid.new(10, 10)
	g.set_wall(5, 5, SimTile.WALL_E, true)
	assert_true(g.tile_at(5, 5).has_wall(SimTile.WALL_E))
	assert_true(g.tile_at(6, 5).has_wall(SimTile.WALL_W), "neighbor must share the seam")
	g.set_wall(6, 5, SimTile.WALL_W, false)
	assert_false(g.tile_at(5, 5).has_wall(SimTile.WALL_E), "clearing from either side clears both")


func test_wall_at_map_border_does_not_crash() -> void:
	var g := SimGrid.new(10, 10)
	g.set_wall(0, 0, SimTile.WALL_N, true)
	g.set_wall(0, 0, SimTile.WALL_W, true)
	assert_true(g.tile_at(0, 0).has_wall(SimTile.WALL_N))
	assert_true(g.tile_at(0, 0).has_wall(SimTile.WALL_W))


func test_edge_open_blocked_by_wall() -> void:
	var g := SimGrid.new(10, 10)
	assert_true(g.edge_open(5, 5, 1, 0))
	g.set_wall(5, 5, SimTile.WALL_E, true)
	assert_false(g.edge_open(5, 5, 1, 0))
	assert_false(g.edge_open(6, 5, -1, 0), "blocked from both directions")
	assert_true(g.edge_open(5, 5, 0, 1), "other edges unaffected")


func test_edge_open_at_map_border() -> void:
	var g := SimGrid.new(10, 10)
	assert_false(g.edge_open(0, 0, -1, 0), "off-map is never open")
	assert_false(g.edge_open(9, 9, 0, 1))


func test_grid_version_increments_on_change() -> void:
	var g := SimGrid.new(10, 10)
	var v := g.grid_version
	g.set_floor(1, 1, SimTile.FloorType.CONCRETE)
	assert_gt(g.grid_version, v)
	v = g.grid_version
	g.set_wall(1, 1, SimTile.WALL_N, true)
	assert_gt(g.grid_version, v)


func test_serialization_roundtrip() -> void:
	var a := SimGrid.new(6, 6)
	a.set_floor(2, 3, SimTile.FloorType.TILE)
	a.set_wall(2, 3, SimTile.WALL_S, true)
	a.tile_at(4, 4).room_id = 7
	a.tile_at(4, 4).is_outdoor = false
	var b := SimGrid.new(1, 1)
	b.from_dict(a.to_dict())
	assert_eq(b.width, 6)
	assert_eq(b.tiles.size(), 36)
	assert_eq(b.tile_at(2, 3).floor_type, SimTile.FloorType.TILE)
	assert_true(b.tile_at(2, 3).has_wall(SimTile.WALL_S))
	assert_true(b.tile_at(2, 4).has_wall(SimTile.WALL_N), "mirrored wall survives roundtrip")
	assert_eq(b.tile_at(4, 4).room_id, 7)
	assert_false(b.tile_at(4, 4).is_outdoor)
	assert_eq(b.grid_version, a.grid_version)
