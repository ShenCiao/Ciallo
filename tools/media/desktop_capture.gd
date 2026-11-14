extends RefCounted


# Rectangles passed to this helper use desktop coordinates, before capture scaling.
static func save(ctx: Variant, region: Rect2i, highlights: Array[Rect2i], output_stem: String, description: String) -> bool:
	var shot: Image = DisplayServer.screen_get_image_rect(region)
	if shot == null or shot.is_empty():
		ctx.error("Desktop region capture failed: %s" % region)
		return false
	var directory_error: Error = DirAccess.make_dir_recursive_absolute(output_stem.get_base_dir())
	if directory_error != OK:
		ctx.error("Cannot create capture output directory: %s" % error_string(directory_error))
		return false
	var raw_path: String = output_stem + ".raw.png"
	if shot.save_png(raw_path) != OK:
		ctx.error("Cannot save raw screenshot: %s" % raw_path)
		return false
	var scale: Vector2 = Vector2(shot.get_size()) / Vector2(region.size)
	var annotated: Image = shot.duplicate()
	var boxes: Array[Dictionary] = []
	var color: Color = Color("ff685d")
	var thickness: int = maxi(2, roundi(3.0 * scale.x))
	for desktop_box: Rect2i in highlights:
		var box: Rect2i = Rect2i(Vector2(desktop_box.position - region.position) * scale, Vector2(desktop_box.size) * scale)
		outline(annotated, box, color, thickness)
		boxes.append({"x": box.position.x, "y": box.position.y, "width": box.size.x, "height": box.size.y})
	var image_path: String = output_stem + ".png"
	if annotated.save_png(image_path) != OK:
		ctx.error("Cannot save annotated screenshot: %s" % image_path)
		return false
	var receipt: Dictionary = {
		"raw": raw_path, "annotated": image_path,
		"region": {"x": region.position.x, "y": region.position.y, "width": region.size.x, "height": region.size.y},
		"image_width": shot.get_width(), "image_height": shot.get_height(),
		"highlights": boxes, "color": color.to_html(false), "thickness": thickness,
		"capture": "DisplayServer.screen_get_image_rect", "cursor": false,
	}
	var manifest: FileAccess = FileAccess.open(output_stem + ".json", FileAccess.WRITE)
	if manifest == null:
		ctx.error("Cannot write screenshot manifest: %s" % FileAccess.get_open_error())
		return false
	manifest.store_string(JSON.stringify(receipt, "\t"))
	manifest.close()
	ctx.log("Screenshot saved", receipt)
	ctx.output(annotated, description)
	return true


static func outline(image: Image, box: Rect2i, color: Color, width: int) -> void:
	image.fill_rect(Rect2i(box.position, Vector2i(box.size.x, width)), color)
	image.fill_rect(Rect2i(box.position.x, box.end.y - width, box.size.x, width), color)
	image.fill_rect(Rect2i(box.position, Vector2i(width, box.size.y)), color)
	image.fill_rect(Rect2i(box.end.x - width, box.position.y, width, box.size.y), color)


# English MenuBar labels without icons; matches Godot's text + normal-style layout.
static func menu_button_rect(bar: MenuBar, index: int) -> Rect2:
	var font: Font = bar.get_theme_font("font")
	var font_size: int = bar.get_theme_font_size("font_size")
	var padding: Vector2 = bar.get_theme_stylebox("normal").get_minimum_size()
	var separation: int = bar.get_theme_constant("h_separation")
	var x: float = 0.0
	for i: int in range(index + 1):
		if bar.is_menu_hidden(i):
			continue
		var size: Vector2 = font.get_string_size(bar.get_menu_title(i), HORIZONTAL_ALIGNMENT_LEFT, -1, font_size) + padding
		if i == index:
			return Rect2(bar.global_position + Vector2(x, 0), size)
		x += size.x + separation
	assert(false, "Menu index must refer to a visible item")
	return Rect2()
