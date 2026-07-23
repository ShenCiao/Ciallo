@tool
extends EditorPlugin

## Autoloads to strip from every export (debug and release).
## Add more names here if needed.
const STRIP_AUTOLOADS: Array[String] = [
	"_fennara_game_capture",
]

var _export_plugin: _StripAutoloadExportPlugin


func _enter_tree() -> void:
	_export_plugin = _StripAutoloadExportPlugin.new(STRIP_AUTOLOADS)
	add_export_plugin(_export_plugin)


func _exit_tree() -> void:
	remove_export_plugin(_export_plugin)
	_export_plugin = null


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
