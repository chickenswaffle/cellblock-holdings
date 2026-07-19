class_name StaffRenderer3D
extends MultiMeshInstance3D
## One batched draw call for the whole roster, colour-coded by role via
## per-instance MultiMesh colours (guards navy, workers hi-vis, support
## white) so the player can read staffing at a glance without an overlay.
##
## Off-duty staff are parked on the gate tile by StaffAI and would otherwise
## render as a pile of bodies at the entrance — they're skipped here, so the
## roster you see on the map is the roster actually covering the floor.
## Reads sim state only; never writes it.

const ROLE_COLORS := {
	Staff.Role.GUARD: Color(0.16, 0.24, 0.45),
	Staff.Role.WORKER: Color(0.92, 0.74, 0.13),
	Staff.Role.SUPPORT: Color(0.85, 0.87, 0.9),
}
## Staff read as slightly taller than prisoners at a glance.
const CAPSULE_HEIGHT := 1.7
const CAPSULE_RADIUS := 0.24

var world: SimWorld


func setup(p_world: SimWorld) -> void:
	world = p_world

	var capsule := CapsuleMesh.new()
	capsule.radius = CAPSULE_RADIUS
	capsule.height = CAPSULE_HEIGHT

	var mat := StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.roughness = 0.6
	material_override = mat

	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.use_colors = true
	mm.mesh = capsule
	mm.instance_count = 0
	multimesh = mm


func _process(_delta: float) -> void:
	if world == null:
		return
	var on_duty: Array[Staff] = []
	for s in world.staff:
		if s.state != Staff.State.OFF_DUTY:
			on_duty.append(s)

	if multimesh.instance_count != on_duty.size():
		multimesh.instance_count = on_duty.size()
	for i in range(on_duty.size()):
		var s := on_duty[i]
		multimesh.set_instance_transform(
			i, Transform3D(Basis(), Vector3(s.pos.x, CAPSULE_HEIGHT / 2.0, s.pos.y))
		)
		multimesh.set_instance_color(i, ROLE_COLORS.get(s.role, Color.WHITE))
