class_name Payroll
extends RefCounted
## Daily wage bill. Salary is the biggest lever on margin, which is the whole
## trap the franchise layer sets — so missing payroll has to hurt rather than
## silently overdraw. It can't be paid partially: either the whole day's bill
## clears or nobody is paid and every staffer banks an unpaid day.
##
## Staff who go QUIT_AFTER_UNPAID_DAYS unpaid walk off the job permanently.

var days_paid: int = 0
var days_missed: int = 0
var last_amount: int = 0


## Total wage bill for the current roster, per day.
static func daily_cost(staff: Array[Staff]) -> int:
	var total := 0
	for s in staff:
		total += s.salary_per_day()
	return total


## Charge one day of wages. Called at the day rollover by SimWorld.
func run_day(world: SimWorld) -> void:
	var amount := daily_cost(world.staff)
	last_amount = amount
	if amount == 0:
		return
	if world.ledger.spend(amount, "payroll"):
		days_paid += 1
		for s in world.staff:
			s.unpaid_days = 0
		world.events.emit("payroll_paid", {"amount": amount, "staff": world.staff.size()})
		return

	days_missed += 1
	var quitters: Array[Staff] = []
	for s in world.staff:
		s.unpaid_days += 1
		if s.unpaid_days >= Staff.QUIT_AFTER_UNPAID_DAYS:
			quitters.append(s)
	world.events.emit("payroll_missed", {"amount": amount, "quitting": quitters.size()})
	for s in quitters:
		world.dismiss_staff(s.id, "quit — unpaid")


func to_dict() -> Dictionary:
	return {"days_paid": days_paid, "days_missed": days_missed, "last_amount": last_amount}


func from_dict(d: Dictionary) -> void:
	days_paid = int(d.get("days_paid", 0))
	days_missed = int(d.get("days_missed", 0))
	last_amount = int(d.get("last_amount", 0))
