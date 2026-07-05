extends Node
## Sprite provider. If res://assets/sprites/<key>.png exists it is used
## (drop in the real asset sheets sliced to these names); otherwise a
## readable pixel-art placeholder is generated at runtime.

var _cache := {}


func tex(key: String) -> Texture2D:
	if _cache.has(key):
		return _cache[key]
	var t: Texture2D
	var path := "res://assets/sprites/%s.png" % key
	if ResourceLoader.exists(path):
		t = load(path)
	else:
		t = _generate(key)
	_cache[key] = t
	return t


func _generate(key: String) -> Texture2D:
	var img: Image
	if key == "player":
		img = _make_player()
	elif Data.ENEMIES.has(key):
		var d: Dictionary = Data.ENEMIES[key]
		img = _make_creature(str(d["kind"]), d["col"], d["col2"], 32)
	elif Data.BOSSES.has(key):
		var d: Dictionary = Data.BOSSES[key]
		img = _make_creature(str(d["kind"]), d["col"], d["col2"], 56)
	elif key.begins_with("proj_"):
		img = _make_projectile(key.trim_prefix("proj_"))
	elif key.begins_with("decor_"):
		img = _make_decor(key)
	else:
		img = _make_misc(key)
	return ImageTexture.create_from_image(img)


func _blank(s: int) -> Image:
	var img := Image.create(s, s, false, Image.FORMAT_RGBA8)
	img.fill(Color(0, 0, 0, 0))
	return img


func _disc(img: Image, cx: float, cy: float, r: float, col: Color) -> void:
	for y in range(max(0, int(cy - r)), min(img.get_height(), int(cy + r) + 1)):
		for x in range(max(0, int(cx - r)), min(img.get_width(), int(cx + r) + 1)):
			if Vector2(x - cx, y - cy).length() <= r:
				img.set_pixel(x, y, col)


func _ring(img: Image, cx: float, cy: float, r: float, w: float, col: Color) -> void:
	for y in range(img.get_height()):
		for x in range(img.get_width()):
			var d := Vector2(x - cx, y - cy).length()
			if d <= r and d >= r - w:
				img.set_pixel(x, y, col)


func _rect(img: Image, x: int, y: int, w: int, h: int, col: Color) -> void:
	img.fill_rect(Rect2i(x, y, w, h), col)


const BLOOD := Color(0.62, 0.10, 0.16)
const FROST := Color(0.70, 0.88, 0.95)
const DARK := Color(0.08, 0.08, 0.11)


func _make_player() -> Image:
	var img := _blank(32)
	# cape
	_rect(img, 8, 10, 16, 16, Color(0.13, 0.12, 0.16))
	_rect(img, 7, 12, 2, 12, BLOOD.darkened(0.3))
	_rect(img, 23, 12, 2, 12, BLOOD.darkened(0.3))
	# body armor
	_rect(img, 12, 12, 8, 11, Color(0.22, 0.25, 0.32))
	_rect(img, 13, 13, 6, 3, FROST.darkened(0.4))
	# legs
	_rect(img, 12, 23, 3, 6, Color(0.16, 0.18, 0.24))
	_rect(img, 17, 23, 3, 6, Color(0.16, 0.18, 0.24))
	# head
	_disc(img, 16, 8, 4.0, Color(0.85, 0.82, 0.78))
	# hair
	_rect(img, 11, 3, 10, 3, Color(0.10, 0.09, 0.11))
	_rect(img, 11, 5, 3, 3, Color(0.10, 0.09, 0.11))
	# eyes
	img.set_pixel(14, 8, BLOOD)
	img.set_pixel(18, 8, BLOOD)
	# collar
	_rect(img, 10, 10, 3, 4, DARK)
	_rect(img, 19, 10, 3, 4, DARK)
	return img


func _make_creature(kind: String, col: Color, col2: Color, s: int) -> Image:
	var img := _blank(s)
	var c := s / 2.0
	match kind:
		"humanoid":
			_rect(img, int(c - s * 0.14), int(s * 0.36), int(s * 0.28), int(s * 0.36), col)
			_rect(img, int(c - s * 0.12), int(s * 0.72), int(s * 0.09), int(s * 0.2), col2)
			_rect(img, int(c + s * 0.03), int(s * 0.72), int(s * 0.09), int(s * 0.2), col2)
			_disc(img, c, s * 0.24, s * 0.13, col2)
			img.set_pixel(int(c - s * 0.06), int(s * 0.24), BLOOD)
			img.set_pixel(int(c + s * 0.06), int(s * 0.24), BLOOD)
		"brute":
			_disc(img, c, s * 0.55, s * 0.30, col)
			_disc(img, c, s * 0.30, s * 0.15, col2)
			_rect(img, int(c - s * 0.34), int(s * 0.45), int(s * 0.12), int(s * 0.3), col2)
			_rect(img, int(c + s * 0.22), int(s * 0.45), int(s * 0.12), int(s * 0.3), col2)
			img.set_pixel(int(c - s * 0.06), int(s * 0.28), Color(0.95, 0.9, 0.3))
			img.set_pixel(int(c + s * 0.06), int(s * 0.28), Color(0.95, 0.9, 0.3))
		"wraith":
			for y in range(int(s * 0.2), int(s * 0.9)):
				var wdt := s * 0.30 * (1.0 - float(y) / s * 0.5)
				var wob := sin(y * 0.8) * s * 0.04
				var a := clampf(1.4 - float(y) / s * 1.4, 0.15, 0.95)
				var cc := Color(col.r, col.g, col.b, a)
				_rect(img, int(c - wdt / 2 + wob), y, int(maxf(wdt, 1)), 1, cc)
			_disc(img, c, s * 0.26, s * 0.11, col2)
			img.set_pixel(int(c - s * 0.05), int(s * 0.26), FROST)
			img.set_pixel(int(c + s * 0.05), int(s * 0.26), FROST)
		"beast":
			_rect(img, int(s * 0.18), int(s * 0.42), int(s * 0.55), int(s * 0.28), col)
			_disc(img, s * 0.75, s * 0.40, s * 0.14, col)
			_rect(img, int(s * 0.80), int(s * 0.28), int(s * 0.08), int(s * 0.1), col)
			_rect(img, int(s * 0.22), int(s * 0.68), int(s * 0.08), int(s * 0.18), col2)
			_rect(img, int(s * 0.58), int(s * 0.68), int(s * 0.08), int(s * 0.18), col2)
			_rect(img, int(s * 0.06), int(s * 0.40), int(s * 0.14), int(s * 0.06), col2)
			img.set_pixel(int(s * 0.78), int(s * 0.38), BLOOD)
		"serpent":
			for i in range(14):
				var t := i / 13.0
				_disc(img, s * (0.2 + t * 0.6), s * (0.65 - sin(t * PI) * 0.3), s * (0.06 + t * 0.06), col)
			_disc(img, s * 0.8, s * 0.35, s * 0.13, col2)
			img.set_pixel(int(s * 0.84), int(s * 0.33), FROST)
		_:
			_disc(img, c, c, s * 0.3, col)
	return img


func _make_projectile(wid: String) -> Image:
	var img := _blank(24)
	match wid:
		"icicle_spike", "frozen_blood_spear", "bone_lance", "blood_bolt":
			for x in range(2, 22):
				var h := int(3.0 * (1.0 - abs(x - 8.0) / 14.0)) + 1
				_rect(img, x, 12 - h / 2 - 1, 1, h + 1, FROST if x > 12 else BLOOD)
			img.set_pixel(21, 11, Color.WHITE)
		"frozen_blood_scythe", "crescent_blade":
			_ring(img, 12, 12, 9.0, 4.0, BLOOD)
			_rect(img, 0, 12, 24, 12, Color(0, 0, 0, 0))
			_ring(img, 12, 12, 9.0, 1.5, FROST)
			_rect(img, 0, 13, 24, 11, Color(0, 0, 0, 0))
		"shard_bolts", "throwing_stars", "shadow_scythe":
			_disc(img, 12, 12, 3.0, BLOOD)
			_rect(img, 11, 4, 2, 16, BLOOD)
			_rect(img, 4, 11, 16, 2, BLOOD)
			_disc(img, 12, 12, 1.5, FROST)
		"spinning_disc", "frozen_blood_orb":
			_disc(img, 12, 12, 8.0, BLOOD)
			_ring(img, 12, 12, 8.0, 2.0, FROST)
			_disc(img, 9, 9, 2.0, Color(1, 1, 1, 0.8))
		"explosion_burst":
			_disc(img, 12, 12, 10.0, Color(FROST.r, FROST.g, FROST.b, 0.5))
			_disc(img, 12, 12, 6.0, BLOOD)
			_disc(img, 12, 12, 3.0, Color.WHITE)
		"blood_pool":
			for y in range(24):
				for x in range(24):
					var d := Vector2((x - 12) * 1.0, (y - 12) * 2.2).length()
					if d < 11.0:
						img.set_pixel(x, y, BLOOD if d > 5.0 else FROST.darkened(0.2))
		"bite_dash":
			_rect(img, 4, 6, 16, 3, Color(0.9, 0.9, 0.95))
			for i in range(4):
				_rect(img, 5 + i * 4, 9, 2, 4 + (i % 2) * 2, Color(0.95, 0.95, 1.0))
			_rect(img, 4, 15, 16, 2, BLOOD)
		"nova_ring":
			_ring(img, 12, 12, 10.0, 3.0, BLOOD)
			_ring(img, 12, 12, 8.0, 1.0, FROST)
		_:
			_disc(img, 12, 12, 5.0, BLOOD)
	return img


func _make_decor(key: String) -> Image:
	var img := _blank(16)
	var rng := RandomNumberGenerator.new()
	rng.seed = hash(key)
	var base := Color.from_hsv(rng.randf_range(0.0, 1.0), 0.2, rng.randf_range(0.25, 0.45))
	for i in range(rng.randi_range(4, 9)):
		var x := rng.randi_range(2, 12)
		var y := rng.randi_range(2, 12)
		_rect(img, x, y, rng.randi_range(1, 3), rng.randi_range(1, 3), base.lightened(rng.randf_range(-0.1, 0.2)))
	return img


func _make_misc(key: String) -> Image:
	var img := _blank(16)
	match key:
		"gem":
			_rect(img, 6, 3, 4, 2, FROST)
			_rect(img, 4, 5, 8, 5, Color(0.3, 0.6, 0.95))
			_rect(img, 6, 10, 4, 3, Color(0.2, 0.4, 0.8))
			img.set_pixel(6, 5, Color.WHITE)
		"gold":
			_disc(img, 8, 8, 5.0, Color(0.9, 0.75, 0.2))
			_ring(img, 8, 8, 5.0, 1.5, Color(0.65, 0.5, 0.12))
		"heart":
			_disc(img, 5.5, 6, 3.0, BLOOD)
			_disc(img, 10.5, 6, 3.0, BLOOD)
			_rect(img, 4, 7, 8, 3, BLOOD)
			_rect(img, 6, 10, 4, 2, BLOOD)
			_rect(img, 7, 12, 2, 1, BLOOD)
		"chest":
			_rect(img, 2, 5, 12, 8, Color(0.45, 0.30, 0.15))
			_rect(img, 2, 5, 12, 2, Color(0.55, 0.38, 0.20))
			_rect(img, 7, 7, 2, 3, Color(0.9, 0.75, 0.2))
		_:
			_disc(img, 8, 8, 4.0, Color.MAGENTA)
	return img
