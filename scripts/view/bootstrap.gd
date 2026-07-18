class_name Bootstrap
extends Node2D
## Builds the whole runtime scene in code: sim, camera, renderers, input
## tools. Owns the fixed-timestep loop — accumulates real delta into whole
## sim ticks at TICKS_PER_SECOND. The sim never sees delta; speed controls
## change ticks-per-frame, never tick size.

const TICKS_PER_SECOND := 10.0
const SPEEDS: Array[float] = [1.0, 3.0, 10.0]

enum ToolMode { NONE, WALL, DOOR, FLOOR, OBJECT, ZONE }
const MODE_NAMES := ["camera", "wall", "door", "floor", "object", "zone"]

const FLOOR_NAMES := ["dirt", "concrete", "tile", "grass"]
const OBJECT_NAMES := [
	"bed", "toilet", "table", "bench", "phone",
	"weight bench", "sewing station", "cctv", "metal detector",
]
const ZONE_NAMES := ["cell", "canteen", "yard", "workshop", "solitary", "medical", "staff room", "visitation"]

var world: SimWorld
var paused := false
var speed_index := 0
var _accumulator := 0.0

var tool_mode: int = ToolMode.NONE
var build_tool: BuildTool
var zone_tool: ZoneTool

var _renderer: TilemapRenderer
var _walls: WallRenderer
var _drag_preview: Node2D
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

	_walls = WallRenderer.new()
	_walls.name = "WallRenderer"
	add_child(_walls)
	_walls.setup(world)

	_drag_preview = Node2D.new()
	_drag_preview.name = "DragPreview"
	_drag_preview.draw.connect(_draw_drag_preview)
	add_child(_drag_preview)

	build_tool = BuildTool.new(world)
	zone_tool = ZoneTool.new(world)

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
	_drag_preview.queue_redraw()

	if _screenshot_path != "":
		_screenshot_frames += 1
		if _screenshot_frames == 30:
			get_viewport().get_texture().get_image().save_png(_screenshot_path)
			get_tree().quit()


func _unhandled_key_input(event: InputEvent) -> void:
	var key := event as InputEventKey
	if key == null or not key.pressed or key.echo:
		return

	if key.keycode == KEY_SPACE:
		paused = not paused
		return
	if key.keycode == KEY_ESCAPE:
		tool_mode = ToolMode.NONE
		build_tool.cancel_drag()
		return
	if key.keycode == KEY_Q:
		tool_mode = (tool_mode - 1 + ToolMode.size()) % ToolMode.size()
		build_tool.cancel_drag()
		_sync_tool_mode()
		return
	if key.keycode == KEY_E:
		tool_mode = (tool_mode + 1) % ToolMode.size()
		build_tool.cancel_drag()
		_sync_tool_mode()
		return

	var digit := _digit_pressed(key.keycode)
	if digit >= 0:
		if tool_mode == ToolMode.NONE:
			if digit <= SPEEDS.size():
				speed_index = digit - 1
		elif tool_mode == ToolMode.FLOOR and digit >= 1 and digit <= FLOOR_NAMES.size():
			build_tool.floor_type = digit - 1
		elif tool_mode == ToolMode.OBJECT and digit >= 1 and digit <= OBJECT_NAMES.size():
			build_tool.object_type = digit - 1
		elif tool_mode == ToolMode.ZONE and digit >= 1 and digit <= ZONE_NAMES.size():
			zone_tool.zone_kind = digit - 1


func _sync_tool_mode() -> void:
	match tool_mode:
		ToolMode.WALL:
			build_tool.mode = BuildTool.Mode.WALL
		ToolMode.DOOR:
			build_tool.mode = BuildTool.Mode.DOOR
		ToolMode.FLOOR:
			build_tool.mode = BuildTool.Mode.FLOOR
		ToolMode.OBJECT:
			build_tool.mode = BuildTool.Mode.OBJECT


func _unhandled_input(event: InputEvent) -> void:
	if tool_mode == ToolMode.NONE:
		return
	var mb := event as InputEventMouseButton
	if mb != null:
		var tile := _mouse_tile()
		var frac := _mouse_local_frac()
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed:
				if tool_mode == ToolMode.ZONE:
					zone_tool.click(tile)
				elif tool_mode == ToolMode.DOOR or tool_mode == ToolMode.OBJECT:
					build_tool.click(tile, frac)
				else:
					build_tool.begin_drag(tile)
			else:
				build_tool.end_drag()
		elif mb.button_index == MOUSE_BUTTON_RIGHT and mb.pressed:
			build_tool.remove_at(tile, frac)
	elif event is InputEventMouseMotion and build_tool.dragging:
		build_tool.update_drag(_mouse_tile())


func _mouse_tile() -> Vector2i:
	var p := get_global_mouse_position() / TilemapRenderer.TILE_PX
	return Vector2i(floori(p.x), floori(p.y))


func _mouse_local_frac() -> Vector2:
	var p := get_global_mouse_position() / TilemapRenderer.TILE_PX
	return Vector2(fposmod(p.x, 1.0), fposmod(p.y, 1.0))


static func _digit_pressed(keycode: int) -> int:
	if keycode >= KEY_1 and keycode <= KEY_9:
		return keycode - KEY_1 + 1
	return -1


func _draw_drag_preview() -> void:
	if not build_tool.dragging:
		return
	var rect := build_tool.drag_rect()
	var px := TilemapRenderer.TILE_PX
	_drag_preview.draw_rect(
		Rect2(rect.position * px, rect.size * px),
		Color(1, 1, 1, 0.25), true
	)
	_drag_preview.draw_rect(
		Rect2(rect.position * px, rect.size * px),
		Color(1, 1, 1, 0.8), false, 2.0
	)


## Placeholder terrain so the map isn't a flat brown square: a concrete pad
## in the middle, grass patches around it. Uses the sim RNG so the same
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
	var lines := []
	lines.append("Day %d  %02d:%02d   %s   $%d   [Space] pause  [1/2/3] speed %.0fx" % [
		c.day(), c.hour_of_day(), c.minute_of_day() % 60,
		"PAUSED" if paused else "running",
		world.ledger.balance,
		SPEEDS[speed_index],
	])
	lines.append("[Q/E] tool: %s   [Esc] camera-only   LMB build  RMB demolish" % MODE_NAMES[tool_mode])

	match tool_mode:
		ToolMode.FLOOR:
			lines.append("floor [1-4]: %s" % FLOOR_NAMES[build_tool.floor_type])
		ToolMode.OBJECT:
			lines.append("object [1-9]: %s ($%d)" % [OBJECT_NAMES[build_tool.object_type], ObjectDef.cost_of(build_tool.object_type)])
		ToolMode.ZONE:
			lines.append("zone [1-8]: %s" % ZONE_NAMES[zone_tool.zone_kind])
		ToolMode.WALL, ToolMode.DOOR:
			pass

	if build_tool.dragging:
		lines.append("cost preview: $%d" % build_tool.preview_cost())

	if tool_mode == ToolMode.ZONE:
		var t := _mouse_tile()
		if world.grid.in_bounds(t.x, t.y):
			var r := world.room_at(t.x, t.y)
			if r != null:
				lines.append("room: %d tiles, sealed=%s, valid=%s" % [r.tiles.size(), r.sealed, r.zone_valid])

	_hud_label.text = "\n".join(lines)
