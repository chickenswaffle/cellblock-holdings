class_name Pathfinder
extends RefCounted
## 8-way A* over the grid. Diagonal moves are blocked unless both flanking
## orthogonal edges are passable too (no cutting through a wall corner).
## Doors are passable but cost more than open floor.

const DIAGONAL_COST := 1.41421356
const DOOR_COST_MULTIPLIER := 1.6

const DIRS := [
	Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1),
	Vector2i(1, 1), Vector2i(1, -1), Vector2i(-1, 1), Vector2i(-1, -1),
]


## Returns the path from start to goal INCLUSIVE of both endpoints, or an
## empty array if start==goal, either is out of bounds, or no path exists.
static func find_path(grid: SimGrid, start: Vector2i, goal: Vector2i) -> Array[Vector2i]:
	var empty: Array[Vector2i] = []
	if not grid.in_bounds(start.x, start.y) or not grid.in_bounds(goal.x, goal.y):
		return empty
	if start == goal:
		return empty

	var open := _MinHeap.new()
	open.push(_heuristic(start, goal), start)
	var came_from := {}
	var g_score := {start: 0.0}
	var closed := {}

	while not open.is_empty():
		var current: Vector2i = open.pop()
		if closed.has(current):
			continue
		closed[current] = true
		if current == goal:
			return _reconstruct(came_from, current)

		for d in DIRS:
			var n: Vector2i = current + d
			if not grid.in_bounds(n.x, n.y) or closed.has(n):
				continue
			var step_cost := _edge_cost(grid, current, d)
			if step_cost < 0.0:
				continue
			var tentative: float = g_score[current] + step_cost
			if not g_score.has(n) or tentative < g_score[n]:
				g_score[n] = tentative
				came_from[n] = current
				open.push(tentative + _heuristic(n, goal), n)

	return empty


## -1.0 means impassable.
static func _edge_cost(grid: SimGrid, from: Vector2i, d: Vector2i) -> float:
	if absi(d.x) + absi(d.y) == 1:
		if not grid.edge_passable(from.x, from.y, d.x, d.y):
			return -1.0
		return DOOR_COST_MULTIPLIER if grid.edge_is_door(from.x, from.y, d.x, d.y) else 1.0
	if not grid.edge_passable(from.x, from.y, d.x, 0) or not grid.edge_passable(from.x, from.y, 0, d.y):
		return -1.0
	var door := grid.edge_is_door(from.x, from.y, d.x, 0) or grid.edge_is_door(from.x, from.y, 0, d.y)
	return DIAGONAL_COST * (DOOR_COST_MULTIPLIER if door else 1.0)


## Octile distance — admissible for 8-way movement with a sqrt(2) diagonal cost.
static func _heuristic(a: Vector2i, b: Vector2i) -> float:
	var dx := absi(a.x - b.x)
	var dy := absi(a.y - b.y)
	return float(maxi(dx, dy)) + (DIAGONAL_COST - 1.0) * float(mini(dx, dy))


static func _reconstruct(came_from: Dictionary, end: Vector2i) -> Array[Vector2i]:
	var path: Array[Vector2i] = [end]
	var cur := end
	while came_from.has(cur):
		cur = came_from[cur]
		path.append(cur)
	path.reverse()
	return path


## Binary min-heap of (priority, insertion order, value), FIFO tie-broken.
class _MinHeap:
	var _items: Array = []
	var _counter: int = 0

	func is_empty() -> bool:
		return _items.is_empty()

	func push(priority: float, value) -> void:
		_counter += 1
		_items.append([priority, _counter, value])
		var i := _items.size() - 1
		while i > 0:
			var parent := (i - 1) / 2
			if not _less(_items[i], _items[parent]):
				break
			var tmp = _items[parent]
			_items[parent] = _items[i]
			_items[i] = tmp
			i = parent

	func pop():
		var top = _items[0][2]
		var last = _items.pop_back()
		if not _items.is_empty():
			_items[0] = last
			var i := 0
			while true:
				var l := i * 2 + 1
				var r := i * 2 + 2
				var smallest := i
				if l < _items.size() and _less(_items[l], _items[smallest]):
					smallest = l
				if r < _items.size() and _less(_items[r], _items[smallest]):
					smallest = r
				if smallest == i:
					break
				var tmp = _items[smallest]
				_items[smallest] = _items[i]
				_items[i] = tmp
				i = smallest
		return top

	static func _less(a: Array, b: Array) -> bool:
		if a[0] != b[0]:
			return a[0] < b[0]
		return a[1] < b[1]
