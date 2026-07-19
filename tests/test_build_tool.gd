extends GutTest
## Drag-to-build intent: what a given gesture actually queues, what it costs,
## and what it refuses to charge you for twice.


func _world() -> SimWorld:
	return SimWorld.new(1, 30, 20)


func _tool(world: SimWorld, mode: int) -> BuildTool:
	var t := BuildTool.new(world)
	t.mode = mode
	return t


func _drag(t: BuildTool, from: Vector2i, to: Vector2i) -> void:
	t.begin_drag(from)
	t.update_drag(to)


func _wall_flags(orders: Array[BuildOrder]) -> Dictionary:
	var out := {}
	for o in orders:
		out[Vector2i(o.x, o.y)] = o.wall_flag
	return out


# --------------------------------------------------------------- wall modes

func test_outline_drag_encloses_the_box() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.OUTLINE
	_drag(t, Vector2i(4, 4), Vector2i(6, 6))

	var orders := t.preview_orders()
	# 3x3 box: 3 north + 3 south + 3 west + 3 east.
	assert_eq(orders.size(), 12, "every edge of the perimeter")
	var flags := _wall_flags(orders)
	assert_true(flags.has(Vector2i(4, 4)), "corner included")


func test_line_wall_lands_on_the_edge_you_dragged_across() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.LINE

	# Rightwards and a touch downwards: the boundary crossed is the south
	# edge of the row we started on.
	_drag(t, Vector2i(3, 5), Vector2i(8, 6))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_S, "dragged down -> south edge")
		assert_eq(o.y, 5, "still anchored to the starting row")

	# Rightwards and a touch upwards -> north edge instead.
	_drag(t, Vector2i(3, 5), Vector2i(8, 4))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_N, "dragged up -> north edge")

	# Downwards and a touch right -> east edge of the starting column.
	_drag(t, Vector2i(3, 5), Vector2i(4, 11))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_E, "dragged right -> east edge")
		assert_eq(o.x, 3)

	# Downwards and a touch left -> west edge.
	_drag(t, Vector2i(5, 5), Vector2i(4, 11))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_W, "dragged left -> west edge")


func test_a_perfectly_straight_line_drag_falls_back_to_the_near_edge() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.LINE

	_drag(t, Vector2i(3, 5), Vector2i(8, 5))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_N, "no vertical hint -> north")

	_drag(t, Vector2i(3, 5), Vector2i(3, 11))
	for o in t.preview_orders():
		assert_eq(o.wall_flag, SimTile.WALL_W, "no horizontal hint -> west")


func test_line_drag_runs_along_the_dominant_axis() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.LINE

	_drag(t, Vector2i(3, 5), Vector2i(8, 6)) # wider than tall -> horizontal
	var horizontal := t.preview_orders()
	assert_eq(horizontal.size(), 6, "one per column of the run")
	for o in horizontal:
		assert_eq(o.y, 5, "stays on the row the drag started from")

	_drag(t, Vector2i(3, 5), Vector2i(4, 11)) # taller than wide -> vertical
	var vertical := t.preview_orders()
	assert_eq(vertical.size(), 7)
	for o in vertical:
		assert_eq(o.x, 3, "stays on the column the drag started from")


func test_a_line_drag_can_divide_an_existing_room() -> void:
	var world := _world()
	FacilityBuilder.build_box(world.grid, 2, 2, 8, 8)
	world.tick()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.LINE
	_drag(t, Vector2i(3, 5), Vector2i(7, 5))
	assert_eq(t.preview_orders().size(), 5, "a wall straight across the middle")


func test_existing_walls_are_not_charged_for_again() -> void:
	var world := _world()
	FacilityBuilder.build_box(world.grid, 4, 4, 6, 6)
	world.tick()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.OUTLINE
	_drag(t, Vector2i(4, 4), Vector2i(6, 6))
	assert_eq(t.preview_orders().size(), 0, "dragging over a finished box costs nothing")
	assert_eq(t.preview_cost(), 0)


func test_partially_overlapping_a_building_only_charges_the_new_walls() -> void:
	var world := _world()
	FacilityBuilder.build_box(world.grid, 4, 4, 6, 6)
	world.tick()
	var t := _tool(world, BuildTool.Mode.WALL)
	t.wall_style = BuildTool.WallStyle.OUTLINE
	_drag(t, Vector2i(4, 4), Vector2i(9, 6))
	var orders := t.preview_orders()
	assert_gt(orders.size(), 0, "the extension is new")
	assert_lt(orders.size(), 18, "but the shared edge isn't rebuilt")


# ------------------------------------------------------------------ objects

func test_object_drag_lays_one_per_free_tile() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.OBJECT)
	t.object_type = ObjectDef.Type.BED
	_drag(t, Vector2i(3, 3), Vector2i(6, 3))
	var orders := t.preview_orders()
	assert_eq(orders.size(), 4, "a row of four beds in one gesture")
	for o in orders:
		assert_eq(o.object_type, ObjectDef.Type.BED)


func test_object_drag_skips_tiles_that_are_already_occupied() -> void:
	var world := _world()
	world.grid.place_object(4, 3, ObjectDef.Type.TOILET)
	var t := _tool(world, BuildTool.Mode.OBJECT)
	_drag(t, Vector2i(3, 3), Vector2i(6, 3))
	assert_eq(t.preview_orders().size(), 3, "the occupied tile is stepped over, not rejected")


func test_a_drag_that_never_moves_places_exactly_one() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.OBJECT)
	_drag(t, Vector2i(5, 5), Vector2i(5, 5))
	assert_eq(t.preview_orders().size(), 1, "click-to-place is just a one-tile drag")


# ------------------------------------------------------------------- floors

func test_floor_drag_fills_the_box_and_skips_matching_tiles() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.FLOOR)
	t.floor_type = SimTile.FloorType.CONCRETE
	_drag(t, Vector2i(2, 2), Vector2i(4, 3))
	assert_eq(t.preview_orders().size(), 6, "3x2 box")

	world.grid.set_floor(3, 2, SimTile.FloorType.CONCRETE)
	assert_eq(t.preview_orders().size(), 5, "already concrete, nothing to do there")


# ------------------------------------------------------------------ summary

func test_summary_reports_what_the_player_is_about_to_buy() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.FLOOR)
	_drag(t, Vector2i(2, 2), Vector2i(4, 3))

	var summary := t.preview_summary()
	assert_eq(summary["count"], 6)
	assert_eq(summary["cost"], BuildOrder.FLOOR_COST * 6)
	assert_almost_eq(float(summary["work"]), BuildOrder.FLOOR_WORK * 6, 0.001)
	assert_true(summary["affordable"], "starting balance covers six floor tiles")


func test_summary_flags_an_unaffordable_selection() -> void:
	var world := _world()
	world.ledger.spend(world.ledger.balance, "spent it all")
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(9, 9))
	assert_gt(t.preview_summary()["cost"], 0)
	assert_false(t.preview_summary()["affordable"], "and the preview turns red on this")


func test_releasing_a_drag_parks_it_instead_of_spending() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(5, 5))
	assert_eq(world.construction_queue.orders.size(), 0, "still deciding")

	t.end_drag()
	assert_false(t.dragging)
	assert_true(t.has_pending(), "released, awaiting a yes/no")
	assert_eq(world.construction_queue.orders.size(), 0, "an imprecise drag must not spend money on its own")


func test_confirming_commits_the_parked_selection() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(5, 5))
	t.end_drag()
	var expected := t.pending_orders.size()

	assert_eq(t.confirm_pending(), expected, "everything queued")
	assert_eq(world.construction_queue.orders.size(), expected)
	assert_false(t.has_pending(), "and the selection is consumed")


func test_cancelling_a_parked_selection_queues_nothing() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(5, 5))
	t.end_drag()
	t.cancel_pending()
	assert_false(t.has_pending())
	assert_eq(world.construction_queue.orders.size(), 0)
	assert_eq(t.confirm_pending(), 0, "nothing left to confirm")


func test_cancelling_mid_drag_leaves_nothing_parked() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(5, 5))
	t.cancel_drag()
	t.end_drag()
	assert_false(t.has_pending())
	assert_eq(world.construction_queue.orders.size(), 0)


func test_ghost_shows_the_live_drag_then_the_parked_selection() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.WALL)
	_drag(t, Vector2i(2, 2), Vector2i(5, 5))
	var during := t.ghost_orders().size()
	assert_gt(during, 0, "something to draw while dragging")
	t.end_drag()
	assert_eq(t.ghost_orders().size(), during, "and the same thing while it waits for confirmation")


func test_drags_off_the_edge_of_the_map_are_rejected_cleanly() -> void:
	var world := _world()
	var t := _tool(world, BuildTool.Mode.OBJECT)
	_drag(t, Vector2i(-4, -4), Vector2i(1, 1))
	for o in t.preview_orders():
		assert_true(world.grid.in_bounds(o.x, o.y), "never queues an out-of-bounds order")
