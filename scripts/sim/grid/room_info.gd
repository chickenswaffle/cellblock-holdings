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
