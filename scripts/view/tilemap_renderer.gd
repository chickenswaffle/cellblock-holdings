class_name TilemapRenderer
extends TileMapLayer
## Draws the sim grid's floors with a runtime-generated placeholder tileset.
## Read-only view of the sim; refreshes when grid_version changes.

const TILE_PX := 16

## Base colors per SimTile.FloorType, with a darker parity variant so the
## grid reads visually before real art exists.
const FLOOR_COLORS: Array[Color] = [
	Color(0.42, 0.33, 0.24),  # DIRT
	Color(0.55, 0.55, 0.58),  # CONCRETE
	Color(0.72, 0.72, 0.68),  # TILE
	Color(0.30, 0.48, 0.26),  # GRASS
]

var _grid: SimGrid
var _rendered_version: int = -1


func setup(grid: SimGrid) -> void:
	_grid = grid
	tile_set = _build_placeholder_tileset()
	refresh()


func _process(_delta: float) -> void:
	if _grid != null and _grid.grid_version != _rendered_version:
		refresh()


func refresh() -> void:
	clear()
	for y in range(_grid.height):
		for x in range(_grid.width):
			var t := _grid.tile_at(x, y)
			var parity := (x + y) % 2
			set_cell(Vector2i(x, y), 0, Vector2i(t.floor_type, parity))
	_rendered_version = _grid.grid_version


## Atlas layout: column = floor type, row = parity (0 base, 1 slightly dark).
func _build_placeholder_tileset() -> TileSet:
	var img := Image.create(TILE_PX * FLOOR_COLORS.size(), TILE_PX * 2, false, Image.FORMAT_RGBA8)
	for f in range(FLOOR_COLORS.size()):
		for parity in range(2):
			var c := FLOOR_COLORS[f].darkened(0.08 * parity)
			for py in range(TILE_PX):
				for px in range(TILE_PX):
					var edge := px == 0 or py == 0
					img.set_pixel(f * TILE_PX + px, parity * TILE_PX + py, c.darkened(0.15) if edge else c)
	var tex := ImageTexture.create_from_image(img)

	var atlas := TileSetAtlasSource.new()
	atlas.texture = tex
	atlas.texture_region_size = Vector2i(TILE_PX, TILE_PX)
	for f in range(FLOOR_COLORS.size()):
		for parity in range(2):
			atlas.create_tile(Vector2i(f, parity))

	var ts := TileSet.new()
	ts.tile_size = Vector2i(TILE_PX, TILE_PX)
	ts.add_source(atlas, 0)
	return ts
