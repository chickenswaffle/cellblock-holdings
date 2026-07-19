class_name TensionOverlay3D
extends MeshInstance3D
## The tension overlay — per the design doc, "the most important screen in
## the game", and it ships alongside the model rather than after it so the
## player can always see *why* a block is about to go up.
##
## Same one-texel-per-tile trick as the terrain and zone tint, except this
## texture is rebuilt from live sim state on an interval rather than on
## grid_version: tension changes every sim minute without the grid changing
## at all. Linear filtering makes the heat bleed softly across room
## boundaries, which reads as pressure rather than as a choropleth map.

## Cool → warm ramp. Deliberately not a rainbow: the eye should read "worse"
## monotonically, and red should mean exactly one thing.
const CALM := Color(0.16, 0.45, 0.30)
const WARM := Color(0.85, 0.68, 0.15)
const HOT := Color(0.78, 0.13, 0.10)
const OVERLAY_ALPHA := 0.55
## Tension below this is drawn as fully transparent, so a healthy prison
## shows a clean map instead of a wash of green.
const VISIBLE_FLOOR := 0.05

## Seconds of real time between texture rebuilds. The field moves on sim
## minutes, so refreshing every frame would be wasted work.
const REFRESH_INTERVAL := 0.25

var world: SimWorld
var _material: StandardMaterial3D
var _since_refresh := 0.0


func setup(p_world: SimWorld) -> void:
	world = p_world

	var plane := PlaneMesh.new()
	plane.size = Vector2(world.grid.width, world.grid.height)
	mesh = plane
	# Above the zone tint (0.02) so the two don't z-fight when both are on.
	position = Vector3(world.grid.width / 2.0, 0.04, world.grid.height / 2.0)

	_material = StandardMaterial3D.new()
	_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_LINEAR
	# Draw over the walls rather than under them. A cell is only ~3 tiles
	# deep and its walls stand 2.4 units tall, so at this camera pitch the
	# north wall hides nearly the whole floor — leaving the overlay depth
	# tested made it invisible for exactly the rooms the player most needs to
	# read. Disabling the depth test (rather than lifting the plane) keeps it
	# pinned to the ground, so the heat still lines up with the rooms instead
	# of parallaxing off them.
	_material.no_depth_test = true
	_material.render_priority = 1
	material_override = _material

	visible = false
	refresh()


func _process(delta: float) -> void:
	if not visible or world == null:
		return
	_since_refresh += delta
	if _since_refresh >= REFRESH_INTERVAL:
		_since_refresh = 0.0
		refresh()


func refresh() -> void:
	if world == null:
		return
	var img := Image.create(world.grid.width, world.grid.height, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))

	for room in world.rooms:
		if not room.sealed:
			continue
		var value := world.tension.value_for(room)
		if value < VISIBLE_FLOOR:
			continue
		var color := heat_color(value)
		for t in room.tiles:
			img.set_pixel(t.x, t.y, color)

	_material.albedo_texture = ImageTexture.create_from_image(img)


## Green → amber → red, with alpha rising alongside so a hot room is both
## redder and more opaque than a merely warm one.
static func heat_color(value: float) -> Color:
	var v := clampf(value, 0.0, 1.0)
	var rgb := CALM.lerp(WARM, minf(v * 2.0, 1.0)) if v < 0.5 else WARM.lerp(HOT, (v - 0.5) * 2.0)
	rgb.a = OVERLAY_ALPHA * (0.35 + 0.65 * v)
	return rgb
