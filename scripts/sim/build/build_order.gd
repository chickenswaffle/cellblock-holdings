class_name BuildOrder
extends RefCounted
## A single queued construction action, applied to the grid on completion,
## not on order — see ConstructionQueue.
##
## Since M3 an order is a unit of *work*, not a countdown: work_remaining is
## denominated in worker-ticks, and only a Worker standing on the site burns
## it down (StaffAI). With no workers hired nothing gets built — that's the
## milestone's whole point, not a bug.

enum Kind { WALL, DOOR, FLOOR, OBJECT }

const WALL_COST := 10
const DOOR_COST := 25
const FLOOR_COST := 2

## Worker-ticks to complete, by kind. One unfatigued worker delivers 1.0 per
## tick and there are 10 ticks per sim-minute, so a wall is ~4 sim-minutes of
## one worker's time.
const WALL_WORK := 40.0
const DOOR_WORK := 60.0
const FLOOR_WORK := 15.0
const OBJECT_WORK := 50.0

## Stable identity, assigned by ConstructionQueue on enqueue. Workers hold
## this rather than a list index, which shifts as other orders complete.
var id: int = -1
var kind: int
var x: int
var y: int
var wall_flag: int = 0
var floor_type: int = 0
var object_type: int = 0
var cost: int
var work_total: float = WALL_WORK
var work_remaining: float = WALL_WORK
## Staff id of the worker who claimed this order, else -1.
var claimed_by: int = -1


static func make_wall(x: int, y: int, flag: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.WALL
	o.x = x
	o.y = y
	o.wall_flag = flag
	o.cost = WALL_COST
	o._set_work(WALL_WORK)
	return o


static func make_door(x: int, y: int, flag: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.DOOR
	o.x = x
	o.y = y
	o.wall_flag = flag
	o.cost = DOOR_COST
	o._set_work(DOOR_WORK)
	return o


static func make_floor(x: int, y: int, p_floor_type: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.FLOOR
	o.x = x
	o.y = y
	o.floor_type = p_floor_type
	o.cost = FLOOR_COST
	o._set_work(FLOOR_WORK)
	return o


static func make_object(x: int, y: int, p_object_type: int) -> BuildOrder:
	var o := BuildOrder.new()
	o.kind = Kind.OBJECT
	o.x = x
	o.y = y
	o.object_type = p_object_type
	o.cost = ObjectDef.cost_of(p_object_type)
	o._set_work(OBJECT_WORK)
	return o


func _set_work(amount: float) -> void:
	work_total = amount
	work_remaining = amount


func tile() -> Vector2i:
	return Vector2i(x, y)


## 0.0 at order time, 1.0 when the last worker-tick lands.
func progress() -> float:
	if work_total <= 0.0:
		return 1.0
	return 1.0 - work_remaining / work_total


func to_dict() -> Dictionary:
	return {
		"id": id, "k": kind, "x": x, "y": y, "wf": wall_flag,
		"ft": floor_type, "ot": object_type, "c": cost,
		"wt": work_total, "wr": work_remaining, "cb": claimed_by,
	}


static func from_dict(d: Dictionary) -> BuildOrder:
	var o := BuildOrder.new()
	o.id = int(d.get("id", -1))
	o.kind = int(d.get("k", Kind.WALL))
	o.x = int(d.get("x", 0))
	o.y = int(d.get("y", 0))
	o.wall_flag = int(d.get("wf", 0))
	o.floor_type = int(d.get("ft", 0))
	o.object_type = int(d.get("ot", 0))
	o.cost = int(d.get("c", 0))
	o.work_total = float(d.get("wt", WALL_WORK))
	o.work_remaining = float(d.get("wr", o.work_total))
	o.claimed_by = int(d.get("cb", -1))
	return o
