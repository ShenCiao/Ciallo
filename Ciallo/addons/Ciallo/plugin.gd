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
var _ffmpeg_export_plugin: _FFmpegExportPlugin


func _enter_tree() -> void:
	_write_local_runsettings()
	_export_plugin = _StripAutoloadExportPlugin.new(STRIP_AUTOLOADS)
	add_export_plugin(_export_plugin)
	_ffmpeg_export_plugin = _FFmpegExportPlugin.new()
	add_export_plugin(_ffmpeg_export_plugin)


func _exit_tree() -> void:
	remove_export_plugin(_ffmpeg_export_plugin)
	_ffmpeg_export_plugin = null
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
				ProjectSettings.set_setting(key, null)
				print("[Ciallo] Export: stripped autoload '", autoload_name, "'")

	func _export_end() -> void:
		for key: String in _saved:
			ProjectSettings.set_setting(key, _saved[key])
		_saved.clear()


class _FFmpegExportPlugin extends EditorExportPlugin:
	func _get_name() -> String:
		return "CialloFFmpeg"

	func _export_begin(
		features: PackedStringArray, _is_debug: bool, _path: String, _flags: int
	) -> void:
		var rid: String
		var executable: String = "ffmpeg"
		var target: String = "ffmpeg"
		if features.has("windows") and features.has("x86_64"):
			rid = "win-x64"
			executable = "ffmpeg.exe"
		elif features.has("linux") and features.has("x86_64"):
			rid = "linux-x64"
		elif features.has("macos") and features.has("arm64"):
			rid = "osx-arm64"
			target = "Contents/Helpers/ffmpeg"
		else:
			get_export_platform().add_message(EditorExportPlatform.EXPORT_MESSAGE_ERROR,
				"FFmpeg", "No bundled FFmpeg for the selected platform and architecture.")
			return
		var source: String = "res://ExternalData/ffmpeg/" + rid + "/"
		if not FileAccess.file_exists(source + executable):
			get_export_platform().add_message(EditorExportPlatform.EXPORT_MESSAGE_ERROR,
				"FFmpeg", "FFmpeg binary is missing. Run ./engine.sh ffmpeg " + rid + " before exporting.")
			return
		# Native executables must remain outside the PCK. macOS's exporter also
		# signs this helper before signing and notarizing the enclosing app.
		add_shared_object(source + executable, PackedStringArray(), target)
		for filename: String in ["NOTICE.txt", "LICENSE.txt"]:
			add_file("res://ExternalData/ffmpeg/" + filename,
				FileAccess.get_file_as_bytes(source + filename), false)
