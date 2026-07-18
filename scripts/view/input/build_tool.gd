class_name BuildTool
extends RefCounted
## Wall/door/floor/object placement. Drag-rectangle for walls (perimeter of
## the dragged box) and floor (fills the box); single click for doors
## (nearest edge to the click point) and objects. Pure input->intent logic;
## the caller (bootstrap.gd) owns screen<->tile conversion and drawing the
## live drag preview.

const TILE_PX := TilemapRenderer.TILE_PX

enum Mode { WALL, DOOR, FLOOR, OBJECT }

var world: SimWorld
var mode: int = Mode.WALL
var floor_type: int = SimTile.FloorType.CONCRETE
var object_type: int = ObjectDef.Type.BED

var dragging := false
var drag_start := Vector2i.ZERO
var drag_end := Vector2i.ZERO


func _init(p_world: SimWorld) -> void:
	world = p_world


func begin_drag(tile: Vector2i) -> void:
	if mode == Mode.DOOR or mode == Mode.OBJECT:
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
			return _perimeter_orders(drag_rect())
		Mode.FLOOR:
			return _fill_orders(drag_rect())
		_:
			return []


func preview_cost() -> int:
	var total := 0
	for o in preview_orders():
		total += o.cost
	return total


## Click (no drag) — used by DOOR and OBJECT modes.
func click(tile: Vector2i, local_frac: Vector2) -> void:
	match mode:
		Mode.DOOR:
			var flag := _nearest_edge(local_frac)
			var order := BuildOrder.make_door(tile.x, tile.y, flag)
			world.construction_queue.enqueue(order, world.ledger)
		Mode.OBJECT:
			if world.grid.in_bounds(tile.x, tile.y) and world.grid.object_at(tile.x, tile.y) == null:
				world.construction_queue.enqueue(BuildOrder.make_object(tile.x, tile.y, object_type), world.ledger)


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
		out.append(BuildOrder.make_wall(x, y0, SimTile.WALL_N))
		out.append(BuildOrder.make_wall(x, y1, SimTile.WALL_S))
	for y in range(y0, y1 + 1):
		out.append(BuildOrder.make_wall(x0, y, SimTile.WALL_W))
		out.append(BuildOrder.make_wall(x1, y, SimTile.WALL_E))
	return out


func _fill_orders(rect: Rect2i) -> Array[BuildOrder]:
	var out: Array[BuildOrder] = []
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if world.grid.in_bounds(x, y):
				out.append(BuildOrder.make_floor(x, y, floor_type))
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
