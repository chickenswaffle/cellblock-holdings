extends GutTest


func test_starts_at_zero() -> void:
	var c := SimClock.new()
	assert_eq(c.tick_count, 0)
	assert_eq(c.day(), 0)
	assert_eq(c.minute_of_day(), 0)
	assert_eq(c.hour_of_day(), 0)


func test_ticks_to_minutes() -> void:
	var c := SimClock.new()
	for i in range(SimClock.TICKS_PER_SIM_MINUTE):
		c.advance()
	assert_eq(c.total_minutes(), 1)
	assert_eq(c.minute_of_day(), 1)


func test_day_rollover() -> void:
	var c := SimClock.new()
	c.tick_count = SimClock.TICKS_PER_SIM_MINUTE * SimClock.MINUTES_PER_DAY
	assert_eq(c.day(), 1)
	assert_eq(c.minute_of_day(), 0)
	assert_eq(c.hour_of_day(), 0)


func test_hour_of_day() -> void:
	var c := SimClock.new()
	c.tick_count = SimClock.TICKS_PER_SIM_MINUTE * 60 * 13 + 5
	assert_eq(c.hour_of_day(), 13)


func test_serialization_roundtrip() -> void:
	var a := SimClock.new()
	a.tick_count = 98765
	var b := SimClock.new()
	b.from_dict(a.to_dict())
	assert_eq(b.tick_count, 98765)
