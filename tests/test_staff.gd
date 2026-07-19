extends GutTest
## Staff mechanics that aren't construction: shifts, fatigue, breaks, and
## the serialization round-trip.


func test_day_shift_covers_daytime_hours_only() -> void:
	var s := Staff.new()
	s.shift = Staff.Shift.DAY
	assert_false(s.on_shift_at_hour(5))
	assert_true(s.on_shift_at_hour(6))
	assert_true(s.on_shift_at_hour(17))
	assert_false(s.on_shift_at_hour(18))
	assert_false(s.on_shift_at_hour(23))


func test_night_shift_wraps_midnight() -> void:
	var s := Staff.new()
	s.shift = Staff.Shift.NIGHT
	assert_true(s.on_shift_at_hour(18))
	assert_true(s.on_shift_at_hour(23))
	assert_true(s.on_shift_at_hour(0), "still on the clock after midnight")
	assert_true(s.on_shift_at_hour(5))
	assert_false(s.on_shift_at_hour(6))


func test_every_hour_is_covered_by_exactly_one_shift() -> void:
	var day := Staff.new()
	day.shift = Staff.Shift.DAY
	var night := Staff.new()
	night.shift = Staff.Shift.NIGHT
	for hour in range(24):
		var covered := int(day.on_shift_at_hour(hour)) + int(night.on_shift_at_hour(hour))
		assert_eq(covered, 1, "hour %d covered exactly once" % hour)


func test_fatigue_accumulates_over_a_shift_and_clamps() -> void:
	var s := Staff.new()
	for i in range(720): # one 12-hour shift, no breaks
		s.tire_one_minute()
	assert_gt(s.fatigue, 0.7, "a full shift with no break should leave them tired")
	assert_lt(s.fatigue, 1.0, "but not destroyed")
	for i in range(5000):
		s.tire_one_minute()
	assert_almost_eq(s.fatigue, 1.0, 0.0001, "fatigue clamps at 1")


func test_resting_recovers_faster_than_off_duty() -> void:
	var resting := Staff.new()
	var off := Staff.new()
	resting.fatigue = 1.0
	off.fatigue = 1.0
	for i in range(60):
		resting.recover_one_minute(true)
		off.recover_one_minute(false)
	assert_lt(resting.fatigue, off.fatigue, "a proper break beats going home")
	assert_gt(resting.fatigue, 0.0)


func test_exhausted_staff_move_slower() -> void:
	var s := Staff.new()
	assert_almost_eq(s.move_speed(), SimAgent.MOVE_TILES_PER_TICK, 0.001)
	s.fatigue = 1.0
	var expected := SimAgent.MOVE_TILES_PER_TICK * (1.0 - Staff.FATIGUE_SPEED_PENALTY)
	assert_almost_eq(s.move_speed(), expected, 0.001)


func test_fatigue_erodes_nerve() -> void:
	var s := Staff.new()
	s.base_nerve = 0.8
	assert_almost_eq(s.effective_nerve(), 0.8, 0.001)
	s.fatigue = 1.0
	assert_almost_eq(s.effective_nerve(), 0.4, 0.001, "exhausted staff hold it together half as well")


func test_a_worker_past_the_break_threshold_stops_working() -> void:
	var world := SimWorld.new(1, 20, 20)
	world.gate_tile = Vector2i(2, 2)
	Crew.set_hour(world, 8)
	# Several orders so the queue is still non-empty when we interrupt them.
	for i in range(3):
		world.construction_queue.enqueue(BuildOrder.make_wall(6 + i, 6, SimTile.WALL_N), world.ledger)
	var w := Crew.staff_up(world, Staff.Role.WORKER, 1, Vector2i(2, 2))[0]

	# Decisions land on the minute tick, so they claim at ~t10 and arrive at
	# ~t22; the first wall takes 40 worker-ticks after that. t40 is mid-job.
	for i in range(40):
		world.tick()
	assert_eq(w.state, Staff.State.WORKING, "settled into the job first")

	w.fatigue = 0.99
	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 2):
		world.tick()
	assert_eq(w.state, Staff.State.RESTING, "exhausted worker takes a break")
	assert_eq(w.job_order_id, -1, "and gives up the job while they do")
	assert_eq(world.construction_queue.orders[0].claimed_by, -1, "so someone else could take it")


func test_going_off_shift_parks_staff_at_the_gate() -> void:
	var world := SimWorld.new(1, 20, 20)
	world.gate_tile = Vector2i(3, 3)
	Crew.set_hour(world, 17) # last hour of the day shift
	var s := Crew.staff_up(world, Staff.Role.WORKER, 1, Vector2i(10, 10))[0]

	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 61): # tick past 18:00
		world.tick()
	assert_eq(s.state, Staff.State.OFF_DUTY)
	assert_eq(s.tile_pos(), Vector2i(3, 3), "clocked out at the gate")


func test_staff_survive_a_save_load_round_trip() -> void:
	var world := SimWorld.new(7, 20, 20)
	world.gate_tile = Vector2i(4, 4)
	var hired := Hiring.hire(world, Staff.Role.GUARD)
	assert_not_null(hired)
	hired.fatigue = 0.42
	hired.patrol_index = 3

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_eq(restored.staff.size(), 1)
	var s: Staff = restored.staff[0]
	assert_eq(s.id, hired.id)
	assert_eq(s.sname, hired.sname)
	assert_eq(s.role, Staff.Role.GUARD)
	assert_almost_eq(s.fatigue, 0.42, 0.0001)
	assert_eq(s.patrol_index, 3)
	assert_eq(restored.gate_tile, Vector2i(4, 4))
	assert_eq(restored.next_staff_id, world.next_staff_id)


func test_hiring_charges_a_fee_and_alternates_shifts() -> void:
	var world := SimWorld.new(8, 20, 20)
	var before := world.ledger.balance
	var first := Hiring.hire(world, Staff.Role.GUARD)
	assert_eq(world.ledger.balance, before - Staff.hiring_fee(Staff.Role.GUARD))
	assert_eq(first.shift, Staff.Shift.DAY)
	var second := Hiring.hire(world, Staff.Role.GUARD)
	assert_eq(second.shift, Staff.Shift.NIGHT, "second guard covers nights")
	var third := Hiring.hire(world, Staff.Role.GUARD)
	assert_eq(third.shift, Staff.Shift.DAY)


func test_hiring_fails_when_broke() -> void:
	var world := SimWorld.new(9, 20, 20)
	world.ledger.spend(world.ledger.balance, "spent it all")
	assert_null(Hiring.hire(world, Staff.Role.WORKER))
	assert_eq(world.staff.size(), 0)


func test_support_staff_speed_up_meals_in_the_canteen() -> void:
	var world := SimWorld.new(10, 20, 20)
	FacilityBuilder.build_box(world.grid, 4, 4, 9, 8)
	world.grid.set_door(4, 6, SimTile.WALL_W, true)
	world.grid.place_object(6, 6, ObjectDef.Type.TABLE)
	world.tick()
	var room := world.room_at(6, 6)
	assert_not_null(room)
	world.zone_room(room.id, ZoneValidator.Kind.CANTEEN)

	assert_false(world.canteen_is_staffed(Vector2i(6, 6)), "empty canteen isn't staffed")
	var s := Crew.staff_up(world, Staff.Role.SUPPORT, 1, Vector2i(7, 6))[0]
	s.state = Staff.State.WORKING
	assert_true(world.canteen_is_staffed(Vector2i(6, 6)))
	assert_false(world.canteen_is_staffed(Vector2i(1, 1)), "a tile outside the canteen isn't")
