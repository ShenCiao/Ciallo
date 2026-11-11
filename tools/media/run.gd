extends RefCounted


func run(ctx: Variant) -> void:
	# A live Godot process caches previously loaded scripts. Reload the recipe and
	# its helpers explicitly so staging edits takes effect without restarting.
	var recipe: GDScript = ResourceLoader.load(
		"res://.fennara/tmp/media/settings_pen_pressure.gd",
		"GDScript", ResourceLoader.CACHE_MODE_IGNORE_DEEP)
	await recipe.new().run(ctx)
