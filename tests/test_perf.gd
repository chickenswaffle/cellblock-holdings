extends GutTest
## M2 DoD: 200 agents at 10 ticks/sec under 8ms/tick (measured headless).


func test_200_agents_tick_under_budget() -> void:
	var world := SimWorld.new(5, 340, 25)
	FacilityBuilder.build(world, 40)
	FacilityBuilder.fill_all_beds(world)
	assert_eq(world.prisoners.size(), 200)

	# Warm up past the initial rush of everyone pathing out of their cells
	# at once, so the measured window reflects steady-state cost.
	for i in range(300):
		world.tick()

	var samples := 500
	var start_usec := Time.get_ticks_usec()
	for i in range(samples):
		world.tick()
	var elapsed_usec := Time.get_ticks_usec() - start_usec
	var avg_ms := (float(elapsed_usec) / samples) / 1000.0

	gut.p("200-agent avg tick cost: %.3f ms" % avg_ms)
	assert_lt(avg_ms, 8.0, "tick must stay under the 8ms/tick budget with 200 agents")
