class_name SaveManager
extends RefCounted

const SAVE_PATH := "user://cellblock_save.json"
const AUTOSAVE_INTERVAL := 30.0

var _timer := 0.0
var _world: SimWorld
var _last_save_hash := ""


func setup(world: SimWorld) -> void:
	_world = world


func save_exists() -> bool:
	return FileAccess.file_exists(SAVE_PATH)


func save() -> void:
	if _world == null:
		return
	var data := _world.to_dict()
	data["__version"] = 1
	var json := JSON.stringify(data, "\t")
	var f := FileAccess.open(SAVE_PATH, FileAccess.WRITE)
	if f != null:
		f.store_string(json)
		_last_save_hash = json.sha256_text()


func load_save() -> bool:
	if not save_exists():
		return false
	var f := FileAccess.open(SAVE_PATH, FileAccess.READ)
	if f == null:
		return false
	var json := f.get_as_text()
	if json.is_empty():
		return false
	var data: Variant = JSON.parse_string(json)
	if data == null or typeof(data) != TYPE_DICTIONARY:
		return false
	_world.from_dict(data)
	_last_save_hash = json.sha256_text()
	return true


func delete_save() -> void:
	if save_exists():
		DirAccess.remove_absolute(SAVE_PATH)
	_last_save_hash = ""


func tick(delta: float) -> void:
	_timer += delta
	if _timer >= AUTOSAVE_INTERVAL:
		_timer = 0.0
		var data := JSON.stringify(_world.to_dict(), "\t")
		if data.sha256_text() != _last_save_hash:
			save()


func auto_save_path() -> String:
	return SAVE_PATH
