class_name SimTile
extends RefCounted
## One 1m tile. Walls live on edges (wall_flags N/E/S/W bitmask), not on tiles.

enum FloorType { DIRT, CONCRETE, TILE, GRASS }

const WALL_N := 1
const WALL_E := 2
const WALL_S := 4
const WALL_W := 8

var floor_type: int = FloorType.DIRT
var wall_flags: int = 0
var room_id: int = -1
var is_outdoor: bool = true


func has_wall(flag: int) -> bool:
	return (wall_flags & flag) != 0


func set_wall(flag: int, present: bool) -> void:
	if present:
		wall_flags |= flag
	else:
		wall_flags &= ~flag


func to_dict() -> Dictionary:
	return {
		"f": floor_type,
		"w": wall_flags,
		"r": room_id,
		"o": is_outdoor,
	}


func from_dict(d: Dictionary) -> void:
	floor_type = int(d.get("f", FloorType.DIRT))
	wall_flags = int(d.get("w", 0))
	room_id = int(d.get("r", -1))
	is_outdoor = bool(d.get("o", true))
