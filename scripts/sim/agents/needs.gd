class_name Needs
extends RefCounted
## Seven decaying needs, each 0..1 (1 = fully satisfied). Decayed per sim
## minute by NeedSystem, satisfied by whatever action a prisoner is doing.

enum Kind { HUNGER, SLEEP, HYGIENE, SOCIAL, RECREATION, SAFETY, DIGNITY }

## How many sim-minutes it takes a need to decay fully from 1.0 to 0.0.
const MINUTES_TO_EMPTY := {
	Kind.HUNGER: 300.0,
	Kind.SLEEP: 960.0,
	Kind.HYGIENE: 720.0,
	Kind.SOCIAL: 480.0,
	Kind.RECREATION: 600.0,
	Kind.SAFETY: 1440.0,
	Kind.DIGNITY: 1440.0,
}

var values: Dictionary = {
	Kind.HUNGER: 1.0, Kind.SLEEP: 1.0, Kind.HYGIENE: 1.0, Kind.SOCIAL: 1.0,
	Kind.RECREATION: 1.0, Kind.SAFETY: 1.0, Kind.DIGNITY: 1.0,
}


func get_value(kind: int) -> float:
	return values[kind]


func deficit(kind: int) -> float:
	return 1.0 - values[kind]


## Called once per sim minute (NeedSystem), not per tick.
func decay_one_minute() -> void:
	for kind in MINUTES_TO_EMPTY:
		values[kind] = maxf(0.0, values[kind] - 1.0 / MINUTES_TO_EMPTY[kind])


## Restore toward 1.0 at the given rate (0..1 per sim minute), called once
## per minute while an action is actively satisfying this need.
func satisfy_one_minute(kind: int, rate: float) -> void:
	values[kind] = minf(1.0, values[kind] + rate)


## Worst (most urgent) need and its deficit — the seed for utility scoring.
func most_urgent() -> int:
	var worst_kind: int = Kind.HUNGER
	var worst_deficit := -1.0
	for kind in values:
		var d: float = deficit(kind)
		if d > worst_deficit:
			worst_deficit = d
			worst_kind = kind
	return worst_kind


func to_dict() -> Dictionary:
	var out := {}
	for kind in values:
		out[str(kind)] = values[kind]
	return out


func from_dict(d: Dictionary) -> void:
	for kind in values:
		values[kind] = float(d.get(str(kind), 1.0))
