class_name CameraRig
extends Camera2D
## Pan (WASD/arrows/middle-mouse drag), zoom (wheel), edge-scroll.
## Pure input/view concern; never touches the sim.

const PAN_SPEED := 900.0
const EDGE_MARGIN := 12.0
const EDGE_SPEED := 700.0
const ZOOM_STEP := 1.1
const ZOOM_MIN := 0.25
const ZOOM_MAX := 4.0

var _dragging := false


func _ready() -> void:
	make_current()


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_WHEEL_UP and mb.pressed:
			_apply_zoom(ZOOM_STEP)
		elif mb.button_index == MOUSE_BUTTON_WHEEL_DOWN and mb.pressed:
			_apply_zoom(1.0 / ZOOM_STEP)
		elif mb.button_index == MOUSE_BUTTON_MIDDLE:
			_dragging = mb.pressed
	elif event is InputEventMouseMotion and _dragging:
		var mm := event as InputEventMouseMotion
		position -= mm.relative / zoom.x


func _process(delta: float) -> void:
	var dir := Vector2.ZERO
	if Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP):
		dir.y -= 1.0
	if Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN):
		dir.y += 1.0
	if Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT):
		dir.x -= 1.0
	if Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT):
		dir.x += 1.0
	if dir != Vector2.ZERO:
		position += dir.normalized() * PAN_SPEED * delta / zoom.x
		return

	# Edge scroll only when the window has focus and no key-pan is active.
	var vp := get_viewport()
	if vp == null:
		return
	var mouse := vp.get_mouse_position()
	var size := vp.get_visible_rect().size
	var edge := Vector2.ZERO
	if mouse.x <= EDGE_MARGIN:
		edge.x -= 1.0
	elif mouse.x >= size.x - EDGE_MARGIN:
		edge.x += 1.0
	if mouse.y <= EDGE_MARGIN:
		edge.y -= 1.0
	elif mouse.y >= size.y - EDGE_MARGIN:
		edge.y += 1.0
	if edge != Vector2.ZERO:
		position += edge.normalized() * EDGE_SPEED * delta / zoom.x


func _apply_zoom(factor: float) -> void:
	var z: float = clampf(zoom.x * factor, ZOOM_MIN, ZOOM_MAX)
	zoom = Vector2(z, z)
