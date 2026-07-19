class_name ZoneTool
extends RefCounted
## Designating what an area is for.
##
## Two gestures, because there are two things a player means:
##   - **click** a sealed room: designate the whole detected room. The common
##     case once walls are up, and it can't get the boundary wrong.
##   - **drag** a rectangle: paint that area directly, walls or no walls. This
##     is what lets you lay out the site *before* building — mark where the
##     cells and the canteen go, then put walls around them.
##
## Zoning is free, instant and reversible, so unlike construction it commits
## on release rather than asking for confirmation.

var world: SimWorld
var zone_kind: int = ZoneValidator.Kind.CELL

var dragging := false
var drag_start := Vector2i.ZERO
var drag_end := Vector2i.ZERO


func _init(p_world: SimWorld) -> void:
	world = p_world


func begin_drag(tile: Vector2i) -> void:
	dragging = true
	drag_start = tile
	drag_end = tile


func update_drag(tile: Vector2i) -> void:
	if dragging:
		drag_end = tile


func drag_rect() -> Rect2i:
	var x0 := mini(drag_start.x, drag_end.x)
	var y0 := mini(drag_start.y, drag_end.y)
	var x1 := maxi(drag_start.x, drag_end.x)
	var y1 := maxi(drag_start.y, drag_end.y)
	return Rect2i(x0, y0, x1 - x0 + 1, y1 - y0 + 1)


func cancel_drag() -> void:
	dragging = false


## Commit on release. A drag that never moved is treated as a click, which
## designates the whole room rather than the single tile under the cursor —
## clicking inside a finished cell block should zone the block.
func end_drag() -> void:
	if not dragging:
		return
	dragging = false
	if drag_start == drag_end:
		_zone_room_at(drag_start)
	else:
		_zone_area(drag_rect())


## Tiles the current drag would designate — the caller tints these.
func preview_tiles() -> Array[Vector2i]:
	var out: Array[Vector2i] = []
	if not dragging:
		return out
	if drag_start == drag_end:
		var room := world.room_at(drag_start.x, drag_start.y) if world.grid.in_bounds(drag_start.x, drag_start.y) else null
		return room.tiles.duplicate() if room != null else out
	var rect := drag_rect()
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if world.grid.in_bounds(x, y):
				out.append(Vector2i(x, y))
	return out


func _zone_room_at(tile: Vector2i) -> void:
	if not world.grid.in_bounds(tile.x, tile.y):
		return
	var room := world.room_at(tile.x, tile.y)
	if room != null:
		world.zone_room(room.id, zone_kind)


## Paint an arbitrary rectangle. RoomDetector resolves a room's kind from its
## tiles, so an area painted on open ground simply becomes a valid zone once
## walls enclose it — which is exactly what makes plan-then-build work.
##
## One special case, because the alternative is a trap: a drag that lands
## wholly inside a single *sealed* room designates that whole room rather than
## the rectangle. A room whose tiles disagree resolves to "mixed" and counts as
## unzoned, so sloppily dragging across most of a finished cell block would
## otherwise silently un-designate it. Nobody means that. The room has to be
## sealed for this — the unwalled outdoors is one enormous region, and
## snapping to it would zone half the map.
func _zone_area(rect: Rect2i) -> void:
	var tiles: Array[Vector2i] = []
	for y in range(rect.position.y, rect.position.y + rect.size.y):
		for x in range(rect.position.x, rect.position.x + rect.size.x):
			if world.grid.in_bounds(x, y):
				tiles.append(Vector2i(x, y))
	if tiles.is_empty():
		return

	var enclosing := _single_sealed_room(tiles)
	if enclosing != null:
		world.zone_room(enclosing.id, zone_kind)
		return

	world.grid.set_zone(tiles, zone_kind)
	world.refresh_rooms()


## The sealed room containing every one of these tiles, or null.
func _single_sealed_room(tiles: Array[Vector2i]) -> RoomInfo:
	var room_id := world.grid.tile_at(tiles[0].x, tiles[0].y).room_id
	for t in tiles:
		if world.grid.tile_at(t.x, t.y).room_id != room_id:
			return null
	var room := world.room_at_id(room_id)
	return room if room != null and room.sealed else null


## Old single-click entry point, kept for callers that don't drag.
func click(tile: Vector2i) -> void:
	_zone_room_at(tile)
