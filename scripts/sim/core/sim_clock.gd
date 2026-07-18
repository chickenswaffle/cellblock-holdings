class_name SimClock
extends RefCounted
## Fixed-timestep sim clock. 10 ticks = 1 sim second is NOT the mapping;
## the mapping is game-time: TICKS_PER_SIM_MINUTE sim ticks advance one
## in-game minute. The view controls how many ticks run per real frame;
## the clock itself never sees wall time or delta.

const TICKS_PER_SIM_MINUTE := 10
const MINUTES_PER_DAY := 24 * 60

var tick_count: int = 0


## Total in-game minutes elapsed since sim start.
func total_minutes() -> int:
	return tick_count / TICKS_PER_SIM_MINUTE


## Current day, starting at day 0.
func day() -> int:
	return total_minutes() / MINUTES_PER_DAY


## Minute of the current day, 0..1439.
func minute_of_day() -> int:
	return total_minutes() % MINUTES_PER_DAY


## Hour of the current day, 0..23 (drives the 24-slot schedule later).
func hour_of_day() -> int:
	return minute_of_day() / 60


func advance() -> void:
	tick_count += 1


func to_dict() -> Dictionary:
	return {"tick_count": tick_count}


func from_dict(d: Dictionary) -> void:
	tick_count = int(d.get("tick_count", 0))
