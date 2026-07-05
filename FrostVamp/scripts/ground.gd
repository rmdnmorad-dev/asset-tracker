class_name Ground
extends Node2D
## Infinite scrolling tiled ground, drawn procedurally per visible cell.
## Each cell gets a deterministic tint + occasional decor sprite.

const CELL := 64

var map_def: Dictionary
var target: Node2D
var _last_cell := Vector2i(99999, 99999)
var _decor_keys: Array[String] = []


func _ready() -> void:
	z_index = -100
	for i in range(6):
		_decor_keys.append("decor_%s_%d" % [map_def["id"], i])


func _process(_delta: float) -> void:
	if target == null:
		return
	var cell := Vector2i(int(target.global_position.x) / CELL, int(target.global_position.y) / CELL)
	if cell != _last_cell:
		_last_cell = cell
		queue_redraw()


func _draw() -> void:
	if target == null:
		return
	var grounds: Array = map_def["ground"]
	var accent: Color = map_def["accent"]
	var center := target.global_position
	var half := Vector2(800, 500)
	var x0 := int((center.x - half.x) / CELL) - 1
	var x1 := int((center.x + half.x) / CELL) + 1
	var y0 := int((center.y - half.y) / CELL) - 1
	var y1 := int((center.y + half.y) / CELL) + 1
	for cy in range(y0, y1 + 1):
		for cx in range(x0, x1 + 1):
			var h := hash(Vector2i(cx, cy))
			var col: Color = grounds[h % grounds.size()]
			draw_rect(Rect2(cx * CELL, cy * CELL, CELL, CELL), col)
			if h % 17 == 0:
				draw_rect(Rect2(cx * CELL + 8, cy * CELL + 8, 4, 4), accent.darkened(0.5))
			if h % 11 == 0:
				var dk: String = _decor_keys[h % _decor_keys.size()]
				draw_texture(Art.tex(dk), Vector2(cx * CELL + (h % 40), cy * CELL + (h % 32)))
