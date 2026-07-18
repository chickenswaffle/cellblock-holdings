class_name PlacedObject
extends RefCounted

var object_type: int
var x: int
var y: int


func _init(p_object_type: int = 0, p_x: int = 0, p_y: int = 0) -> void:
	object_type = p_object_type
	x = p_x
	y = p_y


func to_dict() -> Dictionary:
	return {"t": object_type, "x": x, "y": y}


static func from_dict(d: Dictionary) -> PlacedObject:
	return PlacedObject.new(int(d.get("t", 0)), int(d.get("x", 0)), int(d.get("y", 0)))
