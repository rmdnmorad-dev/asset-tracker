class_name Enemy
extends Area2D
## A chasing monster. Bosses use the same node with is_boss and an attack pattern.

signal killed(enemy: Enemy)

var eid := ""
var display_name := ""
var hp := 10.0
var max_hp_v := 10.0
var spd := 40.0
var dmg := 5.0
var xp := 1
var is_boss := false
var attack := ""
var player: Player
var run: Node2D

var _sprite: Sprite2D
var _bob := randf() * TAU
var _wobble := randf_range(-0.4, 0.4)
var _hit_cd := 0.0
var _flash := 0.0
var _attack_timer := 4.0
var _charge_dir := Vector2.ZERO
var _charge_time := 0.0
var dead := false


func setup(id: String, def: Dictionary, boss: bool, scale_hp: float, scale_dmg: float) -> void:
	eid = id
	display_name = str(def["name"])
	hp = float(def["hp"]) * scale_hp
	max_hp_v = hp
	spd = float(def["spd"])
	dmg = float(def["dmg"]) * scale_dmg
	xp = int(def["xp"])
	is_boss = boss
	attack = str(def.get("attack", ""))


func _ready() -> void:
	_sprite = Sprite2D.new()
	_sprite.texture = Art.tex(eid)
	add_child(_sprite)
	var shape := CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 22.0 if is_boss else 11.0
	shape.shape = circle
	add_child(shape)
	collision_layer = 2
	collision_mask = 0
	monitoring = false
	monitorable = true
	add_to_group("enemies")
	if is_boss:
		_sprite.scale = Vector2(1.4, 1.4)
		Sfx.play("boss")


func _physics_process(delta: float) -> void:
	if dead or player == null or player.dead:
		return
	var to_player := player.global_position - global_position
	var dist := to_player.length()
	var dir := to_player / maxf(dist, 0.001)
	if _charge_time > 0.0:
		_charge_time -= delta
		global_position += _charge_dir * spd * 4.0 * delta
	else:
		var wob := dir.rotated(sin(_bob * 1.7) * _wobble)
		global_position += wob * spd * delta
	_bob += delta * 8.0
	_sprite.flip_h = dir.x < 0
	_sprite.position.y = -abs(sin(_bob)) * 2.5
	_sprite.rotation = sin(_bob * 0.9) * 0.08
	if _flash > 0.0:
		_flash -= delta
		_sprite.modulate = Color(3.0, 3.0, 3.0)
	else:
		_sprite.modulate = Color.WHITE
	_hit_cd -= delta
	if dist < (30.0 if is_boss else 16.0) and _hit_cd <= 0.0:
		_hit_cd = 0.8
		player.take_damage(dmg)
	if is_boss:
		_boss_attack(delta, dir, dist)


func _boss_attack(delta: float, dir: Vector2, dist: float) -> void:
	_attack_timer -= delta
	if _attack_timer > 0.0:
		return
	_attack_timer = 5.0
	match attack:
		"volley":
			for i in range(10):
				run.spawn_enemy_bullet(global_position, Vector2.RIGHT.rotated(TAU * i / 10.0), dmg * 0.5)
		"summon":
			run.boss_summon(global_position)
		"charge":
			_charge_dir = dir
			_charge_time = 0.9
			Sfx.play("boss", 1.5)
		"mirror":
			global_position = player.global_position + Vector2.RIGHT.rotated(randf() * TAU) * 180.0
			for i in range(6):
				run.spawn_enemy_bullet(global_position, Vector2.RIGHT.rotated(TAU * i / 6.0), dmg * 0.5)
	if dist > 900.0:
		global_position = player.global_position - dir * 500.0


func take_damage(amount: float) -> void:
	if dead:
		return
	hp -= amount
	_flash = 0.08
	Sfx.play("hit", randf_range(0.9, 1.2))
	run.spawn_damage_number(global_position, amount)
	if hp <= 0.0:
		die()


func die() -> void:
	if dead:
		return
	dead = true
	Sfx.play("die", 0.8 if is_boss else randf_range(0.9, 1.3))
	killed.emit(self)
	# death poof: sprite pops and fades out, then frees.
	set_physics_process(false)
	collision_layer = 0
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(_sprite, "scale", _sprite.scale * 1.5, 0.25)
	tw.tween_property(_sprite, "modulate", Color(1, 0.2, 0.2, 0.0), 0.25)
	tw.chain().tween_callback(queue_free)
