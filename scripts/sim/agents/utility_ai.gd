class_name UtilityAI
extends RefCounted
## Scores candidate actions for a prisoner's current schedule block by
## need_deficit x availability x 1/distance, with a little RNG noise for
## emergent variety, and manages the resulting travel/perform state.
##
## M2 keeps WORK/PROGRAM as simplified stand-ins (social/recreation/hygiene,
## same as FREE) — real job assignment and reform programs are later
## milestones; the schedule system already has the full 8 blocks ready for
## them to hook into.

static func reassess(world: SimWorld, p: Prisoner, block: int) -> void:
	match p.action_state:
		Prisoner.ActionState.IDLE:
			_choose_action(world, p, block)
		Prisoner.ActionState.PERFORMING:
			var still_relevant := _block_needs(block).has(p.action_need)
			var satisfied := p.needs.get_value(p.action_need) >= 0.95
			if satisfied or not still_relevant:
				_release(world, p)
				_choose_action(world, p, block)
		Prisoner.ActionState.TRAVELING:
			pass


## Called once a traveling prisoner's path completes (checked every tick).
static func start_performing(p: Prisoner) -> void:
	p.action_state = Prisoner.ActionState.PERFORMING
	p.path.clear()
	p.path_index = 0


static func _block_needs(block: int) -> Array:
	match block:
		ScheduleSystem.Block.SLEEP, ScheduleSystem.Block.LOCKUP:
			return [Needs.Kind.SLEEP]
		ScheduleSystem.Block.EAT:
			return [Needs.Kind.HUNGER]
		ScheduleSystem.Block.YARD:
			return [Needs.Kind.RECREATION, Needs.Kind.SAFETY]
		ScheduleSystem.Block.SHOWER:
			return [Needs.Kind.HYGIENE]
		_:
			return [Needs.Kind.SOCIAL, Needs.Kind.RECREATION, Needs.Kind.HYGIENE]


static func _choose_action(world: SimWorld, p: Prisoner, block: int) -> void:
	var best_need := -1
	var best_score := -INF
	var best_target := {}

	for need in _block_needs(block):
		var target := _target_for_need(world, p, need)
		if target.is_empty():
			continue
		var dist: float = p.pos.distance_to(Vector2(target["tile"]))
		var score: float = p.needs.deficit(need) * (1.0 / (1.0 + dist)) + world.rng.randf01() * 0.05
		if score > best_score:
			best_score = score
			best_need = need
			best_target = target

	if best_need == -1:
		return

	var object_pos: Vector2i = best_target["object_pos"]
	if object_pos.x >= 0:
		var obj := world.grid.object_at(object_pos.x, object_pos.y)
		if obj != null:
			obj.occupied_by = p.id

	p.action_need = best_need
	p.action_object_pos = object_pos
	p.action_rate = best_target["rate"]

	var target_tile: Vector2i = best_target["tile"]
	if p.tile_pos() == target_tile:
		start_performing(p)
		return

	var path := Pathfinder.find_path(world.grid, p.tile_pos(), target_tile)
	if path.is_empty():
		_release(world, p)
		return
	p.path = path
	p.path_index = 0
	p.action_state = Prisoner.ActionState.TRAVELING


static func _release(world: SimWorld, p: Prisoner) -> void:
	if p.action_object_pos.x >= 0:
		var obj := world.grid.object_at(p.action_object_pos.x, p.action_object_pos.y)
		if obj != null and obj.occupied_by == p.id:
			obj.occupied_by = -1
	p.action_state = Prisoner.ActionState.IDLE
	p.action_need = -1
	p.action_object_pos = Vector2i(-1, -1)
	p.action_rate = 0.0
	p.path.clear()
	p.path_index = 0


## {} if nothing available right now, else {tile, object_pos (-1,-1 if none), rate}.
static func _target_for_need(world: SimWorld, p: Prisoner, need: int) -> Dictionary:
	match need:
		Needs.Kind.SLEEP:
			if p.cell_bed_pos.x < 0:
				return {}
			return {"tile": p.cell_bed_pos, "object_pos": p.cell_bed_pos, "rate": 0.02}
		Needs.Kind.HUNGER:
			var o := _nearest_free_object(world, p, ObjectDef.Type.TABLE)
			if o == null:
				return {}
			return {"tile": Vector2i(o.x, o.y), "object_pos": Vector2i(o.x, o.y), "rate": 0.15}
		Needs.Kind.RECREATION, Needs.Kind.SAFETY:
			var o := _nearest_free_object(world, p, ObjectDef.Type.WEIGHT_BENCH)
			if o != null:
				return {"tile": Vector2i(o.x, o.y), "object_pos": Vector2i(o.x, o.y), "rate": 0.1}
			var t := _nearest_zoned_tile(world, p, ZoneValidator.Kind.YARD)
			if t.x >= 0:
				return {"tile": t, "object_pos": Vector2i(-1, -1), "rate": 0.04}
			return {}
		Needs.Kind.HYGIENE:
			if p.cell_bed_pos.x < 0:
				return {}
			return {"tile": p.cell_bed_pos, "object_pos": Vector2i(-1, -1), "rate": 0.08}
		Needs.Kind.SOCIAL:
			var o := _nearest_free_object(world, p, ObjectDef.Type.BENCH)
			if o != null:
				return {"tile": Vector2i(o.x, o.y), "object_pos": Vector2i(o.x, o.y), "rate": 0.08}
			return {"tile": p.tile_pos(), "object_pos": Vector2i(-1, -1), "rate": 0.02}
		_:
			return {}


static func _nearest_free_object(world: SimWorld, p: Prisoner, object_type: int) -> PlacedObject:
	var best: PlacedObject = null
	var best_dist := INF
	for o in world.grid.objects:
		if o.object_type != object_type or o.occupied_by != -1:
			continue
		if o.owner_id != -1 and o.owner_id != p.id:
			continue
		var d: float = p.pos.distance_squared_to(Vector2(o.x, o.y))
		if d < best_dist:
			best_dist = d
			best = o
	return best


static func _nearest_zoned_tile(world: SimWorld, p: Prisoner, zone_kind: int) -> Vector2i:
	var best := Vector2i(-1, -1)
	var best_dist := INF
	for r in world.rooms:
		if r.zone_kind != zone_kind:
			continue
		for t in r.tiles:
			var d: float = p.pos.distance_squared_to(Vector2(t))
			if d < best_dist:
				best_dist = d
				best = t
	return best
