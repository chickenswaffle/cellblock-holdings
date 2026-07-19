class_name Contraband
extends RefCounted
## The facility's illicit supply, tracked per room and moved along the same
## adjacency graph tension diffuses across — contraband physically travels
## the map, so a stash near visitation spreads inward through whatever is
## connected to it.
##
## It matters for two reasons: it feeds faction strength, and above
## WEAPON_THRESHOLD it's what lets an incident escalate past a fistfight into
## a stabbing. Searches suppress it and raise grievance, so no search rate is
## both safe and calm — that tension is the intended design, not a balance
## bug to tune away.

## Amount entering per hour at a visitation room the guards aren't watching.
const VISITATION_INFLOW := 0.05
## Baseline seepage (deliveries, mail, the perimeter) into the gate area.
const DELIVERY_INFLOW := 0.015
## Extra inflow per staffer who is unpaid or exhausted. Corrupt staff are a
## consequence of how the player runs payroll, not a random event.
const CORRUPT_STAFF_INFLOW := 0.02

## Fraction of a stash that moves to connected rooms per hour.
const SPREAD_RATE := 0.12
## Natural attrition per hour — consumed, broken, lost.
const DECAY_PER_HOUR := 0.02

## Stash level at which weapons are available in a room.
const WEAPON_THRESHOLD := 0.35
## Fraction of a room's stash a search removes.
const SEARCH_SEIZURE := 0.8

## Room key -> amount 0..1.
var stash: Dictionary = {}


func amount_in(room_key: Vector2i) -> float:
	return float(stash.get(room_key, 0.0))


## Whether an incident in this room can reach the weapon rungs.
func weapons_available(room_key: Vector2i) -> bool:
	return amount_in(room_key) >= WEAPON_THRESHOLD


func total() -> float:
	var sum := 0.0
	for k in stash:
		sum += stash[k]
	return sum


## Share of the facility's supply sitting in one faction's territory, 0..1.
func share_held_by(faction_id: int) -> float:
	var owner := _territory_owner
	if owner == null or not owner.has(faction_id):
		return 0.0
	var held := 0.0
	for room_key: Vector2i in owner[faction_id]:
		held += amount_in(room_key)
	var all := total()
	return clampf(held / all, 0.0, 1.0) if all > 0.0 else 0.0


## Territory index rebuilt each hour by hour_tick so share_held_by() stays a
## cheap lookup rather than a scan of every faction.
var _territory_owner: Dictionary = {}


func hour_tick(world: SimWorld) -> void:
	_reindex_territory(world)
	_inflow(world)
	_spread(world)
	_decay()


func _reindex_territory(world: SimWorld) -> void:
	_territory_owner = {}
	for f in world.factions:
		_territory_owner[f.id] = f.territory.duplicate()


func _inflow(world: SimWorld) -> void:
	for room in world.rooms:
		if not room.sealed or room.zone_kind != ZoneValidator.Kind.VISITATION:
			continue
		# Guards on the visits room are what actually stems this.
		var watched := clampf(float(world.guard_presence(room)) * 0.5, 0.0, 1.0)
		_add(room.key(), VISITATION_INFLOW * (1.0 - watched))

	var entry_key := _entry_room_key(world)
	if entry_key.x < 0:
		return # nothing built yet; nowhere for it to land
	_add(entry_key, DELIVERY_INFLOW)

	# Staff who are exhausted or haven't been paid start carrying things in.
	var compromised := 0
	for s in world.staff:
		if s.unpaid_days > 0 or s.fatigue >= Staff.BREAK_AT_FATIGUE:
			compromised += 1
	if compromised > 0:
		_add(entry_key, CORRUPT_STAFF_INFLOW * float(compromised))


## Where deliveries and staff-carried contraband land: the room containing
## the gate, or — since the gate is usually on open ground outside — the
## sealed room nearest it. Distance, not popularity: an entry point is a
## place, and keying off occupancy meant an empty facility had no way for
## anything to get in at all, however badly it was staffed.
func _entry_room_key(world: SimWorld) -> Vector2i:
	var gate_room := world.room_at(world.gate_tile.x, world.gate_tile.y)
	if gate_room != null and gate_room.sealed:
		return gate_room.key()

	var gate := Vector2(world.gate_tile)
	var best := Vector2i(-1, -1)
	var best_dist := INF
	for room in world.rooms:
		if not room.sealed or room.tiles.is_empty():
			continue
		var d: float = gate.distance_squared_to(Vector2(world.room_center(room)))
		if d < best_dist:
			best_dist = d
			best = room.key()
	return best


## Move stashes along the room graph. Same snapshot-then-write discipline as
## TensionField: iteration order must not change the result.
func _spread(world: SimWorld) -> void:
	var snapshot := stash.duplicate()
	var adjacency := world.room_adjacency()
	for room in world.rooms:
		if not room.sealed:
			continue
		var here: float = snapshot.get(room.key(), 0.0)
		if here <= 0.0:
			continue
		var links: Array = adjacency.get(room.id, [])
		if links.is_empty():
			continue
		var moving := here * SPREAD_RATE
		var share := moving / float(links.size())
		for link: Dictionary in links:
			var neighbour := world.room_at_id(int(link["id"]))
			if neighbour == null or not neighbour.sealed:
				continue
			_add(neighbour.key(), share)
			_add(room.key(), -share)


func _decay() -> void:
	for k in stash.keys():
		var v: float = stash[k] - DECAY_PER_HOUR
		if v <= 0.0:
			stash.erase(k)
		else:
			stash[k] = v


func _add(room_key: Vector2i, amount: float) -> void:
	if room_key.x < 0:
		return
	var next := clampf(amount_in(room_key) + amount, 0.0, 1.0)
	if next <= 0.0:
		stash.erase(room_key)
	else:
		stash[room_key] = next


## Seize most of a room's stash. Returns how much was taken, so the caller
## can decide how loudly to react to a big find.
func search_room(world: SimWorld, room: RoomInfo) -> float:
	if room == null:
		return 0.0
	var room_key := room.key()
	var found := amount_in(room_key) * SEARCH_SEIZURE
	if found > 0.0:
		_add(room_key, -found)

	# Searches always cost goodwill, whether or not they turn anything up —
	# that's the trap. And the faction whose block was tossed takes it
	# personally.
	GrievanceSystem.spike_room(world, room, GrievanceSystem.SPIKE_SEARCH)
	for f in world.factions:
		if f.holds(room_key):
			f.heat = clampf(f.heat + 0.25, 0.0, 1.0)
	world.events.emit("search_conducted", {"room": [room_key.x, room_key.y], "found": found})
	return found


func to_dict() -> Dictionary:
	var packed: Array = []
	for k: Vector2i in stash:
		packed.append([k.x, k.y, stash[k]])
	return {"stash": packed}


func from_dict(d: Dictionary) -> void:
	stash.clear()
	for entry: Array in d.get("stash", []):
		stash[Vector2i(int(entry[0]), int(entry[1]))] = float(entry[2])
