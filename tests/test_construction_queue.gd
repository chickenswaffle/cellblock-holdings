extends GutTest


func test_enqueue_rejects_unaffordable() -> void:
	var ledger := Ledger.new(5)
	var q := ConstructionQueue.new()
	var ok := q.enqueue(BuildOrder.make_wall(1, 1, SimTile.WALL_N), ledger)
	assert_false(ok)
	assert_eq(q.orders.size(), 0)


func test_money_deducted_on_completion_not_enqueue() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	q.enqueue(BuildOrder.make_wall(1, 1, SimTile.WALL_N), ledger)
	assert_eq(ledger.balance, 1000, "enqueue must not spend")
	for i in range(BuildOrder.BUILD_TICKS - 1):
		q.tick(grid, ledger, events)
	assert_eq(ledger.balance, 1000, "not complete yet, still unspent")
	assert_false(grid.tile_at(1, 1).has_wall(SimTile.WALL_N))
	q.tick(grid, ledger, events)
	assert_eq(ledger.balance, 1000 - BuildOrder.WALL_COST)
	assert_true(grid.tile_at(1, 1).has_wall(SimTile.WALL_N))


func test_orders_process_sequentially() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	q.enqueue(BuildOrder.make_wall(1, 1, SimTile.WALL_N), ledger)
	q.enqueue(BuildOrder.make_wall(2, 2, SimTile.WALL_N), ledger)
	for i in range(BuildOrder.BUILD_TICKS):
		q.tick(grid, ledger, events)
	assert_true(grid.tile_at(1, 1).has_wall(SimTile.WALL_N))
	assert_false(grid.tile_at(2, 2).has_wall(SimTile.WALL_N), "second order hasn't started yet")
	for i in range(BuildOrder.BUILD_TICKS):
		q.tick(grid, ledger, events)
	assert_true(grid.tile_at(2, 2).has_wall(SimTile.WALL_N))


func test_door_order_applies_via_set_door() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	q.enqueue(BuildOrder.make_door(1, 1, SimTile.WALL_E), ledger)
	for i in range(BuildOrder.BUILD_TICKS):
		q.tick(grid, ledger, events)
	assert_true(grid.tile_at(1, 1).has_door(SimTile.WALL_E))
	assert_true(grid.tile_at(2, 1).has_door(SimTile.WALL_W), "door mirrors to neighbor")


func test_object_order_places_object() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	q.enqueue(BuildOrder.make_object(3, 3, ObjectDef.Type.BED), ledger)
	for i in range(BuildOrder.BUILD_TICKS):
		q.tick(grid, ledger, events)
	var o := grid.object_at(3, 3)
	assert_not_null(o)
	assert_eq(o.object_type, ObjectDef.Type.BED)


func test_completion_failure_when_balance_drained_midway() -> void:
	var ledger := Ledger.new(BuildOrder.WALL_COST)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	q.enqueue(BuildOrder.make_wall(1, 1, SimTile.WALL_N), ledger)
	ledger.spend(BuildOrder.WALL_COST, "something else drained it")
	for i in range(BuildOrder.BUILD_TICKS):
		q.tick(grid, ledger, events)
	assert_false(grid.tile_at(1, 1).has_wall(SimTile.WALL_N), "order dropped, no effect applied")
