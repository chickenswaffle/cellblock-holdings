extends GutTest
## The M0 definition-of-done test: same seed, 1000 ticks, identical state hash.


func _run_world(seed_value: int, ticks: int) -> SimWorld:
	var w := SimWorld.new(seed_value, 20, 20)
	# Exercise state mutation through rng + grid so the hash covers more
	# than the clock: deterministic random floor scatter.
	for i in range(30):
		var x := w.rng.randi_range_n(w.grid.width)
		var y := w.rng.randi_range_n(w.grid.height)
		w.grid.set_floor(x, y, SimTile.FloorType.CONCRETE)
	for i in range(ticks):
		w.tick()
	return w


func test_same_seed_1000_ticks_identical_hash() -> void:
	var a := _run_world(12345, 1000)
	var b := _run_world(12345, 1000)
	assert_eq(a.state_hash(), b.state_hash(), "same seed must produce identical state")


func test_different_seed_different_hash() -> void:
	var a := _run_world(12345, 1000)
	var b := _run_world(54321, 1000)
	assert_ne(a.state_hash(), b.state_hash())


func test_serialization_roundtrip_preserves_hash() -> void:
	var a := _run_world(777, 500)
	var b := SimWorld.new(1, 20, 20)
	b.from_dict(a.to_dict())
	assert_eq(a.state_hash(), b.state_hash(), "save/load must be lossless")


func test_restored_world_stays_in_lockstep() -> void:
	var a := _run_world(888, 250)
	var b := SimWorld.new(1, 20, 20)
	b.from_dict(a.to_dict())
	for i in range(250):
		a.tick()
		b.tick()
	assert_eq(a.state_hash(), b.state_hash(), "restored world diverged after further ticks")
