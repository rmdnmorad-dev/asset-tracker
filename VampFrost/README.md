# VampFrost — complete Unity 6 project

A dark, frozen-blood **Vampire-Survivors-style** game, built entirely in code.
Runs with **zero asset files**: every sprite has a generated pixel placeholder and
**100% of the audio (music + SFX + ambience) is synthesized at runtime** — no audio
files, no copyrighted material, fully Steam-safe.

---

## 1 · Quick start (2 minutes)

1. Create a new Unity **6 (LTS)** project — template **Universal 2D** (or 2D Core).
2. `Edit → Project Settings → Player → Other Settings → Active Input Handling` → **Both**
   (or *Input Manager (Old)*). This project uses the classic Input API.
3. Copy the `Assets/` folder from this package into your project (merge).
4. Open any **empty scene** and press **Play**. That's it — `Bootstrap` auto-creates
   the camera, UI, audio and game. (You can also drop the `Bootstrap` component on an
   empty GameObject if you prefer something visible in the hierarchy.)

Build settings: add your scene, build for Windows/macOS/Linux as usual.

### Controls
| Input | Action |
|---|---|
| **WASD / Arrows** | Move |
| **Space / L-Shift** | Dash (i-frames; fuels *Bite Dash*) |
| **Q** | Invisibility (3 s, enemies lose you) |
| **Esc** | Pause |

---

## 2 · What's inside

| System | Details |
|---|---|
| **6 maps** | Graveyard · Plague Village · Cursed Forest · Gothic Cathedral · NYC Streets · Arab Desert — infinite chunked ground + themed decor |
| **18 enemies** | 3 per map, melee + ranged (telegraphed shots), elites every 5th wave |
| **7 bosses** | Crimson Inquisitor, Plague Necromancer, Alpha Werewolf, Mirror Vampire, City Tyrant, High Commando Maniac (NYC wave-15 mini), Desert Djinn — each with **3 escalating phases** (charge / radial / burst / summon / teleport / ring / AoE patterns) |
| **15 weapons** | Icicle Spike, Frozen-Blood Scythe, Frozen-Blood Spear, Bone Lance, Shard Bolts, Spinning Disc, Frozen-Blood Orb, Explosion Burst, Blood Pool, Shadow Scythe, Bite Dash, Blood Bolt, Crescent Blade, Throwing Stars, Nova Ring — 8 levels each, up to 6 equipped |
| **Frost & Blood** | Frost weapons stack slow → **freeze**; Blood weapons **lifesteal** |
| **Progression** | 20 timed waves (45 s each) → final boss. XP gems, level-up cards (weapons + 13 passives), chests, gold, health drops |
| **UI** | Code-built HUD (HP/XP/boss bars, timer, wave, gold, cooldowns), main menu, map select w/ best-wave records, pause, settings, victory/defeat |
| **Persistence** | JSON save: audio settings, total gold, best wave per map (`persistentDataPath/vampfrost_save.json`) |
| **Procedural audio** | See §4 — the full spec implementation |

Performance: object pooling for enemies, projectiles, gems, gold, damage numbers and
audio sources; all audio clips pre-generated & cached; no physics engine (fast manual
distance checks); comfortably 60 FPS with 200+ enemies.

---

## 3 · Dropping in your real sprite sheets

The game runs on generated placeholders. To use the VampFrost art:

1. Put PNGs in **`Assets/Resources/Sprites/`**.
2. Slice sheets with the **Sprite Editor** (Sprite Mode = Multiple → Slice → Automatic),
   then export/rename individual sprites, **or** save single sprites directly with the
   names below (single-sprite files need no slicing).
3. Run **`VampFrost → Fix Sprite Import Settings`** (menu bar) — sets Point filter,
   no compression, PPU 32 (Player = 128).

| Resources name | Used for |
|---|---|
| `Player` | the 256×256 main character |
| `Chest` | reward chest |
| `mob_<Key>` | e.g. `mob_GraveKnight`, `mob_PhantomWraith`, `mob_CryptGuardian`, `mob_PlagueBearer`, `mob_InfectedBrute`, `mob_PlagueCultist`, `mob_FeralDirewolf`, `mob_CursedBear`, `mob_WildHuntsman`, `mob_BloodSorcerer`, `mob_Gargoyle`, `mob_CorruptedWarrior`, `mob_UrbanEnforcer`, `mob_RiotTank`, `mob_SniperAssassin`, `mob_SandWraith`, `mob_CursedNomad`, `mob_ShadowSerpent` |
| `boss_<Key>` | `boss_CrimsonInquisitor`, `boss_PlagueNecromancer`, `boss_AlphaWerewolf`, `boss_MirrorVampire`, `boss_CityTyrant`, `boss_HighCommandoManiac`, `boss_DesertDjinn` |
| `tile_<theme>_0 … _3` | ground tiles; themes: `graveyard, village, forest, ruins, city, desert` |
| `deco_<theme>_0 … _5` | decorations per theme (same theme names) |

Anything missing simply keeps its placeholder — mix and match freely.

---

## 4 · Procedural Audio System (spec deliverables)

Everything below is generated in-engine with `AudioClip.Create` — **no external audio
files, no copyrighted music, no sound packs.**

### Deliverables → files
| Spec deliverable | File |
|---|---|
| AudioManager (mixer routing, pooling, events, settings) | `Scripts/Audio/AudioManager.cs` |
| ProceduralMusicSystem (4-layer adaptive music) | `Scripts/Audio/ProceduralMusicSystem.cs` |
| ProceduralSFXGenerator (all SFX + synth toolkit) | `Scripts/Audio/ProceduralSFXGenerator.cs` |
| Environmental audio (per-map ambience) | `Scripts/Audio/EnvironmentAudio.cs` |
| Event-system integration | `Scripts/Core/GameEvents.cs` (+ subscriptions in AudioManager) |
| AudioMixer setup instructions | §4.4 below |

### 4.1 Adaptive music — 4 synchronized layers
Loops are sample-exact (100 BPM, 8 bars = 19.2 s; drone 20 s with loop-exact partials):

| Layer | Content | Driven by |
|---|---|---|
| **1 · Ambient drone** | low A-minor pad (27.5–164 Hz partials, slow LFOs) + airy noise bed | always on |
| **2 · Rhythm** | kick pulse + ghost off-beat hats | intensity > 0.10 |
| **3 · Combat** | snares, 16th hats, syncopated toms | nearby enemy density |
| **4 · Boss** | gated saw power-stack + vibrato tension lead, soft-clipped | boss fights only, volume per phase |

Layers 2–4 are started with `PlayScheduled` on the same DSP time and share **one pitch
group**, so tempo scaling (`1 + intensity·0.22`) never desyncs them.

### 4.2 Intensity Controller (0 → 1)
`AudioManager.Update` computes:
`0.5·enemies/45 + 0.3·near/12 + 0.25·(1−HP) + 0.35·boss + 0.08·wave` (smoothed,
rises fast / falls slow). It drives layer volumes, tempo/pitch, and the filter rack:

* **Low-pass** sweeps down to 700 Hz as HP drops (muffled near-death).
* **High-pass** up to 230 Hz during chaos (intensity > 0.85).
* **Distortion** at low HP and in boss **phase 3**.
* **Reverb preset** per map (Cathedral = Cave, Forest = Forest, NYC = City…) and
  **Hangar** during boss fights.
* Boss **phase transitions**: 0.7 s swell (volume + pitch rise) → **0.32 s sudden
  silence** → new phase mix slams back in.
* Pause: music keeps playing, dimmed + low-passed (`ignoreListenerPause`), SFX freeze
  (`AudioListener.pause`), UI sounds unaffected.

### 4.3 Synthesized SFX (31 types, cached at startup)
Sine/square/triangle/saw/noise/FM + envelopes, one-pole filters, soft-clip — footsteps,
dash sweep, invisibility fade, hurt, death collapse, per-weapon-pitched fire, impacts,
crit spike, crystal **freeze** shimmer, enemy ticks/telegraphs/deaths (3 variants),
layered boss **roar**, phase sweep, bass **heavy hits** (synced with screen-shake), the
full UI set (hover/click/confirm/cancel/error/open/close/notify), level-up arpeggio,
chest chime, coin, XP blip, heal, wave sting, game-over sting, victory chord.
Playback goes through an **18-source pool** with pitch jitter and per-sound throttling
(no spam, no per-frame allocations).

### 4.4 AudioMixer setup (optional)
The game ships on a direct-volume path. To route through a mixer instead:

1. `Assets/Resources/Audio` → **Create → Audio Mixer**, name it **`VampFrostMixer`**.
2. Under **Master**, add child groups **`Music`**, **`SFX`**, **`UI`**.
3. Right-click each group's *Volume* → **Expose**; in the Exposed Parameters dropdown
   rename them to **`MasterVol`**, **`MusicVol`**, **`SFXVol`**, **`UIVol`**.
4. Press Play — `AudioManager` auto-detects the mixer, routes every source into the
   groups and drives the exposed params (in dB) from the settings sliders.

### 4.5 Event bus (audio never called directly)
`OnEnemySpawn/Death/Tick/Telegraph · OnBossSpawn/PhaseChange/Heavy/Death ·
OnPlayerDamage/Death/Dash/Invisibility/Footstep · OnLevelUp/XPGained/GoldGained/
HealthPickup · OnWeaponFire/Hit/FreezeApplied/Explosion/ChestOpen ·
OnWaveStart/End · OnRunStart/End · OnVictory/GameOver/Pause ·
OnUIHover/Click/Confirm/Cancel/Error/Open/Close/Notify`

---

## 5 · Project layout

```
Assets/
├─ Resources/
│  ├─ Sprites/        ← drop your art here (optional)
│  └─ Audio/          ← optional VampFrostMixer
└─ VampFrost/Scripts/
   ├─ Core/     Bootstrap · GameManager · GameEvents · CameraRig+FX · SaveSystem · SpriteFactory
   ├─ World/    MapDefs · MapGenerator
   ├─ Player/   PlayerController (+Stats, PlayerXP)
   ├─ Combat/   WeaponDefs · WeaponSystem · Projectile · Pickups
   ├─ Enemies/  EnemyDefs · Enemy · Boss · WaveSpawner
   ├─ UI/       UIBuilder · HUD · Menus · LevelUpPanel (+Upgrades)
   ├─ Audio/    AudioManager · ProceduralMusicSystem · ProceduralSFXGenerator · EnvironmentAudio
   └─ Editor/   VampFrostEditorTools (sprite import fixer, save utilities)
```

---

## 6 · Troubleshooting

* **`InvalidOperationException: You are trying to read Input using the UnityEngine.Input class...`**
  → set *Active Input Handling* to **Both** (step 2 of Quick start).
* **No UI clicks** → same fix (the EventSystem uses `StandaloneInputModule`).
* **Sprites blurry after import** → run `VampFrost → Fix Sprite Import Settings`.
* **Reset progress** → `VampFrost → Delete Save File`.
* First Play generates all audio (~a second of log spam) — subsequent runs reuse the cache.

Everything here is original generated content — safe for a Steam release.
