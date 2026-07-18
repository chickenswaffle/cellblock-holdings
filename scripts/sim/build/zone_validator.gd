class_name ZoneValidator
extends RefCounted
## A zoned room is valid only when sealed AND it contains at least one of
## each object type its zone kind requires (RoomDetector checks "sealed"
## itself; validate() here checks the object requirement).

enum Kind {
	CELL, CANTEEN, YARD, WORKSHOP, SOLITARY, MEDICAL, STAFF_ROOM, VISITATION,
}

const REQUIRED_OBJECTS := {
	Kind.CELL: [ObjectDef.Type.BED, ObjectDef.Type.TOILET],
	Kind.CANTEEN: [ObjectDef.Type.TABLE],
	Kind.YARD: [ObjectDef.Type.WEIGHT_BENCH],
	Kind.WORKSHOP: [ObjectDef.Type.SEWING_STATION],
	Kind.SOLITARY: [],
	Kind.MEDICAL: [ObjectDef.Type.BED],
	Kind.STAFF_ROOM: [ObjectDef.Type.TABLE],
	Kind.VISITATION: [ObjectDef.Type.TABLE, ObjectDef.Type.PHONE],
}


## Object-requirement check only. Caller (RoomDetector) already gates this
## on room.sealed and a resolved (non-mixed) zone_kind.
static func validate(room: RoomInfo, grid: SimGrid) -> bool:
	var required: Array = REQUIRED_OBJECTS.get(room.zone_kind, [])
	if required.is_empty():
		return true
	var tile_set := {}
	for t in room.tiles:
		tile_set[t] = true
	var present := {}
	for o in grid.objects:
		if tile_set.has(Vector2i(o.x, o.y)):
			present[o.object_type] = true
	for req_type in required:
		if not present.has(req_type):
			return false
	return true
