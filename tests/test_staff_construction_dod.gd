extends GutTest
## M3 DoD, first clause: "Queued buildings get built by workers over time."
##
## The three things that have to be true for that sentence to mean anything:
## nothing is built without workers, work takes real sim-time, and hiring
## more workers finishes the same queue sooner.

const MAX_TICKS := 40000


func _world_with_queue(seed_value: int, wall_count: int) -> SimWorld:
	var world := SimWorld.new(seed_value, 24, 24)
	world.gate_tile = Vector2i(2, 2)
	Crew.set_hour(world, 8) # early in the day shift, well clear of a break
	for i in range(wall_count):
		var ok := world.construction_queue.enqueue(
			BuildOrder.make_wall(4 + i, 8, SimTile.WALL_N), world.ledger
		)
		assert_true(ok, "starting balance should cover a few walls")
	return world


func test_nothing_is_built_without_workers() -> void:
	var world := _world_with_queue(1, 3)
	for i in range(MAX_TICKS / 4):
		world.tick()
	assert_eq(world.construction_queue.orders.size(), 3, "no crew, no progress")
	assert_false(world.grid.tile_at(4, 8).has_wall(SimTile.WALL_N))


func test_one_worker_builds_the_queue_over_time() -> void:
	var world := _world_with_queue(2, 3)
	Crew.staff_up(world, Staff.Role.WORKER, 1, Vector2i(2, 2))
	var balance_before := world.ledger.balance

	world.tick()
	assert_eq(world.construction_queue.orders.size(), 3, "construction is not instant")

	var ticks := Crew.run_until_built(world, MAX_TICKS)
	assert_lt(ticks, MAX_TICKS, "the worker should finish eventually")
	assert_gt(ticks, int(BuildOrder.WALL_WORK) * 3, "and it should cost more than the bare work time (they have to walk there)")
	for i in range(3):
		assert_true(world.grid.tile_at(4 + i, 8).has_wall(SimTile.WALL_N), "wall %d built" % i)
	assert_eq(world.ledger.balance, balance_before - BuildOrder.WALL_COST * 3, "paid on completion")


func test_more_workers_finish_the_same_queue_sooner() -> void:
	var solo := _world_with_queue(3, 4)
	Crew.staff_up(solo, Staff.Role.WORKER, 1, Vector2i(2, 2))
	var solo_ticks := Crew.run_until_built(solo, MAX_TICKS)

	var crew := _world_with_queue(3, 4)
	Crew.staff_up(crew, Staff.Role.WORKER, 4, Vector2i(2, 2))
	var crew_ticks := Crew.run_until_built(crew, MAX_TICKS)

	assert_lt(crew_ticks, MAX_TICKS)
	assert_lt(crew_ticks, solo_ticks, "4 workers beat 1 on the same 4-order queue")


func test_a_tired_worker_builds_slower() -> void:
	var world := _world_with_queue(4, 1)
	var fresh := Crew.staff_up(world, Staff.Role.WORKER, 1, Vector2i(2, 2))[0]
	assert_almost_eq(fresh.work_rate(), 1.0, 0.001)
	fresh.fatigue = 1.0
	assert_almost_eq(fresh.work_rate(), 1.0 - Staff.FATIGUE_WORK_PENALTY, 0.001)
	assert_lt(fresh.move_speed(), SimAgent.MOVE_TILES_PER_TICK, "exhausted staff also walk slower")


func test_off_shift_workers_do_not_build() -> void:
	var world := _world_with_queue(5, 2)
	var crew := Crew.staff_up(world, Staff.Role.WORKER, 2, Vector2i(2, 2))
	for s in crew:
		s.shift = Staff.Shift.NIGHT # the clock is at 08:00, so they're off
	# Stop short of 18:00 — past that they clock in and (correctly) build.
	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 60 * 9):
		world.tick()
	assert_eq(world.construction_queue.orders.size(), 2, "the night crew isn't in yet")
	for s in crew:
		assert_eq(s.state, Staff.State.OFF_DUTY)


func test_dismissing_a_worker_returns_their_order_to_the_pool() -> void:
	var world := _world_with_queue(6, 1)
	var worker := Crew.staff_up(world, Staff.Role.WORKER, 1, Vector2i(2, 2))[0]
	for i in range(200):
		world.tick()
	var order: BuildOrder = world.construction_queue.orders[0]
	assert_eq(order.claimed_by, worker.id, "worker should have claimed it")
	var partial := order.work_remaining
	assert_lt(partial, order.work_total, "and started on it")

	world.dismiss_staff(worker.id, "fired")
	assert_eq(order.claimed_by, -1, "claim released")
	assert_almost_eq(order.work_remaining, partial, 0.001, "progress kept")
