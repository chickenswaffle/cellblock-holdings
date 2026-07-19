class_name Staff
extends SimAgent
## A hired employee. Three roles, two shifts, one fatigue stat that is the
## whole point: fatigue slows workers down, and (via nerve) is what makes
## overworked guards escalate harder in M4. Salary is charged daily by
## Payroll — staffing is the biggest lever on margin, so it has to bite.

enum Role { GUARD, WORKER, SUPPORT }
enum Shift { DAY, NIGHT }
enum State { OFF_DUTY, IDLE, TRAVELING, WORKING, PATROLLING, RESTING }

const DAY_SHIFT_START_HOUR := 6
const NIGHT_SHIFT_START_HOUR := 18

const SALARY_PER_DAY := {
	Role.GUARD: 120,
	Role.WORKER: 90,
	Role.SUPPORT: 70,
}
## One-off cost to hire, on top of the recurring salary.
const HIRING_FEE_DAYS := 2

## Sim-minutes of unbroken duty to go from fresh (0.0) to exhausted (1.0).
## A 12-hour shift is 720 minutes, so a staffer who never breaks ends their
## shift at ~0.8 fatigue — tired, not destroyed.
const MINUTES_TO_EXHAUST := 900.0
## Sim-minutes to recover fully: faster on a proper break than off the clock,
## because a break is *in* a staff room and off-duty is unmodeled home time.
const MINUTES_TO_RECOVER_RESTING := 240.0
const MINUTES_TO_RECOVER_OFF_DUTY := 480.0

## Fatigue at which a staffer walks off to rest, and the level they return at.
const BREAK_AT_FATIGUE := 0.85
const RESUME_AT_FATIGUE := 0.35

## Exhausted staff move at 60% speed and work at 50% rate.
const FATIGUE_SPEED_PENALTY := 0.4
const FATIGUE_WORK_PENALTY := 0.5

## Days of missed pay before a staffer quits.
const QUIT_AFTER_UNPAID_DAYS := 3

var sname: String
var role: int = Role.GUARD
var shift: int = Shift.DAY
var fatigue: float = 0.0
## Composure under pressure, rolled at hire. M4 reads effective_nerve().
var base_nerve: float = 0.7
var unpaid_days: int = 0

var state: int = State.OFF_DUTY
## Order index claimed from the ConstructionQueue (workers), else -1.
var job_order_id: int = -1
## Index into the guard's patrol route. -1 means "not started" — the first
## _patrol() call advances it to 0.
var patrol_index: int = -1


func salary_per_day() -> int:
	return SALARY_PER_DAY.get(role, 0)


static func hiring_fee(p_role: int) -> int:
	return SALARY_PER_DAY.get(p_role, 0) * HIRING_FEE_DAYS


## True when this staffer's shift covers the given hour. DAY is 06:00–18:00,
## NIGHT is 18:00–06:00 (which wraps midnight — hence the else branch).
func on_shift_at_hour(hour: int) -> bool:
	if shift == Shift.DAY:
		return hour >= DAY_SHIFT_START_HOUR and hour < NIGHT_SHIFT_START_HOUR
	return hour >= NIGHT_SHIFT_START_HOUR or hour < DAY_SHIFT_START_HOUR


func move_speed() -> float:
	return MOVE_TILES_PER_TICK * (1.0 - FATIGUE_SPEED_PENALTY * fatigue)


## Fraction of a full day's work a worker actually delivers right now.
func work_rate() -> float:
	return 1.0 - FATIGUE_WORK_PENALTY * fatigue


## Nerve as it actually applies, after fatigue erodes it.
func effective_nerve() -> float:
	return base_nerve * (1.0 - 0.5 * fatigue)


func tire_one_minute() -> void:
	fatigue = minf(1.0, fatigue + 1.0 / MINUTES_TO_EXHAUST)


func recover_one_minute(resting: bool) -> void:
	var per_min := 1.0 / (MINUTES_TO_RECOVER_RESTING if resting else MINUTES_TO_RECOVER_OFF_DUTY)
	fatigue = maxf(0.0, fatigue - per_min)


func needs_break() -> bool:
	return fatigue >= BREAK_AT_FATIGUE


func to_dict() -> Dictionary:
	return {
		"id": id, "sname": sname, "role": role, "shift": shift,
		"fatigue": fatigue, "base_nerve": base_nerve, "unpaid_days": unpaid_days,
		"state": state, "job_order_id": job_order_id, "patrol_index": patrol_index,
		"pos": [pos.x, pos.y],
	}


static func from_dict(d: Dictionary) -> Staff:
	var s := Staff.new()
	s.id = int(d.get("id", 0))
	s.sname = String(d.get("sname", ""))
	s.role = int(d.get("role", Role.GUARD))
	s.shift = int(d.get("shift", Shift.DAY))
	s.fatigue = float(d.get("fatigue", 0.0))
	s.base_nerve = float(d.get("base_nerve", 0.7))
	s.unpaid_days = int(d.get("unpaid_days", 0))
	s.state = int(d.get("state", State.OFF_DUTY))
	s.job_order_id = int(d.get("job_order_id", -1))
	s.patrol_index = int(d.get("patrol_index", -1))
	var pv: Array = d.get("pos", [0.0, 0.0])
	s.pos = Vector2(float(pv[0]), float(pv[1]))
	return s
