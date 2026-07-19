class_name Contract
extends RefCounted

const PER_DIEM_PER_HEAD := 180
const MIN_OCCUPANCY_PCT := 0.60
const MAX_INCIDENT_RATE := 0.15
const BREACH_DAYS := 5

var day_incidents: int = 0
var breach_days: int = 0
var total_days: int = 0
var breached: bool = false
var total_earned: int = 0


func daily_revenue(occupancy: int, capacity: int) -> int:
	if capacity <= 0:
		return 0
	return occupancy * PER_DIEM_PER_HEAD


func run_day(world: SimWorld) -> void:
	total_days += 1
	var occupancy := world.prisoners.size()
	var capacity := 0
	for room in world.rooms:
		if room.zone_kind == ZoneValidator.Kind.CELL:
			capacity += world.room_capacity(room)
	var occupancy_pct := float(occupancy) / float(maxi(capacity, 1))
	var incident_rate := float(day_incidents) / float(maxi(total_days, 1))

	if occupancy_pct < MIN_OCCUPANCY_PCT or incident_rate > MAX_INCIDENT_RATE:
		breach_days += 1
		if breach_days >= BREACH_DAYS:
			breached = true
			world.events.emit("contract_broken", {"reason": _breach_reason(occupancy_pct, incident_rate)})
	else:
		breach_days = 0

	var revenue := daily_revenue(occupancy, capacity)
	world.ledger.deposit(revenue, "contract per-diem")
	total_earned += revenue
	day_incidents = 0

	world.events.emit("contract_day", {
		"day": total_days, "occupancy": occupancy, "capacity": capacity,
		"occupancy_pct": occupancy_pct, "incident_rate": incident_rate,
		"breach_days": breach_days, "revenue": revenue,
	})


func record_incident() -> void:
	day_incidents += 1


func occupancy_target_str() -> String:
	return "%.0f%%" % (MIN_OCCUPANCY_PCT * 100.0)


func incident_target_str() -> String:
	return "%.0f%%" % (MAX_INCIDENT_RATE * 100.0)


func breach_days_left() -> int:
	return maxi(0, BREACH_DAYS - breach_days)


func _breach_reason(occupancy_pct: float, incident_rate: float) -> String:
	if occupancy_pct < MIN_OCCUPANCY_PCT and incident_rate > MAX_INCIDENT_RATE:
		return "occupancy too low (%.0f%%) and incident rate too high (%.0f%%)" % [occupancy_pct * 100.0, incident_rate * 100.0]
	if occupancy_pct < MIN_OCCUPANCY_PCT:
		return "occupancy too low (%.0f%% — need %.0f%%)" % [occupancy_pct * 100.0, MIN_OCCUPANCY_PCT * 100.0]
	return "incident rate too high (%.0f%% — max %.0f%%)" % [incident_rate * 100.0, MAX_INCIDENT_RATE * 100.0]


func to_dict() -> Dictionary:
	return {
		"day_incidents": day_incidents,
		"breach_days": breach_days,
		"total_days": total_days,
		"breached": breached,
		"total_earned": total_earned,
	}


func from_dict(d: Dictionary) -> void:
	day_incidents = int(d.get("day_incidents", 0))
	breach_days = int(d.get("breach_days", 0))
	total_days = int(d.get("total_days", 0))
	breached = bool(d.get("breached", false))
	total_earned = int(d.get("total_earned", 0))
