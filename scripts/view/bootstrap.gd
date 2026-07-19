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

var _save: SaveManager
var _camera_rig: CameraRig
var _terrain: TerrainRenderer3D
var _structures: StructuresRenderer3D
var _agents: AgentRenderer3D
var _staff_renderer: StaffRenderer3D
var _tension_overlay: TensionOverlay3D
var _drag_preview: MeshInstance3D
var _ghost: BuildGhostRenderer3D
var _hud: GameHud
var _inspected_id: int = -1
var _screenshot_path := ""
var _screenshot_frames := 0
var _title_screen: CanvasLayer
var _game_over_screen: CanvasLayer
var _started := false


func _ready() -> void:
	_setup_environment()
	_save = SaveManager.new()

	_build_renderers()
	_build_hud_node()
	_show_title()

	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--screenshot="):
			_screenshot_path = arg.trim_prefix("--screenshot=")

	# A screenshot run is for verifying the *game*, not the menu — skip the
	# title screen straight into a fresh world.
	if _screenshot_path != "":
		_hud.suppress_onboarding = true
		_start_new()


## Build the 3D renderers with a placeholder sim — replaced when the player
## starts or loads a game.
func _build_renderers() -> void:
	var placeholder := SimWorld.new(0)
	_terrain = TerrainRenderer3D.new()
	_terrain.name = "Terrain"
	add_child(_terrain)
	_terrain.setup(placeholder.grid)

	_structures = StructuresRenderer3D.new()
	_structures.name = "Structures"
	add_child(_structures)

	_agents = AgentRenderer3D.new()
	_agents.name = "Agents"
	add_child(_agents)

	_staff_renderer = StaffRenderer3D.new()
	_staff_renderer.name = "Staff"
	add_child(_staff_renderer)

	_tension_overlay = TensionOverlay3D.new()
	_tension_overlay.name = "TensionOverlay"
	add_child(_tension_overlay)

	_ghost = BuildGhostRenderer3D.new()
	_ghost.name = "BuildGhost"
	add_child(_ghost)

	_drag_preview = MeshInstance3D.new()
	_drag_preview.name = "DragPreview"
	var mat := StandardMaterial3D.new()
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	mat.albedo_color = Color(1, 1, 1, 0.3)
	_drag_preview.material_override = mat
	_drag_preview.visible = false
	add_child(_drag_preview)

	_camera_rig = CameraRig.new()
	_camera_rig.name = "CameraRig"
	_camera_rig.position = Vector3(40.0, 0.0, 30.0)
	add_child(_camera_rig)
	_camera_rig.set_bounds(placeholder.grid.width, placeholder.grid.height)
	_camera_rig.set_home(_camera_rig.position)
	_camera_rig.visible = false


func _init_world(seed_val: int) -> void:
	world = SimWorld.new(seed_val)
	_scatter_placeholder_floors()
	_build_starter_facility()
	_save.setup(world)
	_wire_world()

	_terrain.setup(world.grid)
	_structures.setup(world)
	_agents.setup(world)
	_staff_renderer.setup(world)
	_tension_overlay.setup(world)
	build_tool = BuildTool.new(world)
	zone_tool = ZoneTool.new(world)

	_camera_rig.visible = true
	_camera_rig.position = Vector3(24.0, 0.0, 18.0)
	_camera_rig.set_bounds(world.grid.width, world.grid.height)
	_camera_rig.set_home(_camera_rig.position)

	_hud.setup(world)
	_started = true
	paused = false
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
	_hud.on_confirm_build = func() -> void: build_tool.confirm_pending()
	_hud.on_cancel_build = func() -> void: build_tool.cancel_pending()


func _load_world() -> bool:
	world = SimWorld.new(0)
	_save.setup(world)
	if not _save.load_save():
		return false
	_wire_world()

	_terrain.setup(world.grid)
	_structures.setup(world)
	_agents.setup(world)
	_staff_renderer.setup(world)
	_tension_overlay.setup(world)
	build_tool = BuildTool.new(world)
	zone_tool = ZoneTool.new(world)

	_camera_rig.visible = true
	_camera_rig.position = Vector3(24.0, 0.0, 18.0)
	_camera_rig.set_bounds(world.grid.width, world.grid.height)
	_camera_rig.set_home(_camera_rig.position)

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
	_hud.on_confirm_build = func() -> void: build_tool.confirm_pending()
	_hud.on_cancel_build = func() -> void: build_tool.cancel_pending()
	_started = true
	paused = false
	return true


func _wire_world() -> void:
	world.events.subscribe(_on_sim_event)


func _on_sim_event(event_name: String, payload: Dictionary) -> void:
	match event_name:
		"facility_riot":
			_camera_rig.shake(4.0)
			paused = true
		"incident_escalated":
			if payload.get("kind", 0) >= Incident.Kind.FIGHT:
				_camera_rig.shake(1.5)
		"contract_broken":
			_camera_rig.shake(6.0)
			paused = true
		"incident_started":
			_camera_rig.shake(0.5)
		"force_used":
			_camera_rig.shake(2.0)



func _show_title() -> void:
	_title_screen = CanvasLayer.new()
	_title_screen.name = "TitleScreen"
	_title_screen.layer = 20
	add_child(_title_screen)

	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_title_screen.add_child(root)

	var bg := ColorRect.new()
	bg.color = Color(0.05, 0.06, 0.08, 0.95)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(center)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 16)
	center.add_child(col)

	var title_lbl := Label.new()
	title_lbl.text = "CELLBLOCK HOLDINGS"
	title_lbl.add_theme_color_override("font_color", Color(0.85, 0.20, 0.18))
	title_lbl.add_theme_font_size_override("font_size", 36)
	title_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(title_lbl)

	var subtitle := Label.new()
	subtitle.text = "Prison Franchise Management"
	subtitle.add_theme_color_override("font_color", Color(0.62, 0.66, 0.72))
	subtitle.add_theme_font_size_override("font_size", 16)
	subtitle.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(subtitle)

	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 20)
	col.add_child(spacer)

	if _save.save_exists():
		var continue_btn := UiTheme.button("CONTINUE", "Resume from your last save")
		continue_btn.custom_minimum_size = Vector2(240, 40)
		continue_btn.add_theme_font_size_override("font_size", 18)
		continue_btn.pressed.connect(_start_continue)
		col.add_child(continue_btn)

	var new_btn := UiTheme.button("NEW GAME", "Start a new facility")
	new_btn.custom_minimum_size = Vector2(240, 40)
	new_btn.add_theme_font_size_override("font_size", 18)
	new_btn.pressed.connect(_start_new)
	col.add_child(new_btn)

	var hint := Label.new()
	hint.text = "WASD/arrows pan · wheel zoom · right-drag or , . rotate\n1-5 build tools · Space pause · Home recenter"
	hint.add_theme_color_override("font_color", Color(0.50, 0.54, 0.60))
	hint.add_theme_font_size_override("font_size", 12)
	hint.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(hint)


func _start_continue() -> void:
	if _load_world():
		_title_screen.queue_free()
		_title_screen = null
		_hud._on_game_started()


func _start_new() -> void:
	if _save.save_exists():
		_save.delete_save()
	_init_world(12345)
	_title_screen.queue_free()
	_title_screen = null
	_hud._on_game_started()


func _show_game_over() -> void:
	_save.delete_save()
	_game_over_screen = CanvasLayer.new()
	_game_over_screen.name = "GameOverScreen"
	_game_over_screen.layer = 20
	add_child(_game_over_screen)

	var root := Control.new()
	root.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_game_over_screen.add_child(root)

	var bg := ColorRect.new()
	bg.color = Color(0.05, 0.02, 0.02, 0.92)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)

	var center_over := CenterContainer.new()
	center_over.set_anchors_preset(Control.PRESET_FULL_RECT)
	root.add_child(center_over)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 12)
	center_over.add_child(col)

	var title := Label.new()
	title.text = "GAME OVER"
	title.add_theme_color_override("font_color", Color(0.91, 0.31, 0.26))
	title.add_theme_font_size_override("font_size", 32)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(title)

	var reason_lbl := Label.new()
	reason_lbl.text = world.game_over_reason
	reason_lbl.add_theme_color_override("font_color", Color(0.80, 0.82, 0.85))
	reason_lbl.add_theme_font_size_override("font_size", 15)
	reason_lbl.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	reason_lbl.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	reason_lbl.custom_minimum_size = Vector2(420, 0)
	col.add_child(reason_lbl)

	var stats := Label.new()
	var days := world.clock.day()
	stats.text = "Survived %d day%s  ·  $%s earned  ·  Peak tension %.0f%%" % [
		days, "" if days == 1 else "s",
		GameHud._thousands(world.contract.total_earned),
		world.tension.peak() * 100.0,
	]
	stats.add_theme_color_override("font_color", Color(0.62, 0.66, 0.72))
	stats.add_theme_font_size_override("font_size", 13)
	stats.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(stats)

	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 16)
	col.add_child(spacer)

	var retry := UiTheme.button("TRY AGAIN", "Start a new game")
	retry.custom_minimum_size = Vector2(240, 40)
	retry.add_theme_font_size_override("font_size", 18)
	retry.pressed.connect(_restart_game)
	col.add_child(retry)


func _restart_game() -> void:
	_game_over_screen.queue_free()
	_game_over_screen = null
	_started = false
	get_tree().reload_current_scene()


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
	# Screenshot runs before every other gate so headless captures work in
	# any state — title screen included. No early return: the sim and HUD
	# must keep running or the capture shows frozen default widgets.
	if _screenshot_path != "":
		_screenshot_frames += 1
		if _screenshot_frames == 30:
			get_viewport().get_texture().get_image().save_png(_screenshot_path)
			get_tree().quit()
	if not _started:
		return
	if world.game_over:
		paused = true
	elif not paused:
		_accumulator += delta * TICKS_PER_SECOND * SPEEDS[speed_index]
		while _accumulator >= 1.0:
			world.tick()
			_accumulator -= 1.0
	_update_hud()
	_update_drag_preview()
	_save.tick(delta)

	if world.game_over and _game_over_screen == null:
		_show_game_over()


func _unhandled_key_input(event: InputEvent) -> void:
	if not _started:
		return
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
		# Esc backs out one step at a time: drop a parked selection first,
		# and only leave the tool if there wasn't one.
		if build_tool.has_pending():
			build_tool.cancel_pending()
			return
		_select_tool(ToolMode.NONE)
		return
	if key.keycode == KEY_ENTER or key.keycode == KEY_KP_ENTER:
		build_tool.confirm_pending()
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
	if not _started or world.game_over:
		return
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
					zone_tool.begin_drag(tile)
				elif tool_mode == ToolMode.DOOR:
					build_tool.click(tile, frac)
				else:
					# Objects drag too now, so a press starts a drag and a
					# release with no movement still places exactly one.
					build_tool.begin_drag(tile)
			elif tool_mode == ToolMode.ZONE:
				zone_tool.end_drag()
			else:
				build_tool.end_drag()
		elif mb.button_index == MOUSE_BUTTON_RIGHT and not mb.pressed:
			# Demolish on *release*, and only when the camera didn't claim
			# this as an orbit — right-drag turns the view, right-click
			# knocks something down, and they share a button.
			if not _camera_rig.orbiting:
				build_tool.remove_at(tile, frac)
	elif event is InputEventMouseMotion and (build_tool.dragging or zone_tool.dragging):
		var ground = _camera_rig.ground_point((event as InputEventMouseMotion).position)
		if ground != null:
			var t := Vector2i(floori(ground.x), floori(ground.z))
			build_tool.update_drag(t)
			zone_tool.update_drag(t)


static func _digit_pressed(keycode: int) -> int:
	if keycode >= KEY_1 and keycode <= KEY_9:
		return keycode - KEY_1 + 1
	return -1


## Colour of the selection box: green when the order is valid and paid for,
## red when it isn't. Tinting the actual area is the fastest way to answer
## "what exactly am I about to build" — far quicker to read than the numbers.
const PREVIEW_OK := Color(0.30, 0.85, 0.45, 0.35)
const PREVIEW_BAD := Color(0.92, 0.30, 0.25, 0.38)
## Zoning is a different kind of action from building, so it gets its own
## colour rather than reusing the build green.
const ZONE_PREVIEW := Color(0.35, 0.62, 0.95, 0.38)
## A worker delivers one work unit per tick at full rate.
const WORK_PER_WORKER_TICK := 1.0


func _update_drag_preview() -> void:
	if tool_mode == ToolMode.ZONE:
		_update_zone_preview()
		return

	if not build_tool.dragging and not build_tool.has_pending():
		_drag_preview.visible = false
		_ghost.hide_orders()
		_hud.hide_build_preview()
		return

	var rect := build_tool.drag_rect()
	var summary := build_tool.preview_summary()
	var buildable: bool = summary["count"] > 0 and summary["affordable"]

	_drag_preview.visible = build_tool.dragging
	if build_tool.dragging:
		var plane := PlaneMesh.new()
		plane.size = Vector2(rect.size.x, rect.size.y)
		_drag_preview.mesh = plane
		_drag_preview.position = Vector3(
			rect.position.x + rect.size.x / 2.0, 0.06, rect.position.y + rect.size.y / 2.0
		)
		var mat := _drag_preview.material_override as StandardMaterial3D
		if mat != null:
			mat.albedo_color = PREVIEW_OK if buildable else PREVIEW_BAD

	_ghost.show_orders(build_tool.ghost_orders(), summary["affordable"])

	summary["noun"] = _preview_noun(summary["count"])
	summary["duration"] = _estimate_duration(summary["work"])
	summary["awaiting_confirm"] = build_tool.has_pending()
	_hud.show_build_preview(summary)


## Zoning is free and instant, so it gets the area tint but no price, no
## ghost geometry and no confirmation step.
func _update_zone_preview() -> void:
	_ghost.hide_orders()
	_hud.hide_build_preview()
	if not zone_tool.dragging:
		_drag_preview.visible = false
		return
	var tiles := zone_tool.preview_tiles()
	if tiles.is_empty():
		_drag_preview.visible = false
		return
	var rect := Rect2i(tiles[0], Vector2i.ONE)
	for t in tiles:
		rect = rect.expand(t).expand(t + Vector2i.ONE)
	_drag_preview.visible = true
	var plane := PlaneMesh.new()
	plane.size = Vector2(rect.size.x, rect.size.y)
	_drag_preview.mesh = plane
	_drag_preview.position = Vector3(
		rect.position.x + rect.size.x / 2.0, 0.06, rect.position.y + rect.size.y / 2.0
	)
	var mat := _drag_preview.material_override as StandardMaterial3D
	if mat != null:
		mat.albedo_color = ZONE_PREVIEW


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

	for i in range(4):
		Intake.intake(world)
	# ...plus six more with nowhere to sleep. The yard is their bed tonight.
	for i in range(6):
		Intake.intake_overflow(world, Vector2i(yx0 + 2 + (i % 4), yy0 + 5 + (i / 4)))

	# Thin crew, overcrowded population. The first day is a crisis: 10 inmates
	# in 4 cells, one guard, nobody working the canteen. Two workers means
	# building out of the hole is *possible* — but every dollar spent on
	# staff and walls is a dollar closer to missing payroll. Hire a support
	# hand and another guard before the tension meter finds it for you.
	Hiring.hire(world, Staff.Role.GUARD)
	for i in range(2):
		Hiring.hire(world, Staff.Role.WORKER)


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




func _build_hud_node() -> void:
	_hud = GameHud.new()
	_hud.name = "Hud"
	add_child(_hud)
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
	_hud.on_confirm_build = func() -> void: build_tool.confirm_pending()
	_hud.on_cancel_build = func() -> void: build_tool.cancel_pending()


func _toggle_overlay() -> void:
	_tension_overlay.visible = not _tension_overlay.visible
	if _tension_overlay.visible:
		_tension_overlay.refresh()


func _select_tool(mode: int) -> void:
	tool_mode = mode
	build_tool.cancel_drag()
	zone_tool.cancel_drag()
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
	if not _started or world == null:
		return
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
		"game_over": world.game_over,
		"game_over_reason": world.game_over_reason,
		"contract_breach_days": world.contract.breach_days,
		"contract_breached": world.contract.breached,
		"contract_total_earned": world.contract.total_earned,
		"contract_total_days": world.contract.total_days,
	})


const TRAIT_NAMES := {
	Prisoner.Trait.VOLATILE: "Volatile", Prisoner.Trait.CUNNING: "Cunning",
	Prisoner.Trait.INSTITUTIONALIZED: "Institutionalized", Prisoner.Trait.FRAIL: "Frail",
	Prisoner.Trait.CONNECTED: "Connected", Prisoner.Trait.PENITENT: "Penitent",
}
