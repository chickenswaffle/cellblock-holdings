class_name WallRenderer
extends Node2D
## Draws walls/doors as edge lines, zone assignment as a tile tint, and
## placed objects as small markers. Pure view: reads the sim, never writes.

const TILE_PX := TilemapRenderer.TILE_PX
const WALL_THICKNESS := 3.0
const WALL_COLOR := Color(0.15, 0.15, 0.17)
const DOOR_COLOR := Color(0.55, 0.35, 0.12)

const ZONE_COLORS := {
	ZoneValidator.Kind.CELL: Color(0.3, 0.4, 0.7, 0.35),
	ZoneValidator.Kind.CANTEEN: Color(0.8, 0.5, 0.2, 0.35),
	ZoneValidator.Kind.YARD: Color(0.3, 0.7, 0.3, 0.35),
	ZoneValidator.Kind.WORKSHOP: Color(0.6, 0.3, 0.7, 0.35),
	ZoneValidator.Kind.SOLITARY: Color(0.6, 0.15, 0.15, 0.35),
	ZoneValidator.Kind.MEDICAL: Color(0.7, 0.9, 0.9, 0.35),
	ZoneValidator.Kind.STAFF_ROOM: Color(0.8, 0.8, 0.3, 0.35),
	ZoneValidator.Kind.VISITATION: Color(0.8, 0.4, 0.6, 0.35),
}

const OBJECT_COLORS := {
	ObjectDef.Type.BED: Color(0.5, 0.35, 0.8),
	ObjectDef.Type.TOILET: Color(0.85, 0.85, 0.9),
	ObjectDef.Type.TABLE: Color(0.6, 0.42, 0.24),
	ObjectDef.Type.BENCH: Color(0.5, 0.35, 0.2),
	ObjectDef.Type.PHONE: Color(0.2, 0.2, 0.2),
	ObjectDef.Type.WEIGHT_BENCH: Color(0.3, 0.3, 0.35),
	ObjectDef.Type.SEWING_STATION: Color(0.75, 0.6, 0.2),
	ObjectDef.Type.CCTV: Color(0.1, 0.5, 0.6),
	ObjectDef.Type.METAL_DETECTOR: Color(0.6, 0.6, 0.65),
}

var world: SimWorld
var _rendered_version: int = -1


func setup(p_world: SimWorld) -> void:
	world = p_world
	queue_redraw()


func _process(_delta: float) -> void:
	if world != null and world.grid.grid_version != _rendered_version:
		_rendered_version = world.grid.grid_version
		queue_redraw()


func _draw() -> void:
	if world == null:
		return
	var grid := world.grid

	for r in world.rooms:
		if r.zone_kind == -1:
			continue
		var color: Color = ZONE_COLORS.get(r.zone_kind, Color.TRANSPARENT)
		for t in r.tiles:
			draw_rect(Rect2(t.x * TILE_PX, t.y * TILE_PX, TILE_PX, TILE_PX), color, true)

	for y in range(grid.height):
		for x in range(grid.width):
			var t := grid.tile_at(x, y)
			if t.has_wall(SimTile.WALL_N):
				_draw_edge(x, y, SimTile.WALL_N, t.has_door(SimTile.WALL_N))
			if t.has_wall(SimTile.WALL_W):
				_draw_edge(x, y, SimTile.WALL_W, t.has_door(SimTile.WALL_W))
			# Only draw border-facing S/E edges once (interior seams are
			# already drawn from the neighbor's N/W side).
			if x == grid.width - 1 and t.has_wall(SimTile.WALL_E):
				_draw_edge(x, y, SimTile.WALL_E, t.has_door(SimTile.WALL_E))
			if y == grid.height - 1 and t.has_wall(SimTile.WALL_S):
				_draw_edge(x, y, SimTile.WALL_S, t.has_door(SimTile.WALL_S))

	for o in grid.objects:
		var color: Color = OBJECT_COLORS.get(o.object_type, Color.MAGENTA)
		var pad := TILE_PX * 0.2
		draw_rect(Rect2(o.x * TILE_PX + pad, o.y * TILE_PX + pad, TILE_PX - pad * 2, TILE_PX - pad * 2), color, true)


func _draw_edge(x: int, y: int, flag: int, is_door: bool) -> void:
	var color := DOOR_COLOR if is_door else WALL_COLOR
	var x0 := x * TILE_PX
	var y0 := y * TILE_PX
	var a: Vector2
	var b: Vector2
	match flag:
		SimTile.WALL_N:
			a = Vector2(x0, y0)
			b = Vector2(x0 + TILE_PX, y0)
		SimTile.WALL_S:
			a = Vector2(x0, y0 + TILE_PX)
			b = Vector2(x0 + TILE_PX, y0 + TILE_PX)
		SimTile.WALL_W:
			a = Vector2(x0, y0)
			b = Vector2(x0, y0 + TILE_PX)
		_:
			a = Vector2(x0 + TILE_PX, y0)
			b = Vector2(x0 + TILE_PX, y0 + TILE_PX)
	draw_line(a, b, color, WALL_THICKNESS)
