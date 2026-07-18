class_name Bootstrap
extends Node2D
## Builds the whole runtime scene in code: sim, camera, renderers.
## Owns the fixed-timestep loop — accumulates real delta into whole sim
## ticks at TICKS_PER_SECOND. The sim never sees delta; speed controls
## change ticks-per-frame, never tick size.

const TICKS_PER_SECOND := 10.0
const SPEEDS: Array[float] = [1.0, 3.0, 10.0]

var world: SimWorld
var paused := false
var speed_index := 0
var _accumulator := 0.0

var _renderer: TilemapRenderer
var _hud_label: Label
var _screenshot_path := ""
var _screenshot_frames := 0


func _ready() -> void:
	world = SimWorld.new(12345)
	_scatter_placeholder_floors()

	_renderer = TilemapRenderer.new()
	_renderer.name = "TilemapRenderer"
	add_child(_renderer)
	_renderer.setup(world.grid)

	var cam := CameraRig.new()
	cam.name = "CameraRig"
	cam.position = Vector2(world.grid.width, world.grid.height) * TilemapRenderer.TILE_PX * 0.5
	add_child(cam)

	_build_hud()

	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--screenshot="):
			_screenshot_path = arg.trim_prefix("--screenshot=")


func _process(delta: float) -> void:
	if not paused:
		_accumulator += delta * TICKS_PER_SECOND * SPEEDS[speed_index]
		while _accumulator >= 1.0:
			world.tick()
			_accumulator -= 1.0
	_update_hud()

	if _screenshot_path != "":
		_screenshot_frames += 1
		if _screenshot_frames == 30:
			get_viewport().get_texture().get_image().save_png(_screenshot_path)
			get_tree().quit()


func _unhandled_key_input(event: InputEvent) -> void:
	var key := event as InputEventKey
	if key == null or not key.pressed or key.echo:
		return
	match key.keycode:
		KEY_SPACE:
			paused = not paused
		KEY_1:
			speed_index = 0
		KEY_2:
			speed_index = 1
		KEY_3:
			speed_index = 2


## Placeholder terrain so the M0 grid isn't a flat brown square: a concrete
## pad in the middle, grass patches around it. Uses the sim RNG so the same
## seed always produces the same map.
func _scatter_placeholder_floors() -> void:
	var g := world.grid
	for y in range(40, 60):
		for x in range(40, 60):
			g.set_floor(x, y, SimTile.FloorType.CONCRETE)
	for i in range(600):
		var x := world.rng.randi_range_n(g.width)
		var y := world.rng.randi_range_n(g.height)
		if g.tile_at(x, y).floor_type == SimTile.FloorType.DIRT:
			g.set_floor(x, y, SimTile.FloorType.GRASS)


func _build_hud() -> void:
	var layer := CanvasLayer.new()
	layer.name = "HudLayer"
	add_child(layer)
	_hud_label = Label.new()
	_hud_label.position = Vector2(12, 8)
	_hud_label.add_theme_color_override("font_color", Color.WHITE)
	_hud_label.add_theme_color_override("font_shadow_color", Color.BLACK)
	_hud_label.add_theme_constant_override("shadow_offset_x", 1)
	_hud_label.add_theme_constant_override("shadow_offset_y", 1)
	layer.add_child(_hud_label)


func _update_hud() -> void:
	var c := world.clock
	_hud_label.text = "Day %d  %02d:%02d   %s   [Space] pause  [1/2/3] speed %.0fx" % [
		c.day(), c.hour_of_day(), c.minute_of_day() % 60,
		"PAUSED" if paused else "running",
		SPEEDS[speed_index],
	]
