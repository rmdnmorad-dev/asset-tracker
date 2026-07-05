class_name Projectile
extends Area2D
## One weapon instance in flight. Behavior comes from the weapon def.

var wid := ""
var beh := "straight"
var dmg := 5.0
var dir := Vector2.RIGHT
var speed := 400.0
var pierce := 1
var life := 1.0
var size := 1.0
var owner_player: Player

var orbit_angle := 0.0
var follow_player := false
var _age := 0.0
var _returning := false
var _tick := 0.0
var _hit_once := {}
var _sprite: Sprite2D
var _shape: CollisionShape2D


func setup(id: String, w: Dictionary, direction: Vector2, damage: float, p: Player) -> void:
	wid = id
	beh = str(w["beh"])
	dmg = damage
	dir = direction
	speed = float(w["speed"])
	pierce = int(w["pierce"])
	life = float(w["life"])
	size = float(w["size"])
	owner_player = p


func _ready() -> void:
	_sprite = Sprite2D.new()
	_sprite.texture = Art.tex("proj_" + wid)
	_sprite.scale = Vector2(size, size) * 1.4
	add_child(_sprite)
	_shape = CollisionShape2D.new()
	var circle := CircleShape2D.new()
	circle.radius = 9.0 * size
	_shape.shape = circle
	add_child(_shape)
	collision_layer = 0
	collision_mask = 2
	monitoring = true
	area_entered.connect(_on_area_entered)
	match beh:
		"straight", "boomerang":
			rotation = dir.angle()
		"ring":
			_sprite.scale = Vector2(0.5, 0.5)
		"burst":
			_sprite.scale = Vector2(0.4, 0.4) * size
			var tw := create_tween()
			tw.tween_property(_sprite, "scale", Vector2(2.2, 2.2) * size, life * 0.6)
			tw.parallel().tween_property(_sprite, "modulate:a", 0.0, life)
			Sfx.play("explode", randf_range(0.9, 1.2))
		"pool":
			_sprite.scale = Vector2(2.2, 2.2) * size
			circle.radius = 26.0 * size
			modulate.a = 0.85
		"melee":
			rotation = dir.angle()
			var tw := create_tween()
			tw.tween_property(_sprite, "scale", _sprite.scale * 1.6, life)
			tw.parallel().tween_property(_sprite, "modulate:a", 0.3, life)


func _physics_process(delta: float) -> void:
	_age += delta
	if _age >= life:
		queue_free()
		return
	match beh:
		"straight":
			global_position += dir * speed * delta
			_sprite.rotation += delta * 4.0 if wid in ["throwing_stars", "shadow_scythe", "spinning_disc"] else 0.0
		"boomerang":
			var t := _age / life
			if t < 0.5:
				global_position += dir * speed * (1.0 - t * 1.6) * delta
			elif is_instance_valid(owner_player):
				_returning = true
				var back := (owner_player.global_position - global_position)
				if back.length() < 24.0:
					queue_free()
					return
				global_position += back.normalized() * speed * 1.3 * delta
			rotation += delta * 12.0
		"orbit":
			if not is_instance_valid(owner_player):
				queue_free()
				return
			orbit_angle += speed * delta
			global_position = owner_player.global_position + Vector2.RIGHT.rotated(orbit_angle) * 85.0
			rotation += delta * 10.0
		"ring":
			var r := lerpf(0.5, 5.0 * size, _age / life)
			_sprite.scale = Vector2(r, r)
			(_shape.shape as CircleShape2D).radius = 10.0 * r
			_sprite.modulate.a = 1.0 - _age / life * 0.7
		"pool":
			_sprite.rotation += delta * 0.5
		"melee":
			if follow_player and is_instance_valid(owner_player):
				global_position = owner_player.global_position + dir * 34.0
		"burst":
			pass
	if beh in ["ring", "pool", "burst", "melee", "orbit"]:
		_tick -= delta
		if _tick <= 0.0:
			_tick = 0.45
			_damage_overlaps()


func _damage_overlaps() -> void:
	for a in get_overlapping_areas():
		if a is Enemy and not (a as Enemy).dead:
			if beh in ["burst", "melee", "ring"]:
				if _hit_once.has(a.get_instance_id()):
					continue
				_hit_once[a.get_instance_id()] = true
			(a as Enemy).take_damage(dmg)


func _on_area_entered(a: Area2D) -> void:
	if beh in ["ring", "pool", "burst", "melee", "orbit"]:
		return
	if a is Enemy and not (a as Enemy).dead:
		if _hit_once.has(a.get_instance_id()):
			return
		_hit_once[a.get_instance_id()] = true
		(a as Enemy).take_damage(dmg)
		if not _returning:
			pierce -= 1
			if pierce < 0 and beh == "straight":
				queue_free()
