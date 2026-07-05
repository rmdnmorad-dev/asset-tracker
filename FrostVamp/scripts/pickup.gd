class_name PickupItem
extends Node2D
## XP gems, gold, hearts and chests. Flies to the player when in magnet range.

var kind := "gem"
var value := 1
var player: Player
var _sprite: Sprite2D
var _vel := Vector2.ZERO
var _magnetized := false
var _bob := randf() * TAU


func _ready() -> void:
	_sprite = Sprite2D.new()
	_sprite.texture = Art.tex(kind)
	add_child(_sprite)
	if kind == "chest":
		scale = Vector2(1.6, 1.6)


func _physics_process(delta: float) -> void:
	if player == null or player.dead:
		return
	_bob += delta * 6.0
	_sprite.position.y = sin(_bob) * 2.0
	var to_p := player.global_position - global_position
	var dist := to_p.length()
	var range_ := player.magnet_range() * (2.0 if kind == "chest" else 1.0)
	if dist < range_:
		_magnetized = true
	if _magnetized:
		_vel = _vel.lerp(to_p.normalized() * 420.0, delta * 8.0)
		global_position += _vel * delta
	if dist < 18.0:
		_collect()


func _collect() -> void:
	match kind:
		"gem":
			player.gain_xp(value)
		"gold":
			player.run.add_gold(value)
			Sfx.play("pickup", 1.5)
		"heart":
			player.heal(25.0)
			Sfx.play("pickup", 0.8)
		"chest":
			player.run.open_chest()
	queue_free()
