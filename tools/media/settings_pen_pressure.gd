extends RefCounted

const MENU_PATH: String = "MainGui/MenuBarContainer/Margin/HBoxContainer/MainMenuBar"


func run(ctx: Variant) -> void:
	var capture: GDScript = ResourceLoader.load("res://.fennara/tmp/media/desktop_capture.gd", "GDScript", ResourceLoader.CACHE_MODE_IGNORE)
	var root: Node = ctx.get_scene_root()
	assert(TranslationServer.get_locale().begins_with("en"), "Use the English factory profile")
	var main: Window = root.get_window()
	var was_on_top: bool = main.always_on_top
	var bar: MenuBar = root.get_node(MENU_PATH)
	var index: int = -1
	for i: int in range(bar.get_menu_count()):
		if bar.get_menu_title(i) == "Settings":
			index = i
	assert(index >= 0, "Settings menu must exist")
	var button: Rect2 = capture.menu_button_rect(bar, index)
	bar.get_menu_popup(index).hide()
	main.always_on_top = true
	main.grab_focus()
	await ctx.wait(0.2)
	await ctx.click_at(button.get_center())
	await ctx.wait(0.2)
	var popup: PopupMenu = ctx.get_scene_root().get_node(MENU_PATH).get_menu_popup(index)
	if not popup.visible:
		ctx.get_scene_root().get_window().always_on_top = was_on_top
		ctx.error("Settings menu did not open after clicking its measured button")
		return
	var target: int = popup.get_item_index(101)
	assert(popup.get_item_text(target) == "Global Pen Pressure...")
	# This menu has three equal-height, single-line entries without icons/separators.
	for i: int in range(popup.item_count):
		assert(not popup.is_item_separator(i) and popup.get_item_icon(i) == null)
	var panel: StyleBox = popup.get_theme_stylebox("panel")
	var top: float = panel.get_content_margin(SIDE_TOP)
	var bottom: float = panel.get_content_margin(SIDE_BOTTOM)
	var row_height: float = (popup.size.y - top - bottom) / popup.item_count
	var target_rect: Rect2i = Rect2i(popup.position + Vector2i(2, roundi(top + target * row_height)), Vector2i(popup.size.x - 4, roundi(row_height)))
	popup.set_focused_item(target)
	await ctx.wait(0.2)
	var main_now: Window = ctx.get_scene_root().get_window()
	var origin: Vector2i = DisplayServer.window_get_position(main_now.get_window_id())
	var button_screen: Rect2i = Rect2i(button)
	button_screen.position += origin
	var output_stem: String = ProjectSettings.globalize_path("res://../.ciallo/media/output/settings-global-pen-pressure").simplify_path()
	var highlights: Array[Rect2i] = [button_screen, target_rect]
	capture.save(ctx, Rect2i(origin, Vector2i(720, 210)), highlights, output_stem, "Settings → Global Pen Pressure")
	var popup_now: PopupMenu = ctx.get_scene_root().get_node(MENU_PATH).get_menu_popup(index)
	popup_now.hide()
	main_now.always_on_top = was_on_top
	ctx.release_all_inputs()
