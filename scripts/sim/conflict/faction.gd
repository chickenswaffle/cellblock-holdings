class_name Faction
extends RefCounted
## A prisoner bloc. Factions offer safety and take participation in return —
## an unaffiliated prisoner is prey, an affiliated one is a liability.

var id: int
var fname: String
## Membership and contraband holdings, 0..1. Drives who wins a fight.
var strength: float = 0.1
## How much staff attention is on them, 0..1. Searches and incidents raise
## it; it decays. High heat suppresses contraband flow but breeds grievance.
var heat: float = 0.0
## Room keys this faction dominates.
var territory: Array[Vector2i] = []
## Other faction id -> relation, -1 (blood feud) .. 1 (allied).
var relations: Dictionary = {}


func relation_to(other_id: int) -> float:
	return float(relations.get(other_id, 0.0))


func set_relation(other_id: int, value: float) -> void:
	relations[other_id] = clampf(value, -1.0, 1.0)


func shift_relation(other_id: int, delta: float) -> void:
	set_relation(other_id, relation_to(other_id) + delta)


func holds(room_key: Vector2i) -> bool:
	return room_key in territory


func to_dict() -> Dictionary:
	var packed_territory: Array = []
	for t in territory:
		packed_territory.append([t.x, t.y])
	var packed_relations := {}
	for other_id in relations:
		packed_relations[str(other_id)] = relations[other_id]
	return {
		"id": id, "fname": fname, "strength": strength, "heat": heat,
		"territory": packed_territory, "relations": packed_relations,
	}


static func from_dict(d: Dictionary) -> Faction:
	var f := Faction.new()
	f.id = int(d.get("id", 0))
	f.fname = String(d.get("fname", ""))
	f.strength = float(d.get("strength", 0.1))
	f.heat = float(d.get("heat", 0.0))
	f.territory.clear()
	for t: Array in d.get("territory", []):
		f.territory.append(Vector2i(int(t[0]), int(t[1])))
	f.relations.clear()
	var packed: Dictionary = d.get("relations", {})
	for other_id: String in packed:
		f.relations[int(other_id)] = float(packed[other_id])
	return f
