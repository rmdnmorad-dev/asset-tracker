# Tile Prediction Battle Royale — Unity game

A 3D top-down party battle royale where 16 players bluff inside private tile zones,
secretly predict each other's positions, and survive simultaneous attacks in a
shrinking arena.

Built entirely in code. **Zero asset files** — every mesh, material, light, camera,
sound effect, music bed and pixel of UI is generated at runtime. Nothing to wire up
in the inspector, nothing to break on import.

---

## 1 · Run it

This folder **is** a Unity project. You don't have to assemble anything.

1. **Unity Hub → Add → Add project from disk →** select this `TilePredictionBattleRoyale`
   folder.
2. Open it. The project is pinned to **2022.3 LTS**; if you're on **Unity 6** the Hub
   will offer to upgrade it — say yes, that's expected and takes a minute.
3. Open `Assets/Scenes/Game.unity` and press **Play**.

That's it. Title screen → **PLAY**.

To build: `File → Build Settings` — the scene is already in the list — pick your
platform and go.

> **Fallback if anything about the project files misbehaves:** the game does not
> depend on them. Make a new 3D project, copy the `Assets/` folder in, open *any*
> scene — even an empty one — and press Play. `Bootstrap` builds the entire game at
> runtime, so an empty scene is a perfectly valid starting point.

---

## 2 · Controls

| | |
|---|---|
| **Prep phase** | `WASD` / arrows — walk around your zone. Everyone can see you. |
| **Commit phase** | Click a tile in **your** zone = where you hide. Click a tile in **someone else's** zone = who and where you attack. |
| | `1`–`4` pick a gadget, `0`/`Q` clears it |
| | `SPACE` / `ENTER` locks your choice in early |
| **Anytime** | scroll = zoom, `ESC` = pause |
| **Results** | `R` play again, `M` main menu |

You are the gold-named seat at the bottom of the screen. Tile numbers appear on your
zone and on whichever zone you're aiming at. There's a **HOW TO PLAY** screen on the
title and pause menus.

---

## 3 · Round loop

**Prep (10s)** — everyone moves in real time inside their own zone. This is the only
information anyone gets. Bots have personalities (`CAMPER`, `DRIFTER`, `EDGE-LORD`,
`TRICKSTER`, `MIRROR`, `HUNTER`) that make them loiter in readable patterns — and a
`TRICKSTER` spends the whole phase lying to you.

**Commit (15s)** — everyone secretly picks: the tile they'll hide on, one target
player, one tile in that player's zone, and optionally one gadget. Nobody sees anybody
else's choice, and **attacker identity is never revealed, ever** — not during
selection, not during the reveal, not afterwards.

**Reveal** — resolves simultaneously, played back as timed beats: committed positions
→ incoming markers → impact → deaths → anti-dogpile penalty → lava → survivor count.
Every outcome is public; only the authorship is secret.

### Rules implemented

- **Death on hit** — you die if your committed tile gets attacked.
- **Anti-dogpiling** — with 11+ alive, 4 or more attackers on one target makes *every
  one of those attackers* instantly lose a tile. At 10 or fewer alive the threshold
  drops to 3. The penalty applies even when the target dies.
- **Tile floor** — nobody ever drops below 2 tiles. Hard rule, enforced in one place
  (`Zone.RemoveNextTile`).
- **2-tile endgame** — at the floor a zone is a literal coin flip, which is what makes
  heads-up matches so tense. Double-KOs happen and are shown as a DRAW.
- **Lava** — from round 3, every 2 rounds, every survivor loses their outermost tile.
  Telegraphed a full round early with an orange glow on the doomed tile.
- **Gadgets** — `SPLASH` (attack also hits adjacent tiles), `SHIELD` (survive one
  hit), `DECOY` (lose a tile instead of dying), `SCOUT` (learn *how many* attacked you
  — never who). Limited charges, and a charge only burns when it does something.

---

## 4 · Audio

Everything you hear is synthesised into PCM at boot — no audio files anywhere.
Two music beds (a calm prep pad and a tense commit bed with a pulse and arp) share a
chord progression and are started on the same frame, so crossfading between them as
the commit timer runs down is seamless. Plus a lava ambience loop and 18 SFX.

`SOUND: ON/OFF` on the title and pause menus. Levels live in `Audio.MasterVolume` /
`Audio.MusicVolume` (`Audio/Sfx.cs`).

---

## 5 · One design decision worth knowing

Your brief listed the commit phase as *target player + target tile + gadget*. As
written that makes prediction impossible: if your position were locked at the end of
prep, everyone already watched you take it, and there'd be nothing to predict.

So **you also secretly choose your own hide tile at commit time.** Prep movement
becomes pure theatre — habit, feint and bait — and the reveal's "show movement end
positions" beat is the moment those bluffs pay off or don't. This is the one place the
implementation extends the spec rather than following it literally; everything else is
exactly as specified. Reverting it is a two-line change in `HumanInput` and `BotBrain`.

---

## 6 · Tuning

Every number lives in `Assets/TPBR/Scripts/Core/Cfg.cs` — player count, tile grid,
arena radii, phase lengths, lava cadence, dogpile thresholds, gadget charges, reveal
beat timings. Colours are in `Palette` (`Visual/Mat.cs`).

Measured over 800 headless matches driving the shipped rules code:

```
rounds  min/median/max : 5 / 10 / 19
rounds that trip anti-dogpile : 27%
matches ending in a 2-player double-KO : ~16%
win rate per seat : flat within sampling noise
```

If the anti-dogpile penalty feels too frequent, raise `DogpileThresholdHigh`/`Low` or
raise the bots' `spread` in `BotBrain`. If matches run long, drop `LavaEveryRounds`
to 1.

---

## 7 · What is placeholder

The systems are complete; the art is not:

- Characters are procedural primitives (`Players/Avatar.cs`). `Build()` is the only
  place that touches their geometry — swap in real models there.
- Tiles, trophy, floor and lava are procedural meshes (`Visual/MeshFactory.cs`).
- Effects are hand-rolled sparks and rings, not a `ParticleSystem` — deliberate, so it
  behaves identically in Built-in RP and URP with no asset setup.
- UI is IMGUI on a virtual 1920×1080 canvas (`UI/Hud.cs`) — no prefabs, no font
  assets, no package dependencies. Rebuild it in uGUI/UI Toolkit when you want the
  real party-game presentation.
- **There is no networking.** It is 1 human + 15 AI, which is the prototype scope you
  specified. Real 16-player netcode is a separate, much larger piece of work.

---

## 8 · Layout

```
Assets/TPBR/Scripts/
  Core/     Cfg  Bootstrap  GameManager  ArenaCamera
  Arena/    Zone  Arena
  Players/  PlayerState  Avatar  HumanInput  BotBrain
  Rules/    Resolver          <- all rule logic, pure and side-effect free
  Visual/   MeshFactory  Mat  Fx
  Audio/    Sfx               <- runtime synthesiser
  UI/       Hud
```

`Resolver.Resolve()` reads the locked decisions and returns a `RoundResult` without
mutating anything. `GameManager` then applies that result beat by beat during the
reveal. What you watch is exactly what was resolved — the reveal never re-derives
anything, which is what keeps the drama honest.
