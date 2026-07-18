class_name Bootstrap
extends Node3D
## Builds the whole runtime scene in code: sim, lighting, camera, renderers,
## input tools. Owns the fixed-timestep loop — accumulates real delta into
## whole sim ticks at TICKS_PER_SECOND. The sim never sees delta; speed
## controls change ticks-per-frame, never tick size.

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

var _camera_rig: CameraRig
var _terrain: TerrainRenderer3D
var _structures: StructuresRenderer3D
var _drag_preview: MeshInstance3D
var _hud_label: Label
var _screenshot_path := ""
var _screenshot_frames := 0


func _ready() -> void:
	world = SimWorld.new(12345)
	_scatter_placeholder_floors()

	_setup_environment()

	_terrain = TerrainRenderer3D.new()
	_terrain.name = "Terrain"
	add_child(_terrain)
	_terrain.setup(world.grid)

	_structures = StructuresRenderer3D.new()
	_structures.name = "Structures"
	add_child(_structures)
	_structures.setup(world)

	_drag_preview = MeshInstance3D.new()
	_drag_preview.name = "DragPreview"
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(1, 1, 1, 0.3)
	_drag_preview.material_override = mat
	_drag_preview.visible = false
	add_child(_drag_preview)

	build_tool = BuildTool.new(world)
	zone_tool = ZoneTool.new(world)

	_camera_rig = CameraRig.new()
	_camera_rig.name = "CameraRig"
	_camera_rig.position = Vector3(world.grid.width, 0.0, world.grid.height) * 0.5
	add_child(_camera_rig)

	_build_hud()

	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--screenshot="):
			_screenshot_path = arg.trim_prefix("--screenshot=")
func _setup_environment() -> void:
	var env := Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.55, 0.68, 0.78)
	env.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	env.ambient_light_color = Color(0.65, 0.7, 0.78)
	env.ambient_light_energy = 0.9
	var world_env := WorldEnvironment.new()
	world_env.environment = env
	add_child(world_env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-55, -35, 0)
	sun.light_energy = 1.1
	sun.shadow_enabled = true
	add_child(sun)


func _process(delta: float) -> void:
	if not paused:
		_accumulator += delta * TICKS_PER_SECOND * SPEEDS[speed_index]
		while _accumulator >= 1.0:
			world.tick()
			_accumulator -= 1.0
	_update_hud()
	_update_drag_preview()

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
		var ground = _camera_rig.ground_point(mb.position)
		if ground == null:
			return
		var tile := Vector2i(floori(ground.x), floori(ground.z))
		var frac := Vector2(fposmod(ground.x, 1.0), fposmod(ground.z, 1.0))
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
		var ground = _camera_rig.ground_point((event as InputEventMouseMotion).position)
		if ground != null:
			build_tool.update_drag(Vector2i(floori(ground.x), floori(ground.z)))


static func _digit_pressed(keycode: int) -> int:
	if keycode >= KEY_1 and keycode <= KEY_9:
		return keycode - KEY_1 + 1
	return -1


func _update_drag_preview() -> void:
	if not build_tool.dragging:
		_drag_preview.visible = false
		return
	_drag_preview.visible = true
	var rect := build_tool.drag_rect()
	var plane := PlaneMesh.new()
	plane.size = Vector2(rect.size.x, rect.size.y)
	_drag_preview.mesh = plane
	_drag_preview.position = Vector3(rect.position.x + rect.size.x / 2.0, 0.05, rect.position.y + rect.size.y / 2.0)


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
		var ground = _camera_rig.ground_point(get_viewport().get_mouse_position())
		if ground != null:
			var t := Vector2i(floori(ground.x), floori(ground.z))
			if world.grid.in_bounds(t.x, t.y):
				var r := world.room_at(t.x, t.y)
				if r != null:
					lines.append("room: %d tiles, sealed=%s, valid=%s" % [r.tiles.size(), r.sealed, r.zone_valid])

	_hud_label.text = "\n".join(lines)
