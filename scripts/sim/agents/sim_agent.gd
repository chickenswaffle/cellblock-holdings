class_name SimAgent
extends RefCounted
## Shared movement for anything that walks the grid (prisoners, staff).
##
## pos is **tile-center convention**: pos (5.5, 7.5) means tile (5, 7), which
## is where StructuresRenderer3D draws objects — an agent sitting on an exact
## integer coordinate would render inside the tile-edge wall geometry. Every
## place that sets pos directly must add the +0.5 offset.

## Tiles covered per tick at full speed. At 10 ticks/sim-minute this is
## 5 tiles/sim-minute — crossing a 100-tile site takes ~20 sim-minutes, a
## brisk walk across the facility, not a teleport or a crawl.
const MOVE_TILES_PER_TICK := 0.5

var id: int
var pos: Vector2 = Vector2.ZERO
var path: Array[Vector2i] = []
var path_index: int = 0


## floori (not roundi) is the correct inverse of "+0.5" regardless of
## floating-point rounding mode.
func tile_pos() -> Vector2i:
	return Vector2i(floori(pos.x), floori(pos.y))


## Where this agent moves per tick. Subclasses scale it (e.g. fatigue).
func move_speed() -> float:
	return MOVE_TILES_PER_TICK


## Advance one tick along the current path.
func step_along_path() -> void:
	if path_index >= path.size():
		return
	var target := Vector2(path[path_index]) + Vector2(0.5, 0.5)
	var to_target := target - pos
	var dist := to_target.length()
	var speed := move_speed()
	if dist <= speed:
		pos = target
		path_index += 1
	else:
		pos += to_target.normalized() * speed


func has_arrived() -> bool:
	return path_index >= path.size()


func clear_path() -> void:
	path.clear()
	path_index = 0


## Path to a goal tile, returning false (and leaving the agent unmoved) if
## no route exists. Already-there counts as success with an empty path.
func set_destination(grid: SimGrid, goal: Vector2i) -> bool:
	if tile_pos() == goal:
		clear_path()
		return true
	var found := Pathfinder.find_path(grid, tile_pos(), goal)
	if found.is_empty():
		return false
	path = found
	path_index = 0
	return true


## Snap onto a tile's center. Use instead of assigning pos directly.
func place_at_tile(t: Vector2i) -> void:
	pos = Vector2(t.x + 0.5, t.y + 0.5)
