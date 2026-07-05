class_name Hud
extends CanvasLayer
## In-run UI: bars, timer, kill/gold counters, weapon icons,
## level-up panel, pause panel and the end-of-run screen.

const BLOOD := Color(0.64, 0.11, 0.17)
const FROST := Color(0.70, 0.88, 0.95)
const PANEL_BG := Color(0.06, 0.06, 0.10, 0.96)

var player: Player
var run: Node2D
var boss: Enemy

var _root: Control
var _hp_fill: ColorRect
var _hp_text: Label
var _xp_fill: ColorRect
var _level_text: Label
var _timer_text: Label
var _kills_text: Label
var _gold_text: Label
var _boss_box: Control
var _boss_fill: ColorRect
var _boss_name: Label
var _weapon_row: HBoxContainer
var _level_panel: Control
var _pause_panel: Control
var _end_panel: Control
var _toast: Label


func init(p: Player, r: Node2D) -> void:
	player = p
	run = r


func _ready() -> void:
	process_mode = Node.PROCESS_MODE_ALWAYS
	layer = 10
	_root = Control.new()
	_root.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.mouse_filter = Control.MOUSE_FILTER_IGNORE
	add_child(_root)
	_build_bars()
	player.loadout_changed.connect(_refresh_weapons)
	_refresh_weapons()


func _build_bars() -> void:
	# XP bar across the top.
	var xp_bg := _rect(Color(0, 0, 0, 0.6), Vector2(0, 0), Vector2(1280, 14))
	_root.add_child(xp_bg)
	xp_bg.set_anchors_preset(Control.PRESET_TOP_WIDE)
	_xp_fill = _rect(FROST.darkened(0.15), Vector2(0, 2), Vector2(0, 10))
	xp_bg.add_child(_xp_fill)
	_level_text = _label("LV 1", 11, Color.WHITE)
	_level_text.position = Vector2(8, 16)
	_root.add_child(_level_text)
	# Timer.
	_timer_text = _label("00:00", 26, Color.WHITE)
	_timer_text.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_timer_text.position = Vector2(-40, 22)
	_root.add_child(_timer_text)
	# Kills / gold.
	_kills_text = _label("Kills 0", 14, Color(0.9, 0.6, 0.6))
	_kills_text.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_kills_text.position = Vector2(-130, 22)
	_root.add_child(_kills_text)
	_gold_text = _label("Gold 0", 14, Color(0.92, 0.8, 0.35))
	_gold_text.set_anchors_preset(Control.PRESET_TOP_RIGHT)
	_gold_text.position = Vector2(-130, 42)
	_root.add_child(_gold_text)
	# HP bar bottom left.
	var hp_bg := _rect(Color(0, 0, 0, 0.6), Vector2(16, -46), Vector2(260, 20))
	_root.add_child(hp_bg)
	hp_bg.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	_hp_fill = _rect(BLOOD, Vector2(2, 2), Vector2(256, 16))
	hp_bg.add_child(_hp_fill)
	_hp_text = _label("100/100", 11, Color.WHITE)
	_hp_text.position = Vector2(6, 1)
	hp_bg.add_child(_hp_text)
	# Weapon icons.
	_weapon_row = HBoxContainer.new()
	_weapon_row.set_anchors_preset(Control.PRESET_BOTTOM_LEFT)
	_weapon_row.position = Vector2(16, -84)
	_weapon_row.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(_weapon_row)
	# Boss bar.
	_boss_box = Control.new()
	_boss_box.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_boss_box.position = Vector2(-220, 64)
	_boss_box.visible = false
	_boss_box.mouse_filter = Control.MOUSE_FILTER_IGNORE
	_root.add_child(_boss_box)
	var boss_bg := _rect(Color(0, 0, 0, 0.7), Vector2.ZERO, Vector2(440, 16))
	_boss_box.add_child(boss_bg)
	_boss_fill = _rect(Color(0.75, 0.15, 0.35), Vector2(2, 2), Vector2(436, 12))
	boss_bg.add_child(_boss_fill)
	_boss_name = _label("BOSS", 12, Color(1.0, 0.7, 0.8))
	_boss_name.position = Vector2(0, -18)
	_boss_box.add_child(_boss_name)
	# Toast.
	_toast = _label("", 18, FROST)
	_toast.set_anchors_preset(Control.PRESET_CENTER_TOP)
	_toast.position = Vector2(-240, 110)
	_toast.custom_minimum_size = Vector2(480, 30)
	_toast.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_toast.modulate.a = 0.0
	_root.add_child(_toast)


func _process(_delta: float) -> void:
	if player == null or run == null:
		return
	if Input.is_action_just_pressed("pause"):
		run.toggle_pause()
	_hp_fill.size.x = 256.0 * clampf(player.hp / player.max_hp(), 0.0, 1.0)
	_hp_text.text = "%d/%d" % [int(maxf(player.hp, 0)), int(player.max_hp())]
	_xp_fill.size.x = _root.size.x * clampf(float(player.xp) / player.xp_needed(), 0.0, 1.0)
	_level_text.text = "LV %d" % player.level
	var t := int(run.elapsed)
	_timer_text.text = "%02d:%02d" % [t / 60, t % 60]
	_kills_text.text = "Kills %d" % run.kills
	_gold_text.text = "Gold %d" % run.gold_earned
	if boss != null and is_instance_valid(boss) and not boss.dead:
		_boss_fill.size.x = 436.0 * clampf(boss.hp / boss.max_hp_v, 0.0, 1.0)
	else:
		_boss_box.visible = false


func set_boss(b: Enemy) -> void:
	boss = b
	_boss_box.visible = b != null
	if b != null:
		_boss_name.text = b.display_name


func _refresh_weapons() -> void:
	for c in _weapon_row.get_children():
		c.queue_free()
	for wid in player.weapons:
		var slot := PanelContainer.new()
		slot.add_theme_stylebox_override("panel", _style(Color(0, 0, 0, 0.55), 4))
		var v := VBoxContainer.new()
		var icon := TextureRect.new()
		icon.texture = Art.tex("proj_" + wid)
		icon.custom_minimum_size = Vector2(28, 28)
		icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		v.add_child(icon)
		var lv := _label("Lv%d" % int(player.weapons[wid]), 9, FROST)
		lv.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		v.add_child(lv)
		slot.add_child(v)
		_weapon_row.add_child(slot)


func toast(text: String) -> void:
	_toast.text = text
	_toast.modulate.a = 1.0
	var tw := _toast.create_tween()
	tw.tween_interval(1.8)
	tw.tween_property(_toast, "modulate:a", 0.0, 0.7)


func level_panel_open() -> bool:
	return _level_panel != null and is_instance_valid(_level_panel)


func show_level_up(options: Array, cb: Callable) -> void:
	_level_panel = _dim_panel()
	var box := _center_box(_level_panel, Vector2(760, 360))
	var title := _label("LEVEL UP — CHOOSE", 24, BLOOD.lightened(0.3))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var row := HBoxContainer.new()
	row.add_theme_constant_override("separation", 16)
	row.alignment = BoxContainer.ALIGNMENT_CENTER
	box.add_child(row)
	for opt in options:
		row.add_child(_option_card(opt, cb))


func _option_card(opt: Dictionary, cb: Callable) -> Button:
	var b := Button.new()
	b.custom_minimum_size = Vector2(220, 250)
	b.add_theme_stylebox_override("normal", _style(Color(0.10, 0.10, 0.16), 8, BLOOD.darkened(0.2)))
	b.add_theme_stylebox_override("hover", _style(Color(0.16, 0.12, 0.20), 8, FROST))
	b.add_theme_stylebox_override("pressed", _style(Color(0.2, 0.1, 0.14), 8, FROST))
	var v := VBoxContainer.new()
	v.set_anchors_preset(Control.PRESET_FULL_RECT)
	v.mouse_filter = Control.MOUSE_FILTER_IGNORE
	v.alignment = BoxContainer.ALIGNMENT_CENTER
	v.add_theme_constant_override("separation", 10)
	var icon := TextureRect.new()
	icon.texture = Art.tex(str(opt["icon"]))
	icon.custom_minimum_size = Vector2(64, 64)
	icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
	icon.mouse_filter = Control.MOUSE_FILTER_IGNORE
	v.add_child(icon)
	var name_l := _label(str(opt["title"]), 15, Color.WHITE)
	name_l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_l.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	name_l.custom_minimum_size = Vector2(200, 0)
	v.add_child(name_l)
	var desc := _label(str(opt["desc"]), 11, Color(0.75, 0.78, 0.85))
	desc.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	desc.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	desc.custom_minimum_size = Vector2(200, 0)
	v.add_child(desc)
	b.add_child(v)
	b.pressed.connect(func() -> void:
		Sfx.play("click")
		_level_panel.queue_free()
		_level_panel = null
		cb.call(opt))
	return b


func show_pause(visible_: bool) -> void:
	if not visible_:
		if _pause_panel != null and is_instance_valid(_pause_panel):
			_pause_panel.queue_free()
		_pause_panel = null
		return
	_pause_panel = _dim_panel()
	var box := _center_box(_pause_panel, Vector2(320, 260))
	var title := _label("PAUSED", 26, FROST)
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	box.add_child(_menu_button("Resume", func() -> void: run.toggle_pause()))
	box.add_child(_menu_button("Quit to Menu", func() -> void:
		show_pause(false)
		run.quit_to_menu()))


func show_end(won: bool, stats: Dictionary, cb: Callable) -> void:
	_end_panel = _dim_panel()
	var box := _center_box(_end_panel, Vector2(420, 340))
	var title := _label("THE NIGHT IS YOURS" if won else "YOU HAVE FALLEN", 24, FROST if won else BLOOD.lightened(0.3))
	title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	box.add_child(title)
	var t := int(stats["time"])
	for line in [
		"Survived  %02d:%02d" % [t / 60, t % 60],
		"Level  %d" % stats["level"],
		"Kills  %d" % stats["kills"],
		"Gold earned  %d" % stats["gold"],
	]:
		var l := _label(str(line), 15, Color(0.85, 0.88, 0.95))
		l.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		box.add_child(l)
	box.add_child(_menu_button("Continue", func() -> void:
		_end_panel.queue_free()
		cb.call()))


func _menu_button(text: String, cb: Callable) -> Button:
	var b := Button.new()
	b.text = text
	b.custom_minimum_size = Vector2(220, 44)
	b.add_theme_font_size_override("font_size", 16)
	b.add_theme_stylebox_override("normal", _style(Color(0.12, 0.10, 0.16), 6, BLOOD.darkened(0.25)))
	b.add_theme_stylebox_override("hover", _style(Color(0.2, 0.12, 0.18), 6, FROST))
	b.add_theme_stylebox_override("pressed", _style(Color(0.25, 0.1, 0.15), 6, FROST))
	b.pressed.connect(func() -> void:
		Sfx.play("click")
		cb.call())
	return b


func _dim_panel() -> Control:
	var dim := ColorRect.new()
	dim.color = Color(0, 0, 0, 0.65)
	dim.set_anchors_preset(Control.PRESET_FULL_RECT)
	_root.add_child(dim)
	return dim


func _center_box(parent: Control, size_: Vector2) -> VBoxContainer:
	var center := CenterContainer.new()
	center.set_anchors_preset(Control.PRESET_FULL_RECT)
	parent.add_child(center)
	var panel := PanelContainer.new()
	panel.custom_minimum_size = size_
	panel.add_theme_stylebox_override("panel", _style(PANEL_BG, 10, BLOOD))
	center.add_child(panel)
	var margin := MarginContainer.new()
	for m in ["margin_left", "margin_right", "margin_top", "margin_bottom"]:
		margin.add_theme_constant_override(m, 18)
	panel.add_child(margin)
	var box := VBoxContainer.new()
	box.add_theme_constant_override("separation", 12)
	box.alignment = BoxContainer.ALIGNMENT_CENTER
	margin.add_child(box)
	return box


func _rect(col: Color, pos: Vector2, size_: Vector2) -> ColorRect:
	var r := ColorRect.new()
	r.color = col
	r.position = pos
	r.size = size_
	r.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return r


func _label(text: String, size_: int, col: Color) -> Label:
	var l := Label.new()
	l.text = text
	l.add_theme_font_size_override("font_size", size_)
	l.add_theme_color_override("font_color", col)
	l.add_theme_color_override("font_shadow_color", Color(0, 0, 0, 0.7))
	l.add_theme_constant_override("shadow_offset_y", 1)
	l.mouse_filter = Control.MOUSE_FILTER_IGNORE
	return l


func _style(bg: Color, radius: int, border := Color.TRANSPARENT) -> StyleBoxFlat:
	var s := StyleBoxFlat.new()
	s.bg_color = bg
	s.set_corner_radius_all(radius)
	if border.a > 0.0:
		s.border_color = border
		s.set_border_width_all(2)
	s.content_margin_left = 8
	s.content_margin_right = 8
	s.content_margin_top = 6
	s.content_margin_bottom = 6
	return s
