extends GutTest
## The queue in isolation: claiming, work accounting, and money changing
## hands on completion. Worker behaviour on top of it lives in
## test_staff_construction_dod.gd.


func _work_off(q: ConstructionQueue, o: BuildOrder, grid: SimGrid, ledger: Ledger, events: SimEventBus) -> int:
	var ticks := 0
	while not q.apply_work(o, 1.0, grid, ledger, events):
		ticks += 1
		assert_lt(ticks, 1000, "order never completed")
	return ticks + 1


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
	var o := BuildOrder.make_wall(1, 1, SimTile.WALL_N)
	q.enqueue(o, ledger)
	assert_eq(ledger.balance, 1000, "enqueue must not spend")

	for i in range(int(BuildOrder.WALL_WORK) - 1):
		assert_false(q.apply_work(o, 1.0, grid, ledger, events))
	assert_eq(ledger.balance, 1000, "not complete yet, still unspent")
	assert_false(grid.tile_at(1, 1).has_wall(SimTile.WALL_N))

	assert_true(q.apply_work(o, 1.0, grid, ledger, events), "last worker-tick completes it")
	assert_eq(ledger.balance, 1000 - BuildOrder.WALL_COST)
	assert_true(grid.tile_at(1, 1).has_wall(SimTile.WALL_N))
	assert_eq(q.orders.size(), 0, "completed order leaves the pool")


func test_queue_never_advances_on_its_own() -> void:
	var world := SimWorld.new(1, 12, 12)
	world.construction_queue.enqueue(BuildOrder.make_wall(3, 3, SimTile.WALL_N), world.ledger)
	for i in range(2000):
		world.tick()
	assert_eq(world.construction_queue.orders.size(), 1, "no workers hired, nothing gets built")
	assert_false(world.grid.tile_at(3, 3).has_wall(SimTile.WALL_N))


func test_claims_hand_out_in_enqueue_order_and_are_exclusive() -> void:
	var ledger := Ledger.new(1000)
	var q := ConstructionQueue.new()
	var first := BuildOrder.make_wall(1, 1, SimTile.WALL_N)
	var second := BuildOrder.make_wall(2, 2, SimTile.WALL_N)
	q.enqueue(first, ledger)
	q.enqueue(second, ledger)

	assert_eq(q.claim_next(10), first, "oldest unclaimed order goes first")
	assert_eq(q.claim_next(11), second, "a second worker gets the next one, not the same one")
	assert_null(q.claim_next(12), "nothing left to claim")

	q.release_claims_of(10)
	assert_eq(q.claim_next(12), first, "released work is claimable again")


func test_blocked_claims_reset_when_the_map_changes() -> void:
	var world := SimWorld.new(1, 12, 12)
	var q := world.construction_queue
	var o := BuildOrder.make_wall(4, 4, SimTile.WALL_N)
	q.enqueue(o, world.ledger)
	o.claimed_by = ConstructionQueue.BLOCKED_CLAIM
	assert_null(q.claim_next(1), "unreachable orders aren't handed out")

	world.grid.set_floor(0, 0, SimTile.FloorType.CONCRETE)
	world.tick()
	assert_eq(q.claim_next(1), o, "a grid change makes it worth retrying")


func test_partial_progress_survives_the_worker_leaving() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	var o := BuildOrder.make_wall(1, 1, SimTile.WALL_N)
	q.enqueue(o, ledger)
	assert_eq(q.claim_next(7), o)
	for i in range(10):
		q.apply_work(o, 1.0, grid, ledger, events)
	q.release_claims_of(7)
	assert_almost_eq(o.work_remaining, BuildOrder.WALL_WORK - 10.0, 0.001)
	assert_eq(o.claimed_by, -1)


func test_door_order_applies_via_set_door() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	var o := BuildOrder.make_door(1, 1, SimTile.WALL_E)
	q.enqueue(o, ledger)
	_work_off(q, o, grid, ledger, events)
	assert_true(grid.tile_at(1, 1).has_door(SimTile.WALL_E))
	assert_true(grid.tile_at(2, 1).has_door(SimTile.WALL_W), "door mirrors to neighbor")


func test_object_order_places_object() -> void:
	var ledger := Ledger.new(1000)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	var o := BuildOrder.make_object(3, 3, ObjectDef.Type.BED)
	q.enqueue(o, ledger)
	_work_off(q, o, grid, ledger, events)
	var placed := grid.object_at(3, 3)
	assert_not_null(placed)
	assert_eq(placed.object_type, ObjectDef.Type.BED)


func test_completion_failure_when_balance_drained_midway() -> void:
	var ledger := Ledger.new(BuildOrder.WALL_COST)
	var grid := SimGrid.new(10, 10)
	var events := SimEventBus.new()
	var q := ConstructionQueue.new()
	var o := BuildOrder.make_wall(1, 1, SimTile.WALL_N)
	q.enqueue(o, ledger)
	ledger.spend(BuildOrder.WALL_COST, "something else drained it")
	_work_off(q, o, grid, ledger, events)
	assert_false(grid.tile_at(1, 1).has_wall(SimTile.WALL_N), "order dropped, no effect applied")
	assert_eq(q.orders.size(), 0, "and it doesn't linger in the pool")
