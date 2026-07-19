class_name RoomInfo
extends RefCounted
## One connected region found by RoomDetector. "Sealed" means the region
## never leaks off the grid border through an unwalled edge — the default,
## everything-open map is one giant unsealed region; walling off an area
## disconnects it into its own (potentially sealed) region.

var id: int
var tiles: Array[Vector2i] = []
var sealed: bool = false
var zone_kind: int = -1
var zone_valid: bool = false


## Stable identity across re-detection. `id` is handed out by the flood fill
## in scan order, so inserting one wall anywhere renumbers half the map —
## anything that persists per-room state between detections (TensionField,
## faction territory) must key off this instead.
##
## RoomDetector floods in row-major scan order, so tiles[0] is the room's
## topmost-leftmost tile: same room, same key, regardless of what got built
## elsewhere. A room that genuinely splits or merges gets a new key, which is
## the honest answer — it is not the same room any more.
func key() -> Vector2i:
	return tiles[0] if not tiles.is_empty() else Vector2i(-1, -1)
