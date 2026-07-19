class_name UiTheme
extends RefCounted
## Shared look for the HUD, built in code like everything else in this
## project. One place for colour and spacing so panels can't drift apart.

const BG := Color(0.09, 0.10, 0.12, 0.92)
const BG_RAISED := Color(0.14, 0.15, 0.18, 0.96)
const BORDER := Color(0.30, 0.33, 0.38, 0.9)
const TEXT := Color(0.90, 0.92, 0.95)
const TEXT_DIM := Color(0.62, 0.66, 0.72)
const ACCENT := Color(0.36, 0.62, 0.95)
const GOOD := Color(0.36, 0.78, 0.48)
const WARN := Color(0.95, 0.72, 0.22)
const BAD := Color(0.91, 0.31, 0.26)

const PAD := 8
const GAP := 6
const RADIUS := 5


## Panel background with a subtle border — the base for every HUD surface.
static func panel_style(bg: Color = BG) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.border_color = BORDER
	s.set_border_width_all(1)
	s.set_corner_radius_all(RADIUS)
	s.content_margin_left = PAD
	s.content_margin_right = PAD
	s.content_margin_top = PAD
	s.content_margin_bottom = PAD
	return s


static func panel(bg: Color = BG) -> PanelContainer:
	var p := PanelContainer.new()
	p.add_theme_stylebox_override("panel", panel_style(bg))
	return p


static func label(text: String, color: Color = TEXT, size: int = 13) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_color_override("font_color", color)
	l.add_theme_font_size_override("font_size", size)
	return l


## A button styled for the HUD. `accent` marks the active/selected state so
## the player can always see which tool is live without reading text.
static func button(text: String, tooltip: String = "") -> Button:
	var b := Button.new()
	b.text = text
	b.tooltip_text = tooltip
	b.focus_mode = Control.FOCUS_NONE # keep keyboard shortcuts working
	b.add_theme_font_size_override("font_size", 13)
	_style_button(b, false)
	return b


static func set_button_active(b: Button, active: bool) -> void:
	_style_button(b, active)


static func _style_button(b: Button, active: bool) -> void:
	var normal := StyleBoxFlat.new()
	normal.bg_color = ACCENT if active else Color(0.20, 0.22, 0.26, 0.95)
	normal.border_color = ACCENT.lightened(0.2) if active else BORDER
	normal.set_border_width_all(1)
	normal.set_corner_radius_all(4)
	normal.content_margin_left = 9
	normal.content_margin_right = 9
	normal.content_margin_top = 5
	normal.content_margin_bottom = 5

	var hover := normal.duplicate() as StyleBoxFlat
	hover.bg_color = normal.bg_color.lightened(0.12)
	var pressed := normal.duplicate() as StyleBoxFlat
	pressed.bg_color = normal.bg_color.darkened(0.15)
	var disabled := normal.duplicate() as StyleBoxFlat
	disabled.bg_color = Color(0.16, 0.17, 0.19, 0.7)
	disabled.border_color = Color(0.24, 0.26, 0.29, 0.7)

	b.add_theme_stylebox_override("normal", normal)
	b.add_theme_stylebox_override("hover", hover)
	b.add_theme_stylebox_override("pressed", pressed)
	b.add_theme_stylebox_override("disabled", disabled)
	b.add_theme_color_override("font_color", Color.WHITE if active else TEXT)
	b.add_theme_color_override("font_disabled_color", Color(0.5, 0.53, 0.57))


## Colour for a 0..1 pressure reading: green, amber, red.
static func meter_color(value: float) -> Color:
	if value < 0.4:
		return GOOD
	return WARN if value < 0.7 else BAD


## Pin a panel to an edge/corner and let it size itself to its contents,
## growing *away* from that edge.
##
## `h` and `v` are -1 (start), 0 (centre) or 1 (end). Use this rather than
## set_anchors_and_offsets_preset(..., PRESET_MODE_MINSIZE, ...): that snapshots
## the minimum size at the moment it's called, so a panel whose children are
## added afterwards — or which grows at runtime, like the sub-type palette —
## ends up clipped or hanging off the screen. Anchoring to a zero-size rect
## and setting the grow direction lets the container keep sizing itself.
static func pin(c: Control, h: int, v: int, margin_x: float = 10.0, margin_y: float = 10.0) -> void:
	var ax := 0.0 if h < 0 else (0.5 if h == 0 else 1.0)
	var ay := 0.0 if v < 0 else (0.5 if v == 0 else 1.0)
	c.anchor_left = ax
	c.anchor_right = ax
	c.anchor_top = ay
	c.anchor_bottom = ay
	c.offset_left = margin_x if h < 0 else (-margin_x if h > 0 else 0.0)
	c.offset_right = c.offset_left
	c.offset_top = margin_y if v < 0 else (-margin_y if v > 0 else 0.0)
	c.offset_bottom = c.offset_top
	c.grow_horizontal = (
		Control.GROW_DIRECTION_END if h < 0
		else (Control.GROW_DIRECTION_BEGIN if h > 0 else Control.GROW_DIRECTION_BOTH)
	)
	c.grow_vertical = (
		Control.GROW_DIRECTION_END if v < 0
		else (Control.GROW_DIRECTION_BEGIN if v > 0 else Control.GROW_DIRECTION_BOTH)
	)


static func hseparator() -> Control:
	var c := Control.new()
	c.custom_minimum_size = Vector2(GAP, 0)
	return c
