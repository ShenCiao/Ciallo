@tool
extends EditorPlugin

## Autoloads to strip from every export (debug and release).
## Add more names here if needed.
const STRIP_AUTOLOADS: Array[String] = [
	"_fennara_game_capture",
]
const RUNSETTINGS_PATH := "res://.runsettings"
const LOCAL_RUNSETTINGS_PATH := "res://.runsettings.local"
const LOCAL_GODOT_BIN_MARKER := "        <!-- LOCAL_GODOT_BIN -->"

var _export_plugin: _StripAutoloadExportPlugin


func _enter_tree() -> void:
	_write_local_runsettings()
	_export_plugin = _StripAutoloadExportPlugin.new(STRIP_AUTOLOADS)
	add_export_plugin(_export_plugin)


func _exit_tree() -> void:
	remove_export_plugin(_export_plugin)
	_export_plugin = null


func _write_local_runsettings() -> void:
	var template := FileAccess.get_file_as_string(RUNSETTINGS_PATH)
	assert(template.contains(LOCAL_GODOT_BIN_MARKER))
	var godot_bin := OS.get_executable_path().xml_escape()
	var environment := (
		"        <EnvironmentVariables>\n"
		+ "            <GODOT_BIN>" + godot_bin + "</GODOT_BIN>\n"
		+ "        </EnvironmentVariables>"
	)
	var contents := template.replace(LOCAL_GODOT_BIN_MARKER, environment)
	var local_path := ProjectSettings.globalize_path(LOCAL_RUNSETTINGS_PATH)
	if FileAccess.file_exists(local_path) and FileAccess.get_file_as_string(local_path) == contents:
		return
	var file := FileAccess.open(local_path, FileAccess.WRITE)
	file.store_string(contents)
	print("[Ciallo] Updated local test settings for ", OS.get_executable_path())


class _StripAutoloadExportPlugin extends EditorExportPlugin:
	var _strip_names: Array[String]
	var _saved: Dictionary = {}

	func _init(names: Array[String]) -> void:
		_strip_names = names

	func _get_name() -> String:
		return "StripDevAutoloads"

	func _export_begin(
		_features: PackedStringArray, _is_debug: bool, _path: String, _flags: int
	) -> void:
		_saved.clear()
		for autoload_name in _strip_names:
			var key := "autoload/" + autoload_name
			if ProjectSettings.has_setting(key):
				_saved[key] = ProjectSettings.get_setting(key)
				ProjectSettings.set_setting(key, "")
				print("[Ciallo] Export: stripped autoload '", autoload_name, "'")

	func _export_end() -> void:
		for key: String in _saved:
			ProjectSettings.set_setting(key, _saved[key])
		_saved.clear()
