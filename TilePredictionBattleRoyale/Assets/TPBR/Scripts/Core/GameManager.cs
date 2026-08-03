using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    public enum Phase { Menu, Prep, Decision, Reveal, GameOver }

    /// Which full-screen UI layer is up. Orthogonal to Phase: the match can be
    /// mid-reveal while the pause screen is showing over the top of it.
    public enum UiScreen { Title, HowTo, Playing, Paused }

    /// Round loop, phase timing and every state mutation in the match.
    /// Rules live in <see cref="Resolver"/>; this class only applies them.
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;

        public Arena arena;
        public readonly List<PlayerState> players = new List<PlayerState>();
        public readonly List<BotBrain> bots = new List<BotBrain>();
        public HumanInput human;

        public Phase phase = Phase.Menu;
        public UiScreen screen = UiScreen.Title;
        public int round;
        public float phaseTimer, phaseLength;
        public bool lavaThisRound;
        public RoundResult lastResult;

        public string banner = "";
        public Color bannerColor = Color.white;
        public float bannerUntil;

        public string revealCaption = "";
        public int scoutReveal = -1;
        public readonly List<string> feed = new List<string>();

        bool looksDirty = true;
        int lastTickSecond = -1;
        UiScreen howToReturnTo = UiScreen.Title;
        Coroutine matchRoutine, idleRoutine;

        static readonly string[] Names =
        {
            "VIPER","ROOK","MOTH","CINDER","JUDE","OKRA","PIXEL","BASIL",
            "YOU","NOMAD","QUARTZ","FLINT","WREN","TALLY","HUSK","ZEPHYR"
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake() { I = this; }

        // ------------------------------------------------------------------ setup

        public void Begin()
        {
            arena.IsAlive = IsAlive;

            for (int i = 0; i < Cfg.PlayerCount; i++)
            {
                bool isHuman = i == Cfg.HumanIndex;
                var p = new PlayerState(i, isHuman ? "YOU" : Names[i % Names.Length], isHuman);
                p.zone = arena.zones[i];
                p.avatar = Avatar.Create(transform, i, p.color);
                p.avatar.Warp(p.zone.CenterOf(0));
                players.Add(p);

                if (isHuman) human = new HumanInput(p, arena);
                else bots.Add(new BotBrain(p));
            }

            phase = Phase.Menu;
            screen = UiScreen.Title;
            Audio.SetIntensity(0f);
            if (ArenaCamera.I != null) ArenaCamera.I.orbit = true;
            idleRoutine = StartCoroutine(MenuIdle());
        }

        public bool IsAlive(int index)
        {
            return index >= 0 && index < players.Count && players[index].alive;
        }

        /// Idles the arena behind the title screen so the menu is not a still frame.
        IEnumerator MenuIdle()
        {
            while (true)
            {
                float dt = Time.deltaTime;
                for (int i = 0; i < bots.Count; i++)
                {
                    var b = bots[i];
                    if (b.me.avatar == null) continue;
                    b.me.avatar.MoveTo(b.TickPrep(dt, b.me.avatar.transform.position), dt);
                }
                if (human != null && human.me.avatar != null)
                {
                    var hp = human.me;
                    hp.avatar.MoveTo(hp.zone.Clamp(hp.avatar.transform.position), dt);
                }
                yield return null;
            }
        }

        public void StartMatch()
        {
            if (matchRoutine != null) return;
            if (idleRoutine != null) { StopCoroutine(idleRoutine); idleRoutine = null; }
            screen = UiScreen.Playing;
            if (ArenaCamera.I != null) ArenaCamera.I.orbit = false;
            Audio.Play(Sfx.RoundStart);
            matchRoutine = StartCoroutine(RunMatch());
        }

        // ------------------------------------------------------------- pause / ui

        public void SetPaused(bool p)
        {
            if (p && screen == UiScreen.Playing) { screen = UiScreen.Paused; Time.timeScale = 0f; }
            else if (!p && screen == UiScreen.Paused) { screen = UiScreen.Playing; Time.timeScale = 1f; }
        }

        public void ShowHowTo()
        {
            howToReturnTo = screen == UiScreen.HowTo ? howToReturnTo : screen;
            screen = UiScreen.HowTo;
        }

        public void CloseHowTo()
        {
            screen = howToReturnTo;
            Time.timeScale = screen == UiScreen.Paused ? 0f : 1f;
        }

        public void ToMainMenu()
        {
            Time.timeScale = 1f;
            Bootstrap.AutoStart = false;
            Bootstrap.Restart();
        }

        public void PlayAgain()
        {
            Time.timeScale = 1f;
            Bootstrap.AutoStart = true;
            Bootstrap.Restart();
        }

        // ------------------------------------------------------------- match loop

        IEnumerator RunMatch()
        {
            Banner("TILE PREDICTION BATTLE ROYALE", Palette.Gold, 2.2f);
            Feed("16 players. Private zones. Targeting is anonymous.");
            Feed("Watch how they move - it is the only tell you get.");
            yield return new WaitForSeconds(2.2f);

            while (AliveCount() > 1)
            {
                round++;
                lavaThisRound = round >= Cfg.LavaFirstRound
                             && (round - Cfg.LavaFirstRound) % Cfg.LavaEveryRounds == 0;

                yield return PrepPhase();
                yield return DecisionPhase();

                lastResult = Resolver.Resolve(players, round, lavaThisRound);
                SpendGadgets(lastResult);
                yield return RevealPhase(lastResult);
            }

            EndMatch();
        }

        IEnumerator PrepPhase()
        {
            phase = Phase.Prep;
            phaseLength = phaseTimer = Cfg.PrepSeconds;
            scoutReveal = -1;
            revealCaption = "";
            lastTickSecond = -1;
            Audio.SetIntensity(0.12f);
            Audio.Play(Sfx.RoundStart, 1f, 0.5f);

            for (int i = 0; i < players.Count; i++) players[i].DecayDwell();

            arena.ResetAllLooks();
            if (lavaThisRound) arena.PreviewLava();
            if (ArenaCamera.I != null) ArenaCamera.I.Focus(-1);

            Banner("ROUND " + round + "  -  PREP", Palette.Paper, 1.5f);
            if (lavaThisRound) Feed("LAVA RISES at the end of this round.");

            while (phaseTimer > 0f)
            {
                float dt = Time.deltaTime;
                phaseTimer -= dt;

                if (human != null && human.me.alive) human.TickPrep(dt);

                for (int i = 0; i < bots.Count; i++)
                {
                    var b = bots[i];
                    if (!b.me.alive || b.me.avatar == null) continue;
                    Vector3 cur = b.me.avatar.transform.position;
                    b.me.avatar.MoveTo(b.TickPrep(dt, cur), dt);
                }

                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    if (!p.alive || p.avatar == null) continue;
                    p.RememberDwell(p.zone.CellAt(p.avatar.transform.position), dt);
                }

                yield return null;
            }
        }

        IEnumerator DecisionPhase()
        {
            phase = Phase.Decision;
            bool spectating = human == null || !human.me.alive;
            phaseLength = phaseTimer = spectating ? 2.0f : Cfg.DecisionSeconds;
            lastTickSecond = -1;

            for (int i = 0; i < bots.Count; i++)
                if (bots[i].me.alive)
                    bots[i].Decide(players, round, bots[i].me.avatar.transform.position);

            if (!spectating)
            {
                human.BeginDecision(human.me.avatar.transform.position);
                Banner("COMMIT  -  nobody can see your choice", Palette.Paper, 1.8f);
            }

            looksDirty = true;

            while (phaseTimer > 0f)
            {
                phaseTimer -= Time.deltaTime;
                Audio.SetIntensity(Mathf.Lerp(0.45f, 1f, 1f - Mathf.Clamp01(phaseTimer / Mathf.Max(phaseLength, 0.01f))));

                int sec = Mathf.CeilToInt(phaseTimer);
                if (!spectating && sec != lastTickSecond && sec <= 3 && sec > 0)
                {
                    lastTickSecond = sec;
                    Audio.Play(Sfx.Tick, 1f + (4 - sec) * 0.12f, 0.5f);
                }

                if (!spectating)
                {
                    human.TickDecision(players);
                    if (human.Dirty) { human.Dirty = false; looksDirty = true; }
                    if (ArenaCamera.I != null) ArenaCamera.I.Focus(human.targetPlayer);
                    if (human.locked) { Audio.Play(Sfx.Lock); break; }
                }

                if (looksDirty) { RefreshLooks(); looksDirty = false; }
                yield return null;
            }

            if (!spectating) human.Commit(players);
            if (ArenaCamera.I != null) ArenaCamera.I.Focus(-1);
        }

        // ---------------------------------------------------------------- reveal

        IEnumerator RevealPhase(RoundResult res)
        {
            phase = Phase.Reveal;
            phaseLength = phaseTimer = 0f;
            Audio.SetIntensity(0.55f);
            arena.ResetAllLooks();

            // ---- beat 1: everyone reveals where they actually went -------------
            revealCaption = "COMMITTED POSITIONS";
            int hops = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.alive || p.avatar == null) continue;
                var cell = p.zone.Tile(p.decision.hideTile);
                if (cell == null) continue;
                p.avatar.Hop(cell.center, 0.5f);
                p.lastHideTile = p.decision.hideTile;
                p.RememberHide(cell);
                arena.SetLook(cell, TileLook.Standing, p.color);
                if (hops++ < 4) Audio.Play(Sfx.Hop, Random.Range(0.85f, 1.25f), 0.35f);
            }
            yield return new WaitForSeconds(Cfg.BeatLock);

            // ---- beat 2: telegraph every incoming strike ------------------------
            revealCaption = "INCOMING";
            Audio.Play(Sfx.Incoming, 1f, 0.7f);
            foreach (var kv in res.hitTilesByZone)
            {
                var z = players[kv.Key].zone;
                foreach (int idx in kv.Value)
                {
                    var cell = z.Tile(idx);
                    if (cell == null) continue;
                    arena.SetLook(cell, TileLook.Incoming, players[kv.Key].color);
                    Fx.Wave(cell.center, Palette.Danger, 1.5f, 0.55f);
                }
            }
            yield return new WaitForSeconds(Cfg.BeatIncoming);

            // ---- beat 3: simultaneous impact -------------------------------------
            revealCaption = "IMPACT";
            Audio.Play(Sfx.Impact, 1f, 1f);
            Audio.Play(Sfx.Impact, 0.72f, 0.8f);
            foreach (var kv in res.hitTilesByZone)
            {
                var z = players[kv.Key].zone;
                foreach (int idx in kv.Value)
                {
                    var cell = z.Tile(idx);
                    if (cell == null) continue;
                    arena.SetLook(cell, TileLook.Hit, players[kv.Key].color);
                    Fx.Impact(cell.center, Palette.Danger);
                }
            }
            Fx.Shake = 1.1f;
            yield return new WaitForSeconds(0.4f);

            for (int i = 0; i < res.shieldSaves.Count; i++)
            {
                var p = players[res.shieldSaves[i]];
                Fx.Say(AvatarTop(p), "SHIELD", Palette.Safe, 2f, 1f);
                Feed(p.name + " was hit and SHIELDED it.");
                if (i < 3) Audio.Play(Sfx.Shield, 1f, 0.7f);
            }
            for (int i = 0; i < res.decoySaves.Count; i++)
            {
                var p = players[res.decoySaves[i]];
                Fx.Say(AvatarTop(p), "DECOY", Palette.Gold, 2f, 1f);
                Feed(p.name + " ate the hit with a DECOY (-1 tile).");
                if (i < 3) Audio.Play(Sfx.Decoy, 1f, 0.7f);
            }

            ApplyDeaths(res);
            yield return new WaitForSeconds(Cfg.BeatImpact);

            // ---- beat 4: anti-dogpiling ------------------------------------------
            if (res.instantTileLoss.Count > 0)
            {
                if (res.dogpiledTargets.Count > 0)
                {
                    Banner("ANTI-DOGPILE  -  " + res.dogpileThreshold + "+ ON ONE TARGET",
                           Palette.Lava, 2.2f);
                    revealCaption = "ANTI-DOGPILE PENALTY";
                    Audio.Play(Sfx.Dogpile, 1f, 0.85f);
                    for (int i = 0; i < res.dogpiledTargets.Count; i++)
                    {
                        int t = res.dogpiledTargets[i];
                        Feed(res.AttackersOn(t) + " players piled on " + players[t].name
                             + " - every one of them loses a tile.");
                    }
                }

                int sounds = 0;
                foreach (var kv in res.instantTileLoss)
                {
                    var p = players[kv.Key];
                    if (!p.alive) continue;
                    bool penalised = res.penalisedAttackers.Contains(p.index);
                    ApplyTileLoss(p, kv.Value);
                    Fx.Say(ZoneTop(p), penalised ? "-1 TILE  DOGPILE" : "-1 TILE", Palette.Lava, 2f, 0.95f);
                    if (sounds++ < 3) Audio.Play(Sfx.Crumble, Random.Range(0.9f, 1.15f), 0.6f);
                }
                yield return new WaitForSeconds(Cfg.BeatDogpile);
            }

            // ---- beat 5: lava ------------------------------------------------------
            if (res.lavaThisRound)
            {
                Banner("LAVA RISES", Palette.Lava, 2f);
                revealCaption = "THE ARENA SHRINKS";
                Fx.Shake = Mathf.Max(Fx.Shake, 0.7f);
                Audio.Play(Sfx.Lava, 1f, 0.9f);
                int sounds = 0;
                for (int i = 0; i < res.lavaLosers.Count; i++)
                {
                    var p = players[res.lavaLosers[i]];
                    if (!p.alive) continue;
                    ApplyTileLoss(p, 1);
                    if (sounds++ < 3) Audio.Play(Sfx.Crumble, Random.Range(0.85f, 1.2f), 0.45f);
                }
                Feed("Lava claimed a tile from every survivor.");
                yield return new WaitForSeconds(Cfg.BeatLava);
            }

            // ---- beat 6: scout + summary --------------------------------------------
            if (human != null && human.me.alive && res.scoutUsers.Contains(human.me.index))
            {
                scoutReveal = human.me.incomingCount;
                Audio.Play(Sfx.Scout, 1f, 0.8f);
                Feed("SCOUT: " + scoutReveal + " player(s) attacked you. (Who? Never told.)");
            }

            NudgeSurvivorsOntoLivingTiles();
            arena.ResetAllLooks();
            revealCaption = AliveCount() + " REMAIN";
            yield return new WaitForSeconds(Cfg.BeatSummary);
        }

        // --------------------------------------------------------------- mutation

        void SpendGadgets(RoundResult res)
        {
            for (int i = 0; i < res.attacks.Count; i++)
                if (res.attacks[i].gadget == Gadget.Splash)
                    players[res.attacks[i].attacker].Spend(Gadget.Splash);

            for (int i = 0; i < res.shieldSaves.Count; i++) players[res.shieldSaves[i]].Spend(Gadget.Shield);
            for (int i = 0; i < res.decoySaves.Count; i++)  players[res.decoySaves[i]].Spend(Gadget.Decoy);
            for (int i = 0; i < res.scoutUsers.Count; i++)  players[res.scoutUsers[i]].Spend(Gadget.Scout);
        }

        void ApplyDeaths(RoundResult res)
        {
            int survivors = res.aliveAtStart - res.deaths.Count;
            for (int i = 0; i < res.deaths.Count; i++)
            {
                var p = players[res.deaths[i]];
                Vector3 where = AvatarTop(p);
                p.alive = false;
                p.deathRound = res.round;
                p.placement = survivors + 1;
                if (p.avatar != null) p.avatar.Die();
                Fx.Say(where, "ELIMINATED", Palette.Danger, 2.4f, 1.15f);
                Feed(p.name + " read wrong. Eliminated. (#" + p.placement + ")");
                if (i < 4) Audio.Play(Sfx.Death, Random.Range(0.9f, 1.1f), p.isHuman ? 1f : 0.55f);
            }
        }

        void ApplyTileLoss(PlayerState p, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var cell = p.zone.RemoveNextTile();
                if (cell == null)
                {
                    Feed(p.name + " is already at the 2-tile floor - nothing left to take.");
                    break;
                }
                arena.Crumble(cell);
            }
        }

        /// A survivor may be standing where a tile just fell away.
        void NudgeSurvivorsOntoLivingTiles()
        {
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (!p.alive || p.avatar == null) continue;
                Vector3 safe = p.zone.Clamp(p.avatar.transform.position);
                if ((safe - p.avatar.transform.position).sqrMagnitude > 0.01f) p.avatar.Hop(safe, 0.35f);
            }
        }

        void EndMatch()
        {
            phase = Phase.GameOver;
            matchRoutine = null;
            Audio.SetIntensity(0f);
            var w = Winner();
            if (w != null)
            {
                w.placement = 1;
                Banner(w.name + " WINS", w.color, 999f);
                Feed(w.name + " is the last one standing.");
                Audio.Play(w.isHuman ? Sfx.Win : Sfx.Lose, 1f, 0.9f);
            }
            else
            {
                Banner("EVERYBODY DIED  -  DRAW", Palette.Lava, 999f);
                Feed("Mutual destruction. Nobody reaches the trophy.");
                Audio.Play(Sfx.Lose, 1f, 0.9f);
            }
        }

        // ----------------------------------------------------------------- helpers

        public int AliveCount()
        {
            int n = 0;
            for (int i = 0; i < players.Count; i++) if (players[i].alive) n++;
            return n;
        }

        public PlayerState Winner()
        {
            PlayerState w = null;
            for (int i = 0; i < players.Count; i++)
                if (players[i].alive) { if (w != null) return null; w = players[i]; }
            return w;
        }

        static Vector3 AvatarTop(PlayerState p)
        {
            return p.avatar != null ? p.avatar.transform.position + Vector3.up * 2f
                                    : p.zone.CenterOf(0) + Vector3.up * 2f;
        }

        static Vector3 ZoneTop(PlayerState p)
        {
            return MeshFactory.Polar((Cfg.InnerR + Cfg.OuterR) * 0.5f, p.zone.centerDeg) + Vector3.up * 2.6f;
        }

        public void Banner(string text, Color c, float seconds)
        {
            banner = text;
            bannerColor = c;
            bannerUntil = Time.time + seconds;
        }

        public bool BannerVisible { get { return Time.time < bannerUntil && !string.IsNullOrEmpty(banner); } }

        public void Feed(string line)
        {
            feed.Add(line);
            while (feed.Count > 7) feed.RemoveAt(0);
        }

        void RefreshLooks()
        {
            arena.ResetAllLooks();
            if (lavaThisRound) arena.PreviewLava();

            if (human == null || !human.me.alive || phase != Phase.Decision) return;

            var z = human.me.zone;
            for (int i = 0; i < z.Active.Count; i++)
                arena.SetLook(z.Active[i], TileLook.Owned, human.me.color);

            var hideCell = z.Tile(human.hideTile);
            if (hideCell != null) arena.SetLook(hideCell, TileLook.Standing, human.me.color);

            if (human.targetPlayer >= 0 && players[human.targetPlayer].alive)
            {
                var tc = players[human.targetPlayer].zone.Tile(human.targetTile);
                if (tc != null) arena.SetLook(tc, TileLook.Aimed, players[human.targetPlayer].color);
            }

            if (human.hoverPlayer >= 0 && human.hoverTile >= 0)
            {
                var hc = players[human.hoverPlayer].zone.Tile(human.hoverTile);
                if (hc != null && hc != hideCell)
                    arena.SetLook(hc,
                        human.hoverPlayer == human.me.index ? TileLook.Standing : TileLook.Aimed,
                        players[human.hoverPlayer].color);
            }
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (screen == UiScreen.HowTo) CloseHowTo();
                else if (screen == UiScreen.Paused) SetPaused(false);
                // the results screen has its own buttons - pausing over it just hides them
                else if (screen == UiScreen.Playing && phase != Phase.GameOver) SetPaused(true);
            }

            if (screen == UiScreen.Playing && phase == Phase.GameOver)
            {
                if (Input.GetKeyDown(KeyCode.R)) PlayAgain();
                if (Input.GetKeyDown(KeyCode.M)) ToMainMenu();
            }

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (p.avatar == null) continue;
                bool me = human != null && p.index == human.me.index;
                p.avatar.SetRingHighlight(me && p.alive);
            }
        }
    }
}
