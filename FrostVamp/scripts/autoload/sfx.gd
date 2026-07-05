extends Node
## Procedural audio: all SFX and the music loop are synthesized at startup,
## so the game ships with sound and zero audio files.

const RATE := 22050

var _clips := {}
var _pool: Array[AudioStreamPlayer] = []
var _music: AudioStreamPlayer
var sfx_volume := 0.8
var music_volume := 0.6


func _ready() -> void:
	for i in range(12):
		var p := AudioStreamPlayer.new()
		p.bus = "Master"
		add_child(p)
		_pool.append(p)
	_music = AudioStreamPlayer.new()
	add_child(_music)
	_clips["shoot"] = _synth(0.12, 900.0, 300.0, 0.15, 0.5)
	_clips["hit"] = _synth(0.08, 200.0, 90.0, 0.5, 0.6)
	_clips["die"] = _synth(0.25, 300.0, 60.0, 0.6, 0.6)
	_clips["pickup"] = _synth(0.10, 700.0, 1400.0, 0.0, 0.45)
	_clips["levelup"] = _synth(0.5, 440.0, 1320.0, 0.0, 0.5)
	_clips["hurt"] = _synth(0.18, 160.0, 70.0, 0.7, 0.7)
	_clips["boss"] = _synth(0.8, 90.0, 45.0, 0.4, 0.8)
	_clips["click"] = _synth(0.05, 1200.0, 900.0, 0.1, 0.4)
	_clips["explode"] = _synth(0.35, 150.0, 40.0, 0.9, 0.7)


func play(name_: String, pitch := 1.0) -> void:
	if not _clips.has(name_):
		return
	for p in _pool:
		if not p.playing:
			p.stream = _clips[name_]
			p.pitch_scale = pitch * randf_range(0.94, 1.06)
			p.volume_db = linear_to_db(clampf(sfx_volume, 0.001, 1.0))
			p.play()
			return


func start_music() -> void:
	if _music.stream == null:
		_music.stream = _make_music()
	_music.volume_db = linear_to_db(clampf(music_volume, 0.001, 1.0))
	if not _music.playing:
		_music.play()


func stop_music() -> void:
	_music.stop()


func set_volumes(sfx: float, mus: float) -> void:
	sfx_volume = sfx
	music_volume = mus
	_music.volume_db = linear_to_db(clampf(music_volume, 0.001, 1.0))


func _synth(dur: float, f0: float, f1: float, noise: float, vol: float) -> AudioStreamWAV:
	var n := int(dur * RATE)
	var data := PackedByteArray()
	data.resize(n * 2)
	var phase := 0.0
	for i in range(n):
		var t := float(i) / n
		var f := lerpf(f0, f1, t)
		phase += f / RATE
		var s := sin(phase * TAU) * (1.0 - noise) + (randf() * 2.0 - 1.0) * noise
		var env := (1.0 - t) * (1.0 - t)
		var v := int(clampf(s * env * vol, -1.0, 1.0) * 32000.0)
		data.encode_s16(i * 2, v)
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = RATE
	wav.stereo = false
	wav.data = data
	return wav


func _make_music() -> AudioStreamWAV:
	# Dark 8-bar minor-key loop: bass square + lead + noise hats.
	var bpm := 110.0
	var beat := 60.0 / bpm
	var bars := 8
	var n := int(bars * 4 * beat * RATE)
	var data := PackedByteArray()
	data.resize(n * 2)
	var bass_notes := [110.0, 110.0, 130.81, 98.0, 110.0, 110.0, 87.31, 98.0]
	var lead_notes := [220.0, 261.63, 293.66, 261.63, 220.0, 196.0, 174.61, 196.0, 220.0, 261.63, 329.63, 293.66, 261.63, 220.0, 196.0, 220.0]
	var pb := 0.0
	var pl := 0.0
	for i in range(n):
		var t := float(i) / RATE
		var beat_i := int(t / beat)
		var bar := beat_i / 4
		var bass_f: float = bass_notes[bar % bass_notes.size()]
		var lead_f: float = lead_notes[int(t / (beat * 0.5)) % lead_notes.size()]
		pb += bass_f / RATE
		pl += lead_f / RATE
		var s := 0.0
		s += (1.0 if fmod(pb, 1.0) < 0.5 else -1.0) * 0.16
		var lead_env := 1.0 - fmod(t, beat * 0.5) / (beat * 0.5)
		s += sin(pl * TAU) * 0.12 * lead_env
		var hat_t := fmod(t, beat * 0.5)
		if hat_t < 0.03:
			s += (randf() * 2.0 - 1.0) * 0.10 * (1.0 - hat_t / 0.03)
		var kick_t := fmod(t, beat)
		if kick_t < 0.09:
			s += sin(kick_t * 220.0 * (1.0 - kick_t * 6.0)) * 0.35 * (1.0 - kick_t / 0.09)
		data.encode_s16(i * 2, int(clampf(s, -1.0, 1.0) * 30000.0))
	var wav := AudioStreamWAV.new()
	wav.format = AudioStreamWAV.FORMAT_16_BITS
	wav.mix_rate = RATE
	wav.stereo = false
	wav.data = data
	wav.loop_mode = AudioStreamWAV.LOOP_FORWARD
	wav.loop_end = n
	return wav
