extends Node
## Entry point: title menu, map select, options, and run lifecycle.

const BLOOD := Color(0.64, 0.11, 0.17)
const FROST := Color(0.70, 0.88, 0.95)

var _menu: CanvasLayer
var _run: Run
var _hero_sprite: TextureRect


func _ready() -> void:
	Sfx.start_music()
	if OS.get_environment("FROSTVAMP_AUTOTEST") == "1":
		# CI smoke test: boot straight into a run so gameplay code executes headless.
		_start_run(0)
		return
	_show_menu()


func _show_menu() -> void:
	_menu = CanvasLayer.new()
	add_child(_menu)
	_build_title()


func _clear_menu_page() -> void:
	for c in _menu.get_children():
		c.queue_free()


func _build_title() -> void:
	_clear_menu_page()
	var root := _page_root()
	var box := VBoxContainer.new()
	box.set_anchors_preset(Control.PRESET_CENTER)
	box.position = Vector2(-240, -240)
	box.custom_minimum_size = Vector2(480, 480)
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 14)
	root.add_child(box)
	var title := _label("FROSTVAMP", 64, BLOOD.lightened(0.25))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var sub := _label("Freeze your blood. Arm the night.", 16, FROST)
	sub.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(sub)
	# Animated hero preview.
	_hero_sprite = TextureRect.new()
	_hero_sprite.texture = Art.tex("player")
	_hero_sprite.custom_minimum_size = Vector2(128, 128)
	_hero_sprite.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	_hero_sprite.pivot_offset = Vector2(64, 64)
	var hero_center := CenterContainer.new()
	hero_center.add_child(_hero_sprite)
	box.add_child(hero_center)
	var tw := _hero_sprite.create_tween().set_loops()
	tw.tween_property(_hero_sprite, "scale", Vector2(1.06, 0.94), 0.9).set_trans(Tween.TRANS_SINE)
	tw.tween_property(_hero_sprite, "scale", Vector2(0.96, 1.04), 0.9).set_trans(Tween.TRANS_SINE)
	var hero_name := _label("VLAD FROSTBLOOD — the Frozen-Blood Vampire", 13, Color(0.8, 0.82, 0.9))
	hero_name.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(hero_name)
	box.add_child(_button("PLAY", _build_map_select))
	box.add_child(_button("OPTIONS", _build_options))
	box.add_child(_button("QUIT", func() -> void: get_tree().quit()))
	var gold := _label("Gold: %d" % G.gold, 14, Color(0.92, 0.8, 0.35))
	gold.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(gold)


func _build_map_select() -> void:
	_clear_menu_page()
	var root := _page_root()
	var box := VBoxContainer.new()
	box.set_anchors_preset(Control.PRESET_CENTER)
	box.position = Vector2(-390, -260)
	box.custom_minimum_size = Vector2(780, 520)
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 16)
	root.add_child(box)
	var title := _label("CHOOSE YOUR HUNTING GROUND", 30, FROST)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var grid := GridContainer.new()
	grid.columns = 3
	grid.add_theme_constant_override("h_separation", 14)
	grid.add_theme_constant_override("v_separation", 14)
	box.add_child(grid)
	for i in range(Data.MAPS.size()):
		grid.add_child(_map_card(i))
	var back := _button("BACK", _build_title)
	var bc := CenterContainer.new()
	bc.add_child(back)
	box.add_child(bc)


func _map_card(i: int) -> Button:
	var m: Dictionary = Data.MAPS[i]
	var locked: bool = i >= G.unlocked_maps
	var b := Button.new()
	b.custom_minimum_size = Vector2(240, 130)
	b.disabled = locked
	var bg: Color = (m["ground"][1] as Color).lightened(0.05)
	b.add_theme_stylebox_override("normal", _style(bg, 8, (m["accent"] as Color).darkened(0.3)))
	b.add_theme_stylebox_override("hover", _style(bg.lightened(0.08), 8, m["accent"]))
	b.add_theme_stylebox_override("pressed", _style(bg.darkened(0.1), 8, m["accent"]))
	b.add_theme_stylebox_override("disabled", _style(Color(0.08, 0.08, 0.09), 8, Color(0.2, 0.2, 0.22)))
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.alignment = BoxContainer.ALIGNMENT_CENTER
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var name_l := _label("???" if locked else str(m["name"]), 18, Color(0.5, 0.5, 0.55) if locked else Color.WHITE)
	name_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(name_l)
	var best: float = G.best_time.get(m["id"], 0.0)
	var info := "Locked — win the previous map" if locked else ("Best: %02d:%02d" % [int(best) / 60, int(best) % 60] if best > 0.0 else "Unexplored")
	var info_l := _label(info, 11, Color(0.6, 0.62, 0.7))
	info_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	v.add_child(info_l)
	if not locked:
		var foes: Array = m["enemies"]
		var row := HBoxContainer.new()
		row.alignment = BoxContainer.ALIGNMENT_CENTER
		row.mouse_filter = Control.MOUSE_FILTER_IGNORE
		for eid in foes:
			var icon := TextureRect.new()
			icon.texture = Art.tex(eid)
			icon.custom_minimum_size = Vector2(32, 32)
			icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
			row.add_child(icon)
		v.add_child(row)
	b.add_child(v)
	if not locked:
		b.pressed.connect(func() -> void:
			Sfx.play("click")
			_start_run(i))
	return b


func _build_options() -> void:
	_clear_menu_page()
	var root := _page_root()
	var box := VBoxContainer.new()
	box.set_anchors_preset(Control.PRESET_CENTER)
	box.position = Vector2(-200, -160)
	box.custom_minimum_size = Vector2(400, 320)
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_theme_constant_override("separation", 14)
	root.add_child(box)
	var title := _label("OPTIONS", 30, FROST)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	box.add_child(_label("SFX Volume", 14, Color.WHITE))
	var sfx := HSlider.new()
	sfx.min_value = 0.0
	sfx.max_value = 1.0
	sfx.step = 0.05
	sfx.value = Sfx.sfx_volume
	sfx.custom_minimum_size = Vector2(360, 24)
	sfx.value_changed.connect(func(v: float) -> void:
		Sfx.set_volumes(v, Sfx.music_volume)
		Sfx.play("hit"))
	box.add_child(sfx)
	box.add_child(_label("Music Volume", 14, Color.WHITE))
	var mus := HSlider.new()
	mus.min_value = 0.0
	mus.max_value = 1.0
	mus.step = 0.05
	mus.value = Sfx.music_volume
	mus.custom_minimum_size = Vector2(360, 24)
	mus.value_changed.connect(func(v: float) -> void: Sfx.set_volumes(Sfx.sfx_volume, v))
	box.add_child(mus)
	var fs := _button("TOGGLE FULLSCREEN", func() -> void:
		var mode := DisplayServer.window_get_mode()
		if mode == DisplayServer.WINDOW_MODE_FULLSCREEN:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_WINDOWED)
		else:
			DisplayServer.window_set_mode(DisplayServer.WINDOW_MODE_FULLSCREEN))
	box.add_child(fs)
	box.add_child(_button("BACK", func() -> void:
		G.save_game()
		_build_title()))


func _start_run(map_index: int) -> void:
	if _menu != null:
		_menu.queue_free()
		_menu = null
	_run = Run.new()
	_run.map_index = map_index
	_run.run_finished.connect(_on_run_finished.bind(map_index))
	add_child(_run)


func _on_run_finished(won: bool, stats: Dictionary, map_index: int) -> void:
	G.report_run(Data.MAPS[map_index]["id"], stats["time"], stats["gold"], won)
	_run.queue_free()
	_run = null
	_show_menu()


func _page_root() -> Control:
	var bg := ColorRect.new()
	bg.color = Color(0.05, 0.05, 0.09)
	bg.set_anchors_preset(Control.PRESET_FULL_RECT)
	_menu.add_child(bg)
	var snow := CPUParticles2D.new()
	snow.amount = 90
	snow.lifetime = 6.0
	snow.preprocess = 6.0
	snow.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	snow.emission_rect_extents = Vector2(700, 10)
	snow.position = Vector2(640, -20)
	snow.direction = Vector2(0, 1)
	snow.spread = 12.0
	snow.gravity = Vector2(0, 18)
	snow.initial_velocity_min = 40.0
	snow.initial_velocity_max = 110.0
	snow.scale_amount_min = 1.0
	snow.scale_amount_max = 2.5
	snow.color = Color(0.8, 0.9, 1.0, 0.5)
	bg.add_child(snow)
	var blood_mist := CPUParticles2D.new()
	blood_mist.amount = 30
	blood_mist.lifetime = 8.0
	blood_mist.preprocess = 8.0
	blood_mist.emission_shape = CPUParticles2D.EMISSION_SHAPE_RECTANGLE
	blood_mist.emission_rect_extents = Vector2(700, 10)
	blood_mist.position = Vector2(640, 740)
	blood_mist.direction = Vector2(0, -1)
	blood_mist.spread = 20.0
	blood_mist.gravity = Vector2(0, -6)
	blood_mist.initial_velocity_min = 10.0
	blood_mist.initial_velocity_max = 30.0
	blood_mist.scale_amount_min = 2.0
	blood_mist.scale_amount_max = 5.0
	blood_mist.color = Color(0.55, 0.1, 0.15, 0.18)
	bg.add_child(blood_mist)
	return bg


func _button(text: String, cb: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(260, 46)
	b.add_theme_font_size_override("font_size", 18)
	b.add_theme_stylebox_override("normal", _style(Color(0.10, 0.09, 0.14), 6, BLOOD.darkened(0.2)))
	b.add_theme_stylebox_override("hover", _style(Color(0.18, 0.11, 0.16), 6, FROST))
	b.add_theme_stylebox_override("pressed", _style(Color(0.22, 0.09, 0.13), 6, FROST))
	b.pressed.connect(func() -> void:
		Sfx.play("click")
		cb.call())
	return b


func _label(text: String, size_: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size_)
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.8))
	l.add_theme_constant_override("shadow_offset_y", 2)
	return l


func _style(bg: Color, radius: int, border := Color.TRANSPARENT) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.set_corner_radius_all(radius)
	if border.a > 0.0:
		s.border_color = border
		s.set_border_width_all(2)
	return s
