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


func setup(grid: SimGrid) -> void:
	_grid = grid

	var plane := PlaneMesh.new()
	plane.size = Vector2(grid.width, grid.height)
	mesh = plane
	position = Vector3(grid.width / 2.0, 0.0, grid.height / 2.0)

	_material = ShaderMaterial.new()
	_material.shader = load("res://assets/shaders/terrain.gdshader")
	material_override = _material

	refresh()


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
