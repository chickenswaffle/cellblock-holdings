extends GutTest
## Contraband supply, searches, and informants.


## A visitation room and a cell block joined by a door, so supply has
## somewhere to enter and somewhere to flow to.
func _supply_world(seed_value: int) -> SimWorld:
	var world := SimWorld.new(seed_value, 24, 14)
	var g := world.grid
	FacilityBuilder.build_box(g, 2, 2, 7, 5)
	g.set_door(7, 3, SimTile.WALL_E, true) # visits <-> block
	g.place_object(4, 3, ObjectDef.Type.TABLE)
	g.place_object(5, 3, ObjectDef.Type.PHONE)

	FacilityBuilder.build_box(g, 8, 2, 13, 5)
	g.set_door(8, 5, SimTile.WALL_S, true)
	for b in range(3):
		g.place_object(9 + b, 3, ObjectDef.Type.BED)
	g.place_object(13, 3, ObjectDef.Type.TOILET)

	world.tick()
	world.zone_room(world.room_at(4, 3).id, ZoneValidator.Kind.VISITATION)
	world.zone_room(world.room_at(10, 3).id, ZoneValidator.Kind.CELL)
	world.gate_tile = Vector2i(1, 12)
	return world


func _visits(world: SimWorld) -> RoomInfo:
	return world.room_at(4, 3)


func _block(world: SimWorld) -> RoomInfo:
	return world.room_at(10, 3)


func test_contraband_enters_through_visitation() -> void:
	var world := _supply_world(1)
	assert_almost_eq(world.contraband.total(), 0.0, 0.001)
	for hour in range(5):
		world.contraband.hour_tick(world)
	assert_gt(world.contraband.amount_in(_visits(world).key()), 0.0, "visits bring things in")


func test_guards_on_the_visits_room_stem_the_flow() -> void:
	var unwatched := _supply_world(2)
	for hour in range(5):
		unwatched.contraband.hour_tick(unwatched)

	var watched := _supply_world(2)
	var guards := Crew.staff_up(watched, Staff.Role.GUARD, 2, watched.room_center(_visits(watched)))
	for g in guards:
		g.state = Staff.State.PATROLLING
	for hour in range(5):
		watched.contraband.hour_tick(watched)

	assert_lt(
		watched.contraband.amount_in(_visits(watched).key()),
		unwatched.contraband.amount_in(_visits(unwatched).key()),
		"watching the visits room suppresses supply"
	)


func test_contraband_spreads_along_the_room_graph() -> void:
	var world := _supply_world(3)
	world.contraband.stash[_visits(world).key()] = 1.0
	for hour in range(10):
		world.contraband.hour_tick(world)
	assert_gt(world.contraband.amount_in(_block(world).key()), 0.0,
		"a stash next door works its way through the door")


func test_unpaid_or_exhausted_staff_carry_things_in() -> void:
	var clean := _supply_world(4)
	for hour in range(6):
		clean.contraband.hour_tick(clean)
	var baseline := clean.contraband.total()

	var compromised := _supply_world(4)
	var crew := Crew.staff_up(compromised, Staff.Role.GUARD, 3, compromised.gate_tile)
	for s in crew:
		s.unpaid_days = 2
	for hour in range(6):
		compromised.contraband.hour_tick(compromised)

	assert_gt(compromised.contraband.total(), baseline,
		"staff you don't pay start bringing things in — a consequence, not an event")


func test_weapons_become_available_past_the_threshold() -> void:
	var world := _supply_world(5)
	var key := _block(world).key()
	world.contraband.stash[key] = Contraband.WEAPON_THRESHOLD - 0.01
	assert_false(world.contraband.weapons_available(key))
	world.contraband.stash[key] = Contraband.WEAPON_THRESHOLD + 0.01
	assert_true(world.contraband.weapons_available(key))


func test_searching_seizes_supply_but_costs_goodwill() -> void:
	var world := _supply_world(6)
	var block := _block(world)
	world.contraband.stash[block.key()] = 1.0
	var p := Intake.generate_prisoner(world)
	p.place_at_tile(Vector2i(10, 3))
	p.grievance = 0.2
	world.prisoners.append(p)

	var found := world.contraband.search_room(world, block)
	assert_gt(found, 0.0, "there was something to find")
	assert_lt(world.contraband.amount_in(block.key()), 1.0, "and it was seized")
	assert_gt(p.grievance, 0.2, "searches always cost goodwill")


func test_a_fruitless_search_still_costs_goodwill() -> void:
	var world := _supply_world(7)
	var block := _block(world)
	var p := Intake.generate_prisoner(world)
	p.place_at_tile(Vector2i(10, 3))
	p.grievance = 0.2
	world.prisoners.append(p)

	assert_almost_eq(world.contraband.search_room(world, block), 0.0, 0.001, "nothing there")
	assert_gt(p.grievance, 0.2, "no search rate is both safe and calm — that's the trap")


func test_searching_a_blocks_owner_raises_their_heat() -> void:
	var world := _supply_world(8)
	var block := _block(world)
	var f := Faction.new()
	f.id = 0
	f.fname = "Test Crew"
	f.territory.append(block.key())
	world.factions.append(f)

	world.contraband.search_room(world, block)
	assert_gt(f.heat, 0.0, "the faction whose block was tossed takes it personally")


func test_serialization_roundtrip() -> void:
	var world := _supply_world(9)
	world.contraband.stash[_block(world).key()] = 0.6
	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_almost_eq(restored.contraband.amount_in(_block(world).key()), 0.6, 0.0001)


# ---------------------------------------------------------------- snitches

func _affiliated_prisoner(world: SimWorld) -> Prisoner:
	var f := Faction.new()
	f.id = 0
	f.fname = "Test Crew"
	world.factions.append(f)
	var p := Intake.generate_prisoner(world)
	p.place_at_tile(Vector2i(10, 3))
	p.faction_id = f.id
	p.needs.values[Needs.Kind.SAFETY] = 0.0 # frightened, so easy to turn
	world.prisoners.append(p)
	return p


func test_unaffiliated_prisoners_have_nothing_to_sell() -> void:
	var world := _supply_world(10)
	var p := Intake.generate_prisoner(world)
	p.place_at_tile(Vector2i(10, 3))
	world.prisoners.append(p)
	assert_false(Snitches.recruit(world, p), "no faction, no information")


func test_a_recruited_informant_reveals_stashes_and_hotspots() -> void:
	var world := _supply_world(11)
	var p := _affiliated_prisoner(world)
	# Recruitment is a roll; try until it takes so the test isn't seed-fragile.
	var turned := false
	for attempt in range(50):
		if Snitches.recruit(world, p):
			turned = true
			break
	assert_true(turned, "a frightened prisoner should be recruitable within 50 tries")

	world.contraband.stash[_block(world).key()] = 0.7
	world.tension.values[_block(world).key()] = 0.6
	var report := Snitches.debrief(world)
	assert_eq((report["stashes"] as Array).size(), 1, "should name the stash")
	assert_eq((report["hotspots"] as Array).size(), 1, "and the room about to go up")


func test_debriefing_with_no_informants_reveals_nothing() -> void:
	var world := _supply_world(12)
	var report := Snitches.debrief(world)
	assert_eq((report["stashes"] as Array).size(), 0)
	assert_eq((report["hotspots"] as Array).size(), 0)


func test_using_an_informant_raises_their_exposure() -> void:
	var world := _supply_world(13)
	var p := _affiliated_prisoner(world)
	p.is_informant = true
	Snitches.debrief(world)
	assert_almost_eq(p.informant_exposure, Snitches.EXPOSURE_PER_USE, 0.0001)


func test_leaning_on_an_informant_gets_them_killed() -> void:
	var world := _supply_world(14)
	var p := _affiliated_prisoner(world)
	p.is_informant = true
	var faction := world.factions[0]

	var discovered := false
	for i in range(40):
		Snitches.debrief(world)
		if world.prisoner_at(p.id) == null:
			discovered = true
			break
	assert_true(discovered, "constant use has to be fatal — that's the whole cost")
	assert_gt(faction.heat, 0.0, "and their faction's heat explodes")


func test_exposure_decays_while_an_informant_is_left_alone() -> void:
	var world := _supply_world(15)
	var p := _affiliated_prisoner(world)
	p.is_informant = true
	p.informant_exposure = 0.5
	p.informant_last_used_day = -1
	Snitches.day_tick(world)
	assert_lt(p.informant_exposure, 0.5, "laying off cools it down")


func test_informant_state_survives_a_round_trip() -> void:
	var world := _supply_world(16)
	var p := _affiliated_prisoner(world)
	p.is_informant = true
	p.informant_exposure = 0.33

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	var copy := restored.prisoner_at(p.id)
	assert_not_null(copy)
	assert_true(copy.is_informant)
	assert_almost_eq(copy.informant_exposure, 0.33, 0.0001)
