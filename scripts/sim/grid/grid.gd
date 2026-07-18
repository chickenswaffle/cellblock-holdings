class_name SimGrid
extends RefCounted
## Flat-array tile grid for one site. Index = y * width + x.
## grid_version increments on any structural change (walls/floors/doors/
## objects) so cached rooms and paths can invalidate cheaply.

var width: int
var height: int
var tiles: Array[SimTile] = []
var objects: Array[PlacedObject] = []
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


## (dx, dy) of the neighbor across this edge, and the neighbor's opposite flag.
static func _edge_neighbor(flag: int) -> Array:
	match flag:
		SimTile.WALL_N:
			return [0, -1, SimTile.WALL_S]
		SimTile.WALL_S:
			return [0, 1, SimTile.WALL_N]
		SimTile.WALL_E:
			return [1, 0, SimTile.WALL_W]
		_:
			return [-1, 0, SimTile.WALL_E]


## Set or clear a wall on one edge of a tile, mirrored onto the neighbor's
## opposite edge so both tiles agree about the shared seam.
func set_wall(x: int, y: int, flag: int, present: bool) -> void:
	tile_at(x, y).set_wall(flag, present)
	var edge := _edge_neighbor(flag)
	if in_bounds(x + edge[0], y + edge[1]):
		tile_at(x + edge[0], y + edge[1]).set_wall(edge[2], present)
	grid_version += 1


## Set or clear a door on an edge. Implies a wall on that edge (a door is an
## openable wall segment, not a hole) — mirrored the same way as set_wall.
func set_door(x: int, y: int, flag: int, present: bool) -> void:
	tile_at(x, y).set_door(flag, present)
	var edge := _edge_neighbor(flag)
	if in_bounds(x + edge[0], y + edge[1]):
		tile_at(x + edge[0], y + edge[1]).set_door(edge[2], present)
	grid_version += 1


## True if an agent can step between two orthogonally adjacent tiles
## (no wall on the shared edge). Doors still block — pathfinding (M2) will
## use a separate, looser check that treats doors as passable at a cost.
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


## Same edge test as edge_open, but doors count as passable too (at a cost
## the pathfinder applies separately). Room detection uses edge_open —
## a door still separates two rooms — pathfinding uses this.
func edge_passable(x: int, y: int, dx: int, dy: int) -> bool:
	assert(absi(dx) + absi(dy) == 1)
	if not in_bounds(x + dx, y + dy):
		return false
	var flag := SimTile.WALL_E if dx == 1 else (SimTile.WALL_W if dx == -1 else (SimTile.WALL_S if dy == 1 else SimTile.WALL_N))
	var t := tile_at(x, y)
	return not t.has_wall(flag) or t.has_door(flag)


func edge_is_door(x: int, y: int, dx: int, dy: int) -> bool:
	var flag := SimTile.WALL_E if dx == 1 else (SimTile.WALL_W if dx == -1 else (SimTile.WALL_S if dy == 1 else SimTile.WALL_N))
	return tile_at(x, y).has_door(flag)


func set_zone(tiles: Array[Vector2i], zone_kind: int) -> void:
	for t in tiles:
		tile_at(t.x, t.y).zone_kind = zone_kind
	grid_version += 1


func object_at(x: int, y: int) -> PlacedObject:
	for o in objects:
		if o.x == x and o.y == y:
			return o
	return null


func place_object(x: int, y: int, object_type: int) -> void:
	assert(object_at(x, y) == null, "tile already occupied")
	objects.append(PlacedObject.new(object_type, x, y))
	grid_version += 1


func remove_object(x: int, y: int) -> void:
	for i in range(objects.size()):
		if objects[i].x == x and objects[i].y == y:
			objects.remove_at(i)
			grid_version += 1
			return


func to_dict() -> Dictionary:
	var tile_data: Array = []
	tile_data.resize(tiles.size())
	for i in range(tiles.size()):
		tile_data[i] = tiles[i].to_dict()
	var object_data: Array = []
	object_data.resize(objects.size())
	for i in range(objects.size()):
		object_data[i] = objects[i].to_dict()
	return {
		"width": width,
		"height": height,
		"grid_version": grid_version,
		"tiles": tile_data,
		"objects": object_data,
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
	var object_data: Array = d.get("objects", [])
	objects.clear()
	objects.resize(object_data.size())
	for i in range(object_data.size()):
		objects[i] = PlacedObject.from_dict(object_data[i])
