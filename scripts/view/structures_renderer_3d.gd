class_name StructuresRenderer3D
extends Node3D
## Walls, doors, zone tint, and placed objects. Walls/doors/each object type
## are batched into their own MultiMeshInstance3D (one draw call per type,
## regardless of count) and rebuilt whenever grid_version changes. Zone tint
## reuses the terrain's "one texel per tile, GPU-filtered" trick.

const WALL_HEIGHT := 2.4
const WALL_THICKNESS := 0.12
const WALL_COLOR := Color(0.62, 0.6, 0.56)
const DOOR_COLOR := Color(0.5, 0.32, 0.14)

const ZONE_COLORS := {
	ZoneValidator.Kind.CELL: Color(0.3, 0.42, 0.75, 0.45),
	ZoneValidator.Kind.CANTEEN: Color(0.85, 0.55, 0.2, 0.45),
	ZoneValidator.Kind.YARD: Color(0.32, 0.72, 0.32, 0.45),
	ZoneValidator.Kind.WORKSHOP: Color(0.62, 0.32, 0.72, 0.45),
	ZoneValidator.Kind.SOLITARY: Color(0.62, 0.15, 0.15, 0.45),
	ZoneValidator.Kind.MEDICAL: Color(0.65, 0.88, 0.88, 0.45),
	ZoneValidator.Kind.STAFF_ROOM: Color(0.82, 0.8, 0.28, 0.45),
	ZoneValidator.Kind.VISITATION: Color(0.82, 0.42, 0.6, 0.45),
}

var world: SimWorld
var _rendered_version: int = -1

var _walls_mmi: MultiMeshInstance3D
var _doors_mmi: MultiMeshInstance3D
var _zone_plane: MeshInstance3D
var _zone_material: StandardMaterial3D
var _object_mmis: Dictionary = {}
var _unit_box := BoxMesh.new()


func setup(p_world: SimWorld) -> void:
	world = p_world

	_walls_mmi = _make_batch_node("Walls", _unit_box, WALL_COLOR)
	_doors_mmi = _make_batch_node("Doors", _unit_box, DOOR_COLOR)

	for type in ObjectDef.Type.values():
		_object_mmis[type] = _make_batch_node("Objects_%d" % type, _object_mesh(type), _object_color(type))

	_zone_plane = MeshInstance3D.new()
	_zone_plane.name = "ZoneTint"
	var plane := PlaneMesh.new()
	plane.size = Vector2(world.grid.width, world.grid.height)
	_zone_plane.mesh = plane
	_zone_plane.position = Vector3(world.grid.width / 2.0, 0.02, world.grid.height / 2.0)
	_zone_material = StandardMaterial3D.new()
	_zone_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_zone_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_zone_material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	_zone_plane.material_override = _zone_material
	add_child(_zone_plane)

	refresh()


func _make_batch_node(node_name: String, mesh: Mesh, color: Color) -> MultiMeshInstance3D:
	var mmi := MultiMeshInstance3D.new()
	mmi.name = node_name
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.roughness = 0.75
	mmi.material_override = mat
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = 0
	mmi.multimesh = mm
	add_child(mmi)
	return mmi


func _process(_delta: float) -> void:
	if world != null and world.grid.grid_version != _rendered_version:
		refresh()


func refresh() -> void:
	var grid := world.grid
	var wall_xforms: Array[Transform3D] = []
	var door_xforms: Array[Transform3D] = []

	for y in range(grid.height):
		for x in range(grid.width):
			var t := grid.tile_at(x, y)
			if t.has_wall(SimTile.WALL_N):
				(door_xforms if t.has_door(SimTile.WALL_N) else wall_xforms).append(_edge_transform(x, y, SimTile.WALL_N))
			if t.has_wall(SimTile.WALL_W):
				(door_xforms if t.has_door(SimTile.WALL_W) else wall_xforms).append(_edge_transform(x, y, SimTile.WALL_W))
			if x == grid.width - 1 and t.has_wall(SimTile.WALL_E):
				(door_xforms if t.has_door(SimTile.WALL_E) else wall_xforms).append(_edge_transform(x, y, SimTile.WALL_E))
			if y == grid.height - 1 and t.has_wall(SimTile.WALL_S):
				(door_xforms if t.has_door(SimTile.WALL_S) else wall_xforms).append(_edge_transform(x, y, SimTile.WALL_S))

	_apply_transforms(_walls_mmi, wall_xforms)
	_apply_transforms(_doors_mmi, door_xforms)

	var by_type := {}
	for type in ObjectDef.Type.values():
		by_type[type] = []
	for o in grid.objects:
		(by_type[o.object_type] as Array).append(_object_transform(o))
	for type in ObjectDef.Type.values():
		_apply_transforms(_object_mmis[type], by_type[type])

	_refresh_zone_tint()
	_rendered_version = grid.grid_version


func _apply_transforms(mmi: MultiMeshInstance3D, xforms: Array) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mmi.multimesh.mesh
	mm.instance_count = xforms.size()
	for i in range(xforms.size()):
		mm.set_instance_transform(i, xforms[i])
	mmi.multimesh = mm


func _edge_transform(x: int, y: int, flag: int) -> Transform3D:
	var basis: Basis
	var origin: Vector3
	match flag:
		SimTile.WALL_N:
			basis = Basis().scaled(Vector3(1.0, WALL_HEIGHT, WALL_THICKNESS))
			origin = Vector3(x + 0.5, WALL_HEIGHT * 0.5, y)
		SimTile.WALL_S:
			basis = Basis().scaled(Vector3(1.0, WALL_HEIGHT, WALL_THICKNESS))
			origin = Vector3(x + 0.5, WALL_HEIGHT * 0.5, y + 1)
		SimTile.WALL_W:
			basis = Basis().scaled(Vector3(WALL_THICKNESS, WALL_HEIGHT, 1.0))
			origin = Vector3(x, WALL_HEIGHT * 0.5, y + 0.5)
		_:
			basis = Basis().scaled(Vector3(WALL_THICKNESS, WALL_HEIGHT, 1.0))
			origin = Vector3(x + 1, WALL_HEIGHT * 0.5, y + 0.5)
	return Transform3D(basis, origin)


func _refresh_zone_tint() -> void:
	var grid := world.grid
	var img := Image.create(grid.width, grid.height, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))
	for r in world.rooms:
		if r.zone_kind == -1:
			continue
		var color: Color = ZONE_COLORS.get(r.zone_kind, Color.TRANSPARENT)
		for t in r.tiles:
			img.set_pixel(t.x, t.y, color)
	_zone_material.albedo_texture = ImageTexture.create_from_image(img)


func _object_transform(o: PlacedObject) -> Transform3D:
	return Transform3D(Basis(), Vector3(o.x + 0.5, _object_y_center(o.object_type), o.y + 0.5))


static func _object_mesh(object_type: int) -> Mesh:
	match object_type:
		ObjectDef.Type.TOILET:
			var c := CylinderMesh.new()
			c.top_radius = 0.18
			c.bottom_radius = 0.22
			c.height = 0.4
			return c
		ObjectDef.Type.CCTV:
			var c := CylinderMesh.new()
			c.top_radius = 0.02
			c.bottom_radius = 0.12
			c.height = 0.25
			return c
		ObjectDef.Type.BED:
			var b := BoxMesh.new()
			b.size = Vector3(0.8, 0.35, 0.95)
			return b
		ObjectDef.Type.TABLE:
			var b := BoxMesh.new()
			b.size = Vector3(0.7, 0.4, 0.7)
			return b
		ObjectDef.Type.BENCH:
			var b := BoxMesh.new()
			b.size = Vector3(0.7, 0.25, 0.3)
			return b
		ObjectDef.Type.PHONE:
			var b := BoxMesh.new()
			b.size = Vector3(0.15, 0.25, 0.08)
			return b
		ObjectDef.Type.WEIGHT_BENCH:
			var b := BoxMesh.new()
			b.size = Vector3(0.9, 0.3, 0.4)
			return b
		ObjectDef.Type.SEWING_STATION:
			var b := BoxMesh.new()
			b.size = Vector3(0.6, 0.5, 0.6)
			return b
		_: # METAL_DETECTOR
			var b := BoxMesh.new()
			b.size = Vector3(0.5, 2.0, 0.15)
			return b


static func _object_color(object_type: int) -> Color:
	match object_type:
		ObjectDef.Type.BED: return Color(0.55, 0.35, 0.75)
		ObjectDef.Type.TOILET: return Color(0.88, 0.88, 0.92)
		ObjectDef.Type.TABLE: return Color(0.58, 0.4, 0.24)
		ObjectDef.Type.BENCH: return Color(0.42, 0.28, 0.16)
		ObjectDef.Type.PHONE: return Color(0.15, 0.15, 0.17)
		ObjectDef.Type.WEIGHT_BENCH: return Color(0.28, 0.28, 0.32)
		ObjectDef.Type.SEWING_STATION: return Color(0.78, 0.62, 0.18)
		ObjectDef.Type.CCTV: return Color(0.1, 0.55, 0.6)
		_: return Color(0.65, 0.65, 0.68) # METAL_DETECTOR


static func _object_y_center(object_type: int) -> float:
	match object_type:
		ObjectDef.Type.PHONE: return 1.2
		ObjectDef.Type.CCTV: return 2.1
		ObjectDef.Type.METAL_DETECTOR: return 1.0
		ObjectDef.Type.TOILET: return 0.2
		ObjectDef.Type.BED: return 0.175
		ObjectDef.Type.TABLE: return 0.2
		ObjectDef.Type.BENCH: return 0.125
		ObjectDef.Type.WEIGHT_BENCH: return 0.15
		_: return 0.25 # SEWING_STATION
