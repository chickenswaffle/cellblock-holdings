class_name Bootstrap
extends Node3D
## Builds the whole runtime scene in code: sim, lighting, camera, renderers,
## input tools. Owns the fixed-timestep loop — accumulates real delta into
## whole sim ticks at TICKS_PER_SECOND. The sim never sees delta; speed
## controls change ticks-per-frame, never tick size.

const TICKS_PER_SECOND := 10.0
const SPEEDS: Array[float] = [1.0, 3.0, 10.0]

enum ToolMode { NONE, WALL, DOOR, FLOOR, OBJECT, ZONE }

const WALL_STYLE_NAMES := ["room outline", "single wall"]
const FLOOR_NAMES := ["dirt", "concrete", "tile", "grass"]
const OBJECT_NAMES := [
	"bed", "toilet", "table", "bench", "phone",
	"weight bench", "sewing station", "cctv", "metal detector",
]
const ZONE_NAMES := ["cell", "canteen", "yard", "workshop", "solitary", "medical", "staff room", "visitation"]

const ROLE_NAMES := ["guard", "worker", "support"]

## Incident resolutions. Hiring has no shortcut on purpose — it's never
## time-critical, so it lives on the staff panel's buttons rather than
## spending three letters of an already-crowded keyboard.
const RESOLUTION_KEYS := {
	KEY_F: "force",
	KEY_G: "solitary",
	KEY_N: "negotiate",
	KEY_B: "separate",
	KEY_K: "concede",
}
const MANUAL_LOCKDOWN_MINUTES := 240

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
var _agents: AgentRenderer3D
var _staff_renderer: StaffRenderer3D
var _tension_overlay: TensionOverlay3D
var _drag_preview: MeshInstance3D
var _hud: GameHud
var _inspected_id: int = -1
var _screenshot_path := ""
var _screenshot_frames := 0


func _ready() -> void:
	world = SimWorld.new(12345)
	_scatter_placeholder_floors()
	_build_starter_facility()

	_setup_environment()

	_terrain = TerrainRenderer3D.new()
	_terrain.name = "Terrain"
	add_child(_terrain)
	_terrain.setup(world.grid)

	_structures = StructuresRenderer3D.new()
	_structures.name = "Structures"
	add_child(_structures)
	_structures.setup(world)

	_agents = AgentRenderer3D.new()
	_agents.name = "Agents"
	add_child(_agents)
	_agents.setup(world)

	_staff_renderer = StaffRenderer3D.new()
	_staff_renderer.name = "Staff"
	add_child(_staff_renderer)
	_staff_renderer.setup(world)

	_tension_overlay = TensionOverlay3D.new()
	_tension_overlay.name = "TensionOverlay"
	add_child(_tension_overlay)
	_tension_overlay.setup(world)

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
	_camera_rig.position = Vector3(24.0, 0.0, 18.0) # centered on the starter facility
	add_child(_camera_rig)
	# Bounds so panning can never lose the map, and a home to come back to.
	_camera_rig.set_bounds(world.grid.width, world.grid.height)
	_camera_rig.set_home(_camera_rig.position)

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

	# One key, one meaning, everywhere. The old scheme overloaded the digits
	# to mean speed OR floor type OR object type OR zone kind depending on
	# the active tool, which meant you couldn't know what a key would do
	# without first reading the HUD to find out what mode you were in.
	if key.keycode == KEY_SPACE:
		paused = not paused
		return
	if key.keycode == KEY_ESCAPE:
		_select_tool(ToolMode.NONE)
		return
	if key.keycode == KEY_HOME:
		_camera_rig.recenter()
		return
	if key.keycode == KEY_T:
		_toggle_overlay()
		return
	if key.keycode == KEY_L:
		IncidentSystem.begin_lockdown(world, MANUAL_LOCKDOWN_MINUTES)
		return

	# Q/E cycle the active tool's sub-type (floor/object/zone kind).
	if key.keycode == KEY_Q:
		_cycle_subtype(-1)
		return
	if key.keycode == KEY_E:
		_cycle_subtype(1)
		return

	# +/- change speed; digits are exclusively tool selection now.
	if key.keycode == KEY_MINUS:
		speed_index = maxi(0, speed_index - 1)
		return
	if key.keycode == KEY_EQUAL or key.keycode == KEY_PLUS:
		speed_index = mini(SPEEDS.size() - 1, speed_index + 1)
		paused = false
		return

	var action: String = RESOLUTION_KEYS.get(key.keycode, "")
	if action != "":
		_resolve_worst(action)
		return

	var digit := _digit_pressed(key.keycode)
	if digit >= 1 and digit < ToolMode.size():
		_select_tool(digit) # 1..5 -> wall, door, floor, object, zone


## Resolutions act on the worst open incident — with no per-incident
## selection UI yet, "deal with the worst thing happening" is the only
## unambiguous target. Per-incident selection lands with M7's UI pass.
func _resolve_worst(action: String) -> void:
	var inc := world.worst_incident()
	if inc == null:
		return
	match action:
		"force":
			IncidentSystem.resolve_force(world, inc)
		"solitary":
			IncidentSystem.resolve_solitary(world, inc)
		"negotiate":
			IncidentSystem.resolve_negotiate(world, inc)
		"separate":
			IncidentSystem.resolve_separate(world, inc)
		"concede":
			IncidentSystem.resolve_concede(world, inc)


func _fire(role: int) -> void:
	var victim := Hiring.newest_of_role(world, role)
	if victim != null:
		world.dismiss_staff(victim.id, "fired")


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
	# Clicking a HUD panel must never also build, demolish or deselect in the
	# world behind it.
	if _hud != null and _hud.pointer_over_ui():
		return
	if tool_mode == ToolMode.NONE:
		var mb_inspect := event as InputEventMouseButton
		if mb_inspect != null and mb_inspect.button_index == MOUSE_BUTTON_LEFT and mb_inspect.pressed:
			var ground_inspect = _camera_rig.ground_point(mb_inspect.position)
			if ground_inspect != null:
				var found := world.nearest_prisoner(Vector2(ground_inspect.x, ground_inspect.z), 1.5)
				_inspected_id = found.id if found != null else -1
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
				elif tool_mode == ToolMode.DOOR:
					build_tool.click(tile, frac)
				else:
					# Objects drag too now, so a press starts a drag and a
					# release with no movement still places exactly one.
					build_tool.begin_drag(tile)
			else:
				build_tool.end_drag()
		elif mb.button_index == MOUSE_BUTTON_RIGHT and not mb.pressed:
			# Demolish on *release*, and only when the camera didn't claim
			# this as an orbit — right-drag turns the view, right-click
			# knocks something down, and they share a button.
			if not _camera_rig.orbiting:
				build_tool.remove_at(tile, frac)
	elif event is InputEventMouseMotion and build_tool.dragging:
		var ground = _camera_rig.ground_point((event as InputEventMouseMotion).position)
		if ground != null:
			build_tool.update_drag(Vector2i(floori(ground.x), floori(ground.z)))


static func _digit_pressed(keycode: int) -> int:
	if keycode >= KEY_1 and keycode <= KEY_9:
		return keycode - KEY_1 + 1
	return -1


## Colour of the selection box: green when the order is valid and paid for,
## red when it isn't. Tinting the actual area is the fastest way to answer
## "what exactly am I about to build" — far quicker to read than the numbers.
const PREVIEW_OK := Color(0.30, 0.85, 0.45, 0.35)
const PREVIEW_BAD := Color(0.92, 0.30, 0.25, 0.38)
## A worker delivers one work unit per tick at full rate.
const WORK_PER_WORKER_TICK := 1.0


func _update_drag_preview() -> void:
	if not build_tool.dragging:
		_drag_preview.visible = false
		_hud.hide_build_preview()
		return

	var rect := build_tool.drag_rect()
	var summary := build_tool.preview_summary()
	var buildable: bool = summary["count"] > 0 and summary["affordable"]

	_drag_preview.visible = true
	var plane := PlaneMesh.new()
	plane.size = Vector2(rect.size.x, rect.size.y)
	_drag_preview.mesh = plane
	_drag_preview.position = Vector3(
		rect.position.x + rect.size.x / 2.0, 0.06, rect.position.y + rect.size.y / 2.0
	)
	var mat := _drag_preview.material_override as StandardMaterial3D
	if mat != null:
		mat.albedo_color = PREVIEW_OK if buildable else PREVIEW_BAD

	summary["noun"] = _preview_noun(summary["count"])
	summary["duration"] = _estimate_duration(summary["work"])
	_hud.show_build_preview(summary)


func _preview_noun(count: int) -> String:
	var plural := count != 1
	match tool_mode:
		ToolMode.WALL:
			return "wall sections" if plural else "wall section"
		ToolMode.FLOOR:
			return "floor tiles" if plural else "floor tile"
		ToolMode.OBJECT:
			return "%ss" % OBJECT_NAMES[build_tool.object_type] if plural else OBJECT_NAMES[build_tool.object_type]
		_:
			return "items" if plural else "item"


## Turn queued worker-ticks into wall-clock sim time, given who is actually
## on shift. With nobody rostered the honest answer isn't a number — it's
## that this will sit in the queue untouched until you hire someone.
func _estimate_duration(work: float) -> String:
	if work <= 0.0:
		return "—"
	var workers := world.on_duty_count(Staff.Role.WORKER)
	if workers == 0:
		return "no workers on shift"
	var ticks := work / (float(workers) * WORK_PER_WORKER_TICK)
	var minutes := int(ticks / float(SimClock.TICKS_PER_SIM_MINUTE))
	if minutes < 60:
		return "~%d min" % maxi(1, minutes)
	return "~%dh %02dm" % [minutes / 60, minutes % 60]


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


## The leased site's existing infrastructure — set up directly (not queued
## through construction) since it's what the player inherits on day one,
## not something they're actively building. 4 cells, a canteen, a small
## yard, and enough intake to fill half the cells.
func _build_starter_facility() -> void:
	var g := world.grid
	var bx := 15
	var by := 15

	for i in range(4):
		var x0 := bx + i * 4
		var x1 := x0 + 2
		_room_box(x0, by, x1, by + 2, SimTile.FloorType.TILE)
		g.set_door(x0 + 1, by + 2, SimTile.WALL_S, true)
		g.place_object(x0, by, ObjectDef.Type.BED)
		g.place_object(x1, by, ObjectDef.Type.TOILET)

	var cx0 := bx
	var cx1 := bx + 9
	var cy0 := by + 5
	var cy1 := by + 9
	_room_box(cx0, cy0, cx1, cy1, SimTile.FloorType.CONCRETE)
	g.set_door(cx0 + 4, cy0, SimTile.WALL_N, true)
	g.place_object(cx0 + 2, cy0 + 2, ObjectDef.Type.TABLE)
	g.place_object(cx0 + 6, cy0 + 2, ObjectDef.Type.TABLE)

	var yx0 := bx + 11
	var yx1 := bx + 18
	var yy0 := by + 5
	var yy1 := by + 12
	_room_box(yx0, yy0, yx1, yy1, SimTile.FloorType.CONCRETE)
	g.set_door(yx0, yy0 + 3, SimTile.WALL_W, true)
	g.place_object(yx0 + 3, yy0 + 3, ObjectDef.Type.WEIGHT_BENCH)

	# Staff room — without one, tired staff rest where they stand and take
	# twice as long to recover.
	var sx0 := bx
	var sx1 := bx + 4
	var sy0 := by + 11
	var sy1 := by + 14
	_room_box(sx0, sy0, sx1, sy1, SimTile.FloorType.TILE)
	g.set_door(sx0 + 2, sy0, SimTile.WALL_N, true)
	g.place_object(sx0 + 2, sy0 + 2, ObjectDef.Type.TABLE)

	# Staff clock in west of the block, on open ground outside every room.
	world.gate_tile = Vector2i(bx - 2, by + 7)

	world.tick() # force one room-detection pass before zoning

	for i in range(4):
		var x0 := bx + i * 4
		var room := world.room_at(x0 + 1, by + 1)
		if room != null:
			world.zone_room(room.id, ZoneValidator.Kind.CELL)
	var canteen := world.room_at(cx0 + 1, cy0 + 1)
	if canteen != null:
		world.zone_room(canteen.id, ZoneValidator.Kind.CANTEEN)
	var yard := world.room_at(yx0 + 1, yy0 + 1)
	if yard != null:
		world.zone_room(yard.id, ZoneValidator.Kind.YARD)
	var staff_room := world.room_at(sx0 + 1, sy0 + 1)
	if staff_room != null:
		world.zone_room(staff_room.id, ZoneValidator.Kind.STAFF_ROOM)

	for i in range(6):
		Intake.intake(world)

	# The skeleton crew the lease comes with: two guards (so one covers
	# nights), two workers to actually build what the player queues, and one
	# support hand for the canteen. Deliberately not enough — hiring up is
	# the first real decision the player makes.
	for i in range(2):
		Hiring.hire(world, Staff.Role.GUARD)
		Hiring.hire(world, Staff.Role.WORKER)
	Hiring.hire(world, Staff.Role.SUPPORT)


func _room_box(x0: int, y0: int, x1: int, y1: int, floor_type: int) -> void:
	var g := world.grid
	for x in range(x0, x1 + 1):
		g.set_wall(x, y0, SimTile.WALL_N, true)
		g.set_wall(x, y1, SimTile.WALL_S, true)
		for y in range(y0, y1 + 1):
			g.set_floor(x, y, floor_type)
	for y in range(y0, y1 + 1):
		g.set_wall(x0, y, SimTile.WALL_W, true)
		g.set_wall(x1, y, SimTile.WALL_E, true)




func _build_hud() -> void:
	_hud = GameHud.new()
	_hud.name = "Hud"
	add_child(_hud)
	_hud.setup(world)
	_hud.on_tool_selected = _select_tool
	_hud.on_subtype_selected = _select_subtype
	_hud.on_speed_selected = func(index: int) -> void:
		speed_index = index
		paused = false
	_hud.on_pause_toggled = func() -> void: paused = not paused
	_hud.on_hire = func(role: int) -> void: Hiring.hire(world, role)
	_hud.on_fire = _fire
	_hud.on_resolve = _resolve_worst
	_hud.on_lockdown = func() -> void:
		IncidentSystem.begin_lockdown(world, MANUAL_LOCKDOWN_MINUTES)
	_hud.on_overlay_toggled = _toggle_overlay
	_hud.on_edge_scroll_toggled = func() -> void:
		_camera_rig.edge_scroll_enabled = not _camera_rig.edge_scroll_enabled
	_hud.on_recenter = func() -> void:
		_camera_rig.recenter()
		_camera_rig.reset_angle()
	_hud.on_rotate = func(degrees: float) -> void: _camera_rig.rotate_by(degrees)


func _toggle_overlay() -> void:
	_tension_overlay.visible = not _tension_overlay.visible
	if _tension_overlay.visible:
		_tension_overlay.refresh()


func _select_tool(mode: int) -> void:
	tool_mode = mode
	build_tool.cancel_drag()
	_sync_tool_mode()


## Sub-type options for whichever tool is active, or [] if it has none.
## The HUD renders these as buttons; Q/E cycle them.
func _subtype_options() -> Array:
	match tool_mode:
		ToolMode.WALL:
			return WALL_STYLE_NAMES
		ToolMode.FLOOR:
			return FLOOR_NAMES
		ToolMode.OBJECT:
			return OBJECT_NAMES
		ToolMode.ZONE:
			return ZONE_NAMES
		_:
			return []


func _subtype_index() -> int:
	match tool_mode:
		ToolMode.WALL:
			return build_tool.wall_style
		ToolMode.FLOOR:
			return build_tool.floor_type
		ToolMode.OBJECT:
			return build_tool.object_type
		ToolMode.ZONE:
			return zone_tool.zone_kind
		_:
			return -1


func _select_subtype(index: int) -> void:
	var options := _subtype_options()
	if index < 0 or index >= options.size():
		return
	match tool_mode:
		ToolMode.WALL:
			build_tool.wall_style = index
		ToolMode.FLOOR:
			build_tool.floor_type = index
		ToolMode.OBJECT:
			build_tool.object_type = index
		ToolMode.ZONE:
			zone_tool.zone_kind = index


func _cycle_subtype(step: int) -> void:
	var options := _subtype_options()
	if options.is_empty():
		return
	_select_subtype(posmod(_subtype_index() + step, options.size()))


func _update_hud() -> void:
	_camera_rig.pointer_over_ui = _hud.pointer_over_ui()
	_hud.refresh({
		"paused": paused,
		"speed_index": speed_index,
		"tool_mode": tool_mode,
		"subtype_options": _subtype_options(),
		"subtype_index": _subtype_index(),
		"overlay_on": _tension_overlay.visible,
		"edge_scroll": _camera_rig.edge_scroll_enabled,
		"inspected_id": _inspected_id,
	})


const TRAIT_NAMES := {
	Prisoner.Trait.VOLATILE: "Volatile", Prisoner.Trait.CUNNING: "Cunning",
	Prisoner.Trait.INSTITUTIONALIZED: "Institutionalized", Prisoner.Trait.FRAIL: "Frail",
	Prisoner.Trait.CONNECTED: "Connected", Prisoner.Trait.PENITENT: "Penitent",
}
