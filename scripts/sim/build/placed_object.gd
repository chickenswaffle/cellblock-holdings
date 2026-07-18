class_name PlacedObject
extends RefCounted

var object_type: int
var x: int
var y: int
## Permanent assignment (e.g. a bed given to a prisoner at intake). -1 = none.
var owner_id: int = -1
## Transient "someone is using this right now" (e.g. a canteen table).
## -1 = free. Distinct from owner_id: an owned bed is still occupied_by
## -1 whenever its owner isn't currently in it.
var occupied_by: int = -1


func _init(p_object_type: int = 0, p_x: int = 0, p_y: int = 0) -> void:
	object_type = p_object_type
	x = p_x
	y = p_y


func to_dict() -> Dictionary:
	return {"t": object_type, "x": x, "y": y, "o": owner_id}


static func from_dict(d: Dictionary) -> PlacedObject:
	var p := PlacedObject.new(int(d.get("t", 0)), int(d.get("x", 0)), int(d.get("y", 0)))
	p.owner_id = int(d.get("o", -1))
	return p
