extends GutTest


func test_starts_full() -> void:
	var n := Needs.new()
	for kind in [Needs.Kind.HUNGER, Needs.Kind.SLEEP, Needs.Kind.HYGIENE]:
		assert_eq(n.get_value(kind), 1.0)
		assert_eq(n.deficit(kind), 0.0)


func test_decay_reduces_value() -> void:
	var n := Needs.new()
	n.decay_one_minute()
	assert_lt(n.get_value(Needs.Kind.HUNGER), 1.0)


func test_decay_never_goes_negative() -> void:
	var n := Needs.new()
	for i in range(10000):
		n.decay_one_minute()
	assert_eq(n.get_value(Needs.Kind.HUNGER), 0.0)


func test_hunger_decays_faster_than_safety() -> void:
	var n := Needs.new()
	for i in range(100):
		n.decay_one_minute()
	assert_lt(n.get_value(Needs.Kind.HUNGER), n.get_value(Needs.Kind.SAFETY))


func test_satisfy_restores_toward_one() -> void:
	var n := Needs.new()
	for i in range(150):
		n.decay_one_minute()
	var before := n.get_value(Needs.Kind.HUNGER)
	n.satisfy_one_minute(Needs.Kind.HUNGER, 0.2)
	assert_almost_eq(n.get_value(Needs.Kind.HUNGER), before + 0.2, 0.0001)


func test_satisfy_caps_at_one() -> void:
	var n := Needs.new()
	n.satisfy_one_minute(Needs.Kind.HUNGER, 0.5)
	assert_eq(n.get_value(Needs.Kind.HUNGER), 1.0)


func test_most_urgent_picks_lowest_value() -> void:
	var n := Needs.new()
	n.values[Needs.Kind.SOCIAL] = 0.1
	assert_eq(n.most_urgent(), Needs.Kind.SOCIAL)


func test_serialization_roundtrip() -> void:
	var a := Needs.new()
	a.values[Needs.Kind.HUNGER] = 0.42
	a.values[Needs.Kind.SLEEP] = 0.13
	var b := Needs.new()
	b.from_dict(a.to_dict())
	assert_almost_eq(b.get_value(Needs.Kind.HUNGER), 0.42, 0.0001)
	assert_almost_eq(b.get_value(Needs.Kind.SLEEP), 0.13, 0.0001)
