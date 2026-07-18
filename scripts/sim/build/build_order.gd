class_name BuildOrder
extends RefCounted
## A single queued construction action. Applied to the grid on completion,
## not on order — see ConstructionQueue. M1 has no workers yet, so every
## order takes a flat BUILD_TICKS to finish; M3 replaces that with
## worker-time consumption without changing this shape.

enum Kind { WALL, DOOR, FLOOR, OBJECT }

const WALL_COST := 10
const DOOR_COST := 25
const FLOOR_COST := 2
const BUILD_TICKS := 20

var kind: int
var x: int
var y: int
var wall_flag: int = 0
var floor_type: int = 0
var object_type: int = 0
var cost: int
var ticks_remaining: int = BUILD_TICKS


static func make_wall(x: int, y: int, flag: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.WALL
	o.x = x
	o.y = y
	o.wall_flag = flag
	o.cost = WALL_COST
	return o


static func make_door(x: int, y: int, flag: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.DOOR
	o.x = x
	o.y = y
	o.wall_flag = flag
	o.cost = DOOR_COST
	return o


static func make_floor(x: int, y: int, p_floor_type: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.FLOOR
	o.x = x
	o.y = y
	o.floor_type = p_floor_type
	o.cost = FLOOR_COST
	return o


static func make_object(x: int, y: int, p_object_type: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.OBJECT
	o.x = x
	o.y = y
	o.object_type = p_object_type
	o.cost = ObjectDef.cost_of(p_object_type)
	return o


func to_dict() -> Dictionary:
	return {
		"k": kind, "x": x, "y": y, "wf": wall_flag,
		"ft": floor_type, "ot": object_type, "c": cost, "tr": ticks_remaining,
	}


static func from_dict(d: Dictionary) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = int(d.get("k", Kind.WALL))
	o.x = int(d.get("x", 0))
	o.y = int(d.get("y", 0))
	o.wall_flag = int(d.get("wf", 0))
	o.floor_type = int(d.get("ft", 0))
	o.object_type = int(d.get("ot", 0))
	o.cost = int(d.get("c", 0))
	o.ticks_remaining = int(d.get("tr", BUILD_TICKS))
	return o
