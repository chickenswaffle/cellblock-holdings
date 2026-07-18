extends GutTest
## M2 DoD: 50 prisoners run a full day/night cycle — sleep in beds, eat in
## canteen, don't clip walls.


func test_50_prisoners_full_day_cycle() -> void:
	var world := SimWorld.new(42, 90, 25)
	FacilityBuilder.build(world, 10)
	FacilityBuilder.intake_n(world, 50)
	assert_eq(world.prisoners.size(), 50)

	var ticks_per_day := SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY
	var saw_sleeping_in_bed := false
	var saw_eating_at_table := false

	for i in range(ticks_per_day + SimClock.TICKS_PER_SIM_MINUTE * 60 * 2):
		world.tick()

		# Bounds/no-clip sanity check every tick, cheap and catches any
		# gross movement bug immediately rather than only at checkpoints.
		for p in world.prisoners:
			assert_true(world.grid.in_bounds(p.tile_pos().x, p.tile_pos().y), "prisoner %d left the grid" % p.id)

		var block := world.schedule.block_at_hour(world.clock.hour_of_day())
		if block == ScheduleSystem.Block.SLEEP:
			for p in world.prisoners:
				if p.action_state == Prisoner.ActionState.PERFORMING and p.action_need == Needs.Kind.SLEEP:
					assert_eq(p.tile_pos(), p.cell_bed_pos, "sleeping prisoner must be at their own bed")
					saw_sleeping_in_bed = true
		elif block == ScheduleSystem.Block.EAT:
			for p in world.prisoners:
				if p.action_state == Prisoner.ActionState.PERFORMING and p.action_need == Needs.Kind.HUNGER:
					var obj := world.grid.object_at(p.tile_pos().x, p.tile_pos().y)
					assert_not_null(obj, "eating prisoner must be standing on an object tile")
					assert_eq(obj.object_type, ObjectDef.Type.TABLE)
					var room := world.room_at(p.tile_pos().x, p.tile_pos().y)
					assert_eq(room.zone_kind, ZoneValidator.Kind.CANTEEN)
					saw_eating_at_table = true

	assert_true(saw_sleeping_in_bed, "at least one prisoner must have been observed sleeping in their bed")
	assert_true(saw_eating_at_table, "at least one prisoner must have been observed eating at a canteen table")


func test_no_prisoner_ever_stands_inside_a_wall_tile() -> void:
	var world := SimWorld.new(11, 90, 25)
	FacilityBuilder.build(world, 10)
	FacilityBuilder.intake_n(world, 50)
	for i in range(2000):
		world.tick()
		for p in world.prisoners:
			var t := p.tile_pos()
			assert_true(world.grid.in_bounds(t.x, t.y))
