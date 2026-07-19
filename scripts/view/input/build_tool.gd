class_name BuildTool
extends RefCounted
## Turns drags and clicks into BuildOrders. Pure input->intent logic; the
## caller (bootstrap.gd) owns screen<->tile conversion and drawing the live
## preview.
##
## Everything except doors is a drag: walls draw a room outline or a single
## run, floors fill the box, and objects fill it too so you can lay a row of
## beds in one gesture. A drag that never moves is just a one-tile drag, so
## click-to-place still works without a separate code path.

enum Mode { WALL, DOOR, FLOOR, OBJECT }
## Wall drags either enclose the box (for making rooms) or run along one edge
## of it (for dividing or extending what's already there).
enum WallStyle { OUTLINE, LINE }

var world: SimWorld
var mode: int = Mode.WALL
var wall_style: int = WallStyle.OUTLINE
var floor_type: int = SimTile.FloorType.CONCRETE
var object_type: int = ObjectDef.Type.BED

var dragging := false
var drag_start := Vector2i.ZERO
var drag_end := Vector2i.ZERO


func _init(p_world: SimWorld) -> void:
	world = p_world


func begin_drag(tile: Vector2i) -> void:
	if mode == Mode.DOOR:
		return
	dragging = true
	drag_start = tile
	drag_end = tile


func update_drag(tile: Vector2i) -> void:
	if dragging:
		drag_end = tile


## Rectangle tiles currently under drag, normalized (x0<=x1, y0<=y1).
func drag_rect() -> Rect2i:
	var x0 := mini(drag_start.x, drag_end.x)
	var y0 := mini(drag_start.y, drag_end.y)
	var x1 := maxi(drag_start.x, drag_end.x)
	var y1 := maxi(drag_start.y, drag_end.y)
	return Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1)


func preview_orders() -> Array[BuildOrder]:
	if not dragging:
		return []
	match mode:
		Mode.WALL:
			return _line_orders() if wall_style == WallStyle.LINE else _perimeter_orders(drag_rect())
		Mode.FLOOR:
			return _fill_orders(drag_rect())
		Mode.OBJECT:
			return _object_orders(drag_rect())
		_:
			return []


func preview_cost() -> int:
	var total := 0
	for o in preview_orders():
		total += o.cost
	return total


## Worker-ticks the current selection would add to the queue. The caller
## turns this into an estimated duration using how many workers are on shift.
func preview_work() -> float:
	var total := 0.0
	for o in preview_orders():
		total += o.work_total
	return total


## What the player is about to get, for the on-screen readout.
func preview_summary() -> Dictionary:
	var orders := preview_orders()
	var cost := 0
	var work := 0.0
	for o in orders:
		cost += o.cost
		work += o.work_total
	return {
		"count": orders.size(),
		"cost": cost,
		"work": work,
		"affordable": cost <= world.ledger.balance,
		"rect": drag_rect(),
	}


## Click (no drag) — doors only; everything else goes through the drag path.
func click(tile: Vector2i, local_frac: Vector2) -> void:
	if mode != Mode.DOOR:
		return
	var flag := _nearest_edge(local_frac)
	world.construction_queue.enqueue(BuildOrder.make_door(tile.x, tile.y, flag), world.ledger)


func end_drag() -> void:
	if not dragging:
		return
	for o in preview_orders():
		world.construction_queue.enqueue(o, world.ledger)
	dragging = false


func cancel_drag() -> void:
	dragging = false


## Instant demolish (no queue, no refund) — right-click in WALL/DOOR/OBJECT
## mode. Bypasses the construction queue the same way zone assignment does;
## it's a direct player intent, not simulation logic.
func remove_at(tile: Vector2i, local_frac: Vector2) -> void:
	if not world.grid.in_bounds(tile.x, tile.y):
		return
	match mode:
		Mode.WALL, Mode.DOOR:
			world.grid.set_wall(tile.x, tile.y, _nearest_edge(local_frac), false)
		Mode.OBJECT:
			world.grid.remove_object(tile.x, tile.y)


func _perimeter_orders(rect: Rect2i) -> Array[BuildOrder]:
	var out: Array[BuildOrder] = []
	var x0 := rect.position.x
	var y0 := rect.position.y
	var x1 := rect.position.x + rect.size.x - 1
	var y1 := rect.position.y + rect.size.y - 1
	if not world.grid.in_bounds(x0, y0) or not world.grid.in_bounds(x1, y1):
		return out
	for x in range(x0, x1 + 1):
		_append_wall(out, x, y0, SimTile.WALL_N)
		_append_wall(out, x, y1, SimTile.WALL_S)
	for y in range(y0, y1 + 1):
		_append_wall(out, x0, y, SimTile.WALL_W)
		_append_wall(out, x1, y, SimTile.WALL_E)
	return out


## A single wall run along whichever axis the player dragged furthest — for
## dividing a room or extending an existing block, where a full outline would
## be wrong. Horizontal runs sit on the north edge, vertical on the west, so
## the result is predictable from the drag alone.
func _line_orders() -> Array[BuildOrder]:
	var out: Array[BuildOrder] = []
	var dx := absi(drag_end.x - drag_start.x)
	var dy := absi(drag_end.y - drag_start.y)
	if dx >= dy:
		var y := drag_start.y
		for x in range(mini(drag_start.x, drag_end.x), maxi(drag_start.x, drag_end.x) + 1):
			_append_wall(out, x, y, SimTile.WALL_N)
	else:
		var x := drag_start.x
		for y in range(mini(drag_start.y, drag_end.y), maxi(drag_start.y, drag_end.y) + 1):
			_append_wall(out, x, y, SimTile.WALL_W)
	return out


## Skips walls that already exist, so dragging over part of a finished
## building doesn't charge you twice for what's already standing.
func _append_wall(out: Array[BuildOrder], x: int, y: int, flag: int) -> void:
	if not world.grid.in_bounds(x, y):
		return
	if world.grid.tile_at(x, y).has_wall(flag):
		return
	out.append(BuildOrder.make_wall(x, y, flag))


func _fill_orders(rect: Rect2i) -> Array[BuildOrder]:
	var out: Array[BuildOrder] = []
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if not world.grid.in_bounds(x, y):
				continue
			if world.grid.tile_at(x, y).floor_type == floor_type:
				continue # already this surface
			out.append(BuildOrder.make_floor(x, y, floor_type))
	return out


## One object per free tile in the box — drag out a row of beds in a single
## gesture. Tiles that already hold something are skipped rather than
## rejecting the whole drag.
func _object_orders(rect: Rect2i) -> Array[BuildOrder]:
	var out: Array[BuildOrder] = []
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if not world.grid.in_bounds(x, y) or world.grid.object_at(x, y) != null:
				continue
			out.append(BuildOrder.make_object(x, y, object_type))
	return out


## Which of N/E/S/W a click landed closest to, given the click's fractional
## position within the tile (0..1 on both axes).
static func _nearest_edge(local_frac: Vector2) -> int:
	var dn := local_frac.y
	var ds := 1.0 - local_frac.y
	var dw := local_frac.x
	var de := 1.0 - local_frac.x
	var m := minf(minf(dn, ds), minf(dw, de))
	if m == dn:
		return SimTile.WALL_N
	if m == ds:
		return SimTile.WALL_S
	if m == dw:
		return SimTile.WALL_W
	return SimTile.WALL_E
