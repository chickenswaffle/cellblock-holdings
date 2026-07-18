class_name Prisoner
extends RefCounted

enum Trait { VOLATILE = 1, CUNNING = 2, INSTITUTIONALIZED = 4, FRAIL = 8, CONNECTED = 16, PENITENT = 32 }
enum ActionState { IDLE, TRAVELING, PERFORMING }

## Tiles covered per tick. At 10 ticks/sim-minute this is 5 tiles/sim-minute
## — crossing a 100-tile site takes roughly 20 sim-minutes, a brisk walk
## across the whole facility, not an instant teleport or a crawl.
const MOVE_TILES_PER_TICK := 0.5

var id: int
var pname: String
var age: int
var needs := Needs.new()
var traits: int = 0
var faction_id: int = -1
var sentence_days: int
var reform: float = 0.0
var grievance: float = 0.0
var cell_bed_pos: Vector2i = Vector2i(-1, -1)

var pos: Vector2 = Vector2.ZERO
var path: Array[Vector2i] = []
var path_index: int = 0

var action_state: int = ActionState.IDLE
var action_need: int = -1
var action_object_pos: Vector2i = Vector2i(-1, -1)
var action_rate: float = 0.0


func has_trait(t: int) -> bool:
	return (traits & t) != 0


## pos is tile-center convention (tile (5,7) means world 5.5,7.5) — matches
## where StructuresRenderer3D draws objects/walls, so agents don't end up
## rendered on top of tile-edge wall geometry. floori (not roundi) is the
## correct inverse of "+0.5" regardless of floating-point rounding mode.
func tile_pos() -> Vector2i:
	return Vector2i(floori(pos.x), floori(pos.y))


## Advance one tick along the current path.
func step_along_path() -> void:
	if path_index >= path.size():
		return
	var target := Vector2(path[path_index]) + Vector2(0.5, 0.5)
	var to_target := target - pos
	var dist := to_target.length()
	if dist <= MOVE_TILES_PER_TICK:
		pos = target
		path_index += 1
	else:
		pos += to_target.normalized() * MOVE_TILES_PER_TICK


func has_arrived() -> bool:
	return path_index >= path.size()


func to_dict() -> Dictionary:
	return {
		"id": id, "pname": pname, "age": age, "needs": needs.to_dict(),
		"traits": traits, "faction_id": faction_id, "sentence_days": sentence_days,
		"reform": reform, "grievance": grievance,
		"cell_bed_pos": [cell_bed_pos.x, cell_bed_pos.y],
		"pos": [pos.x, pos.y],
	}


static func from_dict(d: Dictionary) -> Prisoner:
	var p := Prisoner.new()
	p.id = int(d.get("id", 0))
	p.pname = String(d.get("pname", ""))
	p.age = int(d.get("age", 30))
	p.needs.from_dict(d.get("needs", {}))
	p.traits = int(d.get("traits", 0))
	p.faction_id = int(d.get("faction_id", -1))
	p.sentence_days = int(d.get("sentence_days", 0))
	p.reform = float(d.get("reform", 0.0))
	p.grievance = float(d.get("grievance", 0.0))
	var bed: Array = d.get("cell_bed_pos", [-1, -1])
	p.cell_bed_pos = Vector2i(int(bed[0]), int(bed[1]))
	var pv: Array = d.get("pos", [0.0, 0.0])
	p.pos = Vector2(float(pv[0]), float(pv[1]))
	return p
