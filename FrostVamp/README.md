# FrostVamp

A complete Vampire-Survivors-style action roguelite built in **Godot 4.3**.
You are **Vlad Frostblood**, a vampire who freezes his own blood into weapons —
survive 15 minutes of escalating hordes, level up, and slay the bosses.

This is a native desktop game (Windows `.exe` / Linux binary), not an HTML game.

## Play / develop

1. Install [Godot 4.3](https://godotengine.org/download) (standard build, ~50 MB).
2. Open this folder (`FrostVamp/project.godot`) in Godot and press **F5**.

Controls: **WASD / arrows** to move, **Esc / P** to pause. Everything else is automatic —
weapons auto-fire, walk over gems/gold/hearts to collect.

## What's in the game

- **6 maps** with their own palettes, decor and enemy rosters:
  Graveyard, Plague Village, Cursed Forest, Gothic Ruins, Dead City, Cursed Desert.
  Winning a map unlocks the next. Progress and gold are saved.
- **18 enemy types + 7 bosses** (The Crimson Inquisitor, Plague Necromancer,
  Alpha Werewolf, Mirror Vampire, City Tyrant, High Commando Maniac, Desert Djinn)
  with distinct boss attack patterns (radial volleys, summons, charges, mirror teleports).
  Bosses arrive at 5:00, 10:00, and the final boss at 14:00 — kill it or survive to 15:00 to win.
- **15 frozen-blood weapons** from the VampFrost weapon sheet — Icicle Spike,
  Frozen-Blood Scythe/Spear/Orb, Bone Lance, Shard Bolts, Spinning Disc,
  Explosion Burst, Blood Pool, Shadow Scythe, Bite Dash, Blood Bolt,
  Crescent Blade, Throwing Stars, Nova Ring — each with 5 upgrade levels,
  plus 5 passive items. Max 6 weapons per run.
- **Everything is animated**: walk-bob and flip on every creature, hurt flashes,
  death pops, floating damage numbers, spinning/expanding/returning projectiles,
  magnetized pickups, camera shake, animated menus with snow and blood-mist particles.
- **Full UI**: title menu with animated hero, map select with best times and unlocks,
  options (volume, fullscreen), HUD (HP/XP bars, timer, kills, gold, weapon loadout,
  boss health bar), level-up choice cards, pause menu, victory/defeat screens.
- **Procedural audio**: all SFX and the music loop are synthesized at startup — the
  game has sound with zero audio files shipped.

## Art assets

All sprites load from `assets/sprites/<key>.png` **if present**, with generated
pixel-art placeholders as fallback, so the project runs before any art is added.
See [`assets/sprites/README.md`](assets/sprites/README.md) for the exact file
names to slice the asset sheets into.

## Automated builds (CI)

`.github/workflows/frostvamp-build.yml` runs on every push touching `FrostVamp/`:

1. Boots the menu headless and fails on any script error.
2. Runs 10 seconds of real gameplay headless (autotest mode) and fails on any script error.
3. Exports **Windows** and **Linux** release builds and uploads them as artifacts.

Download `FrostVamp-windows` from the workflow run — it contains a self-contained
`FrostVamp.exe` (pck embedded).

## Shipping to Steam

The exported build is Steam-ready as-is (Steam does not require any SDK integration
to launch a game). Checklist:

1. Create the app in [Steamworks](https://partner.steamgames.com/), pay the app fee,
   fill in the store page.
2. Create one depot per OS; upload `build/windows/` (and optionally `build/linux/`)
   with `steamcmd` + `app_build`/`depot_build` scripts, or the Steamworks web uploader.
3. Set the launch option to `FrostVamp.exe`.
4. Optional (achievements/overlay extras): add [GodotSteam](https://godotsteam.com/)
   (a drop-in Godot build with Steamworks bindings) and ship `steam_api64.dll` +
   a `steam_appid.txt` next to the exe.
5. Push your depot to the default branch and press **Release**.

## Code layout

```
scripts/autoload/data.gd  — all balance data: maps, enemies, bosses, weapons, passives
scripts/autoload/art.gd   — sprite loading + procedural placeholder generation
scripts/autoload/sfx.gd   — synthesized SFX + looping music
scripts/autoload/g.gd     — input map, save/load, meta progression
scripts/main.gd           — title / map select / options menus
scripts/run.gd            — one survival run: spawner, bosses, drops, level-ups
scripts/player.gd         — movement, stats, auto-firing weapon system
scripts/enemy.gd          — enemies and bosses (attack patterns)
scripts/projectile.gd     — all weapon behaviors (straight/orbit/boomerang/pool/ring/burst/melee)
scripts/pickup.gd         — gems, gold, hearts, chests
scripts/ground.gd         — infinite procedural scrolling ground
scripts/hud.gd            — in-run UI and panels
```
