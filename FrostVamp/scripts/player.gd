class_name Player
extends CharacterBody2D
## The vampire. Movement, stats, weapon auto-fire, XP and damage.

signal hp_changed(hp: float, max_hp: float)
signal xp_changed(level: int, xp: int, xp_needed: int)
signal leveled_up
signal died
signal loadout_changed

var run: Node2D

var weapons := {}
var passives := {}

var level := 1
var xp := 0
var base_speed := 155.0
var base_max_hp := 100.0
var hp := 100.0
var facing := Vector2.RIGHT
var _invuln := 0.0
var _timers := {}
var _sprite: Sprite2D
var _shadow: Sprite2D
var _bob := 0.0
var dead := false


func max_hp() -> float:
	return base_max_hp + 20.0 * passives.get("vigor", 0)


func speed() -> float:
	return base_speed * (1.0 + 0.08 * passives.get("haste", 0))


func might() -> float:
	return 1.0 + 0.10 * passives.get("might", 0)


func magnet_range() -> float:
	return 70.0 * (1.0 + 0.30 * passives.get("magnet", 0))


func cd_mult() -> float:
	return pow(0.94, passives.get("frost", 0))


func _ready() -> void:
	_shadow = Sprite2D.new()
	_shadow.texture = Art.tex("player")
	_shadow.modulate = Color(0, 0, 0, 0.35)
	_shadow.scale = Vector2(1.0, 0.35)
	_shadow.position = Vector2(0, 12)
	add_child(_shadow)
	_sprite = Sprite2D.new()
	_sprite.texture = Art.tex("player")
	add_child(_sprite)
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 8.0
	shape.shape = circle
	add_child(shape)
	collision_layer = 1
	collision_mask = 0
	hp = max_hp()
	add_weapon(Data.STARTING_WEAPON)


func add_weapon(wid: String) -> void:
	if weapons.has(wid):
		weapons[wid] = min(int(weapons[wid]) + 1, Data.MAX_WEAPON_LEVEL)
	else:
		weapons[wid] = 1
		_timers[wid] = 0.3
	loadout_changed.emit()


func add_passive(pid: String) -> void:
	var before := max_hp()
	passives[pid] = min(int(passives.get(pid, 0)) + 1, Data.PASSIVES[pid]["max"])
	if pid == "vigor":
		hp += max_hp() - before
		hp_changed.emit(hp, max_hp())
	loadout_changed.emit()


func _physics_process(delta: float) -> void:
	if dead:
		return
	var dir := Input.get_vector("move_left", "move_right", "move_up", "move_down")
	velocity = dir * speed()
	move_and_slide()
	if dir.length() > 0.1:
		facing = dir.normalized()
		_bob += delta * 10.0
	_sprite.flip_h = facing.x < 0
	_shadow.flip_h = _sprite.flip_h
	_sprite.position.y = -abs(sin(_bob)) * 3.0
	_sprite.rotation = sin(_bob) * 0.06
	if _invuln > 0.0:
		_invuln -= delta
		_sprite.modulate = Color(1, 1, 1, 0.5 + 0.5 * sin(_invuln * 40.0))
	else:
		_sprite.modulate = Color.WHITE
	_update_weapons(delta)


func _update_weapons(delta: float) -> void:
	for wid in weapons:
		_timers[wid] = float(_timers.get(wid, 0.5)) - delta
		if _timers[wid] <= 0.0:
			var w := Data.upgraded(Data.WEAPONS[wid], int(weapons[wid]))
			_timers[wid] = float(w["cd"]) * cd_mult()
			_fire(wid, w)


func _fire(wid: String, w: Dictionary) -> void:
	var dmg := float(w["dmg"]) * might()
	var count := int(w["count"])
	var beh := str(w["beh"])
	Sfx.play("shoot", 1.0 if beh != "melee" else 0.7)
	match beh:
		"straight":
			_fire_straight(wid, w, dmg, count)
		"boomerang":
			for i in range(count):
				var d := facing.rotated((i - (count - 1) * 0.5) * 0.35)
				run.spawn_projectile(wid, w, global_position, d, dmg, self)
		"orbit":
			for i in range(count):
				var p: Projectile = run.spawn_projectile(wid, w, global_position, Vector2.RIGHT, dmg, self)
				p.orbit_angle = TAU * i / count
		"pool":
			run.spawn_projectile(wid, w, global_position, Vector2.ZERO, dmg, self)
		"burst":
			for i in range(count):
				var e: Node2D = run.random_enemy_within(global_position, 340.0)
				var pos := global_position + Vector2(randf_range(-120, 120), randf_range(-120, 120))
				if e != null:
					pos = e.global_position
				run.spawn_projectile(wid, w, pos, Vector2.ZERO, dmg, self)
		"ring":
			run.spawn_projectile(wid, w, global_position, Vector2.ZERO, dmg, self)
		"melee":
			var p: Projectile = run.spawn_projectile(wid, w, global_position + facing * 34.0, facing, dmg, self)
			p.follow_player = true


func _fire_straight(wid: String, w: Dictionary, dmg: float, count: int) -> void:
	var dirs: Array[Vector2] = []
	match wid:
		"bone_lance":
			for i in range(count):
				dirs.append(facing.rotated((i - (count - 1) * 0.5) * 0.25))
		"shadow_scythe":
			var n := max(count, 4)
			for i in range(n):
				dirs.append(Vector2.RIGHT.rotated(TAU * i / n))
		"throwing_stars":
			for i in range(count):
				var e: Node2D = run.random_enemy_within(global_position, 500.0)
				if e != null:
					dirs.append((e.global_position - global_position).normalized())
				else:
					dirs.append(facing.rotated(randf_range(-0.5, 0.5)))
		"shard_bolts":
			var base := _aim_dir()
			for i in range(count):
				dirs.append(base.rotated((i - (count - 1) * 0.5) * 0.22))
		_:
			var base := _aim_dir()
			for i in range(count):
				dirs.append(base.rotated((i - (count - 1) * 0.5) * 0.18))
	for d in dirs:
		run.spawn_projectile(wid, w, global_position, d, dmg, self)


func _aim_dir() -> Vector2:
	var e: Node2D = run.nearest_enemy(global_position)
	if e != null:
		return (e.global_position - global_position).normalized()
	return facing


func take_damage(amount: float) -> void:
	if dead or _invuln > 0.0:
		return
	_invuln = 0.5
	hp -= amount
	Sfx.play("hurt")
	run.shake_camera(4.0)
	hp_changed.emit(hp, max_hp())
	if hp <= 0.0:
		dead = true
		died.emit()


func heal(amount: float) -> void:
	hp = minf(hp + amount, max_hp())
	hp_changed.emit(hp, max_hp())


func gain_xp(amount: int) -> void:
	xp += amount
	Sfx.play("pickup", randf_range(1.0, 1.3))
	while xp >= xp_needed():
		xp -= xp_needed()
		level += 1
		Sfx.play("levelup")
		leveled_up.emit()
	xp_changed.emit(level, xp, xp_needed())


func xp_needed() -> int:
	return int(8 + level * 6 * (1.0 + level * 0.06))
