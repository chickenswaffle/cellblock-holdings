extends GutTest
## Enforces the architecture's core rule: everything under scripts/sim/ is
## pure GDScript — no Nodes, no engine RNG, no signals, no scene tree.
## If this test fails, a sim file grew an engine dependency. Fix the file,
## not the test.

const SIM_DIR := "res://scripts/sim"

## token -> reason. Tokens are matched per line, ignoring comment lines.
const BANNED := {
	"extends Node": "sim classes must extend RefCounted",
	"get_tree()": "no scene tree access in sim",
	"randf(": "use SimWorld.rng",
	"randi(": "use SimWorld.rng",
	"randfn(": "use SimWorld.rng",
	"randomize(": "use SimWorld.rng",
	"RandomNumberGenerator": "use SimRng",
	"signal ": "use SimEventBus, signals need Nodes",
	".emit_signal(": "use SimEventBus",
	"Engine.": "no engine access in sim",
	"_process(": "sim is ticked by the view, never by frame callbacks",
	"_physics_process(": "sim is ticked by the view, never by frame callbacks",
	"Input.": "sim never reads input",
	"OS.": "no OS access in sim",
	"Time.": "sim time comes from SimClock, never wall time",
}


func test_sim_layer_is_pure() -> void:
	var files := _collect_gd_files(SIM_DIR)
	assert_gt(files.size(), 0, "no sim files found — wrong path?")
	var violations: Array[String] = []
	for path in files:
		var f := FileAccess.open(path, FileAccess.READ)
		assert_not_null(f, "could not open %s" % path)
		var line_no := 0
		while not f.eof_reached():
			var line := f.get_line()
			line_no += 1
			var code := line.split("#")[0]
			for token: String in BANNED:
				if token in code:
					violations.append("%s:%d uses '%s' (%s)" % [path, line_no, token, BANNED[token]])
	assert_eq(violations.size(), 0, "sim purity violations:\n" + "\n".join(violations))


func _collect_gd_files(dir_path: String) -> Array[String]:
	var out: Array[String] = []
	var dir := DirAccess.open(dir_path)
	if dir == null:
		return out
	dir.list_dir_begin()
	var entry := dir.get_next()
	while entry != "":
		var full := dir_path + "/" + entry
		if dir.current_is_dir():
			if not entry.begins_with("."):
				out.append_array(_collect_gd_files(full))
		elif entry.ends_with(".gd"):
			out.append(full)
		entry = dir.get_next()
	dir.list_dir_end()
	return out
