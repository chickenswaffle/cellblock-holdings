class_name NeedSystem
extends RefCounted
## Runs once per sim minute (not per tick — decay/satisfaction rates are
## defined per minute): decay every prisoner's needs, apply active
## satisfaction for whoever is mid-action, and let UtilityAI reassess.

## A canteen with a support staffer working it serves meals this much
## faster — the reason to hire the role at all (M3).
const STAFFED_CANTEEN_MULTIPLIER := 1.6


static func minute_tick(world: SimWorld) -> void:
	# current_block(), not the raw timetable — a lockdown overrides it.
	var block := world.current_block()
	for p in world.prisoners:
		p.needs.decay_one_minute()
		if p.action_state == Prisoner.ActionState.PERFORMING:
			p.needs.satisfy_one_minute(p.action_need, _rate_for(world, p))
		UtilityAI.reassess(world, p, block)


static func _rate_for(world: SimWorld, p: Prisoner) -> float:
	if p.action_need == Needs.Kind.HUNGER and world.canteen_is_staffed(p.tile_pos()):
		return p.action_rate * STAFFED_CANTEEN_MULTIPLIER
	return p.action_rate
