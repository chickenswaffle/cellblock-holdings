class_name CameraRig
extends Node3D
## Fixed-pitch angled camera (Cities Skylines-ish), orthogonal projection.
## Pan (WASD/arrows/middle-drag/optional edge-scroll), zoom (wheel).
##
## Middle-drag pans by raycasting the ground plane so the point under the
## cursor stays under the cursor at any zoom level — robust without having to
## hand-derive the pitch's screen-to-world foreshortening.
##
## The rig's position is always clamped to the map (see _clamp_to_bounds).
## Without that you can pan off the grid entirely and end up staring at empty
## background with no landmark and no way to tell which way is back — the
## camera has to be incapable of losing the player, not merely unlikely to.

const PITCH_DEG := 35.0
const CAMERA_DISTANCE := 40.0
const ZOOM_MIN := 6.0
const ZOOM_MAX := 70.0
const ZOOM_STEP := 1.1
const PAN_SPEED := 18.0
const EDGE_MARGIN := 12.0
const EDGE_SPEED := 14.0

## How far past the map edge the centre of view may go, in tiles. A little
## slack lets you look at the border comfortably; more than this and the map
## starts sliding off screen.
const BOUNDS_SLACK := 8.0

var camera: Camera3D
var zoom_size := 45.0

## Set by Bootstrap from the grid. Until then, panning is unclamped.
var bounds_min := Vector2.ZERO
var bounds_max := Vector2.ZERO
var _has_bounds := false

## Edge-scroll is off by default: with a real HUD occupying the screen edges,
## a camera that drifts whenever you reach for a button is actively hostile.
## Toggleable for players who want the city-builder convention.
var edge_scroll_enabled := false

## Where recenter() returns to — the facility, not the map origin.
var home_position := Vector3.ZERO

var _dragging := false
var _mouse_seen := false
## Set by the HUD each frame; suppresses edge-scroll and drag while the
## pointer is over UI so reaching for a button never moves the world.
var pointer_over_ui := false


func _ready() -> void:
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.near = 0.1
	camera.far = 200.0
	add_child(camera)
	camera.current = true
	_apply_camera_transform()


## Keep the view centre within the map (plus a little slack).
func set_bounds(grid_width: int, grid_height: int) -> void:
	bounds_min = Vector2(-BOUNDS_SLACK, -BOUNDS_SLACK)
	bounds_max = Vector2(grid_width + BOUNDS_SLACK, grid_height + BOUNDS_SLACK)
	_has_bounds = true
	_clamp_to_bounds()
	_apply_camera_transform()


func set_home(world_pos: Vector3) -> void:
	home_position = world_pos


func recenter() -> void:
	position = home_position
	_clamp_to_bounds()
	_apply_camera_transform()


func _clamp_to_bounds() -> void:
	if not _has_bounds:
		return
	position.x = clampf(position.x, bounds_min.x, bounds_max.x)
	position.z = clampf(position.z, bounds_min.y, bounds_max.y)
	position.y = 0.0


func _apply_camera_transform() -> void:
	var pitch := deg_to_rad(PITCH_DEG)
	camera.position = Vector3(0.0, sin(pitch) * CAMERA_DISTANCE, cos(pitch) * CAMERA_DISTANCE)
	camera.look_at(global_position, Vector3.UP)
	camera.size = zoom_size


## Move the rig and keep it legal. Every pan path goes through here.
func _pan_by(delta_pos: Vector3) -> void:
	position += delta_pos
	_clamp_to_bounds()
	_apply_camera_transform()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			_zoom_by(1.0 / ZOOM_STEP)
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			_zoom_by(ZOOM_STEP)
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = mb.pressed and not pointer_over_ui
	elif event is InputEventMouseMotion:
		_mouse_seen = true
		if _dragging:
			var mm := event as InputEventMouseMotion
			var old_world = ground_point(mm.position - mm.relative)
			var new_world = ground_point(mm.position)
			if old_world != null and new_world != null:
				_pan_by(-(new_world - old_world))


func _zoom_by(factor: float) -> void:
	zoom_size = clampf(zoom_size * factor, ZOOM_MIN, ZOOM_MAX)
	_apply_camera_transform()


func ground_point(screen_pos: Vector2) -> Variant:
	var origin := camera.project_ray_origin(screen_pos)
	var dir := camera.project_ray_normal(screen_pos)
	if absf(dir.y) < 0.0001:
		return null
	var t := -origin.y / dir.y
	if t < 0.0:
		return null
	return origin + dir * t


func _process(delta: float) -> void:
	var speed_scale := zoom_size / 24.0
	var dir := Vector3.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		dir.z -= 1.0
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		dir.z += 1.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		dir.x -= 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		dir.x += 1.0
	if dir != Vector3.ZERO:
		_pan_by(dir.normalized() * PAN_SPEED * speed_scale * delta)
		return

	if not edge_scroll_enabled or pointer_over_ui:
		return
	# Gate on a real mouse motion having happened: the OS/engine's default
	# cursor position at startup can sit inside the edge margin, which used
	# to drift the camera before the player touched anything.
	if not _mouse_seen or not get_window().has_focus():
		return
	var vp := get_viewport()
	if vp == null:
		return
	var mouse := vp.get_mouse_position()
	var view_size := vp.get_visible_rect().size
	var edge := Vector3.ZERO
	if mouse.x <= EDGE_MARGIN:
		edge.x -= 1.0
	elif mouse.x >= view_size.x - EDGE_MARGIN:
		edge.x += 1.0
	if mouse.y <= EDGE_MARGIN:
		edge.z -= 1.0
	elif mouse.y >= view_size.y - EDGE_MARGIN:
		edge.z += 1.0
	if edge != Vector3.ZERO:
		_pan_by(edge.normalized() * EDGE_SPEED * speed_scale * delta)
