class_name Run
extends Node2D
## One survival run on one map: world, spawner, boss schedule, drops, level-ups.

signal run_finished(won: bool, stats: Dictionary)

const ENEMY_CAP := 250

var map_index := 0
var map_def: Dictionary
var player: Player
var hud: Hud
var camera: Camera2D
var elapsed := 0.0
var kills := 0
var gold_earned := 0
var boss: Enemy
var final_boss_spawned := false

var _spawn_timer := 1.0
var _swarm_timer := 120.0
var _boss_spawned := [false, false, false]
var _shake := 0.0
var _pending_levels := 0
var _over := false
var _dmg_count := 0
var enemies_node: Node2D
var proj_node: Node2D
var pickups_node: Node2D


class EnemyBullet:
	extends Node2D
	var dir := Vector2.RIGHT
	var dmg := 5.0
	var player: Player
	var _age := 0.0

	func _ready() -> void:
		var s := Sprite2D.new()
		s.texture = Art.tex("proj_blood_bolt")
		s.modulate = Color(1.0, 0.5, 0.9)
		s.rotation = dir.angle()
		add_child(s)

	func _physics_process(delta: float) -> void:
		_age += delta
		if _age > 4.0:
			queue_free()
			return
		global_position += dir * 230.0 * delta
		if player != null and not player.dead:
			if global_position.distance_to(player.global_position) < 14.0:
				player.take_damage(dmg)
				queue_free()


func _ready() -> void:
	map_def = Data.MAPS[map_index]
	var ground := Ground.new()
	ground.map_def = map_def
	add_child(ground)
	enemies_node = Node2D.new()
	enemies_node.y_sort_enabled = true
	add_child(enemies_node)
	proj_node = Node2D.new()
	add_child(proj_node)
	pickups_node = Node2D.new()
	add_child(pickups_node)
	player = Player.new()
	player.run = self
	add_child(player)
	ground.target = player
	camera = Camera2D.new()
	camera.zoom = Vector2(1.3, 1.3)
	player.add_child(camera)
	camera.make_current()
	hud = Hud.new()
	hud.init(player, self)
	add_child(hud)
	player.leveled_up.connect(_on_leveled_up)
	player.died.connect(_on_player_died)
	hud.toast("%s — survive the night!" % map_def["name"])


func _physics_process(delta: float) -> void:
	if _over:
		return
	elapsed += delta
	_spawn_tick(delta)
	_boss_tick()
	_swarm_timer -= delta
	if _swarm_timer <= 0.0:
		_swarm_timer = 120.0
		_spawn_swarm()
	if _shake > 0.0:
		_shake = maxf(_shake - delta * 12.0, 0.0)
		camera.offset = Vector2(randf_range(-_shake, _shake), randf_range(-_shake, _shake))
	else:
		camera.offset = Vector2.ZERO
	if elapsed >= Data.RUN_LENGTH:
		_finish(true)


func _spawn_tick(delta: float) -> void:
	_spawn_timer -= delta
	if _spawn_timer > 0.0:
		return
	_spawn_timer = maxf(1.3 - elapsed / 700.0, 0.3)
	if enemies_node.get_child_count() >= ENEMY_CAP:
		return
	var batch := 1 + int(elapsed / 80.0)
	for i in range(batch):
		_spawn_enemy(_pick_enemy_id(), false, 1.0)


func _pick_enemy_id() -> String:
	var ids: Array = map_def["enemies"]
	var r := randf()
	if elapsed < 120.0:
		return ids[0]
	elif elapsed < 300.0:
		return ids[0] if r < 0.6 else ids[1]
	else:
		if r < 0.35:
			return ids[0]
		elif r < 0.7:
			return ids[1]
		return ids[2]


func hp_scale() -> float:
	return (1.0 + elapsed / 60.0 * 0.10) * (1.0 + map_index * 0.25)


func _spawn_enemy(eid: String, elite: bool, mult: float) -> Enemy:
	var e := Enemy.new()
	var dmg_scale := (1.0 + elapsed / 900.0 * 0.6) * mult
	e.setup(eid, Data.ENEMIES[eid], false, hp_scale() * (6.0 if elite else 1.0) * mult, dmg_scale)
	if elite:
		e.xp *= 10
		e.scale = Vector2(1.6, 1.6)
	e.player = player
	e.run = self
	e.global_position = _offscreen_pos()
	e.killed.connect(_on_enemy_killed.bind(elite))
	enemies_node.add_child(e)
	return e


func _offscreen_pos() -> Vector2:
	return player.global_position + Vector2.RIGHT.rotated(randf() * TAU) * randf_range(620.0, 780.0)


func _spawn_swarm() -> void:
	var eid: String = map_def["enemies"][0]
	for i in range(24):
		var e := _spawn_enemy(eid, false, 0.6)
		e.global_position = player.global_position + Vector2.RIGHT.rotated(TAU * i / 24.0) * 640.0
		e.spd *= 1.8
	hud.toast("A swarm approaches!")
	if elapsed > 480.0:
		_spawn_enemy(_pick_enemy_id(), true, 1.0)


func _boss_tick() -> void:
	var times := [Data.MID_BOSS_TIME, Data.BOSS2_TIME, Data.FINAL_BOSS_TIME]
	for i in range(3):
		if elapsed >= float(times[i]) and not _boss_flag(i):
			_set_boss_flag(i)
			var bosses: Array = map_def["bosses"]
			var bid: String = bosses[0] if i < 2 else bosses[1]
			var b := Enemy.new()
			b.setup(bid, Data.BOSSES[bid], true, hp_scale() * (1.0 if i == 0 else 1.8), 1.0 + i * 0.3)
			b.player = player
			b.run = self
			b.global_position = _offscreen_pos()
			b.killed.connect(_on_boss_killed.bind(i))
			enemies_node.add_child(b)
			boss = b
			if i == 2:
				final_boss_spawned = true
			hud.set_boss(b)
			hud.toast("%s has risen!" % b.display_name)


func _boss_flag(i: int) -> bool:
	return _boss_spawned[i]


func _set_boss_flag(i: int) -> void:
	_boss_spawned[i] = true


func _on_boss_killed(b: Enemy, index: int) -> void:
	kills += 1
	hud.set_boss(null)
	_drop(b.global_position, "gem", b.xp)
	if index < 2:
		var chest := PickupItem.new()
		chest.kind = "chest"
		chest.player = player
		chest.global_position = b.global_position + Vector2(0, 20)
		pickups_node.add_child(chest)
	else:
		_finish(true)


func _on_enemy_killed(e: Enemy, elite: bool) -> void:
	kills += 1
	_drop(e.global_position, "gem", e.xp)
	var r := randf()
	if elite:
		_drop(e.global_position + Vector2(14, 0), "heart", 1)
		_drop(e.global_position + Vector2(-14, 0), "gold", 20)
	elif r < 0.06:
		_drop(e.global_position, "gold", randi_range(1, 4))
	elif r < 0.075:
		_drop(e.global_position, "heart", 1)


func _drop(pos: Vector2, kind: String, value: int) -> void:
	var p := PickupItem.new()
	p.kind = kind
	p.value = value
	p.player = player
	p.global_position = pos + Vector2(randf_range(-8, 8), randf_range(-8, 8))
	pickups_node.add_child(p)


func spawn_projectile(wid: String, w: Dictionary, pos: Vector2, dir: Vector2, dmg: float, p: Player) -> Projectile:
	var proj := Projectile.new()
	proj.setup(wid, w, dir, dmg, p)
	proj.global_position = pos
	proj_node.add_child(proj)
	return proj


func spawn_enemy_bullet(pos: Vector2, dir: Vector2, dmg: float) -> void:
	var b := EnemyBullet.new()
	b.dir = dir
	b.dmg = dmg
	b.player = player
	b.global_position = pos
	proj_node.add_child(b)


func boss_summon(pos: Vector2) -> void:
	for i in range(4):
		var e := _spawn_enemy(map_def["enemies"][randi() % 3], false, 1.0)
		e.global_position = pos + Vector2.RIGHT.rotated(TAU * i / 4.0) * 60.0


func spawn_damage_number(pos: Vector2, amount: float) -> void:
	if _dmg_count > 70:
		return
	_dmg_count += 1
	var l := Label.new()
	l.text = str(int(maxf(amount, 1.0)))
	l.add_theme_font_size_override("font_size", 11)
	l.add_theme_color_override("font_color", Color(0.85, 0.92, 1.0))
	l.z_index = 50
	l.position = pos + Vector2(randf_range(-10, 10), -18)
	proj_node.add_child(l)
	var tw := l.create_tween()
	tw.tween_property(l, "position:y", l.position.y - 22.0, 0.5)
	tw.parallel().tween_property(l, "modulate:a", 0.0, 0.5)
	tw.tween_callback(func() -> void:
		_dmg_count -= 1
		l.queue_free())


func nearest_enemy(pos: Vector2) -> Node2D:
	var best: Node2D = null
	var best_d := INF
	for e in enemies_node.get_children():
		if e is Enemy and not (e as Enemy).dead:
			var d: float = pos.distance_squared_to((e as Node2D).global_position)
			if d < best_d:
				best_d = d
				best = e
	return best


func random_enemy_within(pos: Vector2, radius: float) -> Node2D:
	var pool: Array[Node2D] = []
	var r2 := radius * radius
	for e in enemies_node.get_children():
		if e is Enemy and not (e as Enemy).dead:
			if pos.distance_squared_to((e as Node2D).global_position) < r2:
				pool.append(e)
	if pool.is_empty():
		return null
	return pool[randi() % pool.size()]


func add_gold(v: int) -> void:
	gold_earned += v


func open_chest() -> void:
	add_gold(25)
	var options := build_upgrade_options()
	if not options.is_empty():
		var opt: Dictionary = options[randi() % options.size()]
		apply_upgrade(opt)
		hud.toast("Chest: %s!" % opt["title"])
	Sfx.play("levelup", 0.8)


func shake_camera(strength: float) -> void:
	_shake = maxf(_shake, strength)


func _on_leveled_up() -> void:
	if _over:
		return
	_pending_levels += 1
	if _pending_levels == 1:
		_open_level_menu()


func _open_level_menu() -> void:
	var options := build_upgrade_options()
	if options.is_empty():
		player.heal(30.0)
		add_gold(10)
		_pending_levels = 0
		return
	options.shuffle()
	get_tree().paused = true
	hud.show_level_up(options.slice(0, 3), _on_upgrade_chosen)


func _on_upgrade_chosen(opt: Dictionary) -> void:
	apply_upgrade(opt)
	_pending_levels -= 1
	if _pending_levels > 0:
		_open_level_menu()
	else:
		get_tree().paused = false


func apply_upgrade(opt: Dictionary) -> void:
	if opt["type"] == "weapon":
		player.add_weapon(opt["id"])
	else:
		player.add_passive(opt["id"])


func build_upgrade_options() -> Array:
	var options := []
	var unowned: Array[String] = []
	for wid in Data.WEAPONS:
		if player.weapons.has(wid):
			if int(player.weapons[wid]) < Data.MAX_WEAPON_LEVEL:
				var lvl := int(player.weapons[wid]) + 1
				options.append({"type": "weapon", "id": wid, "title": "%s Lv%d" % [Data.WEAPONS[wid]["name"], lvl], "desc": Data.WEAPONS[wid]["desc"], "icon": "proj_" + wid})
		else:
			unowned.append(wid)
	if player.weapons.size() < Data.MAX_WEAPONS:
		unowned.shuffle()
		for i in range(mini(3, unowned.size())):
			var wid := unowned[i]
			options.append({"type": "weapon", "id": wid, "title": "NEW: %s" % Data.WEAPONS[wid]["name"], "desc": Data.WEAPONS[wid]["desc"], "icon": "proj_" + wid})
	for pid in Data.PASSIVES:
		var rank := int(player.passives.get(pid, 0))
		if rank < int(Data.PASSIVES[pid]["max"]):
			options.append({"type": "passive", "id": pid, "title": "%s %d" % [Data.PASSIVES[pid]["name"], rank + 1], "desc": Data.PASSIVES[pid]["desc"], "icon": "gem"})
	options.shuffle()
	return options


func _on_player_died() -> void:
	_finish(false)


func _finish(won: bool) -> void:
	if _over:
		return
	_over = true
	get_tree().paused = true
	if won:
		Sfx.play("levelup", 0.6)
	else:
		Sfx.play("die", 0.5)
	var stats := {"time": elapsed, "kills": kills, "gold": gold_earned, "level": player.level}
	hud.show_end(won, stats, func() -> void:
		get_tree().paused = false
		run_finished.emit(won, stats))


func toggle_pause() -> void:
	if _over or hud.level_panel_open():
		return
	var paused := not get_tree().paused
	get_tree().paused = paused
	hud.show_pause(paused)


func quit_to_menu() -> void:
	get_tree().paused = false
	_over = true
	var stats := {"time": elapsed, "kills": kills, "gold": gold_earned, "level": player.level}
	run_finished.emit(false, stats)
