class_name Prisoner
extends SimAgent
## An inmate. Movement (pos/path/step_along_path) comes from SimAgent; this
## class is needs, traits, and the action UtilityAI has assigned.

enum Trait { VOLATILE = 1, CUNNING = 2, INSTITUTIONALIZED = 4, FRAIL = 8, CONNECTED = 16, PENITENT = 32 }
enum ActionState { IDLE, TRAVELING, PERFORMING }

var pname: String
var age: int
var needs := Needs.new()
var traits: int = 0
var faction_id: int = -1
var sentence_days: int
## Sim day this prisoner arrived — factions won't recruit a fresh arrival.
var admitted_day: int = 0
var reform: float = 0.0
var grievance: float = 0.0
## Informant state (see Snitches). Exposure rises each time they're debriefed
## and decays while they're left alone; at high exposure they're found out.
var is_informant: bool = false
var informant_exposure: float = 0.0
var informant_last_used_day: int = -1
var cell_bed_pos: Vector2i = Vector2i(-1, -1)

var action_state: int = ActionState.IDLE
var action_need: int = -1
var action_object_pos: Vector2i = Vector2i(-1, -1)
var action_rate: float = 0.0


func has_trait(t: int) -> bool:
	return (traits & t) != 0


func to_dict() -> Dictionary:
	return {
		"id": id, "pname": pname, "age": age, "needs": needs.to_dict(),
		"traits": traits, "faction_id": faction_id, "sentence_days": sentence_days,
		"admitted_day": admitted_day, "reform": reform, "grievance": grievance,
		"is_informant": is_informant, "informant_exposure": informant_exposure,
		"informant_last_used_day": informant_last_used_day,
		"cell_bed_pos": [cell_bed_pos.x, cell_bed_pos.y],
		"pos": [pos.x, pos.y],
	}


static func from_dict(d: Dictionary) -> Prisoner:
	var p := Prisoner.new()
	p.id = int(d.get("id", 0))
	p.pname = String(d.get("pname", ""))
	p.age = int(d.get("age", 30))
	p.needs.from_dict(d.get("needs", {}))
	p.traits = int(d.get("traits", 0))
	p.faction_id = int(d.get("faction_id", -1))
	p.sentence_days = int(d.get("sentence_days", 0))
	p.admitted_day = int(d.get("admitted_day", 0))
	p.reform = float(d.get("reform", 0.0))
	p.grievance = float(d.get("grievance", 0.0))
	p.is_informant = bool(d.get("is_informant", false))
	p.informant_exposure = float(d.get("informant_exposure", 0.0))
	p.informant_last_used_day = int(d.get("informant_last_used_day", -1))
	var bed: Array = d.get("cell_bed_pos", [-1, -1])
	p.cell_bed_pos = Vector2i(int(bed[0]), int(bed[1]))
	var pv: Array = d.get("pos", [0.0, 0.0])
	p.pos = Vector2(float(pv[0]), float(pv[1]))
	return p
