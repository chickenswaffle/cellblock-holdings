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


# ------------------------------------------------------------- 360 rotation

func test_yaw_wraps_all_the_way_around() -> void:
	var rig := _rig()
	rig.rotate_by(370.0)
	assert_almost_eq(rig.yaw_deg, 10.0, 0.001, "past 360 comes back around")
	rig.rotate_by(-20.0)
	assert_almost_eq(rig.yaw_deg, 350.0, 0.001, "and negative wraps the other way")


func test_you_can_keep_turning_in_one_direction_forever() -> void:
	var rig := _rig()
	for i in range(100):
		rig.rotate_by(37.0)
	assert_between(rig.yaw_deg, 0.0, 360.0, "yaw stays normalised")


func test_pitch_is_clamped_to_a_readable_range() -> void:
	var rig := _rig()
	rig.tilt_by(-1000.0)
	assert_almost_eq(rig.pitch_deg, CameraRig.PITCH_MIN, 0.001, "never goes edge-on")
	rig.tilt_by(1000.0)
	assert_almost_eq(rig.pitch_deg, CameraRig.PITCH_MAX, 0.001, "never fully top-down")


func test_reset_angle_restores_the_default_view_without_moving() -> void:
	var rig := _rig()
	rig._pan_by(Vector3(20, 0, 20))
	var where := rig.position
	rig.rotate_by(123.0)
	rig.tilt_by(20.0)
	rig.reset_angle()
	assert_almost_eq(rig.yaw_deg, 0.0, 0.001)
	assert_almost_eq(rig.pitch_deg, CameraRig.PITCH_DEG, 0.001)
	assert_almost_eq(rig.position.x, where.x, 0.001, "resetting the angle must not move the centre")
	assert_almost_eq(rig.position.z, where.z, 0.001)


func test_rotating_orbits_the_camera_around_the_centre() -> void:
	var rig := _rig()
	rig._pan_by(Vector3(30, 0, 30))
	var pivot := rig.global_position
	var before := rig.camera.global_position
	rig.rotate_by(90.0)
	var after := rig.camera.global_position
	assert_almost_eq(
		before.distance_to(pivot), after.distance_to(pivot), 0.01,
		"the camera swings around the pivot, it doesn't move away from it"
	)
	assert_gt(before.distance_to(after), 1.0, "and it genuinely moved")


func test_rotation_keeps_the_camera_pointed_at_the_centre() -> void:
	var rig := _rig()
	for angle in [0.0, 45.0, 90.0, 180.0, 270.0]:
		rig.yaw_deg = angle
		rig._apply_camera_transform()
		var to_pivot := (rig.global_position - rig.camera.global_position).normalized()
		var facing := -rig.camera.global_transform.basis.z
		assert_almost_eq(facing.dot(to_pivot), 1.0, 0.01, "still looking at the pivot at %d deg" % angle)


## Tile picking under rotation is deliberately NOT tested here.
##
## ground_point() calls Camera3D.project_ray_origin/normal, which need a real
## viewport; headless there isn't one, so the projection is degenerate and the
## test would fail for reasons that have nothing to do with the code. This
## project already has a standing rule against trusting headless rendering
## checks (see CLAUDE.md) — picking-after-rotation is verified by building
## something from a rotated view in an actual window instead.
##
## What *is* covered above is the part that's pure math and does regress
## silently: that rotation orbits around the pivot and keeps the camera aimed
## at it, which is what makes the projection correct in the first place.
