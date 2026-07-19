class_name GrievanceSystem
extends RefCounted
## Grievance is how a prisoner feels about being here, 0..1, and it is the
## main input to the tension model. It is deliberately *slow*: conditions set
## a target and grievance crawls toward it over hours, so a prison doesn't
## calm down the instant the player fixes something. Recovering trust costs
## more time than losing it — SETTLE_RATE is a quarter of RESENT_RATE.
##
## Spikes (solitary, force, searches) are applied directly by whatever caused
## them via add_spike(); everything else flows through the target.

## Fraction of the gap to the target closed per sim minute, going up.
const RESENT_RATE := 1.0 / 240.0
## And going down. Slower on purpose.
const SETTLE_RATE := 1.0 / 960.0

## What conditions contribute to the target, before trait modifiers.
const DEPRIVATION_WEIGHT := 0.65
const CROWDING_WEIGHT := 0.35
## Needs that read as indignities rather than inconveniences count double.
const HEAVY_NEEDS := [Needs.Kind.SAFETY, Needs.Kind.DIGNITY]

## Trait multipliers on the target. Volatile inmates resent more of what they
## experience; penitent ones less; institutionalized ones are used to it.
const TRAIT_MULTIPLIERS := {
	Prisoner.Trait.VOLATILE: 1.3,
	Prisoner.Trait.PENITENT: 0.7,
	Prisoner.Trait.INSTITUTIONALIZED: 0.85,
	Prisoner.Trait.FRAIL: 1.15,
}

## Grievance added by things done *to* a prisoner.
const SPIKE_SOLITARY := 0.35
const SPIKE_FORCE := 0.25
const SPIKE_SEARCH := 0.08
const SPIKE_WITNESSED_VIOLENCE := 0.05


static func minute_tick(world: SimWorld) -> void:
	# Cell crowding is the same lookup for every prisoner in a block, so it's
	# computed once per minute rather than once per prisoner — otherwise this
	# is O(population²) every sim minute, which the 20-seed DoD test feels.
	var crowding := crowding_by_prisoner(world)
	for p in world.prisoners:
		var target := target_for(world, p, crowding)
		var rate := RESENT_RATE if target > p.grievance else SETTLE_RATE
		p.grievance = move_toward(p.grievance, target, rate)


## Where this prisoner's grievance is heading given how they currently live.
## Pass the index from crowding_by_prisoner() in hot loops; without it this
## recomputes the whole facility's occupancy to answer for one prisoner.
static func target_for(world: SimWorld, p: Prisoner, crowding: Dictionary = {}) -> float:
	var index := crowding if not crowding.is_empty() else crowding_by_prisoner(world)
	var crowd: float = index.get(p.id, 1.0 if p.cell_bed_pos.x < 0 else 0.0)
	var raw := DEPRIVATION_WEIGHT * _deprivation(p) + CROWDING_WEIGHT * crowd
	return clampf(raw * _trait_multiplier(p), 0.0, 1.0)


## Prisoner id -> how overcrowded their own block is, 0..1 (1.0 = at least
## double-bunked, or nowhere to sleep at all, which is the worse case).
##
## Built in a single pass: resolving each prisoner's cell room is a linear
## room lookup, and doing it per-prisoner-per-lookup instead of once made
## this the second most expensive thing in the conflict layer.
static func crowding_by_prisoner(world: SimWorld) -> Dictionary:
	var room_of := {} # prisoner id -> room id
	var assigned := {} # room id -> prisoners assigned there
	for p in world.prisoners:
		if p.cell_bed_pos.x < 0:
			continue
		var room := world.room_at(p.cell_bed_pos.x, p.cell_bed_pos.y)
		if room == null:
			continue
		room_of[p.id] = room.id
		assigned[room.id] = int(assigned.get(room.id, 0)) + 1

	var by_room := {}
	for room_id: int in assigned:
		var capacity := world.room_capacity(world.room_at_id(room_id))
		by_room[room_id] = 1.0 if capacity <= 0 else clampf(
			float(assigned[room_id]) / float(capacity) - 1.0, 0.0, 1.0
		)

	var out := {}
	for p in world.prisoners:
		# No bed anywhere is the worst case, not the absence of a problem.
		out[p.id] = float(by_room.get(room_of.get(p.id, -1), 1.0)) if room_of.has(p.id) else 1.0
	return out


## Mean unmet need, with safety and dignity double-weighted.
static func _deprivation(p: Prisoner) -> float:
	var total := 0.0
	var weight := 0.0
	for kind in p.needs.values:
		var w := 2.0 if kind in HEAVY_NEEDS else 1.0
		total += p.needs.deficit(kind) * w
		weight += w
	return total / weight if weight > 0.0 else 0.0


static func _trait_multiplier(p: Prisoner) -> float:
	var m := 1.0
	for t in TRAIT_MULTIPLIERS:
		if p.has_trait(t):
			m *= TRAIT_MULTIPLIERS[t]
	return m


## Apply a one-off grievance hit, clamped. Used by solitary, force, searches.
static func add_spike(p: Prisoner, amount: float) -> void:
	p.grievance = clampf(p.grievance + amount, 0.0, 1.0)


## Same, to everyone inside one room — how force and searches actually land.
static func spike_room(world: SimWorld, room: RoomInfo, amount: float) -> void:
	for p in world.prisoners_in_room(room):
		add_spike(p, amount)


static func spike_facility(world: SimWorld, amount: float) -> void:
	for p in world.prisoners:
		add_spike(p, amount)
