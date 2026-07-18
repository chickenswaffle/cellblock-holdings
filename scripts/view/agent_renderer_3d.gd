class_name AgentRenderer3D
extends MultiMeshInstance3D
## One batched draw call for every prisoner. Positions are updated every
## frame straight from sim state (already smoothly interpolated tick-by-tick
## by Prisoner.step_along_path) — this node only ever reads, never writes.

var world: SimWorld
var _last_count: int = -1


func setup(p_world: SimWorld) -> void:
	world = p_world

	var capsule := CapsuleMesh.new()
	capsule.radius = 0.22
	capsule.height = 1.5
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.82, 0.45, 0.16)
	mat.roughness = 0.7
	material_override = mat

	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = capsule
	mm.instance_count = 0
	multimesh = mm


func _process(_delta: float) -> void:
	if world == null:
		return
	if world.prisoners.size() != _last_count:
		_last_count = world.prisoners.size()
		multimesh.instance_count = _last_count
	for i in range(_last_count):
		var p := world.prisoners[i]
		multimesh.set_instance_transform(i, Transform3D(Basis(), Vector3(p.pos.x, 0.75, p.pos.y)))
