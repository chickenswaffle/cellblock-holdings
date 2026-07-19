class_name IncidentSystem
extends RefCounted
## Sparks incidents out of tense rooms, climbs them up the ladder, and lets
## guards (or the player) pull them back down.
##
## The whole milestone's DoD lives in the balance between two numbers here:
## escalation pressure scales with room tension, and guard presence both
## suppresses escalation and drives de-escalation. An understaffed prison has
## nothing applying the second force, so incidents ratchet; a well-staffed one
## catches them at the grudge stage and they never become anything.

## Tension below which nothing ever starts. A well-run facility must sit
## under this — it's the line between "a prison" and "a prison in trouble".
const SPARK_THRESHOLD := 0.4
## Chance per sim minute that a maximally tense room sparks something.
const SPARK_CHANCE := 0.03
## Never spark a second incident into a room that already has one.
const ONE_PER_ROOM := true

## Base per-minute chance of climbing a rung, scaled by pressure.
const ESCALATE_CHANCE := 0.05
## Minimum minutes at a rung before it can climb again — stops a quiet room
## from teleporting a grudge to a riot in six minutes.
const MIN_MINUTES_PER_RUNG := 8

## Each guard present multiplies escalation chance by this, and adds this
## much to the per-minute chance of the thing simply being broken up.
const GUARD_ESCALATION_DAMPING := 0.45
const GUARD_DEESCALATION := 0.06
## Chance per minute a low-rung incident fizzles on its own in a calm room.
const BURNOUT_CHANCE := 0.02

## Concurrent block riots that tip the whole facility over.
const BLOCK_RIOTS_FOR_FACILITY_RIOT := 2
## A single block riot this old also tips it, if the facility is already tense.
const LONE_RIOT_MINUTES := 90
const FACILITY_RIOT_TENSION := 0.65

## Violence memory added to a room when an incident turns violent.
const VIOLENCE_PER_ESCALATION := 0.25
## Sim minutes of lockdown triggered automatically by a facility riot.
const AUTO_LOCKDOWN_MINUTES := 720

## Costs and effects of the player's resolution options.
const TRANSFER_COST_PER_HEAD := 400
const CONCEDE_COST := 1200
const NEGOTIATE_RELIEF := 0.3
const CONCEDE_RELIEF := 0.2
const FORCE_INJURY_CHANCE := 0.35


static func minute_tick(world: SimWorld) -> void:
	_age_and_escalate(world)
	_spark(world)
	_check_facility_riot(world)
	_tick_lockdown(world)


# ---------------------------------------------------------------- sparking

static func _spark(world: SimWorld) -> void:
	# Built lazily: most minutes no room is over the threshold at all, and
	# indexing the whole roster to discover that would be wasted work.
	var occupancy := {}
	for room in world.rooms:
		if not room.sealed or room.tiles.is_empty():
			continue
		var tension := world.tension.value_for(room)
		if tension < SPARK_THRESHOLD:
			continue
		if ONE_PER_ROOM and _open_incident_in(world, room.key()) != null:
			continue

		var over := (tension - SPARK_THRESHOLD) / (1.0 - SPARK_THRESHOLD)
		if not world.rng.chance(SPARK_CHANCE * over):
			continue

		if occupancy.is_empty():
			occupancy = world.occupancy_index()
		var occupants := world.occupants_of(room, occupancy)
		if occupants.size() < 2:
			continue
		_open_incident(world, room, _pick_participants(world, room, occupants))


## Prefer a pair with a reason: rival faction members first, then a faction
## member and an unaffiliated prisoner in that faction's territory (prey),
## then simply the two angriest people in the room.
static func _pick_participants(world: SimWorld, room: RoomInfo, occupants: Array[Prisoner]) -> Array[Prisoner]:
	var rivals := _find_rival_pair(world, occupants)
	if not rivals.is_empty():
		return rivals

	for p in occupants:
		if not FactionSystem.is_preyed_on(world, p, room):
			continue
		for other in occupants:
			if other.faction_id >= 0:
				return [other, p]

	var sorted := occupants.duplicate()
	sorted.sort_custom(func(a: Prisoner, b: Prisoner) -> bool:
		if a.grievance == b.grievance:
			return a.id < b.id # deterministic tiebreak
		return a.grievance > b.grievance)
	return [sorted[0], sorted[1]]


static func _find_rival_pair(world: SimWorld, occupants: Array[Prisoner]) -> Array:
	for i in range(occupants.size()):
		for j in range(i + 1, occupants.size()):
			var a: Prisoner = occupants[i]
			var b: Prisoner = occupants[j]
			if a.faction_id < 0 or b.faction_id < 0 or a.faction_id == b.faction_id:
				continue
			var fa := FactionSystem.faction_at(world, a.faction_id)
			if fa != null and fa.relation_to(b.faction_id) < -0.2:
				return [a, b]
	return []


static func _open_incident(world: SimWorld, room: RoomInfo, participants: Array) -> Incident:
	var inc := Incident.new()
	inc.id = world.next_incident_id
	world.next_incident_id += 1
	inc.room_key = room.key()
	inc.kind = Incident.Kind.GRUDGE
	for p: Prisoner in participants:
		inc.participants.append(p.id)
		if p.faction_id >= 0 and not (p.faction_id in inc.faction_ids):
			inc.faction_ids.append(p.faction_id)
	world.incidents.append(inc)
	world.events.emit("incident_started", {
		"id": inc.id, "kind": inc.kind, "room": [inc.room_key.x, inc.room_key.y],
	})
	return inc


# -------------------------------------------------------------- escalation

static func _age_and_escalate(world: SimWorld) -> void:
	for inc in world.incidents:
		if not inc.is_open():
			continue
		inc.age_minutes += 1
		inc.minutes_at_rung += 1

		var room := world.room_by_key(inc.room_key)
		if room == null:
			# The room was demolished out from under it.
			_close(world, inc, Incident.Resolution.BURNED_OUT)
			continue

		var guards := world.guard_presence(room)
		if _try_deescalate(world, inc, room, guards):
			continue
		_try_escalate(world, inc, room, guards)


static func _try_deescalate(world: SimWorld, inc: Incident, room: RoomInfo, guards: int) -> bool:
	# Past a point nobody is talking anyone down; it has to be resolved.
	if inc.kind >= Incident.UNCONTAINABLE_RUNG:
		return false
	var tension := world.tension.value_for(room)
	var chance := GUARD_DEESCALATION * float(guards)
	if tension < SPARK_THRESHOLD and inc.kind <= Incident.Kind.SHOVE:
		chance += BURNOUT_CHANCE
	if chance <= 0.0 or not world.rng.chance(chance):
		return false

	if guards > 0:
		_close(world, inc, Incident.Resolution.SEPARATED)
		world.events.emit("incident_defused", {"id": inc.id, "by": "guards"})
	else:
		_close(world, inc, Incident.Resolution.BURNED_OUT)
	return true


static func _try_escalate(world: SimWorld, inc: Incident, room: RoomInfo, guards: int) -> void:
	if inc.minutes_at_rung < MIN_MINUTES_PER_RUNG:
		return
	var next := _next_rung(world, inc)
	if next < 0:
		return

	var pressure := _escalation_pressure(world, inc, room)
	var chance := ESCALATE_CHANCE * pressure * pow(GUARD_ESCALATION_DAMPING, float(guards))
	if not world.rng.chance(chance):
		return
	_escalate(world, inc, room, next)


## The ladder is a graph, not a line — the design doc draws it on two rows
## for a reason. The weapon rungs are a *detour* taken when contraband is
## around, not a toll gate: a brawl with no knives in the block still becomes
## a faction war and then a riot. Gating riots behind contraband would mean a
## prison with no visitation room could never riot however badly it was run,
## which is both wrong and would quietly break this milestone's DoD.
##
## Returns the next kind, or -1 if this incident can't climb any further.
static func _next_rung(world: SimWorld, inc: Incident) -> int:
	match inc.kind:
		Incident.Kind.BRAWL:
			if world.contraband.weapons_available(inc.room_key):
				return Incident.Kind.WEAPON
			return Incident.Kind.FACTION_WAR if inc.faction_ids.size() >= 2 else Incident.Kind.BLOCK_RIOT
		Incident.Kind.STABBING:
			return Incident.Kind.FACTION_WAR if inc.faction_ids.size() >= 2 else Incident.Kind.BLOCK_RIOT
		Incident.Kind.FACTION_WAR:
			return Incident.Kind.BLOCK_RIOT
		# A facility riot is promoted by _check_facility_riot once enough of
		# the place is alight, not climbed into from a single block.
		Incident.Kind.BLOCK_RIOT, Incident.Kind.FACILITY_RIOT, Incident.Kind.HOSTAGE:
			return -1
		_:
			return inc.kind + 1




## 0..~1.5. Room tension does most of the work; the participants' own anger
## and the strength of the factions behind them push it further.
static func _escalation_pressure(world: SimWorld, inc: Incident, room: RoomInfo) -> float:
	var tension := world.tension.value_for(room)
	var anger := 0.0
	var counted := 0
	for pid in inc.participants:
		var p := world.prisoner_at(pid)
		if p != null:
			anger += p.grievance
			counted += 1
	if counted > 0:
		anger /= float(counted)

	var backing := 0.0
	for fid in inc.faction_ids:
		var f := FactionSystem.faction_at(world, fid)
		if f != null:
			backing = maxf(backing, f.strength)

	return clampf(0.7 * tension + 0.4 * anger + 0.4 * backing, 0.0, 1.5)


static func _escalate(world: SimWorld, inc: Incident, room: RoomInfo, next_kind: int) -> void:
	inc.kind = next_kind
	inc.minutes_at_rung = 0

	if inc.is_violent():
		world.tension.add_violence(inc.room_key, VIOLENCE_PER_ESCALATION)
		for pid in inc.participants:
			var p := world.prisoner_at(pid)
			if p != null:
				GrievanceSystem.add_spike(p, GrievanceSystem.SPIKE_WITNESSED_VIOLENCE * 2.0)
		GrievanceSystem.spike_room(world, room, GrievanceSystem.SPIKE_WITNESSED_VIOLENCE)

	# A riot stops being about two people — the whole room is in it now.
	if inc.is_riot():
		for p in world.prisoners_in_room(world.room_by_key(inc.room_key)):
			if not (p.id in inc.participants):
				inc.participants.append(p.id)

	for fid in inc.faction_ids:
		var f := FactionSystem.faction_at(world, fid)
		if f != null:
			f.heat = clampf(f.heat + 0.1, 0.0, 1.0)
	# Blood between blocs is what turns a fight into a standing feud.
	if inc.faction_ids.size() >= 2 and inc.is_violent():
		_worsen_relations(world, inc.faction_ids)

	world.events.emit("incident_escalated", {
		"id": inc.id, "kind": inc.kind, "label": inc.label(),
		"room": [inc.room_key.x, inc.room_key.y],
	})


static func _worsen_relations(world: SimWorld, faction_ids: Array[int]) -> void:
	for i in range(faction_ids.size()):
		for j in range(i + 1, faction_ids.size()):
			var a := FactionSystem.faction_at(world, faction_ids[i])
			var b := FactionSystem.faction_at(world, faction_ids[j])
			if a != null and b != null:
				a.shift_relation(b.id, -0.1)
				b.shift_relation(a.id, -0.1)


# ------------------------------------------------------------ facility riot

static func _check_facility_riot(world: SimWorld) -> void:
	var riots: Array[Incident] = []
	for inc in world.incidents:
		if inc.is_open() and inc.kind == Incident.Kind.BLOCK_RIOT:
			riots.append(inc)
	if riots.is_empty():
		return

	var tips := riots.size() >= BLOCK_RIOTS_FOR_FACILITY_RIOT
	if not tips:
		var lone := riots[0]
		tips = lone.minutes_at_rung >= LONE_RIOT_MINUTES and world.tension.mean() >= FACILITY_RIOT_TENSION
	if not tips:
		return

	# The oldest block riot becomes the facility riot; the others fold into it.
	var primary := riots[0]
	for inc in riots:
		if inc.age_minutes > primary.age_minutes:
			primary = inc
	primary.kind = Incident.Kind.FACILITY_RIOT
	primary.minutes_at_rung = 0
	for inc in riots:
		if inc != primary:
			_close(world, inc, Incident.Resolution.BURNED_OUT)
	for p in world.prisoners:
		if not (p.id in primary.participants):
			primary.participants.append(p.id)

	world.events.emit("facility_riot", {"id": primary.id, "participants": primary.participants.size()})
	begin_lockdown(world, AUTO_LOCKDOWN_MINUTES)


# ---------------------------------------------------------------- lockdown

## Confine everyone. Ends incidents' ability to spread but costs goodwill
## every minute it runs, which is the trade the player is actually making.
static func begin_lockdown(world: SimWorld, minutes: int) -> void:
	world.lockdown_minutes = maxi(world.lockdown_minutes, minutes)
	world.events.emit("lockdown_started", {"minutes": world.lockdown_minutes})


static func _tick_lockdown(world: SimWorld) -> void:
	if world.lockdown_minutes <= 0:
		return
	world.lockdown_minutes -= 1
	# Being locked in a cell all day is its own grievance engine — this is
	# why lockdown can't be the player's default answer to everything.
	for p in world.prisoners:
		GrievanceSystem.add_spike(p, 0.0006)
	if world.lockdown_minutes == 0:
		world.events.emit("lockdown_ended", {})


# -------------------------------------------------------------- resolution

static func open_incidents(world: SimWorld) -> Array[Incident]:
	var out: Array[Incident] = []
	for inc in world.incidents:
		if inc.is_open():
			out.append(inc)
	return out


static func worst_open(world: SimWorld) -> Incident:
	var worst: Incident = null
	for inc in open_incidents(world):
		if worst == null or inc.kind > worst.kind:
			worst = inc
	return worst


static func _open_incident_in(world: SimWorld, room_key: Vector2i) -> Incident:
	for inc in world.incidents:
		if inc.is_open() and inc.room_key == room_key:
			return inc
	return null


static func _close(world: SimWorld, inc: Incident, resolution: int) -> void:
	inc.resolution = resolution
	world.events.emit("incident_closed", {
		"id": inc.id, "kind": inc.kind, "resolution": resolution,
	})


## Move the participants out of the facility entirely. Fast and clean, costs
## money — and in the franchise layer it's someone else's problem now.
static func resolve_separate(world: SimWorld, inc: Incident) -> bool:
	if not inc.is_open():
		return false
	var cost := TRANSFER_COST_PER_HEAD * mini(inc.participants.size(), 4)
	if not world.ledger.spend(cost, "transfer"):
		return false
	var moved := 0
	for pid in inc.participants.duplicate():
		if moved >= 4:
			break
		if world.remove_prisoner(pid, "transferred"):
			moved += 1
	_close(world, inc, Incident.Resolution.SEPARATED)
	return true


## Ends it immediately and poisons the well. Tracked for M6's oversight.
static func resolve_solitary(world: SimWorld, inc: Incident) -> bool:
	if not inc.is_open():
		return false
	for pid in inc.participants:
		var p := world.prisoner_at(pid)
		if p == null:
			continue
		GrievanceSystem.add_spike(p, GrievanceSystem.SPIKE_SOLITARY)
		p.reform = maxf(0.0, p.reform - 0.5)
	world.solitary_uses += 1
	_close(world, inc, Incident.Resolution.SOLITARY)
	return true


## The only option that lowers grievance for real — and it needs a support
## staffer on duty, so it's unavailable exactly when the player has cut costs
## to the bone. Talking down a full riot doesn't work.
static func resolve_negotiate(world: SimWorld, inc: Incident) -> bool:
	if not inc.is_open() or world.on_duty_count(Staff.Role.SUPPORT) == 0:
		return false
	if inc.kind >= Incident.Kind.FACILITY_RIOT:
		return false
	var room := world.room_by_key(inc.room_key)
	if room != null:
		for p in world.prisoners_in_room(room):
			p.grievance = maxf(0.0, p.grievance - NEGOTIATE_RELIEF)
	for pid in inc.participants:
		var p := world.prisoner_at(pid)
		if p != null:
			p.grievance = maxf(0.0, p.grievance - NEGOTIATE_RELIEF)
	_close(world, inc, Incident.Resolution.NEGOTIATED)
	return true


## Guards wade in. Always works, always costs — injuries and a facility-wide
## grievance spike. Needs guards on duty to be possible at all.
static func resolve_force(world: SimWorld, inc: Incident) -> bool:
	if not inc.is_open() or world.on_duty_count(Staff.Role.GUARD) == 0:
		return false
	var injuries := 0
	for pid in inc.participants:
		var p := world.prisoner_at(pid)
		if p == null:
			continue
		GrievanceSystem.add_spike(p, GrievanceSystem.SPIKE_FORCE)
		if world.rng.chance(FORCE_INJURY_CHANCE):
			injuries += 1
			p.needs.values[Needs.Kind.SAFETY] = maxf(0.0, p.needs.get_value(Needs.Kind.SAFETY) - 0.4)
	GrievanceSystem.spike_facility(world, GrievanceSystem.SPIKE_FORCE * 0.3)
	world.tension.add_violence(inc.room_key, VIOLENCE_PER_ESCALATION)
	_close(world, inc, Incident.Resolution.FORCED)
	world.events.emit("force_used", {"id": inc.id, "injuries": injuries})
	return true


## Give them what they want. It genuinely works; the board reads the cost line.
static func resolve_concede(world: SimWorld, inc: Incident) -> bool:
	if not inc.is_open() or not world.ledger.spend(CONCEDE_COST, "concession"):
		return false
	GrievanceSystem.spike_facility(world, -CONCEDE_RELIEF)
	_close(world, inc, Incident.Resolution.CONCEDED)
	return true
