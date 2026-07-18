class_name ZoneTool
extends RefCounted
## Click a tile to assign its room to the selected zone kind.

var world: SimWorld
var zone_kind: int = ZoneValidator.Kind.CELL


func _init(p_world: SimWorld) -> void:
	world = p_world


func click(tile: Vector2i) -> void:
	if not world.grid.in_bounds(tile.x, tile.y):
		return
	var room := world.room_at(tile.x, tile.y)
	if room != null:
		world.zone_room(room.id, zone_kind)
