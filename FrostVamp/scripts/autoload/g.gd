extends Node
## Global state: input map setup, meta progression, save/load.

const SAVE_PATH := "user://frostvamp.save"

var gold := 0
var best_time := {}
var unlocked_maps := 1
var selected_map := 0


func _init() -> void:
	_setup_input()


func _ready() -> void:
	load_game()


func _setup_input() -> void:
	var actions := {
		"move_up": [KEY_W, KEY_UP],
		"move_down": [KEY_S, KEY_DOWN],
		"move_left": [KEY_A, KEY_LEFT],
		"move_right": [KEY_D, KEY_RIGHT],
		"pause": [KEY_ESCAPE, KEY_P],
	}
	for action in actions:
		if not InputMap.has_action(action):
			InputMap.add_action(action)
		for key in actions[action]:
			var ev := InputEventKey.new()
			ev.physical_keycode = key
			InputMap.action_add_event(action, ev)


func save_game() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("meta", "gold", gold)
	cfg.set_value("meta", "best_time", best_time)
	cfg.set_value("meta", "unlocked_maps", unlocked_maps)
	cfg.set_value("options", "sfx", Sfx.sfx_volume)
	cfg.set_value("options", "music", Sfx.music_volume)
	cfg.save(SAVE_PATH)


func load_game() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SAVE_PATH) != OK:
		return
	gold = cfg.get_value("meta", "gold", 0)
	best_time = cfg.get_value("meta", "best_time", {})
	unlocked_maps = cfg.get_value("meta", "unlocked_maps", 1)
	Sfx.set_volumes(cfg.get_value("options", "sfx", 0.8), cfg.get_value("options", "music", 0.6))


func report_run(map_id: String, time_survived: float, gold_earned: int, won: bool) -> void:
	gold += gold_earned
	var prev: float = best_time.get(map_id, 0.0)
	if time_survived > prev:
		best_time[map_id] = time_survived
	if won:
		var idx := 0
		for i in range(Data.MAPS.size()):
			if Data.MAPS[i]["id"] == map_id:
				idx = i
		unlocked_maps = maxi(unlocked_maps, mini(idx + 2, Data.MAPS.size()))
	save_game()
