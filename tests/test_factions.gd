extends GutTest
## Faction formation, recruitment, territory and the rivalry term the tension
## model reads.


func _populated_world(seed_value: int, population: int) -> SimWorld:
	var world := SimWorld.new(seed_value, 40, 16)
	var g := world.grid
	for i in range(4):
		var x0 := 1 + i * 8
		FacilityBuilder.build_box(g, x0, 1, x0 + 6, 3)
		g.set_door(x0 + 3, 3, SimTile.WALL_S, true)
		for b in range(6):
			g.place_object(x0 + b, 1, ObjectDef.Type.BED)
		g.place_object(x0 + 6, 1, ObjectDef.Type.TOILET)
	world.tick()
	for i in range(4):
		world.zone_room(world.room_at(1 + i * 8 + 1, 1).id, ZoneValidator.Kind.CELL)
	FacilityBuilder.intake_n(world, population)
	return world


func test_no_factions_form_below_the_population_threshold() -> void:
	var world := _populated_world(1, FactionSystem.MIN_POPULATION - 1)
	FactionSystem.hour_tick(world)
	assert_eq(world.factions.size(), 0, "too few people for blocs to mean anything")


func test_factions_form_once_the_population_is_big_enough() -> void:
	var world := _populated_world(2, 20)
	FactionSystem.hour_tick(world)
	assert_between(world.factions.size(), FactionSystem.MIN_FACTIONS, FactionSystem.MAX_FACTIONS)
	var names := {}
	for f in world.factions:
		assert_ne(f.fname, "", "factions should be named")
		names[f.fname] = true
	assert_eq(names.size(), world.factions.size(), "names must be unique")


func test_relations_start_hostile_and_are_symmetric() -> void:
	var world := _populated_world(3, 20)
	FactionSystem.hour_tick(world)
	for a in world.factions:
		for b in world.factions:
			if a.id == b.id:
				continue
			assert_lt(a.relation_to(b.id), 0.0, "%s should be wary of %s" % [a.fname, b.fname])
			assert_almost_eq(a.relation_to(b.id), b.relation_to(a.id), 0.0001, "relations are mutual")


func test_recruitment_prefers_the_aggrieved_and_unsafe() -> void:
	var world := _populated_world(4, 24)
	FactionSystem.hour_tick(world)
	# Everyone is a day in and thoroughly miserable, so recruitment should
	# actually happen rather than being gated on time served.
	world.clock.tick_count = SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY * 3
	for p in world.prisoners:
		p.grievance = 0.9
		p.needs.values[Needs.Kind.SAFETY] = 0.1

	for hour in range(72):
		FactionSystem.hour_tick(world)
	var affiliated := 0
	for p in world.prisoners:
		if p.faction_id >= 0:
			affiliated += 1
	assert_gt(affiliated, 0, "a miserable population should produce members")


func test_fresh_arrivals_are_not_recruited() -> void:
	var world := _populated_world(5, 20)
	FactionSystem.hour_tick(world)
	for p in world.prisoners:
		p.grievance = 1.0
		p.needs.values[Needs.Kind.SAFETY] = 0.0
		p.admitted_day = world.clock.day() # arrived today
	for hour in range(48):
		FactionSystem.hour_tick(world)
	for p in world.prisoners:
		assert_eq(p.faction_id, -1, "nobody is trusted on day one")


func test_rivalry_needs_two_hostile_blocs_in_the_same_room() -> void:
	var world := _populated_world(6, 20)
	FactionSystem.hour_tick(world)
	var a: Faction = world.factions[0]
	var b: Faction = world.factions[1]
	a.set_relation(b.id, -0.9)
	b.set_relation(a.id, -0.9)

	var solo: Array[Prisoner] = [world.prisoners[0], world.prisoners[1]]
	solo[0].faction_id = a.id
	solo[1].faction_id = a.id
	assert_almost_eq(FactionSystem.rivalry_in(world, solo), 0.0, 0.001,
		"one bloc alone is not a standoff, however strong")

	solo[1].faction_id = b.id
	assert_gt(FactionSystem.rivalry_in(world, solo), 0.0, "two rivals in a room is")


func test_allied_factions_generate_no_rivalry() -> void:
	var world := _populated_world(7, 20)
	FactionSystem.hour_tick(world)
	var a: Faction = world.factions[0]
	var b: Faction = world.factions[1]
	a.set_relation(b.id, 0.5)
	b.set_relation(a.id, 0.5)
	var pair: Array[Prisoner] = [world.prisoners[0], world.prisoners[1]]
	pair[0].faction_id = a.id
	pair[1].faction_id = b.id
	assert_almost_eq(FactionSystem.rivalry_in(world, pair), 0.0, 0.001)


func test_territory_follows_where_members_sleep() -> void:
	var world := _populated_world(8, 24)
	FactionSystem.hour_tick(world)
	var a: Faction = world.factions[0]

	# Put a clear majority of one block's residents in faction A.
	var block := world.room_at(2, 1)
	var claimed := 0
	for p in world.prisoners:
		if p.cell_bed_pos.x < 0:
			continue
		var room := world.room_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
		if room != null and room.id == block.id:
			p.faction_id = a.id
			claimed += 1
	assert_gt(claimed, 0, "fixture should have residents in that block")

	FactionSystem.hour_tick(world)
	assert_true(a.holds(block.key()), "a faction that fills a block owns it")


func test_unaffiliated_prisoners_in_owned_territory_are_prey() -> void:
	var world := _populated_world(9, 20)
	FactionSystem.hour_tick(world)
	var a: Faction = world.factions[0]
	var block := world.room_at(2, 1)
	a.territory.append(block.key())

	var loner := world.prisoners[0]
	loner.faction_id = -1
	assert_true(FactionSystem.is_preyed_on(world, loner, block))

	loner.faction_id = a.id
	assert_false(FactionSystem.is_preyed_on(world, loner, block), "members aren't prey")


func test_heat_decays_over_time() -> void:
	var world := _populated_world(10, 20)
	FactionSystem.hour_tick(world)
	var f: Faction = world.factions[0]
	f.heat = 1.0
	for hour in range(10):
		FactionSystem.hour_tick(world)
	assert_lt(f.heat, 1.0, "attention fades")


func test_serialization_roundtrip() -> void:
	var world := _populated_world(11, 20)
	FactionSystem.hour_tick(world)
	var original: Faction = world.factions[0]
	original.heat = 0.42
	original.territory.append(Vector2i(3, 4))

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_eq(restored.factions.size(), world.factions.size())
	var copy: Faction = restored.factions[0]
	assert_eq(copy.fname, original.fname)
	assert_almost_eq(copy.heat, 0.42, 0.0001)
	assert_true(copy.holds(Vector2i(3, 4)))
	assert_almost_eq(
		copy.relation_to(world.factions[1].id),
		original.relation_to(world.factions[1].id), 0.0001
	)
