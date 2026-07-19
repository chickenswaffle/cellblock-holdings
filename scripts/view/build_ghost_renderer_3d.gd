class_name BuildGhostRenderer3D
extends Node3D
## Translucent preview of exactly what a selection will build, drawn in place
## before anything is paid for.
##
## The area tint alone can't answer "which side of the row does this wall land
## on" — and that's the question a player actually has mid-drag. Ghost walls
## are placed with StructuresRenderer3D.edge_transform(), the same function
## the real renderer uses, so what you see is what you get rather than an
## approximation that can drift out of agreement with the result.

const GHOST_OK := Color(0.45, 0.95, 0.55, 0.45)
const GHOST_BAD := Color(0.95, 0.35, 0.30, 0.45)

var _walls: MultiMeshInstance3D
var _objects: MultiMeshInstance3D
var _material: StandardMaterial3D


func _ready() -> void:
	_material = StandardMaterial3D.new()
	_material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	_material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	_material.albedo_color = GHOST_OK
	# Draw over the existing building so a wall you're adding against one
	# that's already there stays visible instead of z-fighting with it.
	_material.no_depth_test = true
	_material.render_priority = 2

	_walls = _make_batch(BoxMesh.new())
	_objects = _make_batch(BoxMesh.new())
	visible = false


func _make_batch(mesh: Mesh) -> MultiMeshInstance3D:
	var mmi := MultiMeshInstance3D.new()
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mesh
	mm.instance_count = 0
	mmi.multimesh = mm
	mmi.material_override = _material
	add_child(mmi)
	return mmi


## Draw the given orders as ghosts. `affordable` tints the whole batch.
func show_orders(orders: Array[BuildOrder], affordable: bool) -> void:
	if orders.is_empty():
		visible = false
		return
	visible = true
	_material.albedo_color = GHOST_OK if affordable else GHOST_BAD

	var wall_xforms: Array[Transform3D] = []
	var object_xforms: Array[Transform3D] = []
	for o in orders:
		match o.kind:
			BuildOrder.Kind.WALL, BuildOrder.Kind.DOOR:
				wall_xforms.append(StructuresRenderer3D.edge_transform(o.x, o.y, o.wall_flag))
			BuildOrder.Kind.OBJECT:
				object_xforms.append(Transform3D(
					Basis().scaled(Vector3(0.7, 0.5, 0.7)),
					Vector3(o.x + 0.5, 0.25, o.y + 0.5)
				))
			# Floor orders are already conveyed by the area tint; drawing a
			# ghost slab on top of them would just muddy it.
	_apply(_walls, wall_xforms)
	_apply(_objects, object_xforms)


func hide_orders() -> void:
	visible = false


func _apply(mmi: MultiMeshInstance3D, xforms: Array[Transform3D]) -> void:
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = mmi.multimesh.mesh
	mm.instance_count = xforms.size()
	for i in range(xforms.size()):
		mm.set_instance_transform(i, xforms[i])
	mmi.multimesh = mm
