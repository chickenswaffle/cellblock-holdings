extends GutTest
## End-to-end M1 DoD: draw a sealed box with a door -> detected as one room
## -> zone it a Cell -> bed + toilet makes it valid. Everything goes through
## SimWorld.tick(), the same path the real game uses.
##
## Since M3 that path runs through workers, so these worlds get a crew hired
## and put on the day shift first — an unstaffed site builds nothing.

const MAX_TICKS := 20000


func _staffed_world(seed_value: int, w: int, h: int) -> SimWorld:
	var world := SimWorld.new(seed_value, w, h)
	world.gate_tile = Vector2i(1, 1)
	Crew.set_hour(world, 9) # mid day-shift
	Crew.staff_up(world, Staff.Role.WORKER, 3, Vector2i(1, 1))
	return world


func _run_queue_to_completion(world: SimWorld, _order_count: int) -> void:
	var ticks := Crew.run_until_built(world, MAX_TICKS)
	assert_lt(ticks, MAX_TICKS, "workers should have drained the queue")


func test_draw_sealed_box_zone_cell_then_furnish() -> void:
	var world := _staffed_world(1, 20, 20)
	var q := world.construction_queue
	var l := world.ledger

	var walls := [
		BuildOrder.make_wall(5, 5, SimTile.WALL_N), BuildOrder.make_wall(6, 5, SimTile.WALL_N),
		BuildOrder.make_wall(5, 7, SimTile.WALL_S), BuildOrder.make_wall(6, 7, SimTile.WALL_S),
		BuildOrder.make_wall(5, 5, SimTile.WALL_W), BuildOrder.make_wall(5, 6, SimTile.WALL_W), BuildOrder.make_wall(5, 7, SimTile.WALL_W),
		BuildOrder.make_wall(6, 5, SimTile.WALL_E), BuildOrder.make_wall(6, 6, SimTile.WALL_E), BuildOrder.make_wall(6, 7, SimTile.WALL_E),
	]
	for w in walls:
		assert_true(q.enqueue(w, l))
	assert_true(q.enqueue(BuildOrder.make_door(5, 6, SimTile.WALL_W), l))
	_run_queue_to_completion(world, walls.size() + 1)

	var room := world.room_at(5, 6)
	assert_not_null(room, "interior tile should belong to a detected room")
	assert_eq(room.tiles.size(), 6)
	assert_true(room.sealed, "fully walled box (door included) must be sealed")

	var zoned := world.zone_room(room.id, ZoneValidator.Kind.CELL)
	assert_true(zoned)
	room = world.room_at(5, 6)
	assert_eq(room.zone_kind, ZoneValidator.Kind.CELL)
	assert_false(room.zone_valid, "no bed or toilet yet")

	assert_true(q.enqueue(BuildOrder.make_object(5, 5, ObjectDef.Type.BED), l))
	_run_queue_to_completion(world, 1)
	room = world.room_at(5, 6)
	assert_false(room.zone_valid, "bed alone isn't enough")

	assert_true(q.enqueue(BuildOrder.make_object(6, 5, ObjectDef.Type.TOILET), l))
	_run_queue_to_completion(world, 1)
	room = world.room_at(5, 6)
	assert_true(room.zone_valid, "bed + toilet in a sealed, zoned room is a valid cell")


func test_save_load_preserves_rooms_and_zoning() -> void:
	var world := _staffed_world(2, 12, 12)
	var walls := [
		BuildOrder.make_wall(2, 2, SimTile.WALL_N), BuildOrder.make_wall(2, 3, SimTile.WALL_S),
		BuildOrder.make_wall(2, 2, SimTile.WALL_W), BuildOrder.make_wall(2, 3, SimTile.WALL_W),
		BuildOrder.make_wall(2, 2, SimTile.WALL_E), BuildOrder.make_wall(2, 3, SimTile.WALL_E),
	]
	for w in walls:
		world.construction_queue.enqueue(w, world.ledger)
	_run_queue_to_completion(world, walls.size())
	var room := world.room_at(2, 2)
	world.zone_room(room.id, ZoneValidator.Kind.SOLITARY)

	var restored := SimWorld.new(9, 1, 1)
	restored.from_dict(world.to_dict())
	var restored_room := restored.room_at(2, 2)
	assert_not_null(restored_room)
	assert_eq(restored_room.zone_kind, ZoneValidator.Kind.SOLITARY)
	assert_true(restored_room.zone_valid)
	assert_eq(restored.ledger.balance, world.ledger.balance)
