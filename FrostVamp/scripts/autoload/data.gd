extends Node
## Static game data: maps, enemies, bosses, weapons, passives.

const MAPS := [
	{
		"id": "graveyard", "name": "Graveyard",
		"ground": [Color(0.10, 0.11, 0.13), Color(0.13, 0.14, 0.17), Color(0.11, 0.13, 0.15)],
		"accent": Color(0.45, 0.62, 0.68),
		"enemies": ["grave_knight", "phantom_wraith", "crypt_guardian"],
		"bosses": ["crimson_inquisitor", "mirror_vampire"],
	},
	{
		"id": "village", "name": "Plague Village",
		"ground": [Color(0.14, 0.13, 0.10), Color(0.17, 0.16, 0.12), Color(0.12, 0.13, 0.10)],
		"accent": Color(0.45, 0.68, 0.30),
		"enemies": ["plague_bearer", "infected_brute", "plague_cultist"],
		"bosses": ["plague_necromancer", "mirror_vampire"],
	},
	{
		"id": "forest", "name": "Cursed Forest",
		"ground": [Color(0.08, 0.13, 0.09), Color(0.10, 0.16, 0.11), Color(0.07, 0.11, 0.08)],
		"accent": Color(0.35, 0.60, 0.35),
		"enemies": ["feral_direwolf", "cursed_bear", "wild_huntsman"],
		"bosses": ["alpha_werewolf", "mirror_vampire"],
	},
	{
		"id": "gothic", "name": "Gothic Ruins",
		"ground": [Color(0.13, 0.11, 0.15), Color(0.16, 0.13, 0.18), Color(0.11, 0.10, 0.13)],
		"accent": Color(0.62, 0.35, 0.55),
		"enemies": ["blood_sorcerer", "gargoyle", "corrupted_warrior"],
		"bosses": ["plague_necromancer", "mirror_vampire"],
	},
	{
		"id": "nyc", "name": "Dead City",
		"ground": [Color(0.15, 0.15, 0.16), Color(0.18, 0.18, 0.19), Color(0.13, 0.13, 0.14)],
		"accent": Color(0.85, 0.65, 0.20),
		"enemies": ["urban_enforcer", "riot_tank", "sniper_assassin"],
		"bosses": ["city_tyrant", "high_commando_maniac"],
	},
	{
		"id": "desert", "name": "Cursed Desert",
		"ground": [Color(0.28, 0.22, 0.13), Color(0.32, 0.25, 0.15), Color(0.25, 0.20, 0.12)],
		"accent": Color(0.80, 0.60, 0.30),
		"enemies": ["sand_wraith", "cursed_nomad", "shadow_serpent"],
		"bosses": ["desert_djinn", "mirror_vampire"],
	},
]

const ENEMIES := {
	"grave_knight": {"name": "Grave Knight", "hp": 34.0, "spd": 42.0, "dmg": 9.0, "xp": 1, "kind": "humanoid", "col": Color(0.55, 0.58, 0.62), "col2": Color(0.25, 0.26, 0.30)},
	"phantom_wraith": {"name": "Phantom Wraith", "hp": 18.0, "spd": 66.0, "dmg": 7.0, "xp": 1, "kind": "wraith", "col": Color(0.55, 0.80, 0.90), "col2": Color(0.30, 0.45, 0.55)},
	"crypt_guardian": {"name": "Crypt Guardian", "hp": 60.0, "spd": 30.0, "dmg": 12.0, "xp": 2, "kind": "humanoid", "col": Color(0.80, 0.78, 0.66), "col2": Color(0.45, 0.42, 0.34)},
	"plague_bearer": {"name": "Plague Bearer", "hp": 24.0, "spd": 46.0, "dmg": 8.0, "xp": 1, "kind": "humanoid", "col": Color(0.45, 0.60, 0.30), "col2": Color(0.25, 0.35, 0.16)},
	"infected_brute": {"name": "Infected Brute", "hp": 80.0, "spd": 28.0, "dmg": 15.0, "xp": 3, "kind": "brute", "col": Color(0.75, 0.50, 0.42), "col2": Color(0.45, 0.25, 0.22)},
	"plague_cultist": {"name": "Plague Cultist", "hp": 26.0, "spd": 50.0, "dmg": 9.0, "xp": 1, "kind": "wraith", "col": Color(0.22, 0.20, 0.24), "col2": Color(0.40, 0.60, 0.30)},
	"feral_direwolf": {"name": "Feral Direwolf", "hp": 22.0, "spd": 78.0, "dmg": 8.0, "xp": 1, "kind": "beast", "col": Color(0.30, 0.32, 0.38), "col2": Color(0.16, 0.17, 0.21)},
	"cursed_bear": {"name": "Cursed Bear", "hp": 95.0, "spd": 34.0, "dmg": 16.0, "xp": 3, "kind": "brute", "col": Color(0.30, 0.20, 0.16), "col2": Color(0.55, 0.15, 0.15)},
	"wild_huntsman": {"name": "Wild Huntsman", "hp": 30.0, "spd": 52.0, "dmg": 10.0, "xp": 2, "kind": "humanoid", "col": Color(0.50, 0.38, 0.26), "col2": Color(0.30, 0.24, 0.16)},
	"blood_sorcerer": {"name": "Blood Sorcerer", "hp": 28.0, "spd": 48.0, "dmg": 11.0, "xp": 2, "kind": "wraith", "col": Color(0.25, 0.10, 0.14), "col2": Color(0.75, 0.15, 0.22)},
	"gargoyle": {"name": "Gargoyle", "hp": 70.0, "spd": 40.0, "dmg": 13.0, "xp": 2, "kind": "beast", "col": Color(0.48, 0.48, 0.50), "col2": Color(0.28, 0.28, 0.32)},
	"corrupted_warrior": {"name": "Corrupted Warrior", "hp": 55.0, "spd": 44.0, "dmg": 12.0, "xp": 2, "kind": "humanoid", "col": Color(0.60, 0.16, 0.20), "col2": Color(0.32, 0.10, 0.12)},
	"urban_enforcer": {"name": "Urban Enforcer", "hp": 32.0, "spd": 50.0, "dmg": 10.0, "xp": 1, "kind": "humanoid", "col": Color(0.16, 0.17, 0.20), "col2": Color(0.30, 0.32, 0.36)},
	"riot_tank": {"name": "Riot Tank", "hp": 110.0, "spd": 26.0, "dmg": 17.0, "xp": 3, "kind": "brute", "col": Color(0.24, 0.26, 0.32), "col2": Color(0.50, 0.52, 0.58)},
	"sniper_assassin": {"name": "Sniper Assassin", "hp": 26.0, "spd": 58.0, "dmg": 12.0, "xp": 2, "kind": "humanoid", "col": Color(0.28, 0.34, 0.24), "col2": Color(0.18, 0.22, 0.15)},
	"sand_wraith": {"name": "Sand Wraith", "hp": 24.0, "spd": 62.0, "dmg": 9.0, "xp": 1, "kind": "wraith", "col": Color(0.25, 0.22, 0.30), "col2": Color(0.50, 0.42, 0.55)},
	"cursed_nomad": {"name": "Cursed Nomad", "hp": 40.0, "spd": 46.0, "dmg": 11.0, "xp": 2, "kind": "humanoid", "col": Color(0.70, 0.55, 0.35), "col2": Color(0.45, 0.32, 0.20)},
	"shadow_serpent": {"name": "Shadow Serpent", "hp": 65.0, "spd": 55.0, "dmg": 14.0, "xp": 3, "kind": "serpent", "col": Color(0.15, 0.17, 0.24), "col2": Color(0.35, 0.65, 0.80)},
}

const BOSSES := {
	"crimson_inquisitor": {"name": "The Crimson Inquisitor", "hp": 1400.0, "spd": 46.0, "dmg": 24.0, "xp": 40, "kind": "humanoid", "col": Color(0.72, 0.14, 0.16), "col2": Color(0.55, 0.55, 0.60), "attack": "volley"},
	"plague_necromancer": {"name": "Plague Necromancer", "hp": 1200.0, "spd": 40.0, "dmg": 20.0, "xp": 40, "kind": "wraith", "col": Color(0.35, 0.48, 0.24), "col2": Color(0.20, 0.28, 0.14), "attack": "summon"},
	"alpha_werewolf": {"name": "Alpha Werewolf", "hp": 1500.0, "spd": 72.0, "dmg": 26.0, "xp": 40, "kind": "beast", "col": Color(0.20, 0.21, 0.26), "col2": Color(0.55, 0.12, 0.12), "attack": "charge"},
	"mirror_vampire": {"name": "Mirror Vampire", "hp": 2600.0, "spd": 52.0, "dmg": 28.0, "xp": 80, "kind": "humanoid", "col": Color(0.16, 0.15, 0.18), "col2": Color(0.80, 0.85, 0.90), "attack": "mirror"},
	"city_tyrant": {"name": "City Tyrant", "hp": 1600.0, "spd": 38.0, "dmg": 24.0, "xp": 40, "kind": "brute", "col": Color(0.35, 0.38, 0.28), "col2": Color(0.20, 0.22, 0.16), "attack": "volley"},
	"high_commando_maniac": {"name": "High Commando Maniac", "hp": 2400.0, "spd": 56.0, "dmg": 26.0, "xp": 80, "kind": "humanoid", "col": Color(0.48, 0.42, 0.30), "col2": Color(0.60, 0.15, 0.15), "attack": "charge"},
	"desert_djinn": {"name": "Desert Djinn", "hp": 1400.0, "spd": 60.0, "dmg": 22.0, "xp": 40, "kind": "wraith", "col": Color(0.22, 0.18, 0.30), "col2": Color(0.55, 0.30, 0.75), "attack": "volley"},
}

## beh: straight | boomerang | orbit | pool | ring | burst | melee
const WEAPONS := {
	"icicle_spike": {"name": "Icicle Spike", "beh": "straight", "dmg": 12.0, "cd": 1.0, "count": 1, "speed": 460.0, "pierce": 1, "life": 1.4, "size": 1.0, "desc": "Frozen blood spike fired at the nearest foe."},
	"frozen_blood_scythe": {"name": "Frozen-Blood Scythe", "beh": "boomerang", "dmg": 18.0, "cd": 1.7, "count": 1, "speed": 380.0, "pierce": 99, "life": 1.6, "size": 1.3, "desc": "A returning scythe of crystallized blood."},
	"frozen_blood_spear": {"name": "Frozen-Blood Spear", "beh": "straight", "dmg": 20.0, "cd": 1.6, "count": 1, "speed": 620.0, "pierce": 6, "life": 1.1, "size": 1.2, "desc": "Piercing spear that skewers a whole line."},
	"bone_lance": {"name": "Bone Lance", "beh": "straight", "dmg": 26.0, "cd": 2.0, "count": 1, "speed": 540.0, "pierce": 10, "life": 1.3, "size": 1.3, "desc": "Heavy lance thrown where you face."},
	"shard_bolts": {"name": "Shard Bolts", "beh": "straight", "dmg": 7.0, "cd": 0.9, "count": 3, "speed": 500.0, "pierce": 0, "life": 1.0, "size": 0.8, "desc": "A spread of razor blood-ice shards."},
	"spinning_disc": {"name": "Spinning Disc", "beh": "orbit", "dmg": 10.0, "cd": 3.2, "count": 2, "speed": 3.2, "pierce": 99, "life": 2.6, "size": 1.1, "desc": "Discs of frost-blood orbit around you."},
	"frozen_blood_orb": {"name": "Frozen-Blood Orb", "beh": "straight", "dmg": 30.0, "cd": 2.4, "count": 1, "speed": 220.0, "pierce": 99, "life": 2.6, "size": 1.6, "desc": "A slow crushing orb that pierces everything."},
	"explosion_burst": {"name": "Explosion Burst", "beh": "burst", "dmg": 24.0, "cd": 2.2, "count": 1, "speed": 0.0, "pierce": 99, "life": 0.45, "size": 1.6, "desc": "Detonates frozen blood under a random foe."},
	"blood_pool": {"name": "Blood Pool", "beh": "pool", "dmg": 6.0, "cd": 3.0, "count": 1, "speed": 0.0, "pierce": 99, "life": 3.5, "size": 1.7, "desc": "A freezing pool that burns foes standing in it."},
	"shadow_scythe": {"name": "Shadow Scythe", "beh": "straight", "dmg": 12.0, "cd": 1.4, "count": 4, "speed": 430.0, "pierce": 2, "life": 1.2, "size": 1.0, "desc": "Dark stars thrown in four directions."},
	"bite_dash": {"name": "Bite Dash", "beh": "melee", "dmg": 22.0, "cd": 1.5, "count": 1, "speed": 0.0, "pierce": 99, "life": 0.3, "size": 1.5, "desc": "Fanged lunge that mauls everything ahead."},
	"blood_bolt": {"name": "Blood Bolt", "beh": "straight", "dmg": 9.0, "cd": 0.5, "count": 1, "speed": 640.0, "pierce": 0, "life": 1.0, "size": 0.9, "desc": "Rapid-fire bolts of frozen blood."},
	"crescent_blade": {"name": "Crescent Blade", "beh": "boomerang", "dmg": 14.0, "cd": 1.3, "count": 1, "speed": 420.0, "pierce": 99, "life": 1.4, "size": 1.1, "desc": "A crescent that carves out and returns."},
	"throwing_stars": {"name": "Throwing Stars", "beh": "straight", "dmg": 10.0, "cd": 1.1, "count": 3, "speed": 520.0, "pierce": 1, "life": 1.1, "size": 0.9, "desc": "Stars hurled at several foes at once."},
	"nova_ring": {"name": "Nova Ring", "beh": "ring", "dmg": 16.0, "cd": 2.6, "count": 1, "speed": 300.0, "pierce": 99, "life": 0.9, "size": 1.0, "desc": "An expanding ring of blood-frost."},
}

const PASSIVES := {
	"vigor": {"name": "Vampiric Vigor", "desc": "+20 Max HP per rank.", "max": 5},
	"haste": {"name": "Bat Haste", "desc": "+8% move speed per rank.", "max": 5},
	"might": {"name": "Crimson Might", "desc": "+10% damage per rank.", "max": 5},
	"magnet": {"name": "Blood Call", "desc": "+30% pickup range per rank.", "max": 5},
	"frost": {"name": "Deep Frost", "desc": "-6% weapon cooldowns per rank.", "max": 5},
}

const STARTING_WEAPON := "icicle_spike"
const MAX_WEAPONS := 6
const MAX_WEAPON_LEVEL := 5
const RUN_LENGTH := 900.0
const MID_BOSS_TIME := 300.0
const BOSS2_TIME := 600.0
const FINAL_BOSS_TIME := 840.0


static func upgraded(base: Dictionary, level: int) -> Dictionary:
	var w := base.duplicate()
	var l := level - 1
	w["dmg"] = base["dmg"] * (1.0 + 0.30 * l)
	w["cd"] = base["cd"] * pow(0.92, l)
	w["size"] = base["size"] * (1.0 + 0.10 * l)
	if l >= 2:
		w["count"] = int(base["count"]) + 1
	if l >= 4:
		w["count"] = int(base["count"]) + 2
	return w
