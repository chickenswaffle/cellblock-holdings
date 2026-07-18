extends GutTest


func _build_box(grid: SimGrid, x0: int, y0: int, x1: int, y1: int) -> void:
	for x in range(x0, x1 + 1):
		grid.set_wall(x, y0, SimTile.WALL_N, true)
		grid.set_wall(x, y1, SimTile.WALL_S, true)
	for y in range(y0, y1 + 1):
		grid.set_wall(x0, y, SimTile.WALL_W, true)
		grid.set_wall(x1, y, SimTile.WALL_E, true)


func test_straight_line_open_floor() -> void:
	var grid := SimGrid.new(10, 10)
	var path := Pathfinder.find_path(grid, Vector2i(0, 0), Vector2i(5, 0))
	assert_eq(path[0], Vector2i(0, 0))
	assert_eq(path[-1], Vector2i(5, 0))
	assert_eq(path.size(), 6)


func test_same_start_and_goal_is_empty() -> void:
	var grid := SimGrid.new(10, 10)
	var path := Pathfinder.find_path(grid, Vector2i(3, 3), Vector2i(3, 3))
	assert_eq(path.size(), 0)


func test_out_of_bounds_is_empty() -> void:
	var grid := SimGrid.new(10, 10)
	assert_eq(Pathfinder.find_path(grid, Vector2i(-1, 0), Vector2i(5, 5)).size(), 0)
	assert_eq(Pathfinder.find_path(grid, Vector2i(0, 0), Vector2i(50, 50)).size(), 0)


func test_routes_around_a_sealed_box() -> void:
	var grid := SimGrid.new(20, 20)
	_build_box(grid, 5, 5, 10, 10)
	var path := Pathfinder.find_path(grid, Vector2i(7, 7), Vector2i(15, 7))
	assert_eq(path.size(), 0, "fully sealed box, no door — genuinely unreachable")


func test_routes_through_a_door() -> void:
	var grid := SimGrid.new(20, 20)
	_build_box(grid, 5, 5, 10, 10)
	grid.set_door(10, 7, SimTile.WALL_E, true)
	var path := Pathfinder.find_path(grid, Vector2i(7, 7), Vector2i(15, 7))
	assert_gt(path.size(), 0, "door makes it reachable")
	# Every consecutive step in the returned path must be a legal move.
	for i in range(1, path.size()):
		var a: Vector2i = path[i - 1]
		var b: Vector2i = path[i]
		var d := b - a
		assert_true(absi(d.x) <= 1 and absi(d.y) <= 1 and d != Vector2i.ZERO, "step %d is not adjacent" % i)


func test_never_cuts_a_wall_corner_diagonally() -> void:
	var grid := SimGrid.new(10, 10)
	# Block just one of the two edges a diagonal (4,5)->(5,4) step would
	# need (the (4,5)-(5,5) edge). A corner-cutting pathfinder would still
	# jump straight there in 2 tiles; a correct one detours via (4,4),
	# landing on 3.
	grid.set_wall(4, 5, SimTile.WALL_E, true)
	var path := Pathfinder.find_path(grid, Vector2i(4, 5), Vector2i(5, 4))
	assert_eq(path.size(), 3, "must detour around the blocked corner, not cut through it")
	assert_eq(path[1], Vector2i(4, 4))


func test_door_costs_more_than_open_floor() -> void:
	var grid := SimGrid.new(10, 10)
	grid.set_wall(4, 4, SimTile.WALL_E, true)
	grid.set_door(4, 4, SimTile.WALL_E, true)
	var door_cost = Pathfinder._edge_cost(grid, Vector2i(4, 4), Vector2i(1, 0))
	var open_cost = Pathfinder._edge_cost(grid, Vector2i(0, 0), Vector2i(1, 0))
	assert_gt(door_cost, open_cost)


func test_blocked_edge_has_negative_cost() -> void:
	var grid := SimGrid.new(10, 10)
	grid.set_wall(4, 4, SimTile.WALL_E, true)
	var cost = Pathfinder._edge_cost(grid, Vector2i(4, 4), Vector2i(1, 0))
	assert_lt(cost, 0.0)


func test_deterministic_same_grid_same_path() -> void:
	var grid := SimGrid.new(15, 15)
	_build_box(grid, 4, 4, 8, 8)
	grid.set_door(4, 6, SimTile.WALL_W, true)
	var a := Pathfinder.find_path(grid, Vector2i(0, 6), Vector2i(6, 6))
	var b := Pathfinder.find_path(grid, Vector2i(0, 6), Vector2i(6, 6))
	assert_eq(a, b)
