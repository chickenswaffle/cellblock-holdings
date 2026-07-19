class_name TerrainRenderer3D
extends MeshInstance3D
## Ground plane for the whole site: one flat mesh, one draw call. Floor type
## per tile is baked into a small texture and sampled with linear filtering,
## which blends smoothly across tile boundaries "for free" — no per-tile
## geometry needed, and it reads as natural terrain instead of a grid.

const FLOOR_COLORS: Array[Color] = [
	Color(0.40, 0.31, 0.22),  # DIRT
	Color(0.56, 0.56, 0.59),  # CONCRETE
	Color(0.74, 0.74, 0.70),  # TILE
	Color(0.29, 0.44, 0.25),  # GRASS
]

var _grid: SimGrid
var _rendered_version: int = -1
var _material: ShaderMaterial


## How far the surrounding backdrop extends past the playable grid, in tiles.
const BACKDROP_MARGIN := 200.0
## Slightly darker than dirt so the site still reads as the place you own.
const BACKDROP_COLOR := Color(0.32, 0.26, 0.19)


func setup(grid: SimGrid) -> void:
	_grid = grid

	var plane := PlaneMesh.new()
	plane.size = Vector2(grid.width, grid.height)
	mesh = plane
	position = Vector3(grid.width / 2.0, 0.0, grid.height / 2.0)
	_add_backdrop(grid)

	_material = ShaderMaterial.new()
	_material.shader = load("res://assets/shaders/terrain.gdshader")
	material_override = _material

	refresh()


## Ground that extends well past the playable grid, sitting just below it.
## Without this, looking anywhere near the map edge shows the environment
## background — an empty blue void with no horizon and no landmark, which
## reads as the camera having broken rather than as the edge of the site.
## The camera is clamped too; this is the second line of defence, and the one
## that makes the edge look deliberate.
func _add_backdrop(grid: SimGrid) -> void:
	var backdrop := MeshInstance3D.new()
	backdrop.name = "Backdrop"
	var plane := PlaneMesh.new()
	plane.size = Vector2(grid.width + BACKDROP_MARGIN * 2.0, grid.height + BACKDROP_MARGIN * 2.0)
	backdrop.mesh = plane
	# A hair below the real terrain so it never z-fights with it.
	backdrop.position = Vector3(grid.width / 2.0, -0.02, grid.height / 2.0)
	var mat := StandardMaterial3D.new()
	mat.albedo_color = BACKDROP_COLOR
	mat.roughness = 1.0
	backdrop.material_override = mat
	add_child(backdrop)


func _process(_delta: float) -> void:
	if _grid != null and _grid.grid_version != _rendered_version:
		refresh()


func refresh() -> void:
	var img := Image.create(_grid.width, _grid.height, false, Image.FORMAT_RGB8)
	for y in range(_grid.height):
		for x in range(_grid.width):
			img.set_pixel(x, y, FLOOR_COLORS[_grid.tile_at(x, y).floor_type])
	_material.set_shader_parameter("splat_tex", ImageTexture.create_from_image(img))
	_rendered_version = _grid.grid_version
