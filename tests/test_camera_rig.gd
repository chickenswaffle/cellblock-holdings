extends GutTest
## Regression cover for "the camera scrolls off into empty background".
##
## The rig had no bounds check at all, so WASD, edge-scroll and middle-drag
## could all pan arbitrarily far off the map, leaving the player staring at
## the environment background with no landmark to navigate back by.

const GRID_W := 100
const GRID_H := 80


func _rig() -> CameraRig:
	var rig := CameraRig.new()
	add_child_autofree(rig)
	rig.set_bounds(GRID_W, GRID_H)
	return rig


func _assert_in_bounds(rig: CameraRig, context: String) -> void:
	assert_between(rig.position.x, rig.bounds_min.x, rig.bounds_max.x, "x %s" % context)
	assert_between(rig.position.z, rig.bounds_min.y, rig.bounds_max.y, "z %s" % context)


func test_bounds_cover_the_map_plus_a_little_slack() -> void:
	var rig := _rig()
	assert_almost_eq(rig.bounds_min.x, -CameraRig.BOUNDS_SLACK, 0.001)
	assert_almost_eq(rig.bounds_max.x, GRID_W + CameraRig.BOUNDS_SLACK, 0.001)
	assert_almost_eq(rig.bounds_max.y, GRID_H + CameraRig.BOUNDS_SLACK, 0.001)


func test_setting_bounds_pulls_an_already_stray_camera_back() -> void:
	var rig := CameraRig.new()
	add_child_autofree(rig)
	rig.position = Vector3(9000.0, 0.0, -4000.0)
	rig.set_bounds(GRID_W, GRID_H)
	_assert_in_bounds(rig, "after set_bounds")


func test_panning_far_past_the_edge_clamps_in_every_direction() -> void:
	for direction in [
		Vector3(10000, 0, 0), Vector3(-10000, 0, 0),
		Vector3(0, 0, 10000), Vector3(0, 0, -10000),
	]:
		var rig := _rig()
		rig._pan_by(direction)
		_assert_in_bounds(rig, "panning %s" % direction)


func test_many_small_pans_cannot_creep_out_of_bounds() -> void:
	var rig := _rig()
	for i in range(2000):
		rig._pan_by(Vector3(5.0, 0.0, 5.0))
	_assert_in_bounds(rig, "after repeated pans")


func test_the_rig_never_leaves_the_ground_plane() -> void:
	var rig := _rig()
	rig._pan_by(Vector3(10.0, 500.0, 10.0))
	assert_almost_eq(rig.position.y, 0.0, 0.001, "panning must not lift the rig")


func test_recenter_returns_to_the_facility() -> void:
	var rig := _rig()
	rig.set_home(Vector3(24.0, 0.0, 18.0))
	rig._pan_by(Vector3(10000, 0, 10000))
	rig.recenter()
	assert_almost_eq(rig.position.x, 24.0, 0.001)
	assert_almost_eq(rig.position.z, 18.0, 0.001)


func test_edge_scroll_is_off_by_default() -> void:
	# With a HUD occupying the screen edges, a camera that drifts whenever
	# you reach for a button is worse than no edge scrolling at all.
	assert_false(_rig().edge_scroll_enabled)


func test_zoom_stays_within_limits() -> void:
	var rig := _rig()
	for i in range(200):
		rig._zoom_by(1.0 / CameraRig.ZOOM_STEP)
	assert_almost_eq(rig.zoom_size, CameraRig.ZOOM_MIN, 0.001)
	for i in range(400):
		rig._zoom_by(CameraRig.ZOOM_STEP)
	assert_almost_eq(rig.zoom_size, CameraRig.ZOOM_MAX, 0.001)


func test_an_unbounded_rig_still_works() -> void:
	# Bounds are set by Bootstrap after construction; the rig must not break
	# in the window before that happens.
	var rig := CameraRig.new()
	add_child_autofree(rig)
	rig._pan_by(Vector3(50, 0, 50))
	assert_almost_eq(rig.position.x, 50.0, 0.001)
