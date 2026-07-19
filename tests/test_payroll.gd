extends GutTest
## M3 DoD, third clause: "Payroll debits daily." Plus what happens when the
## money isn't there, which is the part that makes staffing a real decision.

const TICKS_PER_DAY := SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY


func _run_days(world: SimWorld, days: int) -> void:
	for i in range(TICKS_PER_DAY * days):
		world.tick()


func test_daily_cost_sums_the_roster() -> void:
	var world := SimWorld.new(1, 12, 12)
	Crew.staff_up(world, Staff.Role.GUARD, 2)
	Crew.staff_up(world, Staff.Role.WORKER, 1)
	var expected := Staff.SALARY_PER_DAY[Staff.Role.GUARD] * 2 + Staff.SALARY_PER_DAY[Staff.Role.WORKER]
	assert_eq(Payroll.daily_cost(world.staff), expected)


func test_payroll_debits_once_per_day() -> void:
	var world := SimWorld.new(2, 12, 12)
	Crew.staff_up(world, Staff.Role.GUARD, 2)
	var daily := Payroll.daily_cost(world.staff)
	var start := world.ledger.balance

	_run_days(world, 1)
	assert_eq(world.payroll.days_paid, 1, "one debit after one day")
	assert_eq(world.ledger.balance, start - daily)

	_run_days(world, 1)
	assert_eq(world.payroll.days_paid, 2)
	assert_eq(world.ledger.balance, start - daily * 2, "and it keeps recurring")


func test_no_staff_means_no_payroll() -> void:
	var world := SimWorld.new(3, 12, 12)
	var start := world.ledger.balance
	_run_days(world, 2)
	assert_eq(world.ledger.balance, start, "an unstaffed site costs nothing in wages")
	assert_eq(world.payroll.days_paid, 0)


func test_missed_payroll_banks_unpaid_days_instead_of_overdrawing() -> void:
	var world := SimWorld.new(4, 12, 12)
	Crew.staff_up(world, Staff.Role.GUARD, 1)
	world.ledger.spend(world.ledger.balance, "blew the budget")

	_run_days(world, 1)
	assert_eq(world.ledger.balance, 0, "balance never goes negative")
	assert_eq(world.payroll.days_missed, 1)
	assert_eq(world.staff[0].unpaid_days, 1)


func test_staff_quit_after_three_unpaid_days() -> void:
	var world := SimWorld.new(5, 12, 12)
	Crew.staff_up(world, Staff.Role.GUARD, 2)
	world.ledger.spend(world.ledger.balance, "blew the budget")

	var left: Array = []
	world.events.subscribe(func(name: String, payload: Dictionary) -> void:
		if name == "staff_left":
			left.append(payload))

	_run_days(world, Staff.QUIT_AFTER_UNPAID_DAYS - 1)
	assert_eq(world.staff.size(), 2, "still hanging on")

	_run_days(world, 1)
	assert_eq(world.staff.size(), 0, "everyone walks after %d unpaid days" % Staff.QUIT_AFTER_UNPAID_DAYS)
	assert_eq(left.size(), 2)
	assert_eq(left[0]["reason"], "quit — unpaid")


func test_paying_up_clears_the_unpaid_counter() -> void:
	var world := SimWorld.new(6, 12, 12)
	Crew.staff_up(world, Staff.Role.GUARD, 1)
	var daily := Payroll.daily_cost(world.staff)
	world.ledger.spend(world.ledger.balance, "broke")

	_run_days(world, 1)
	assert_eq(world.staff[0].unpaid_days, 1)

	world.ledger.deposit(daily * 5, "contract payment came in")
	_run_days(world, 1)
	assert_eq(world.staff[0].unpaid_days, 0, "back pay isn't modelled, but the strike clock resets")
	assert_eq(world.payroll.days_paid, 1)


func test_payroll_state_survives_a_round_trip() -> void:
	var world := SimWorld.new(7, 12, 12)
	Crew.staff_up(world, Staff.Role.WORKER, 1)
	_run_days(world, 1)

	var restored := SimWorld.new(1, 1, 1)
	restored.from_dict(world.to_dict())
	assert_eq(restored.payroll.days_paid, world.payroll.days_paid)
	assert_eq(restored.payroll.last_amount, world.payroll.last_amount)
