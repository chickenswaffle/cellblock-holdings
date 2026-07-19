class_name Snitches
extends RefCounted
## Informants. High-information, high-cost: a snitch shows you where the
## contraband is and which rooms are about to go up, but every time you use
## them the odds they're discovered go up, and a discovered informant is
## killed and their faction's heat explodes.
##
## The intended shape is that snitches are *worth it* and still a mistake to
## lean on — the exposure curve is superlinear in uses, so the player who
## checks constantly loses the asset and gets a body.

## Chance a prisoner refuses to be recruited at all, before traits.
const BASE_REFUSAL := 0.5
## Trait multipliers on the chance of accepting.
const RECRUIT_MULTIPLIERS := {
	Prisoner.Trait.CUNNING: 1.6,
	Prisoner.Trait.PENITENT: 1.4,
	Prisoner.Trait.CONNECTED: 0.5, # too embedded to turn
	Prisoner.Trait.INSTITUTIONALIZED: 0.7,
}

## Exposure added per use, and the exponent that makes leaning on them fatal.
const EXPOSURE_PER_USE := 0.12
const EXPOSURE_EXPONENT := 1.6
## Exposure bled off per sim day of not being used.
const EXPOSURE_DECAY_PER_DAY := 0.05

## Heat added to the betrayed faction when an informant is found out.
const DISCOVERY_HEAT := 0.8
## Grievance the killing spreads through the block that witnessed it.
const DISCOVERY_GRIEVANCE := 0.2


## Try to turn a prisoner. Fails if they're already an informant or simply
## refuse. Unaffiliated prisoners have nothing to trade, so they're no use.
static func recruit(world: SimWorld, p: Prisoner) -> bool:
	if p == null or p.is_informant or p.faction_id < 0:
		return false
	var chance := 1.0 - BASE_REFUSAL
	for t in RECRUIT_MULTIPLIERS:
		if p.has_trait(t):
			chance *= RECRUIT_MULTIPLIERS[t]
	# A frightened, aggrieved prisoner is easier to turn.
	chance *= 0.6 + 0.8 * p.needs.deficit(Needs.Kind.SAFETY)
	if not world.rng.chance(clampf(chance, 0.0, 0.95)):
		world.events.emit("snitch_refused", {"prisoner": p.id})
		return false
	p.is_informant = true
	p.informant_exposure = 0.0
	world.events.emit("snitch_recruited", {"prisoner": p.id, "faction": p.faction_id})
	return true


static func informants(world: SimWorld) -> Array[Prisoner]:
	var out: Array[Prisoner] = []
	for p in world.prisoners:
		if p.is_informant:
			out.append(p)
	return out


## Everything the player's informants can currently tell them: where the
## stashes are and which rooms are closest to going up. Using this is what
## exposes them — reading the tension overlay is free, this is not.
##
## Returns {"stashes": [{room_key, amount}], "hotspots": [{room_key, tension}]}.
static func debrief(world: SimWorld) -> Dictionary:
	var sources := informants(world)
	if sources.is_empty():
		return {"stashes": [], "hotspots": []}

	for p in sources:
		p.informant_exposure = minf(1.0, p.informant_exposure + EXPOSURE_PER_USE)
		p.informant_last_used_day = world.clock.day()

	var stashes: Array = []
	for room in world.rooms:
		if not room.sealed:
			continue
		var amount := world.contraband.amount_in(room.key())
		if amount > 0.01:
			stashes.append({"room_key": room.key(), "amount": amount})
	stashes.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return float(a["amount"]) > float(b["amount"]))

	var hotspots: Array = []
	for room in world.rooms:
		if not room.sealed:
			continue
		var value := world.tension.value_for(room)
		if value > 0.25:
			hotspots.append({"room_key": room.key(), "tension": value})
	hotspots.sort_custom(func(a: Dictionary, b: Dictionary) -> bool:
		return float(a["tension"]) > float(b["tension"]))

	_roll_for_discovery(world, sources)
	return {"stashes": stashes, "hotspots": hotspots}


## Exposure decays while an informant is left alone — called daily.
static func day_tick(world: SimWorld) -> void:
	for p in informants(world):
		if world.clock.day() > p.informant_last_used_day:
			p.informant_exposure = maxf(0.0, p.informant_exposure - EXPOSURE_DECAY_PER_DAY)


static func _roll_for_discovery(world: SimWorld, sources: Array[Prisoner]) -> void:
	for p in sources:
		# Superlinear: occasional use is survivable, constant use is not.
		var risk := pow(p.informant_exposure, EXPOSURE_EXPONENT)
		if not world.rng.chance(clampf(risk, 0.0, 1.0)):
			continue
		_discovered(world, p)


static func _discovered(world: SimWorld, p: Prisoner) -> void:
	var faction := FactionSystem.faction_at(world, p.faction_id)
	if faction != null:
		faction.heat = clampf(faction.heat + DISCOVERY_HEAT, 0.0, 1.0)
	var t := p.tile_pos()
	var room := world.room_at(t.x, t.y) if world.grid.in_bounds(t.x, t.y) else null
	if room != null:
		world.tension.add_violence(room.key(), 0.5)
		GrievanceSystem.spike_room(world, room, DISCOVERY_GRIEVANCE)

	world.events.emit("snitch_discovered", {
		"prisoner": p.id, "pname": p.pname, "faction": p.faction_id,
	})
	world.remove_prisoner(p.id, "killed — informant")
