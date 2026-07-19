class_name GameHud
extends CanvasLayer
## The whole player-facing UI, built in code like the rest of the view.
##
## Replaces the original wall-of-text label. The rules it's built to:
##   - every action is a button you can click; keyboard shortcuts are shown
##     ON the button rather than memorised from a help line
##   - a control that can't be used right now is visibly disabled and its
##     tooltip says why ("no support staffer on duty"), instead of silently
##     doing nothing
##   - panels that aren't relevant aren't on screen
##
## Bootstrap owns the sim and the tools; this owns layout and input routing,
## and calls back into Bootstrap through the callables set in setup().

const TOOL_LABELS := ["Camera", "Wall", "Door", "Floor", "Object", "Zone"]
const TOOL_KEYS := ["Esc", "1", "2", "3", "4", "5"]
const ROLE_LABELS := ["Guard", "Worker", "Support"]
const SPEEDS := ["1x", "3x", "10x"]

var world: SimWorld

## Set by Bootstrap.
var on_tool_selected: Callable
var on_subtype_selected: Callable
var on_speed_selected: Callable
var on_pause_toggled: Callable
var on_hire: Callable
var on_fire: Callable
var on_resolve: Callable
var on_lockdown: Callable
var on_overlay_toggled: Callable
var on_edge_scroll_toggled: Callable
var on_recenter: Callable
var on_rotate: Callable
var on_confirm_build: Callable
var on_cancel_build: Callable

var _root: Control
var _tool_buttons: Array[Button] = []
var _subtype_bar: VBoxContainer
var _subtype_panel: PanelContainer
var _subtype_buttons: Array[Button] = []
var _status: Label
var _tension_meter: ProgressBar
var _tension_label: Label
var _tension_pct: Label
var _speed_buttons: Array[Button] = []
var _pause_button: Button
var _overlay_button: Button
var _edge_button: Button
var _staff_rows: Dictionary = {}
var _incident_panel: PanelContainer
var _incident_title: Label
var _incident_detail: Label
var _resolve_buttons: Dictionary = {}
var _lockdown_button: Button
var _inspector: PanelContainer
var _inspector_text: Label
var _faction_label: Label
var _hint: Label
var _build_panel: PanelContainer
var _build_text: Label
var _confirm_button: Button
var _cancel_button: Button
var _confirm_row: HBoxContainer

var _contract_label: Label
var _contract_warn: Label
var _first_play := true


func _on_game_started() -> void:
	if _first_play:
		_first_play = false
		_show_onboarding()


func setup(p_world: SimWorld) -> void:
	world = p_world
	layer = 10

	# Clear any previous UI so calling setup() multiple times is safe.
	for c in get_children():
		c.queue_free()

	_root = Control.new()
	_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	# The root is a passthrough: only the actual panels should swallow
	# clicks, or the whole screen would stop reaching the world.
	_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_root)

	_build_top_bar()
	_build_tool_palette()
	_build_staff_panel()
	_build_incident_panel()
	_build_inspector()
	_build_preview_panel()
	_build_hint()


# ------------------------------------------------------------------ layout

func _build_top_bar() -> void:
	var panel := UiTheme.panel()
	_root.add_child(panel)
	panel.anchor_left = 0.0
	panel.anchor_right = 1.0
	panel.anchor_top = 0.0
	panel.anchor_bottom = 0.0
	panel.offset_left = 10
	panel.offset_right = -10
	panel.offset_top = 8
	panel.offset_bottom = 8
	panel.grow_vertical = Control.GROW_DIRECTION_END

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 14)
	panel.add_child(row)

	_pause_button = UiTheme.button("Pause", "Pause / resume  [Space]")
	_pause_button.pressed.connect(func() -> void: on_pause_toggled.call())
	row.add_child(_pause_button)

	for i in range(SPEEDS.size()):
		var b := UiTheme.button(SPEEDS[i], "Simulation speed  [%d]" % (i + 1))
		var index := i
		b.pressed.connect(func() -> void: on_speed_selected.call(index))
		_speed_buttons.append(b)
		row.add_child(b)

	row.add_child(UiTheme.hseparator())
	_status = UiTheme.label("", UiTheme.TEXT, 14)
	row.add_child(_status)

	row.add_child(UiTheme.hseparator())
	_tension_label = UiTheme.label("Tension", UiTheme.TEXT_DIM, 12)
	row.add_child(_tension_label)
	_tension_meter = ProgressBar.new()
	_tension_meter.custom_minimum_size = Vector2(110, 14)
	_tension_meter.max_value = 1.0
	_tension_meter.show_percentage = false
	row.add_child(_tension_meter)
	_tension_pct = UiTheme.label("0%", UiTheme.TEXT, 12)
	_tension_pct.custom_minimum_size = Vector2(38, 0)
	row.add_child(_tension_pct)

	row.add_child(UiTheme.hseparator())
	_contract_label = UiTheme.label("", UiTheme.GOOD, 12)
	row.add_child(_contract_label)
	_contract_warn = UiTheme.label("", UiTheme.WARN, 12)
	row.add_child(_contract_warn)

	var spacer := Control.new()
	spacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	row.add_child(spacer)

	_overlay_button = UiTheme.button("Tension overlay", "Show per-room pressure  [T]")
	_overlay_button.pressed.connect(func() -> void: on_overlay_toggled.call())
	row.add_child(_overlay_button)

	# Plain words rather than rotation glyphs: ⟲/⟳ aren't in the default
	# font and render as dots.
	var rotate_left := UiTheme.button("Turn L", "Rotate the view left  [,]  — or right-drag")
	rotate_left.pressed.connect(func() -> void: on_rotate.call(-45.0))
	row.add_child(rotate_left)

	var rotate_right := UiTheme.button("Turn R", "Rotate the view right  [.]  — or right-drag")
	rotate_right.pressed.connect(func() -> void: on_rotate.call(45.0))
	row.add_child(rotate_right)

	var recenter := UiTheme.button("Recenter", "Jump back to the facility, facing north  [Home]")
	recenter.pressed.connect(func() -> void: on_recenter.call())
	row.add_child(recenter)

	_edge_button = UiTheme.button("Edge scroll", "Pan by pushing the cursor to the screen edge (off by default)")
	_edge_button.pressed.connect(func() -> void: on_edge_scroll_toggled.call())
	row.add_child(_edge_button)


func _build_tool_palette() -> void:
	var panel := UiTheme.panel()
	_root.add_child(panel)
	# MINSIZE so the panel hugs its contents. With plain offsets it either
	# clips its own rows or collapses to zero width — both of which shipped
	# in the first pass of this HUD.
	UiTheme.pin(panel, -1, 0)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", UiTheme.GAP)
	panel.add_child(col)
	col.add_child(UiTheme.label("BUILD", UiTheme.TEXT_DIM, 11))

	for i in range(TOOL_LABELS.size()):
		var b := UiTheme.button("%s  [%s]" % [TOOL_LABELS[i], TOOL_KEYS[i]], _tool_tooltip(i))
		b.alignment = HORIZONTAL_ALIGNMENT_LEFT
		b.custom_minimum_size = Vector2(132, 0)
		var index := i
		b.pressed.connect(func() -> void: on_tool_selected.call(index))
		_tool_buttons.append(b)
		col.add_child(b)

	# Sub-type palette, shown beside the tools only when one has options.
	_subtype_panel = UiTheme.panel()
	_subtype_panel.visible = false
	_root.add_child(_subtype_panel)
	UiTheme.pin(_subtype_panel, -1, 0, 158)

	var subtype_col := VBoxContainer.new()
	subtype_col.add_theme_constant_override("separation", 4)
	_subtype_panel.add_child(subtype_col)
	subtype_col.add_child(UiTheme.label("TYPE   [Q/E]", UiTheme.TEXT_DIM, 11))
	_subtype_bar = VBoxContainer.new()
	_subtype_bar.add_theme_constant_override("separation", 4)
	subtype_col.add_child(_subtype_bar)


static func _tool_tooltip(index: int) -> String:
	match index:
		0: return "Click a prisoner to inspect them"
		1: return "Drag to build a wall run — costs worker time"
		2: return "Click a wall edge to fit a door"
		3: return "Drag to lay flooring"
		4: return "Click to place furniture"
		5: return "Click a sealed room to assign what it's for"
		_: return ""


func _build_staff_panel() -> void:
	var panel := UiTheme.panel()
	_root.add_child(panel)
	UiTheme.pin(panel, -1, 1)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 4)
	panel.add_child(col)
	col.add_child(UiTheme.label("STAFF", UiTheme.TEXT_DIM, 11))

	for role in [Staff.Role.GUARD, Staff.Role.WORKER, Staff.Role.SUPPORT]:
		var row := HBoxContainer.new()
		row.add_theme_constant_override("separation", 6)
		var name_label := UiTheme.label(ROLE_LABELS[role], UiTheme.TEXT, 12)
		name_label.custom_minimum_size = Vector2(62, 0)
		row.add_child(name_label)

		var count := UiTheme.label("0/0", UiTheme.TEXT_DIM, 12)
		count.custom_minimum_size = Vector2(46, 0)
		row.add_child(count)

		var hire := UiTheme.button("+", "Hire a %s" % ROLE_LABELS[role].to_lower())
		var fire := UiTheme.button("−", "Dismiss the newest %s" % ROLE_LABELS[role].to_lower())
		var r: int = role
		hire.pressed.connect(func() -> void: on_hire.call(r))
		fire.pressed.connect(func() -> void: on_fire.call(r))
		row.add_child(hire)
		row.add_child(fire)
		col.add_child(row)
		_staff_rows[role] = {"count": count, "hire": hire, "fire": fire}

	_faction_label = UiTheme.label("", UiTheme.TEXT_DIM, 11)
	col.add_child(_faction_label)


func _build_incident_panel() -> void:
	_incident_panel = UiTheme.panel(UiTheme.BG_RAISED)
	_incident_panel.visible = false
	_root.add_child(_incident_panel)
	UiTheme.pin(_incident_panel, 0, 1)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 4)
	_incident_panel.add_child(col)

	_incident_title = UiTheme.label("", UiTheme.BAD, 16)
	col.add_child(_incident_title)
	_incident_detail = UiTheme.label("", UiTheme.TEXT_DIM, 12)
	col.add_child(_incident_detail)

	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", UiTheme.GAP)
	col.add_child(row)

	for spec in [
		["force", "Force  [F]"], ["solitary", "Solitary  [G]"],
		["negotiate", "Negotiate  [N]"], ["separate", "Transfer  [B]"],
		["concede", "Concede  [K]"],
	]:
		var action: String = spec[0]
		var b := UiTheme.button(spec[1])
		b.pressed.connect(func() -> void: on_resolve.call(action))
		_resolve_buttons[action] = b
		row.add_child(b)

	_lockdown_button = UiTheme.button("Lockdown  [L]", "Confine everyone. Ends the spread, costs goodwill every minute.")
	_lockdown_button.pressed.connect(func() -> void: on_lockdown.call())
	row.add_child(_lockdown_button)


func _build_inspector() -> void:
	_inspector = UiTheme.panel()
	_inspector.custom_minimum_size = Vector2(230, 0)
	_inspector.visible = false
	_root.add_child(_inspector)
	UiTheme.pin(_inspector, 1, -1, 10, 62)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 4)
	_inspector.add_child(col)
	col.add_child(UiTheme.label("INMATE", UiTheme.TEXT_DIM, 11))
	_inspector_text = UiTheme.label("", UiTheme.TEXT, 12)
	col.add_child(_inspector_text)


## Live readout for whatever area is being dragged out: how much of it there
## is, what it costs, and how long the crew will take. Sits just above the
## hint so it never covers the selection itself.
func _build_preview_panel() -> void:
	_build_panel = UiTheme.panel(UiTheme.BG_RAISED)
	_build_panel.visible = false
	_root.add_child(_build_panel)

	var col := VBoxContainer.new()
	col.add_theme_constant_override("separation", 6)
	_build_panel.add_child(col)
	_build_text = UiTheme.label("", UiTheme.TEXT, 13)
	_build_text.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	col.add_child(_build_text)

	# Confirm/cancel only appear once the drag is released and the selection
	# is parked — during the drag itself they'd be unclickable noise.
	_confirm_row = HBoxContainer.new()
	_confirm_row.add_theme_constant_override("separation", UiTheme.GAP)
	_confirm_row.alignment = BoxContainer.ALIGNMENT_CENTER
	_confirm_row.visible = false
	col.add_child(_confirm_row)

	_confirm_button = UiTheme.button("✓  Build", "Queue this work  [Enter]")
	_confirm_button.pressed.connect(func() -> void: on_confirm_build.call())
	_confirm_row.add_child(_confirm_button)

	_cancel_button = UiTheme.button("✗  Cancel", "Discard this selection  [Esc]")
	_cancel_button.pressed.connect(func() -> void: on_cancel_build.call())
	_confirm_row.add_child(_cancel_button)

	UiTheme.pin(_build_panel, 0, 1, 0, 52)


## Called by Bootstrap every frame while a selection is live or parked.
func show_build_preview(info: Dictionary) -> void:
	_build_panel.visible = true
	var awaiting: bool = info.get("awaiting_confirm", false)
	_confirm_row.visible = awaiting

	var count: int = info["count"]
	if count == 0:
		_build_text.text = "Nothing to build here"
		_build_text.add_theme_color_override("font_color", UiTheme.TEXT_DIM)
		_confirm_row.visible = false
		return

	var affordable: bool = info["affordable"]
	_build_text.text = "%d %s   ·   $%s   ·   %s" % [
		count, info["noun"], _thousands(info["cost"]), info["duration"],
	]
	_build_text.add_theme_color_override(
		"font_color", UiTheme.TEXT if affordable else UiTheme.BAD
	)
	if not affordable:
		_build_text.text += "   — can't afford it"
	elif not awaiting:
		_build_text.text += "   ·   release to confirm"
	_confirm_button.disabled = not affordable
	_confirm_button.tooltip_text = (
		"Queue this work  [Enter]" if affordable else "You can't afford this"
	)


func hide_build_preview() -> void:
	_build_panel.visible = false


func _show_onboarding() -> void:
	var overlay := PanelContainer.new()
	overlay.name = "Onboarding"
	overlay.add_theme_stylebox_override("panel", UiTheme.panel_style(Color(0.05, 0.06, 0.08, 0.92)))
	overlay.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.add_child(overlay)

	var col := VBoxContainer.new()
	col.set_anchors_preset(Control.PRESET_CENTER)
	col.add_theme_constant_override("separation", 10)
	col.size = Vector2(420, 0)
	col.add_theme_constant_override("hseparation", 0)
	overlay.add_child(col)

	col.add_child(UiTheme.label("YOUR FIRST DAY", Color(0.85, 0.20, 0.18), 20))
	col.add_child(UiTheme.label("", UiTheme.TEXT_DIM, 6))

	var tips := [
		"You've inherited a leased facility on a state contract.",
		"The contract pays $180/head/day — but demands 60%+ occupancy\nand penalizes excessive incidents.",
		"Build more cells (tool [1]-[5]) to house more inmates.\nHire workers [Staff panel] to build; hire guards to keep order.",
		"Keep the tension meter under 60% or things break.\nPress [T] to see per-room pressure.",
		"Stay profitable. If the contract breaches for 5 days — you're out.",
	]
	for t in tips:
		var l := UiTheme.label(t, Color(0.80, 0.82, 0.88), 13)
		l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		col.add_child(l)

	col.add_child(UiTheme.label("", UiTheme.TEXT_DIM, 10))
	var got_it := UiTheme.button("GOT IT", "Dismiss this message")
	got_it.custom_minimum_size = Vector2(200, 36)
	got_it.pressed.connect(func() -> void: overlay.queue_free())
	col.add_child(got_it)


func _build_hint() -> void:
	var panel := UiTheme.panel()
	_root.add_child(panel)
	_hint = UiTheme.label(
		"WASD / middle-drag pan · wheel zoom · right-drag or , . rotate · PgUp/PgDn tilt · Home recenter",
		UiTheme.TEXT_DIM, 11
	)
	panel.add_child(_hint)
	UiTheme.pin(panel, 1, 1)


# ------------------------------------------------------------------ update

func refresh(state: Dictionary) -> void:
	if world == null:
		return
	_refresh_top_bar(state)
	_refresh_tools(state)
	_refresh_staff()
	_refresh_incident()
	_refresh_inspector(state.get("inspected_id", -1))


func _refresh_top_bar(state: Dictionary) -> void:
	var c := world.clock
	_status.text = "Day %d   %02d:%02d      $%s      %d inmates" % [
		c.day(), c.hour_of_day(), c.minute_of_day() % 60,
		_thousands(world.ledger.balance), world.prisoners.size(),
	]

	var peak := world.tension.peak()
	_tension_meter.value = peak
	_tension_meter.tooltip_text = "Peak room tension %.0f%% (facility average %.0f%%)" % [
		peak * 100.0, world.tension.mean() * 100.0,
	]
	_tension_pct.text = "%.0f%%" % (peak * 100.0)
	_tension_pct.add_theme_color_override("font_color", UiTheme.meter_color(peak))
	var fill := StyleBoxFlat.new()
	fill.bg_color = UiTheme.meter_color(peak)
	fill.set_corner_radius_all(3)
	_tension_meter.add_theme_stylebox_override("fill", fill)
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0.2, 0.21, 0.24)
	bg.set_corner_radius_all(3)
	_tension_meter.add_theme_stylebox_override("background", bg)

	var contract := world.contract
	if contract.breached:
		_contract_label.text = "CONTRACT BREACHED"
		_contract_label.add_theme_color_override("font_color", UiTheme.BAD)
		_contract_warn.text = ""
	elif contract.breach_days > 0:
		var left := Contract.BREACH_DAYS - contract.breach_days
		_contract_label.text = "CONTRACT WARNING — %d day%s left" % [left, "" if left == 1 else "s"]
		_contract_label.add_theme_color_override("font_color", UiTheme.WARN)
		_contract_warn.text = ""
	else:
		_contract_label.text = "CONTRACT OK  ·  $%s/day" % _thousands(Contract.PER_DIEM_PER_HEAD * world.prisoners.size())
		_contract_label.add_theme_color_override("font_color", UiTheme.GOOD)
		_contract_warn.text = "occupancy %d%%+" % int(Contract.MIN_OCCUPANCY_PCT * 100.0)

	var paused: bool = state.get("paused", false)
	_pause_button.text = "Resume" if paused else "Pause"
	UiTheme.set_button_active(_pause_button, paused)
	var speed: int = state.get("speed_index", 0)
	for i in range(_speed_buttons.size()):
		UiTheme.set_button_active(_speed_buttons[i], i == speed and not paused)
	UiTheme.set_button_active(_overlay_button, state.get("overlay_on", false))
	UiTheme.set_button_active(_edge_button, state.get("edge_scroll", false))


func _refresh_tools(state: Dictionary) -> void:
	var active: int = state.get("tool_mode", 0)
	for i in range(_tool_buttons.size()):
		UiTheme.set_button_active(_tool_buttons[i], i == active)

	var options: Array = state.get("subtype_options", [])
	var selected: int = state.get("subtype_index", -1)
	_subtype_panel.visible = not options.is_empty()
	if options.size() != _subtype_buttons.size():
		for b in _subtype_buttons:
			b.queue_free()
		_subtype_buttons.clear()
		for i in range(options.size()):
			var b := UiTheme.button("")
			b.alignment = HORIZONTAL_ALIGNMENT_LEFT
			b.custom_minimum_size = Vector2(150, 0)
			var index := i
			b.pressed.connect(func() -> void: on_subtype_selected.call(index))
			_subtype_buttons.append(b)
			_subtype_bar.add_child(b)
	for i in range(_subtype_buttons.size()):
		_subtype_buttons[i].text = String(options[i]).capitalize()
		UiTheme.set_button_active(_subtype_buttons[i], i == selected)


func _refresh_staff() -> void:
	for role: int in _staff_rows:
		var row: Dictionary = _staff_rows[role]
		var on_duty := world.on_duty_count(role)
		var total := world.staff_count(role)
		var count_label: Label = row["count"]
		count_label.text = "%d/%d" % [on_duty, total]
		count_label.add_theme_color_override(
			"font_color", UiTheme.BAD if total > 0 and on_duty == 0 else UiTheme.TEXT_DIM
		)
		var fee := Staff.hiring_fee(role)
		var hire: Button = row["hire"]
		hire.disabled = world.ledger.balance < fee
		hire.tooltip_text = "Hire a %s — $%d up front, $%d/day" % [
			ROLE_LABELS[role].to_lower(), fee, Staff.SALARY_PER_DAY[role],
		]
		var fire: Button = row["fire"]
		fire.disabled = total == 0

	if world.factions.is_empty():
		_faction_label.text = "payroll $%d/day" % Payroll.daily_cost(world.staff)
		return
	var parts := []
	for f in world.factions:
		parts.append("%s %d" % [f.fname, FactionSystem.members(world, f.id).size()])
	_faction_label.text = "payroll $%d/day\n%s" % [
		Payroll.daily_cost(world.staff), ", ".join(parts),
	]


func _refresh_incident() -> void:
	var worst := world.worst_incident()
	var locked := world.is_locked_down()
	_incident_panel.visible = worst != null or locked
	if not _incident_panel.visible:
		return

	if worst == null:
		_incident_title.text = "LOCKDOWN"
		_incident_title.add_theme_color_override("font_color", UiTheme.WARN)
		_incident_detail.text = "%d minutes remaining — grievance is climbing the whole time." % world.lockdown_minutes
	else:
		var open := IncidentSystem.open_incidents(world).size()
		_incident_title.text = worst.label().to_upper()
		_incident_title.add_theme_color_override(
			"font_color", UiTheme.BAD if worst.is_violent() else UiTheme.WARN
		)
		_incident_detail.text = "%d involved · %d incident%s open%s" % [
			worst.participants.size(), open, "" if open == 1 else "s",
			"  ·  LOCKDOWN %d min" % world.lockdown_minutes if locked else "",
		]

	# Disable what genuinely can't be done, and say why in the tooltip —
	# a button that silently does nothing is worse than one that's greyed out.
	var has_incident := worst != null
	var guards := world.on_duty_count(Staff.Role.GUARD)
	var support := world.on_duty_count(Staff.Role.SUPPORT)
	_set_action(
		"force", has_incident and guards > 0,
		"Guards wade in. Fast, injures people, spikes grievance facility-wide.",
		"No guards on duty."
	)
	_set_action(
		"solitary", has_incident,
		"Ends it now. Spikes grievance, destroys reform, counted against you in oversight.",
		"Nothing to resolve."
	)
	var can_negotiate := has_incident and support > 0 and worst != null and worst.kind < Incident.Kind.FACILITY_RIOT
	var negotiate_why := "No support staffer on duty."
	if worst != null and worst.kind >= Incident.Kind.FACILITY_RIOT:
		negotiate_why = "Too far gone to talk down."
	_set_action(
		"negotiate", can_negotiate,
		"The only option that lowers grievance for real. Needs a support staffer.",
		negotiate_why
	)
	_set_action(
		"separate", has_incident and world.ledger.balance >= IncidentSystem.TRANSFER_COST_PER_HEAD,
		"Ship the participants out — $%d a head." % IncidentSystem.TRANSFER_COST_PER_HEAD,
		"Not enough money to transfer anyone."
	)
	_set_action(
		"concede", has_incident and world.ledger.balance >= IncidentSystem.CONCEDE_COST,
		"Give them what they want — $%d. It works; the board reads the cost line." % IncidentSystem.CONCEDE_COST,
		"Not enough money to concede."
	)
	_lockdown_button.disabled = locked
	UiTheme.set_button_active(_lockdown_button, locked)


func _set_action(action: String, enabled: bool, tooltip: String, blocked_reason: String) -> void:
	var b: Button = _resolve_buttons[action]
	b.disabled = not enabled
	b.tooltip_text = tooltip if enabled else blocked_reason


func _refresh_inspector(inspected_id: int) -> void:
	var p := world.prisoner_at(inspected_id)
	_inspector.visible = p != null
	if p == null:
		return
	var faction := FactionSystem.faction_at(world, p.faction_id)
	_inspector_text.text = "%s, %d\n%s\n\ngrievance %.0f%%\nhunger %.0f%%  sleep %.0f%%\nhygiene %.0f%%  social %.0f%%\n\n%s" % [
		p.pname, p.age,
		faction.fname if faction != null else "unaffiliated",
		p.grievance * 100.0,
		p.needs.get_value(Needs.Kind.HUNGER) * 100.0,
		p.needs.get_value(Needs.Kind.SLEEP) * 100.0,
		p.needs.get_value(Needs.Kind.HYGIENE) * 100.0,
		p.needs.get_value(Needs.Kind.SOCIAL) * 100.0,
		_trait_list(p.traits),
	]


static func _trait_list(traits: int) -> String:
	var names := []
	for t in Bootstrap.TRAIT_NAMES:
		if (traits & t) != 0:
			names.append(Bootstrap.TRAIT_NAMES[t])
	return "" if names.is_empty() else ", ".join(names)


static func _thousands(amount: int) -> String:
	var s := str(absi(amount))
	var out := ""
	for i in range(s.length()):
		if i > 0 and (s.length() - i) % 3 == 0:
			out += ","
		out += s[i]
	return ("-" if amount < 0 else "") + out


## True when the pointer is over any HUD panel — the camera reads this so
## reaching for a button never pans the world.
func pointer_over_ui() -> bool:
	var mouse := _root.get_global_mouse_position()
	for child in _root.get_children():
		var c := child as Control
		if c != null and c.visible and c.get_global_rect().has_point(mouse):
			return true
	return false
