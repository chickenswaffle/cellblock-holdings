extends GutTest

var _received: Array = []


func before_each() -> void:
	_received.clear()


func _on_event(event_name: String, payload: Dictionary) -> void:
	_received.append([event_name, payload])


func test_subscribe_and_emit() -> void:
	var bus := SimEventBus.new()
	bus.subscribe(_on_event)
	bus.emit("hello", {"x": 1})
	assert_eq(_received.size(), 1)
	assert_eq(_received[0][0], "hello")
	assert_eq(_received[0][1]["x"], 1)


func test_duplicate_subscribe_ignored() -> void:
	var bus := SimEventBus.new()
	bus.subscribe(_on_event)
	bus.subscribe(_on_event)
	bus.emit("once")
	assert_eq(_received.size(), 1)


func test_unsubscribe() -> void:
	var bus := SimEventBus.new()
	bus.subscribe(_on_event)
	bus.unsubscribe(_on_event)
	bus.emit("gone")
	assert_eq(_received.size(), 0)


func test_world_emits_minute_and_day_events() -> void:
	var w := SimWorld.new(1, 4, 4)
	w.events.subscribe(_on_event)
	for i in range(SimClock.TICKS_PER_SIM_MINUTE * 3):
		w.tick()
	var minutes := _received.filter(func(e: Array) -> bool: return e[0] == "minute_passed")
	assert_eq(minutes.size(), 3, "one minute_passed per sim minute")
	assert_eq(minutes[2][1]["minute"], 3)
