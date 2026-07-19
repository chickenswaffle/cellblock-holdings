class_name FactionSystem
extends RefCounted
## Forms factions once a population is big enough to sustain them, recruits
## into them, and keeps territory/strength/relations current.
##
## Runs hourly rather than per-minute: joining a faction is a decision made
## over weeks, and rolling it 1440 times a day would both cost more and make
## the rate constants unreadable.

const NAME_PREFIXES := [
	"Iron", "Northside", "Ghost", "Red", "Low", "Blackwater", "Cinder", "Quiet",
]
const NAME_NOUNS := [
	"Syndicate", "Crew", "Kings", "Firm", "Circle", "Boys", "Union", "Chapter",
]

## No factions until there are enough bodies for blocs to mean anything.
const MIN_POPULATION := 8
const MIN_FACTIONS := 3
const MAX_FACTIONS := 5

## Baseline hourly chance an eligible unaffiliated prisoner joins something.
const BASE_JOIN_CHANCE := 0.02
## Trait multipliers on that chance.
const JOIN_MULTIPLIERS := {
	Prisoner.Trait.CONNECTED: 2.5,
	Prisoner.Trait.VOLATILE: 1.6,
	Prisoner.Trait.INSTITUTIONALIZED: 1.3,
	Prisoner.Trait.PENITENT: 0.25,
	Prisoner.Trait.FRAIL: 1.4, # frail inmates need protection most
}
## Days served before anyone is trusted enough to be asked.
const MIN_DAYS_SERVED := 1

## Share of a room's occupants a faction needs to call it territory.
const TERRITORY_SHARE := 0.5
## Heat bleeds off at this much per hour.
const HEAT_DECAY_PER_HOUR := 0.04


static func hour_tick(world: SimWorld) -> void:
	_ensure_factions(world)
	if world.factions.is_empty():
		return
	_recruit(world)
	_update_territory(world)
	_update_strength(world)
	for f in world.factions:
		f.heat = maxf(0.0, f.heat - HEAT_DECAY_PER_HOUR)


static func faction_at(world: SimWorld, faction_id: int) -> Faction:
	for f in world.factions:
		if f.id == faction_id:
			return f
	return null


static func members(world: SimWorld, faction_id: int) -> Array[Prisoner]:
	var out: Array[Prisoner] = []
	for p in world.prisoners:
		if p.faction_id == faction_id:
			out.append(p)
	return out


## How hostile a group standing in one place is, 0..1 — the faction term in
## the tension model. One bloc alone is calm however strong it is; it takes
## two who dislike each other in the same room to make a problem.
static func rivalry_in(world: SimWorld, occupants: Array[Prisoner]) -> float:
	var present := {}
	for p in occupants:
		if p.faction_id >= 0:
			present[p.faction_id] = int(present.get(p.faction_id, 0)) + 1
	if present.size() < 2:
		return 0.0

	var worst := 0.0
	var ids: Array = present.keys()
	ids.sort() # deterministic pair order
	for i in range(ids.size()):
		for j in range(i + 1, ids.size()):
			var a := faction_at(world, ids[i])
			if a == null:
				continue
			var hostility := maxf(0.0, -a.relation_to(ids[j]))
			# Two rivals present in equal numbers is worse than one plus a
			# token presence — a lopsided room is a beating, not a standoff.
			var balance := minf(present[ids[i]], present[ids[j]]) / float(occupants.size())
			worst = maxf(worst, hostility * (0.5 + balance))
	return clampf(worst, 0.0, 1.0)


## Unaffiliated prisoners in a room where a faction holds territory are prey.
static func is_preyed_on(world: SimWorld, p: Prisoner, room: RoomInfo) -> bool:
	if p.faction_id >= 0 or room == null:
		return false
	for f in world.factions:
		if f.holds(room.key()):
			return true
	return false


static func _ensure_factions(world: SimWorld) -> void:
	if not world.factions.is_empty() or world.prisoners.size() < MIN_POPULATION:
		return
	var count := world.rng.randi_between(MIN_FACTIONS, MAX_FACTIONS)
	var used_names := {}
	for i in range(count):
		var f := Faction.new()
		f.id = world.next_faction_id
		world.next_faction_id += 1
		f.fname = _unique_name(world, used_names)
		f.strength = 0.1 + world.rng.randf01() * 0.15
		world.factions.append(f)

	# Everyone starts wary of everyone: relations are seeded negative so the
	# rivalry term has something to work with from day one, but not so
	# negative that a well-run prison can't hold. Symmetric by construction.
	for i in range(world.factions.size()):
		for j in range(i + 1, world.factions.size()):
			var relation := -0.5 - world.rng.randf01() * 0.4
			world.factions[i].set_relation(world.factions[j].id, relation)
			world.factions[j].set_relation(world.factions[i].id, relation)

	world.events.emit("factions_formed", {"count": world.factions.size()})


static func _unique_name(world: SimWorld, used: Dictionary) -> String:
	# Bounded retries, then fall back to an index suffix — an RNG that keeps
	# colliding must never be able to hang the sim.
	for attempt in range(12):
		var candidate := "%s %s" % [
			NAME_PREFIXES[world.rng.randi_range_n(NAME_PREFIXES.size())],
			NAME_NOUNS[world.rng.randi_range_n(NAME_NOUNS.size())],
		]
		if not used.has(candidate):
			used[candidate] = true
			return candidate
	var fallback := "Crew %d" % world.next_faction_id
	used[fallback] = true
	return fallback


static func _recruit(world: SimWorld) -> void:
	for p in world.prisoners:
		if p.faction_id >= 0:
			continue
		if world.clock.day() - p.admitted_day < MIN_DAYS_SERVED:
			continue

		# Vulnerability is the real driver: a prisoner who feels unsafe and
		# resents the place is who a faction can actually recruit.
		var vulnerability := 0.5 * p.grievance + 0.5 * p.needs.deficit(Needs.Kind.SAFETY)
		var chance := BASE_JOIN_CHANCE * (0.3 + 1.7 * vulnerability)
		for t in JOIN_MULTIPLIERS:
			if p.has_trait(t):
				chance *= JOIN_MULTIPLIERS[t]
		if not world.rng.chance(clampf(chance, 0.0, 0.5)):
			continue

		var chosen := _pick_faction(world, p)
		if chosen == null:
			continue
		p.faction_id = chosen.id
		world.events.emit("prisoner_joined_faction", {
			"prisoner": p.id, "faction": chosen.id, "fname": chosen.fname,
		})


## Prisoners join whoever runs the block they sleep in, if anyone does;
## otherwise the strongest faction. Territory begets membership begets
## territory — that positive feedback is what makes blocks feel owned.
static func _pick_faction(world: SimWorld, p: Prisoner) -> Faction:
	if p.cell_bed_pos.x >= 0:
		var room := world.room_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
		if room != null:
			for f in world.factions:
				if f.holds(room.key()):
					return f
	var best: Faction = null
	for f in world.factions:
		if best == null or f.strength > best.strength:
			best = f
	return best


static func _update_territory(world: SimWorld) -> void:
	var counts := {} # room key -> {faction id -> members living there}
	var totals := {} # room key -> affiliated prisoners living there
	for p in world.prisoners:
		if p.faction_id < 0 or p.cell_bed_pos.x < 0:
			continue
		var room := world.room_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
		if room == null or not room.sealed:
			continue
		var room_key := room.key()
		if not counts.has(room_key):
			counts[room_key] = {}
		counts[room_key][p.faction_id] = int(counts[room_key].get(p.faction_id, 0)) + 1
		totals[room_key] = int(totals.get(room_key, 0)) + 1

	for f in world.factions:
		f.territory.clear()
	for room_key: Vector2i in counts:
		for faction_id: int in counts[room_key]:
			var share := float(counts[room_key][faction_id]) / float(totals[room_key])
			if share <= TERRITORY_SHARE:
				continue
			var f := faction_at(world, faction_id)
			if f != null:
				f.territory.append(room_key)


static func _update_strength(world: SimWorld) -> void:
	var population := maxi(1, world.prisoners.size())
	for f in world.factions:
		var member_share := float(members(world, f.id).size()) / float(population)
		var supply := world.contraband.share_held_by(f.id)
		# Heat suppresses: a faction under the spotlight can't operate freely.
		f.strength = clampf(0.65 * member_share + 0.35 * supply - 0.2 * f.heat, 0.0, 1.0)
