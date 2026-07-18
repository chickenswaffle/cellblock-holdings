class_name RoomDetector
extends RefCounted
## Flood-fills the grid into connected regions via open edges (walls and
## doors both block, since a door is a wall with a flag). Re-run whenever
## grid.grid_version changes — see SimWorld.tick().

const DIRS := [Vector2i(0, -1), Vector2i(1, 0), Vector2i(0, 1), Vector2i(-1, 0)]
const DIR_WALL_FLAG := [SimTile.WALL_N, SimTile.WALL_E, SimTile.WALL_S, SimTile.WALL_W]


static func detect(grid: SimGrid) -> Array[RoomInfo]:
	var visited := {}
	var rooms: Array[RoomInfo] = []
	var next_id := 0

	for y in range(grid.height):
		for x in range(grid.width):
			var start := Vector2i(x, y)
			if visited.has(start):
				continue

			var room := RoomInfo.new()
			room.id = next_id
			next_id += 1
			var sealed := true

			var queue: Array[Vector2i] = [start]
			visited[start] = true
			var head := 0
			while head < queue.size():
				var t: Vector2i = queue[head]
				head += 1
				room.tiles.append(t)
				grid.tile_at(t.x, t.y).room_id = room.id

				for i in range(DIRS.size()):
					var d: Vector2i = DIRS[i]
					var n := t + d
					if grid.in_bounds(n.x, n.y):
						if grid.edge_open(t.x, t.y, d.x, d.y) and not visited.has(n):
							visited[n] = true
							queue.append(n)
					elif not grid.tile_at(t.x, t.y).has_wall(DIR_WALL_FLAG[i]):
						sealed = false

			room.sealed = sealed
			_compute_zone(room, grid)
			rooms.append(room)

	return rooms


static func _compute_zone(room: RoomInfo, grid: SimGrid) -> void:
	var kind := grid.tile_at(room.tiles[0].x, room.tiles[0].y).zone_kind
	for t in room.tiles:
		if grid.tile_at(t.x, t.y).zone_kind != kind:
			room.zone_kind = -1
			room.zone_valid = false
			return
	room.zone_kind = kind
	room.zone_valid = room.sealed and kind != -1 and ZoneValidator.validate(room, grid)
