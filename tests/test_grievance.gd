extends GutTest
## Grievance: the slow-moving stat the whole conflict model is built on.


func _cell_world() -> SimWorld:
	var world := SimWorld.new(1, 20, 12)
	var g := world.grid
	FacilityBuilder.build_box(g, 2, 2, 6, 4)
	g.set_door(2, 3, SimTile.WALL_W, true)
	for b in range(2):
		g.place_object(3 + b, 3, ObjectDef.Type.BED)
	g.place_object(6, 3, ObjectDef.Type.TOILET)
	world.tick()
	world.zone_room(world.room_at(4, 3).id, ZoneValidator.Kind.CELL)
	return world


func _add_prisoner(world: SimWorld, bed: Vector2i) -> Prisoner:
	var p := Intake.generate_prisoner(world)
	p.traits = 0 # neutral, so trait multipliers don't muddy the assertions
	p.cell_bed_pos = bed
	p.place_at_tile(bed)
	world.prisoners.append(p)
	return p


func test_a_content_prisoner_has_nothing_to_resent() -> void:
	var world := _cell_world()
	var p := _add_prisoner(world, Vector2i(3, 3))
	assert_almost_eq(GrievanceSystem.target_for(world, p), 0.0, 0.001,
		"needs full, own bed, no crowding")


func test_unmet_needs_drive_the_target_up() -> void:
	var world := _cell_world()
	var p := _add_prisoner(world, Vector2i(3, 3))
	for kind in p.needs.values:
		p.needs.values[kind] = 0.0
	assert_gt(GrievanceSystem.target_for(world, p), 0.5, "deprivation is the main driver")


func test_safety_and_dignity_count_double() -> void:
	var world := _cell_world()
	var heavy := _add_prisoner(world, Vector2i(3, 3))
	heavy.needs.values[Needs.Kind.SAFETY] = 0.0
	var light := _add_prisoner(world, Vector2i(4, 3))
	light.needs.values[Needs.Kind.SOCIAL] = 0.0
	assert_gt(
		GrievanceSystem.target_for(world, heavy), GrievanceSystem.target_for(world, light),
		"feeling unsafe is worse than being bored"
	)


func test_overcrowding_raises_the_target() -> void:
	var world := _cell_world()
	var alone := _add_prisoner(world, Vector2i(3, 3))
	var solo_target := GrievanceSystem.target_for(world, alone)
	for i in range(5):
		_add_prisoner(world, Vector2i(3, 3)) # 6 assigned, 2 beds
	assert_gt(GrievanceSystem.target_for(world, alone), solo_target, "packed in is worse")


func test_having_no_bed_at_all_is_the_worst_case() -> void:
	var world := _cell_world()
	var homeless := _add_prisoner(world, Vector2i(-1, -1))
	homeless.cell_bed_pos = Vector2i(-1, -1)
	var index := GrievanceSystem.crowding_by_prisoner(world)
	assert_almost_eq(float(index[homeless.id]), 1.0, 0.001, "nowhere to sleep is maximal crowding")


func test_volatile_prisoners_resent_more_and_penitent_ones_less() -> void:
	var world := _cell_world()
	var volatile := _add_prisoner(world, Vector2i(3, 3))
	var penitent := _add_prisoner(world, Vector2i(4, 3))
	for kind in volatile.needs.values:
		volatile.needs.values[kind] = 0.3
		penitent.needs.values[kind] = 0.3
	var neutral := GrievanceSystem.target_for(world, volatile)

	volatile.traits = Prisoner.Trait.VOLATILE
	penitent.traits = Prisoner.Trait.PENITENT
	assert_gt(GrievanceSystem.target_for(world, volatile), neutral)
	assert_lt(GrievanceSystem.target_for(world, penitent), neutral)


func test_grievance_rises_faster_than_it_falls() -> void:
	var world := _cell_world()
	var p := _add_prisoner(world, Vector2i(3, 3))
	for kind in p.needs.values:
		p.needs.values[kind] = 0.0

	GrievanceSystem.minute_tick(world)
	var risen := p.grievance
	assert_gt(risen, 0.0, "conditions are dire, resentment starts building")

	for kind in p.needs.values:
		p.needs.values[kind] = 1.0
	var before_settling := p.grievance
	GrievanceSystem.minute_tick(world)
	var settled := before_settling - p.grievance
	assert_gt(settled, 0.0, "and it comes back down once things improve")
	assert_lt(settled, risen, "but more slowly — trust is harder to win than lose")


func test_grievance_converges_on_the_target_and_stays_bounded() -> void:
	var world := _cell_world()
	var p := _add_prisoner(world, Vector2i(3, 3))
	for kind in p.needs.values:
		p.needs.values[kind] = 0.2
	var target := GrievanceSystem.target_for(world, p)

	for i in range(2000):
		GrievanceSystem.minute_tick(world)
	assert_almost_eq(p.grievance, target, 0.01)
	assert_between(p.grievance, 0.0, 1.0)


func test_spikes_are_clamped_at_both_ends() -> void:
	var world := _cell_world()
	var p := _add_prisoner(world, Vector2i(3, 3))
	GrievanceSystem.add_spike(p, 5.0)
	assert_almost_eq(p.grievance, 1.0, 0.001)
	GrievanceSystem.add_spike(p, -5.0)
	assert_almost_eq(p.grievance, 0.0, 0.001)


func test_room_and_facility_spikes_hit_the_right_people() -> void:
	var world := _cell_world()
	var inside := _add_prisoner(world, Vector2i(3, 3))
	var outside := _add_prisoner(world, Vector2i(3, 3))
	outside.place_at_tile(Vector2i(10, 8)) # out on the yard

	GrievanceSystem.spike_room(world, world.room_at(4, 3), 0.2)
	assert_almost_eq(inside.grievance, 0.2, 0.001)
	assert_almost_eq(outside.grievance, 0.0, 0.001, "not in the room, not affected")

	GrievanceSystem.spike_facility(world, 0.1)
	assert_almost_eq(outside.grievance, 0.1, 0.001, "facility-wide reaches everyone")
