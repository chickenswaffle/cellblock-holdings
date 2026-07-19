extends GutTest
## The incident ladder: what sparks, what climbs, what stops it, and what the
## player's five resolution options actually cost.


## One sealed cell block, deliberately tiny so tension is easy to control.
func _block_world(seed_value: int) -> SimWorld:
	var world := SimWorld.new(seed_value, 20, 12)
	var g := world.grid
	FacilityBuilder.build_box(g, 2, 2, 7, 4)
	g.set_door(2, 3, SimTile.WALL_W, true)
	for b in range(3):
		g.place_object(3 + b, 3, ObjectDef.Type.BED)
	g.place_object(7, 3, ObjectDef.Type.TOILET)
	world.tick()
	world.zone_room(world.room_at(4, 3).id, ZoneValidator.Kind.CELL)
	world.gate_tile = Vector2i(1, 10)
	return world


func _room(world: SimWorld) -> RoomInfo:
	return world.room_at(4, 3)


func _fill(world: SimWorld, count: int, grievance: float) -> void:
	for i in range(count):
		var p := Intake.generate_prisoner(world)
		p.place_at_tile(Vector2i(4, 3))
		p.grievance = grievance
		world.prisoners.append(p)


## Force a room to a chosen tension so escalation can be tested directly
## rather than waited for.
func _set_tension(world: SimWorld, value: float) -> void:
	world.tension.values[_room(world).key()] = value


func _start_incident(world: SimWorld) -> Incident:
	_set_tension(world, 0.95)
	for i in range(600):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95) # minute_tick doesn't recompute it here
		var open := IncidentSystem.open_incidents(world)
		if not open.is_empty():
			return open[0]
	return null


func test_calm_rooms_never_spark() -> void:
	var world := _block_world(1)
	_fill(world, 6, 0.9)
	_set_tension(world, IncidentSystem.SPARK_THRESHOLD - 0.01)
	for i in range(2000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, IncidentSystem.SPARK_THRESHOLD - 0.01)
	assert_eq(world.incidents.size(), 0, "below the threshold nothing starts, ever")


func test_a_tense_room_sparks_an_incident_at_the_bottom_of_the_ladder() -> void:
	var world := _block_world(2)
	_fill(world, 6, 0.9)
	var inc := _start_incident(world)
	assert_not_null(inc, "a maximally tense room should spark something")
	assert_eq(inc.kind, Incident.Kind.GRUDGE, "incidents start at the bottom rung")
	assert_eq(inc.participants.size(), 2, "a grudge is between two people")
	assert_false(inc.is_violent())
	assert_false(inc.is_riot())


func test_only_one_incident_runs_per_room_at_a_time() -> void:
	var world := _block_world(3)
	_fill(world, 6, 0.9)
	_start_incident(world)
	for i in range(1000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95)
	assert_eq(IncidentSystem.open_incidents(world).size(), 1)


func test_an_unopposed_incident_climbs_all_the_way_to_a_riot() -> void:
	var world := _block_world(4)
	_fill(world, 8, 0.95)
	var inc := _start_incident(world)
	assert_not_null(inc)
	for i in range(5000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95)
		if inc.is_riot() or not inc.is_open():
			break
	assert_true(inc.is_riot(), "with nobody intervening it should reach a riot, got '%s'" % inc.label())


## The weapon rungs are a branch, not a gate. A prison with no contraband
## still riots — gating riots behind contraband would silently break the
## milestone DoD for any facility without a visitation room.
func test_a_riot_does_not_require_contraband() -> void:
	var world := _block_world(5)
	_fill(world, 8, 0.95)
	assert_almost_eq(world.contraband.total(), 0.0, 0.001, "fixture has no contraband")
	var inc := _start_incident(world)
	for i in range(5000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95)
		if inc.is_riot() or not inc.is_open():
			break
	assert_true(inc.is_riot())
	assert_almost_eq(world.contraband.total(), 0.0, 0.001, "and still none by the end")


func test_weapons_are_only_reachable_when_contraband_is_present() -> void:
	var world := _block_world(6)
	_fill(world, 8, 0.95)
	var inc := _start_incident(world)
	inc.kind = Incident.Kind.BRAWL
	inc.minutes_at_rung = IncidentSystem.MIN_MINUTES_PER_RUNG
	assert_ne(IncidentSystem._next_rung(world, inc), Incident.Kind.WEAPON,
		"no contraband, no weapon rung")

	world.contraband.stash[_room(world).key()] = 1.0
	assert_eq(IncidentSystem._next_rung(world, inc), Incident.Kind.WEAPON,
		"with a stash on the block, weapons come out")


func test_a_riot_pulls_in_everyone_in_the_room() -> void:
	var world := _block_world(7)
	_fill(world, 8, 0.95)
	var inc := _start_incident(world)
	for i in range(5000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95)
		if inc.is_riot() or not inc.is_open():
			break
	assert_true(inc.is_riot())
	assert_eq(inc.participants.size(), 8, "a riot is the whole block, not two people")


func test_guards_suppress_escalation_and_break_things_up() -> void:
	var unguarded := _block_world(8)
	_fill(unguarded, 6, 0.9)
	var a := _start_incident(unguarded)

	var guarded := _block_world(8)
	_fill(guarded, 6, 0.9)
	var b := _start_incident(guarded)
	var guard := Crew.staff_up(guarded, Staff.Role.GUARD, 3, guarded.room_center(_room(guarded)))
	for g in guard:
		g.state = Staff.State.PATROLLING

	for i in range(600):
		IncidentSystem.minute_tick(unguarded)
		_set_tension(unguarded, 0.95)
		IncidentSystem.minute_tick(guarded)
		_set_tension(guarded, 0.95)

	assert_true(not b.is_open() or b.kind < a.kind,
		"guards should have defused it or at least held it lower (a=%s b=%s)" % [a.label(), b.label()])


func test_violence_leaves_a_mark_on_the_room_and_raises_grievance() -> void:
	var world := _block_world(9)
	_fill(world, 6, 0.5)
	var inc := _start_incident(world)
	var before := world.prisoners[0].grievance
	for i in range(3000):
		IncidentSystem.minute_tick(world)
		_set_tension(world, 0.95)
		if inc.is_violent() or not inc.is_open():
			break
	assert_true(inc.is_violent(), "should have reached a fight")
	assert_gt(float(world.tension.violence.get(inc.room_key, 0.0)), 0.0, "the room remembers")
	assert_gt(world.prisoners[0].grievance, before, "and everyone present resents it")


# ------------------------------------------------------------- resolutions

func test_force_needs_guards_and_costs_facility_wide_goodwill() -> void:
	var world := _block_world(10)
	_fill(world, 6, 0.3)
	var inc := _start_incident(world)
	assert_false(IncidentSystem.resolve_force(world, inc), "no guards on duty, no force option")

	var guards := Crew.staff_up(world, Staff.Role.GUARD, 2, world.gate_tile)
	for g in guards:
		g.shift = Staff.Shift.DAY
	Crew.set_hour(world, 10)
	var before := world.prisoners[0].grievance
	assert_true(IncidentSystem.resolve_force(world, inc))
	assert_false(inc.is_open())
	assert_eq(inc.resolution, Incident.Resolution.FORCED)
	assert_gt(world.prisoners[0].grievance, before, "force is never free")


func test_negotiate_needs_a_support_staffer_and_actually_lowers_grievance() -> void:
	var world := _block_world(11)
	_fill(world, 6, 0.8)
	var inc := _start_incident(world)
	assert_false(IncidentSystem.resolve_negotiate(world, inc), "nobody to do the talking")

	var support := Crew.staff_up(world, Staff.Role.SUPPORT, 1, world.gate_tile)[0]
	support.shift = Staff.Shift.DAY
	Crew.set_hour(world, 10)
	var before := world.prisoners[0].grievance
	assert_true(IncidentSystem.resolve_negotiate(world, inc))
	assert_lt(world.prisoners[0].grievance, before, "the only option that genuinely helps")


func test_negotiating_a_facility_riot_is_not_an_option() -> void:
	var world := _block_world(12)
	_fill(world, 6, 0.8)
	var inc := _start_incident(world)
	inc.kind = Incident.Kind.FACILITY_RIOT
	var support := Crew.staff_up(world, Staff.Role.SUPPORT, 1, world.gate_tile)[0]
	support.shift = Staff.Shift.DAY
	Crew.set_hour(world, 10)
	assert_false(IncidentSystem.resolve_negotiate(world, inc), "you can't talk down a full riot")


func test_solitary_ends_it_spikes_grievance_and_is_counted_for_oversight() -> void:
	var world := _block_world(13)
	_fill(world, 6, 0.3)
	var inc := _start_incident(world)
	var victim := world.prisoner_at(inc.participants[0])
	victim.reform = 1.0
	var before := victim.grievance

	assert_true(IncidentSystem.resolve_solitary(world, inc))
	assert_false(inc.is_open())
	assert_gt(victim.grievance, before)
	assert_almost_eq(victim.reform, 0.5, 0.001, "solitary destroys reform progress")
	assert_eq(world.solitary_uses, 1, "M6's oversight model needs this counted")


func test_separate_transfers_participants_out_and_frees_their_beds() -> void:
	var world := _block_world(14)
	_fill(world, 6, 0.3)
	# Give the two who'll be transferred an owned bed so we can watch it free.
	var inc := _start_incident(world)
	var first := world.prisoner_at(inc.participants[0])
	first.cell_bed_pos = Vector2i(3, 3)
	world.grid.object_at(3, 3).owner_id = first.id

	var population := world.prisoners.size()
	var balance := world.ledger.balance
	assert_true(IncidentSystem.resolve_separate(world, inc))
	assert_lt(world.prisoners.size(), population, "they're somebody else's problem now")
	assert_lt(world.ledger.balance, balance, "transfers cost money")
	assert_eq(world.grid.object_at(3, 3).owner_id, -1, "and the bed is reusable")


func test_concede_costs_money_and_calms_the_whole_facility() -> void:
	var world := _block_world(15)
	_fill(world, 6, 0.8)
	var inc := _start_incident(world)
	var before := world.prisoners[0].grievance
	var balance := world.ledger.balance

	assert_true(IncidentSystem.resolve_concede(world, inc))
	assert_eq(world.ledger.balance, balance - IncidentSystem.CONCEDE_COST)
	assert_lt(world.prisoners[0].grievance, before, "giving in works, that's the point")


func test_a_broke_operator_cannot_concede() -> void:
	var world := _block_world(16)
	_fill(world, 6, 0.8)
	var inc := _start_incident(world)
	world.ledger.spend(world.ledger.balance, "spent it all")
	assert_false(IncidentSystem.resolve_concede(world, inc))
	assert_true(inc.is_open(), "and the incident is still running")


func test_resolved_incidents_ignore_further_resolution() -> void:
	var world := _block_world(17)
	_fill(world, 6, 0.5)
	var inc := _start_incident(world)
	assert_true(IncidentSystem.resolve_solitary(world, inc))
	assert_false(IncidentSystem.resolve_solitary(world, inc), "already closed")


# ---------------------------------------------------------------- lockdown

func test_lockdown_confines_everyone_regardless_of_the_timetable() -> void:
	var world := _block_world(18)
	_fill(world, 4, 0.2)
	Crew.set_hour(world, 12) # normally a YARD hour
	assert_ne(world.current_block(), ScheduleSystem.Block.LOCKUP)

	IncidentSystem.begin_lockdown(world, 120)
	assert_true(world.is_locked_down())
	assert_eq(world.current_block(), ScheduleSystem.Block.LOCKUP, "lockdown overrides the schedule")


func test_lockdown_expires_and_grinds_on_people_while_it_runs() -> void:
	var world := _block_world(19)
	_fill(world, 4, 0.2)
	var before := world.prisoners[0].grievance
	IncidentSystem.begin_lockdown(world, 60)
	for i in range(60):
		IncidentSystem.minute_tick(world)
	assert_false(world.is_locked_down(), "it should end on schedule")
	assert_gt(world.prisoners[0].grievance, before, "being locked in all day costs goodwill")


func test_serialization_roundtrip() -> void:
	var world := _block_world(20)
	_fill(world, 6, 0.6)
	var inc := _start_incident(world)
	IncidentSystem.begin_lockdown(world, 90)

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_eq(restored.incidents.size(), world.incidents.size())
	assert_eq(restored.lockdown_minutes, world.lockdown_minutes)
	var copy: Incident = restored.incidents[0]
	assert_eq(copy.id, inc.id)
	assert_eq(copy.kind, inc.kind)
	assert_eq(copy.room_key, inc.room_key)
	assert_eq(copy.participants.size(), inc.participants.size())
