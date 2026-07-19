class_name TensionField
extends RefCounted
## Per-room pressure, 0..1, diffusing between connected rooms like heat.
##
## Two things make this the model the rest of M4 hangs off. First, tension is
## *local but leaky*: a bad cell block raises the corridor next to it, which
## raises the canteen, so trouble spreads along the map's actual topology
## rather than teleporting. Second, it's slow — rooms relax toward their
## drivers over hours, so the player can watch a problem build and still have
## time to act. Both properties are what make the overlay worth reading.
##
## Keyed by RoomInfo.key() (stable tile identity), not room id — the flood
## fill renumbers ids whenever anything is built, and tension that reshuffled
## on every wall placement would be meaningless.

## Diffusion weights per adjacency kind; an open doorway conducts far more
## pressure than a solid wall does.
const DOOR_WEIGHT := 1.0
const WALL_WEIGHT := 0.2

## Fraction of the gradient that flows per sim minute. Keep
## DIFFUSION_RATE * (max neighbours * DOOR_WEIGHT) well under 1 or the
## explicit integration below oscillates instead of settling.
##
## Its ratio to RESPONSE_RATE is what sets how far apart two connected rooms
## can sit: at equilibrium the gap is RESPONSE_RATE / DIFFUSION_RATE, so
## these values let a bad block run ~0.37 hotter than the corridor next to
## it. Raise diffusion and the whole facility homogenises into one number;
## drop it and trouble never spreads at all.
const DIFFUSION_RATE := 0.015
## How fast a room moves toward its own local pressure, per sim minute.
## 1/180 means a room takes about three hours to fully express a change —
## slow enough that the player can watch a problem build and still act.
const RESPONSE_RATE := 1.0 / 180.0

## Local pressure weights.
const GRIEVANCE_WEIGHT := 0.5
const CROWDING_WEIGHT := 0.2
const RIVALRY_WEIGHT := 0.15
const VIOLENCE_WEIGHT := 0.35

## Each guard covering a room takes this much off its local pressure. This is
## the single most important number for the DoD: staffing has to visibly buy
## calm, or "understaffed prisons riot" isn't a mechanic, it's a coin flip.
const GUARD_CALM := 0.18

## Recent-violence memory decays over roughly this many sim minutes.
const VIOLENCE_DECAY_MINUTES := 240.0

## Room key -> tension 0..1.
var values: Dictionary = {}
## Room key -> recent violence memory 0..1, fed by IncidentSystem.
var violence: Dictionary = {}


func value_for(room: RoomInfo) -> float:
	return float(values.get(room.key(), 0.0)) if room != null else 0.0


func value_at_key(room_key: Vector2i) -> float:
	return float(values.get(room_key, 0.0))


## Highest tension anywhere, and the mean across occupied rooms — what the
## HUD reports and what facility-wide escalation checks read.
func peak() -> float:
	var best := 0.0
	for k in values:
		best = maxf(best, values[k])
	return best


func mean() -> float:
	if values.is_empty():
		return 0.0
	var total := 0.0
	for k in values:
		total += values[k]
	return total / float(values.size())


## Remember that something violent happened here. Decays on its own.
func add_violence(room_key: Vector2i, amount: float) -> void:
	violence[room_key] = clampf(float(violence.get(room_key, 0.0)) + amount, 0.0, 1.0)


func minute_tick(world: SimWorld) -> void:
	_decay_violence()
	_prune_stale_rooms(world)

	# One pass over the roster for the whole facility rather than one per
	# room — this is the difference between O(population) and
	# O(rooms x population) every sim minute.
	var occupancy := world.occupancy_index()

	# Pass 1: relax each room toward the pressure its own occupants generate.
	for room in world.rooms:
		if not _is_interesting(room):
			continue
		var room_key := room.key()
		var current := float(values.get(room_key, 0.0))
		values[room_key] = move_toward(current, local_pressure(world, room, occupancy), RESPONSE_RATE)

	# Pass 2: diffuse. Reads a snapshot so every room sees the same instant —
	# updating in place would make the result depend on room iteration order,
	# which is exactly the kind of thing that breaks determinism tests.
	var snapshot := values.duplicate()
	var adjacency := world.room_adjacency()
	for room in world.rooms:
		if not _is_interesting(room):
			continue
		var room_key := room.key()
		var here: float = snapshot.get(room_key, 0.0)
		var flow := 0.0
		for link: Dictionary in adjacency.get(room.id, []):
			var neighbour := world.room_at_id(int(link["id"]))
			# Only exchange with other built rooms. The unsealed outdoors is
			# adjacent to nearly everything and holds no tension of its own,
			# so letting it participate made it an infinite heat sink —
			# every room drained into it and the field pinned itself at
			# RESPONSE_RATE/DIFFUSION_RATE (~0.09) no matter how bad things
			# got. Tension spreads between rooms, not out into the yard.
			if neighbour == null or not _is_interesting(neighbour):
				continue
			var there: float = snapshot.get(neighbour.key(), 0.0)
			flow += float(link["weight"]) * (there - here)
		values[room_key] = clampf(here + DIFFUSION_RATE * flow, 0.0, 1.0)


## The pressure a room generates right now, before diffusion. Pass the
## occupancy index from SimWorld.occupancy_index() when asking about many
## rooms at once; without it this rescans the roster per call.
func local_pressure(world: SimWorld, room: RoomInfo, occupancy: Dictionary = {}) -> float:
	var occupants := world.occupants_of(room, occupancy)
	if occupants.is_empty():
		return 0.0

	var total_grievance := 0.0
	for p in occupants:
		total_grievance += p.grievance
	var avg_grievance := total_grievance / float(occupants.size())

	var capacity := world.room_capacity(room)
	var crowding := 0.0
	if capacity > 0:
		crowding = clampf(float(occupants.size()) / float(capacity) - 1.0, 0.0, 1.0)
	elif occupants.size() > room.tiles.size():
		crowding = 1.0

	var raw := (
		GRIEVANCE_WEIGHT * avg_grievance
		+ CROWDING_WEIGHT * crowding
		+ RIVALRY_WEIGHT * FactionSystem.rivalry_in(world, occupants)
		+ VIOLENCE_WEIGHT * float(violence.get(room.key(), 0.0))
	)
	var calm := GUARD_CALM * float(world.guard_presence(room))
	return clampf(raw - calm, 0.0, 1.0)


## Empty outdoor sprawl isn't a room in any meaningful sense — the whole
## unwalled map is one giant region, and letting it participate would average
## the entire facility's tension into a single meaningless number.
func _is_interesting(room: RoomInfo) -> bool:
	return room != null and room.sealed and not room.tiles.is_empty()


func _decay_violence() -> void:
	var step := 1.0 / VIOLENCE_DECAY_MINUTES
	for k in violence.keys():
		var v: float = violence[k] - step
		if v <= 0.0:
			violence.erase(k)
		else:
			violence[k] = v


## Drop keys for rooms that no longer exist, so a demolished block doesn't
## keep contributing to peak()/mean() forever.
func _prune_stale_rooms(world: SimWorld) -> void:
	var live := {}
	for room in world.rooms:
		if _is_interesting(room):
			live[room.key()] = true
	for k in values.keys():
		if not live.has(k):
			values.erase(k)


func to_dict() -> Dictionary:
	var packed: Array = []
	for k: Vector2i in values:
		packed.append([k.x, k.y, values[k]])
	var packed_violence: Array = []
	for k: Vector2i in violence:
		packed_violence.append([k.x, k.y, violence[k]])
	return {"values": packed, "violence": packed_violence}


func from_dict(d: Dictionary) -> void:
	values.clear()
	for entry: Array in d.get("values", []):
		values[Vector2i(int(entry[0]), int(entry[1]))] = float(entry[2])
	violence.clear()
	for entry: Array in d.get("violence", []):
		violence[Vector2i(int(entry[0]), int(entry[1]))] = float(entry[2])
