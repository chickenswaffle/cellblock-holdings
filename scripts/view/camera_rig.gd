class_name CameraRig
extends Node3D
## Fixed-pitch angled camera (Cities Skylines-ish), orthogonal projection.
## Pan (WASD/arrows/middle-drag/edge-scroll), zoom (wheel). No rotation yet.
## Middle-drag pans by raycasting the ground plane so the point under the
## cursor stays under the cursor at any zoom level — robust without having
## to hand-derive the pitch's screen-to-world foreshortening.

const PITCH_DEG := 35.0
const CAMERA_DISTANCE := 40.0
const ZOOM_MIN := 6.0
const ZOOM_MAX := 70.0
const ZOOM_STEP := 1.1
const PAN_SPEED := 18.0
const EDGE_MARGIN := 12.0
const EDGE_SPEED := 14.0

var camera: Camera3D
var zoom_size := 45.0
var _dragging := false
var _mouse_seen := false


func _ready() -> void:
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.near = 0.1
	camera.far = 200.0
	add_child(camera)
	camera.current = true
	_apply_camera_transform()


func _apply_camera_transform() -> void:
	var pitch := deg_to_rad(PITCH_DEG)
	camera.position = Vector3(0.0, sin(pitch) * CAMERA_DISTANCE, cos(pitch) * CAMERA_DISTANCE)
	camera.look_at(global_position, Vector3.UP)
	camera.size = zoom_size


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			zoom_size = clampf(zoom_size / ZOOM_STEP, ZOOM_MIN, ZOOM_MAX)
			_apply_camera_transform()
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			zoom_size = clampf(zoom_size * ZOOM_STEP, ZOOM_MIN, ZOOM_MAX)
			_apply_camera_transform()
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = mb.pressed
	elif event is InputEventMouseMotion:
		_mouse_seen = true
		if _dragging:
			var mm := event as InputEventMouseMotion
			var old_world = ground_point(mm.position - mm.relative)
			var new_world = ground_point(mm.position)
			if old_world != null and new_world != null:
				position -= (new_world - old_world)
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
	var scale := zoom_size / 24.0
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
		position += dir.normalized() * PAN_SPEED * scale * delta
		_apply_camera_transform()
		return

	if not _mouse_seen or not get_window().has_focus():
		return
	var vp := get_viewport()
	if vp == null:
		return
	var mouse := vp.get_mouse_position()
	var size := vp.get_visible_rect().size
	var edge := Vector3.ZERO
	if mouse.x <= EDGE_MARGIN:
		edge.x -= 1.0
	elif mouse.x >= size.x - EDGE_MARGIN:
		edge.x += 1.0
	if mouse.y <= EDGE_MARGIN:
		edge.z -= 1.0
	elif mouse.y >= size.y - EDGE_MARGIN:
		edge.z += 1.0
	if edge != Vector3.ZERO:
		position += edge.normalized() * EDGE_SPEED * scale * delta
		_apply_camera_transform()
