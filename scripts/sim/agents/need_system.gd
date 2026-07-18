class_name NeedSystem
extends RefCounted
## Runs once per sim minute (not per tick — decay/satisfaction rates are
## defined per minute): decay every prisoner's needs, apply active
## satisfaction for whoever is mid-action, and let UtilityAI reassess.

static func minute_tick(world: SimWorld) -> void:
	var block := world.schedule.block_at_hour(world.clock.hour_of_day())
	for p in world.prisoners:
		p.needs.decay_one_minute()
		if p.action_state == Prisoner.ActionState.PERFORMING:
			p.needs.satisfy_one_minute(p.action_need, p.action_rate)
		UtilityAI.reassess(world, p, block)
