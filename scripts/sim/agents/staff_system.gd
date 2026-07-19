class_name StaffSystem
extends RefCounted
## Drives staff each tick (movement, construction work) and each sim minute
## (shifts, fatigue, re-decisions) — the staff-side mirror of NeedSystem.
##
## The split matters: work and movement resolve per tick so a worker who
## reaches a build site starts building immediately, while fatigue and shift
## changes are defined per minute and would be wrong to apply 10x a minute.

static func tick(world: SimWorld) -> void:
	for s in world.staff:
		if s.state == Staff.State.OFF_DUTY:
			continue
		if s.state == Staff.State.TRAVELING:
			s.step_along_path()
			if s.has_arrived():
				StaffAI.on_arrived(world, s)
			continue
		if s.role == Staff.Role.WORKER and s.state == Staff.State.WORKING:
			_apply_construction_work(world, s)


static func _apply_construction_work(world: SimWorld, s: Staff) -> void:
	if s.job_order_id < 0:
		return
	var order := world.construction_queue.order_by_id(s.job_order_id)
	if order == null or order.claimed_by != s.id:
		s.job_order_id = -1
		s.state = Staff.State.IDLE
		return
	# Only work the site you're standing on — a worker knocked off course
	# shouldn't keep building remotely.
	if s.tile_pos() != order.tile():
		s.state = Staff.State.IDLE
		return
	var completed := world.construction_queue.apply_work(
		order, s.work_rate(), world.grid, world.ledger, world.events
	)
	if completed:
		s.job_order_id = -1
		s.state = Staff.State.IDLE


static func minute_tick(world: SimWorld) -> void:
	var hour := world.clock.hour_of_day()
	for s in world.staff:
		if not s.on_shift_at_hour(hour):
			StaffAI.go_off_duty(world, s)
			s.recover_one_minute(false)
			continue
		if s.state == Staff.State.OFF_DUTY:
			_come_on_shift(world, s)
		if s.state == Staff.State.RESTING:
			s.recover_one_minute(true)
		else:
			s.tire_one_minute()
		StaffAI.reassess(world, s)


static func _come_on_shift(world: SimWorld, s: Staff) -> void:
	s.place_at_tile(world.gate_tile)
	s.clear_path()
	s.state = Staff.State.IDLE
	s.patrol_index = -1 if s.role == Staff.Role.GUARD else 0
	world.events.emit("staff_on_shift", {"id": s.id, "role": s.role})
