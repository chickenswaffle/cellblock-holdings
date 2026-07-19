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

## Hiring works in any tool mode (the digit keys are already spoken for by
## speed and sub-type selection). Shift+key fires the newest of that role.
const HIRE_KEYS := {
	KEY_Z: Staff.Role.GUARD,
	KEY_X: Staff.Role.WORKER,
	KEY_C: Staff.Role.SUPPORT,
}
const ROLE_NAMES := ["guard", "worker", "support"]

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
var _drag_preview: MeshInstance3D
var _hud_label: Label
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

	# Explicit type, not `:=` — Dictionary.get() infers Variant, which this
	# project treats as a hard parse error.
	var role: int = HIRE_KEYS.get(key.keycode, -1)
	if role >= 0:
		if key.shift_pressed:
			_fire(role)
		else:
			Hiring.hire(world, role)
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

	lines.append(_staff_line())
	lines.append("[Z/X/C] hire guard/worker/support  [Shift+] fire   payroll $%d/day%s" % [
		Payroll.daily_cost(world.staff),
		"   ⚠ PAYROLL MISSED" if _payroll_is_in_arrears() else "",
	])
	if not world.construction_queue.orders.is_empty():
		lines.append("build queue: %d order(s)%s" % [
			world.construction_queue.orders.size(),
			"   ⚠ no workers on shift" if world.on_duty_count(Staff.Role.WORKER) == 0 else "",
		])

	lines.append("prisoners: %d   [click] inspect nearest" % world.prisoners.size())
	if tool_mode == ToolMode.NONE:
		var p := world.prisoner_at(_inspected_id)
		if p != null:
			lines.append("— %s, age %d, %d days left%s" % [p.pname, p.age, p.sentence_days, _trait_suffix(p.traits)])
			lines.append("  %s   hunger %.0f%% sleep %.0f%% hygiene %.0f%% social %.0f%% rec %.0f%%" % [
				_action_desc(p),
				p.needs.get_value(Needs.Kind.HUNGER) * 100.0,
				p.needs.get_value(Needs.Kind.SLEEP) * 100.0,
				p.needs.get_value(Needs.Kind.HYGIENE) * 100.0,
				p.needs.get_value(Needs.Kind.SOCIAL) * 100.0,
				p.needs.get_value(Needs.Kind.RECREATION) * 100.0,
			])

	_hud_label.text = "\n".join(lines)


## "on duty / on the books" per role — the gap between those two numbers is
## what a player staffing only the day shift needs to see.
func _staff_line() -> String:
	var parts := []
	for role in [Staff.Role.GUARD, Staff.Role.WORKER, Staff.Role.SUPPORT]:
		parts.append("%s %d/%d" % [ROLE_NAMES[role], world.on_duty_count(role), world.staff_count(role)])
	return "staff on duty: %s   avg fatigue %.0f%%" % [", ".join(parts), _average_fatigue() * 100.0]


func _average_fatigue() -> float:
	if world.staff.is_empty():
		return 0.0
	var total := 0.0
	for s in world.staff:
		total += s.fatigue
	return total / float(world.staff.size())


func _payroll_is_in_arrears() -> bool:
	for s in world.staff:
		if s.unpaid_days > 0:
			return true
	return false


const TRAIT_NAMES := {
	Prisoner.Trait.VOLATILE: "Volatile", Prisoner.Trait.CUNNING: "Cunning",
	Prisoner.Trait.INSTITUTIONALIZED: "Institutionalized", Prisoner.Trait.FRAIL: "Frail",
	Prisoner.Trait.CONNECTED: "Connected", Prisoner.Trait.PENITENT: "Penitent",
}
const NEED_NAMES := {
	Needs.Kind.HUNGER: "eating", Needs.Kind.SLEEP: "sleeping", Needs.Kind.HYGIENE: "washing up",
	Needs.Kind.SOCIAL: "socializing", Needs.Kind.RECREATION: "recreating",
	Needs.Kind.SAFETY: "staying safe", Needs.Kind.DIGNITY: "keeping dignity",
}


static func _trait_suffix(traits: int) -> String:
	var names := []
	for t in TRAIT_NAMES:
		if (traits & t) != 0:
			names.append(TRAIT_NAMES[t])
	return "" if names.is_empty() else " [%s]" % ", ".join(names)


static func _action_desc(p: Prisoner) -> String:
	match p.action_state:
		Prisoner.ActionState.TRAVELING:
			return "walking to %s" % NEED_NAMES.get(p.action_need, "somewhere")
		Prisoner.ActionState.PERFORMING:
			return NEED_NAMES.get(p.action_need, "idle")
		_:
			return "idle"
