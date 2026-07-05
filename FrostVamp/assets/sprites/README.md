# FrostVamp sprite drop-in folder

The game looks for `assets/sprites/<key>.png` for every sprite it draws.
**Any file found here is used automatically; anything missing is replaced by a
generated placeholder at runtime**, so the game always runs.

Slice your asset sheets into individual transparent PNGs with these exact names:

## Character (32x32 or larger)
| file | sheet source |
|---|---|
| `player.png` | the vampire character portrait sheet |

## Enemies (32x32) — from the biome enemy sheet
`grave_knight.png`, `phantom_wraith.png`, `crypt_guardian.png`,
`plague_bearer.png`, `infected_brute.png`, `plague_cultist.png`,
`feral_direwolf.png`, `cursed_bear.png`, `wild_huntsman.png`,
`blood_sorcerer.png`, `gargoyle.png`, `corrupted_warrior.png`,
`urban_enforcer.png`, `riot_tank.png`, `sniper_assassin.png`,
`sand_wraith.png`, `cursed_nomad.png`, `shadow_serpent.png`

## Bosses (56x56 or larger) — from the boss sheet
`crimson_inquisitor.png`, `plague_necromancer.png`, `alpha_werewolf.png`,
`mirror_vampire.png`, `city_tyrant.png`, `high_commando_maniac.png`,
`desert_djinn.png`

## Weapons / projectiles (24x24, drawn pointing RIGHT) — from the 'VampFrost' weapon sheet
`proj_icicle_spike.png`, `proj_frozen_blood_scythe.png`, `proj_frozen_blood_spear.png`,
`proj_bone_lance.png`, `proj_shard_bolts.png`, `proj_spinning_disc.png`,
`proj_frozen_blood_orb.png`, `proj_explosion_burst.png`, `proj_blood_pool.png`,
`proj_shadow_scythe.png`, `proj_bite_dash.png`, `proj_blood_bolt.png`,
`proj_crescent_blade.png`, `proj_throwing_stars.png`, `proj_nova_ring.png`

## Pickups (16x16)
`gem.png`, `gold.png`, `heart.png`, `chest.png`

## Ground decor (16x16, optional) — from the urban/tileset sheet
`decor_<map>_<n>.png` where map is one of
`graveyard, village, forest, gothic, nyc, desert` and n is `0..5`,
e.g. `decor_nyc_0.png`, `decor_graveyard_3.png`.

After dropping files in, re-run the game (or re-export) — no code changes needed.
