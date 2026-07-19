class_name Incident
extends RefCounted
## One escalating situation, sitting somewhere on the ladder from a grudge to
## a facility riot. Incidents live in a room, carry participants, and either
## climb, resolve, or burn out.

## The ladder, in order. Rung index IS the severity — code compares kinds
## with < and >, so never reorder these without meaning to.
enum Kind {
	GRUDGE, VERBAL, SHOVE, FIGHT, BRAWL, WEAPON, STABBING,
	FACTION_WAR, BLOCK_RIOT, FACILITY_RIOT, HOSTAGE,
}

## Rungs at or above which an incident is a riot, and counts toward the DoD.
const FIRST_RIOT_RUNG := Kind.BLOCK_RIOT
## Rung at which weapons must be available to climb any further.
const FIRST_WEAPON_RUNG := Kind.WEAPON
## Rung above which guards stop being able to quietly de-escalate.
const UNCONTAINABLE_RUNG := Kind.FACTION_WAR

const KIND_NAMES := [
	"grudge", "verbal argument", "shoving match", "fight", "brawl",
	"weapon drawn", "stabbing", "faction war", "block riot",
	"facility riot", "hostage situation",
]

enum Resolution { UNRESOLVED, SEPARATED, SOLITARY, NEGOTIATED, FORCED, CONCEDED, BURNED_OUT }

var id: int
var kind: int = Kind.GRUDGE
var room_key: Vector2i = Vector2i(-1, -1)
var participants: Array[int] = []
## Sim minutes since it started, and since it last climbed a rung.
var age_minutes: int = 0
var minutes_at_rung: int = 0
var resolution: int = Resolution.UNRESOLVED
## Factions involved, if this is between blocs rather than individuals.
var faction_ids: Array[int] = []


func is_open() -> bool:
	return resolution == Resolution.UNRESOLVED


func is_riot() -> bool:
	return kind >= FIRST_RIOT_RUNG


func is_violent() -> bool:
	return kind >= Kind.FIGHT


func label() -> String:
	return KIND_NAMES[kind] if kind < KIND_NAMES.size() else "incident"


## 0..1, for the tension model and the HUD's sort order.
func severity() -> float:
	return float(kind) / float(Kind.HOSTAGE)


func to_dict() -> Dictionary:
	return {
		"id": id, "kind": kind, "room_key": [room_key.x, room_key.y],
		"participants": participants.duplicate(), "age_minutes": age_minutes,
		"minutes_at_rung": minutes_at_rung, "resolution": resolution,
		"faction_ids": faction_ids.duplicate(),
	}


static func from_dict(d: Dictionary) -> Incident:
	var i := Incident.new()
	i.id = int(d.get("id", 0))
	i.kind = int(d.get("kind", Kind.GRUDGE))
	var rk: Array = d.get("room_key", [-1, -1])
	i.room_key = Vector2i(int(rk[0]), int(rk[1]))
	i.participants.clear()
	for p in d.get("participants", []):
		i.participants.append(int(p))
	i.age_minutes = int(d.get("age_minutes", 0))
	i.minutes_at_rung = int(d.get("minutes_at_rung", 0))
	i.resolution = int(d.get("resolution", Resolution.UNRESOLVED))
	i.faction_ids.clear()
	for f in d.get("faction_ids", []):
		i.faction_ids.append(int(f))
	return i
