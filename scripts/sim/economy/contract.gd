class_name Contract
extends RefCounted
## The state contract: the facility's reason to exist and the player's
## visible goal. Pays per head per day; a day is BAD if beds are under
## MIN_OCCUPANCY_PCT full or more than MAX_INCIDENTS_PER_DAY incidents
## sparked. BREACH_DAYS bad days in a row and the state pulls the contract.
##
## Deliberately legible: the player should be able to say, at any moment,
## "today is going badly because X". Averages over the whole run hide that.

const PER_DIEM_PER_HEAD := 180
const MIN_OCCUPANCY_PCT := 0.60
const MAX_INCIDENTS_PER_DAY := 2
const BREACH_DAYS := 5

var day_incidents: int = 0
var breach_days: int = 0
var total_days: int = 0
var breached: bool = false
var total_earned: int = 0
## Yesterday's outcome, kept for the HUD: was it a bad day, and why.
var last_day_bad: bool = false
var last_day_reason: String = ""


func daily_revenue(occupancy: int) -> int:
	return occupancy * PER_DIEM_PER_HEAD


func run_day(world: SimWorld) -> void:
	total_days += 1
	var occupancy := world.prisoners.size()
	var capacity := 0
	for room in world.rooms:
		if room.zone_kind == ZoneValidator.Kind.CELL:
			capacity += world.room_capacity(room)
	var occupancy_pct := float(occupancy) / float(maxi(capacity, 1))

	last_day_bad = false
	last_day_reason = ""
	if occupancy > 0 and occupancy_pct < MIN_OCCUPANCY_PCT:
		last_day_bad = true
		last_day_reason = "only %.0f%% of beds full (need %.0f%%)" % [
			occupancy_pct * 100.0, MIN_OCCUPANCY_PCT * 100.0]
	elif day_incidents > MAX_INCIDENTS_PER_DAY:
		last_day_bad = true
		last_day_reason = "%d incidents (max %d)" % [day_incidents, MAX_INCIDENTS_PER_DAY]

	if last_day_bad:
		breach_days += 1
		if breach_days >= BREACH_DAYS:
			breached = true
			world.events.emit("contract_broken", {"reason": last_day_reason})
	else:
		breach_days = 0

	var revenue := daily_revenue(occupancy)
	world.ledger.deposit(revenue, "contract per-diem")
	total_earned += revenue

	world.events.emit("contract_day", {
		"day": total_days, "occupancy": occupancy, "capacity": capacity,
		"occupancy_pct": occupancy_pct, "incidents": day_incidents,
		"bad_day": last_day_bad, "reason": last_day_reason,
		"breach_days": breach_days, "revenue": revenue,
	})
	day_incidents = 0


func occupancy_target_str() -> String:
	return "%.0f%%" % (MIN_OCCUPANCY_PCT * 100.0)


func breach_days_left() -> int:
	return maxi(0, BREACH_DAYS - breach_days)


func to_dict() -> Dictionary:
	return {
		"day_incidents": day_incidents,
		"breach_days": breach_days,
		"total_days": total_days,
		"breached": breached,
		"total_earned": total_earned,
		"last_day_bad": last_day_bad,
		"last_day_reason": last_day_reason,
	}


func from_dict(d: Dictionary) -> void:
	day_incidents = int(d.get("day_incidents", 0))
	breach_days = int(d.get("breach_days", 0))
	total_days = int(d.get("total_days", 0))
	breached = bool(d.get("breached", false))
	total_earned = int(d.get("total_earned", 0))
	last_day_bad = bool(d.get("last_day_bad", false))
	last_day_reason = String(d.get("last_day_reason", ""))
