class_name SimGrid
extends RefCounted
## Flat-array tile grid for one site. Index = y * width + x.
## grid_version increments on any structural change (walls/floors) so
## cached paths and room data can invalidate cheaply.

var width: int
var height: int
var tiles: Array[SimTile] = []
var grid_version: int = 0


func _init(w: int = 100, h: int = 100) -> void:
	width = w
	height = h
	tiles.resize(w * h)
	for i in range(w * h):
		tiles[i] = SimTile.new()


func in_bounds(x: int, y: int) -> bool:
	return x >= 0 and x < width and y >= 0 and y < height


func tile_at(x: int, y: int) -> SimTile:
	assert(in_bounds(x, y))
	return tiles[y * width + x]


func set_floor(x: int, y: int, floor_type: int) -> void:
	tile_at(x, y).floor_type = floor_type
	grid_version += 1


## Set or clear a wall on one edge of a tile, mirrored onto the neighbor's
## opposite edge so both tiles agree about the shared seam.
func set_wall(x: int, y: int, flag: int, present: bool) -> void:
	tile_at(x, y).set_wall(flag, present)
	var nx := x
	var ny := y
	var opposite := 0
	match flag:
		SimTile.WALL_N:
			ny -= 1
			opposite = SimTile.WALL_S
		SimTile.WALL_S:
			ny += 1
			opposite = SimTile.WALL_N
		SimTile.WALL_E:
			nx += 1
			opposite = SimTile.WALL_W
		SimTile.WALL_W:
			nx -= 1
			opposite = SimTile.WALL_E
	if in_bounds(nx, ny):
		tile_at(nx, ny).set_wall(opposite, present)
	grid_version += 1


## True if an agent can step between two orthogonally adjacent tiles
## (no wall on the shared edge).
func edge_open(x: int, y: int, dx: int, dy: int) -> bool:
	assert(absi(dx) + absi(dy) == 1)
	if not in_bounds(x + dx, y + dy):
		return false
	if dx == 1:
		return not tile_at(x, y).has_wall(SimTile.WALL_E)
	if dx == -1:
		return not tile_at(x, y).has_wall(SimTile.WALL_W)
	if dy == 1:
		return not tile_at(x, y).has_wall(SimTile.WALL_S)
	return not tile_at(x, y).has_wall(SimTile.WALL_N)


func to_dict() -> Dictionary:
	var tile_data: Array = []
	tile_data.resize(tiles.size())
	for i in range(tiles.size()):
		tile_data[i] = tiles[i].to_dict()
	return {
		"width": width,
		"height": height,
		"grid_version": grid_version,
		"tiles": tile_data,
	}


func from_dict(d: Dictionary) -> void:
	width = int(d.get("width", 100))
	height = int(d.get("height", 100))
	grid_version = int(d.get("grid_version", 0))
	var tile_data: Array = d.get("tiles", [])
	tiles.clear()
	tiles.resize(tile_data.size())
	for i in range(tile_data.size()):
		var t := SimTile.new()
		t.from_dict(tile_data[i])
		tiles[i] = t
