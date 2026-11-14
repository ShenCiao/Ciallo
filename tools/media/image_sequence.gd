extends RefCounted


# Each frame is a desktop capture after the caller has allowed the UI to render.
# Playback timing is fixed by the frame rate passed to encode-webp.ps1.
static func save_frame(region: Rect2i, directory: String, index: int, highlights: Array[Rect2i] = [], pointer: Vector2i = Vector2i(-1, -1)) -> void:
	var shot: Image = DisplayServer.screen_get_image_rect(region)
	assert(shot != null and not shot.is_empty(), "Desktop capture failed")
	var capture: GDScript = ResourceLoader.load("res://.fennara/tmp/media/desktop_capture.gd", "GDScript", ResourceLoader.CACHE_MODE_IGNORE)
	var scale: Vector2 = Vector2(shot.get_size()) / Vector2(region.size)
	for rect: Rect2i in highlights:
		capture.outline(shot, Rect2i(Vector2(rect.position - region.position) * scale, Vector2(rect.size) * scale), Color("ff685d"), 3)
	# A visible pointer marker, added after capture; desktop images omit the OS cursor.
	if region.has_point(pointer):
		var center: Vector2i = Vector2i(Vector2(pointer - region.position) * scale)
		for y: int in range(-9, 10):
			for x: int in range(-9, 10):
				var p: Vector2i = center + Vector2i(x, y)
				var distance: float = Vector2(x, y).length()
				if distance <= 9 and Rect2i(Vector2i.ZERO, shot.get_size()).has_point(p):
					shot.set_pixelv(p, Color("ff685d") if distance > 5 else Color.WHITE)
	assert(DirAccess.make_dir_recursive_absolute(directory) == OK)
	assert(shot.save_png(directory.path_join("frame_%04d.png" % index)) == OK)
