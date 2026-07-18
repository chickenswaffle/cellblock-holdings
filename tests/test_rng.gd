extends GutTest


func test_same_seed_same_sequence() -> void:
	var a := SimRng.new(42)
	var b := SimRng.new(42)
	for i in range(1000):
		assert_eq(a.next(), b.next(), "sequences diverged at step %d" % i)


func test_different_seeds_differ() -> void:
	var a := SimRng.new(1)
	var b := SimRng.new(2)
	var same := true
	for i in range(10):
		if a.next() != b.next():
			same = false
	assert_false(same, "different seeds produced identical first 10 values")


func test_zero_seed_does_not_lock_up() -> void:
	var r := SimRng.new(0)
	assert_ne(r.next(), 0, "zero seed must be remapped, xorshift state 0 is absorbing")


func test_randi_range_bounds() -> void:
	var r := SimRng.new(7)
	for i in range(2000):
		var v := r.randi_range_n(10)
		assert_true(v >= 0 and v < 10, "randi_range_n out of bounds: %d" % v)
	for i in range(2000):
		var v := r.randi_between(-5, 5)
		assert_true(v >= -5 and v <= 5, "randi_between out of bounds: %d" % v)


func test_randf01_bounds() -> void:
	var r := SimRng.new(99)
	for i in range(2000):
		var f := r.randf01()
		assert_true(f >= 0.0 and f < 1.0, "randf01 out of bounds: %f" % f)


func test_serialization_roundtrip_continues_sequence() -> void:
	var a := SimRng.new(1234)
	for i in range(50):
		a.next()
	var b := SimRng.new(1)
	b.from_dict(a.to_dict())
	for i in range(100):
		assert_eq(a.next(), b.next(), "restored rng diverged at step %d" % i)
