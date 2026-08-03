using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    /// All UI is IMGUI drawn against a virtual 1920x1080 canvas. That keeps the
    /// project free of UI prefabs, font assets and package dependencies - it draws
    /// identically in every Unity version and both render pipelines.
    public class Hud : MonoBehaviour
    {
        GUIStyle style;
        float s = 1f, VW = 1920f;
        const float VH = 1080f;

        string hoveredNow, lastHovered;

        static readonly string[] HowToLines =
        {
            "You own one wedge of the ring. You can never leave it, and nobody can enter it.",
            "Last player alive wins.",
            "",
            "@PREP  (10 seconds)",
            "Everyone walks around their own zone in real time, and everyone can see it.",
            "This is the only information in the game. It is also the only place to lie.",
            "",
            "@COMMIT  (15 seconds)",
            "Secretly pick three things:",
            "   1.  a tile in YOUR zone      - where you will actually be standing",
            "   2.  a tile in SOMEONE's zone - where you are attacking",
            "   3.  a gadget, if you want one",
            "Click your own tile, then click an enemy tile. 1-4 picks a gadget. SPACE locks.",
            "",
            "@REVEAL",
            "Everything resolves at once. Stand on a tile that gets hit and you are out.",
            "Results are fully public. Who attacked whom is never revealed, to anyone, ever.",
            "",
            "@THE ANTI-DOGPILE RULE",
            "If 4 players attack the same person in one round, all 4 of them lose a tile.",
            "Once 10 or fewer players are alive that drops to 3.",
            "It applies even if the target dies. Spread your fire or pay for it.",
            "",
            "@LAVA",
            "From round 3, every 2 rounds, everyone loses their outermost tile.",
            "Doomed tiles glow orange a full round before they go.",
            "Nobody ever drops below 2 tiles - and at 2 tiles, a zone is a pure coin flip.",
        };

        public static Hud Create(Transform parent = null)
        {
            var go = new GameObject("HUD");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<Hud>();
        }

        void OnGUI()
        {
            var gm = GameManager.I;
            if (gm == null || gm.players.Count == 0) return;

            if (style == null) style = new GUIStyle(GUI.skin.label) { richText = false, wordWrap = false };

            s = Screen.height / VH;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(s, s, s));
            VW = Screen.width / s;
            hoveredNow = null;

            bool inMatch = gm.screen == UiScreen.Playing || gm.screen == UiScreen.Paused;
            if (inMatch)
            {
                WorldLabels(gm);
                Popups();
                TopBar(gm);
                Feed(gm);
                SidePanel(gm);
                Roster(gm);
                BannerText(gm);
            }

            switch (gm.screen)
            {
                case UiScreen.Title:  TitleScreen(gm); break;
                case UiScreen.HowTo:  HowToScreen(gm); break;
                case UiScreen.Paused: PauseScreen(gm); break;
                default:
                    if (gm.phase == Phase.GameOver) GameOverPanel(gm);
                    break;
            }

            if (Event.current.type == EventType.Repaint && hoveredNow != lastHovered)
            {
                lastHovered = hoveredNow;
                if (!string.IsNullOrEmpty(hoveredNow)) Audio.Play(Sfx.UiHover, 1f, 0.25f);
            }
        }

        // ------------------------------------------------------------- menu layers

        void TitleScreen(GameManager gm)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.02f, 0.025f, 0.04f, 0.55f));

            float cx = VW * 0.5f;
            Text(new Rect(cx - 700, VH * 0.13f, 1400, 96), "TILE PREDICTION", 82, Palette.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);
            Text(new Rect(cx - 700, VH * 0.13f + 88, 1400, 96), "BATTLE ROYALE", 82, Palette.Gold, TextAnchor.MiddleCenter, FontStyle.Bold);

            Text(new Rect(cx - 700, VH * 0.13f + 196, 1400, 36),
                 "16 players bluff inside private tile zones, secretly predict each other,",
                 24, new Color(1, 1, 1, 0.62f), TextAnchor.MiddleCenter, FontStyle.Normal);
            Text(new Rect(cx - 700, VH * 0.13f + 228, 1400, 36),
                 "and survive simultaneous attacks in a shrinking arena.",
                 24, new Color(1, 1, 1, 0.62f), TextAnchor.MiddleCenter, FontStyle.Normal);

            float by = VH * 0.52f;
            if (Button(new Rect(cx - 190, by, 380, 68), "PLAY", Palette.Safe, 32)) gm.StartMatch();
            if (Button(new Rect(cx - 190, by + 82, 380, 56), "HOW TO PLAY", Palette.Gold, 24)) gm.ShowHowTo();
            if (Button(new Rect(cx - 190, by + 150, 182, 48), Audio.Muted ? "SOUND: OFF" : "SOUND: ON",
                       Audio.Muted ? Palette.Danger : Palette.Safe, 19))
                Audio.Muted = !Audio.Muted;
            if (Button(new Rect(cx + 8, by + 150, 182, 48), "QUIT", Palette.Danger, 19)) Application.Quit();

            Text(new Rect(cx - 700, VH - 62, 1400, 28),
                 "1 human + 15 AI   |   prototype   |   every mesh, sound and pixel of UI is generated at runtime",
                 17, new Color(1, 1, 1, 0.34f), TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        void HowToScreen(GameManager gm)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.015f, 0.02f, 0.035f, 0.94f));

            float w = 1180f;
            float x = VW * 0.5f - w * 0.5f;
            Text(new Rect(x, 34, w, 56), "HOW TO PLAY", 42, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
            Fill(new Rect(x, 92, w, 2), new Color(1, 1, 1, 0.16f));

            float y = 112f;
            for (int i = 0; i < HowToLines.Length; i++)
            {
                string line = HowToLines[i];
                if (line.Length == 0) { y += 12f; continue; }

                bool heading = line[0] == '@';
                if (heading) line = line.Substring(1);

                Text(new Rect(x, y, w, 30), line,
                     heading ? 24 : 20,
                     heading ? Palette.Gold : new Color(1, 1, 1, 0.86f),
                     TextAnchor.MiddleLeft, heading ? FontStyle.Bold : FontStyle.Normal);
                y += heading ? 36f : 30f;
            }

            if (Button(new Rect(x, VH - 92, 300, 56), "BACK   [ESC]", Palette.Paper, 24)) gm.CloseHowTo();
        }

        void PauseScreen(GameManager gm)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.01f, 0.015f, 0.03f, 0.78f));

            float cx = VW * 0.5f;
            Text(new Rect(cx - 400, VH * 0.22f, 800, 70), "PAUSED", 58, Palette.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);

            float by = VH * 0.36f;
            if (Button(new Rect(cx - 190, by, 380, 62), "RESUME   [ESC]", Palette.Safe, 26)) gm.SetPaused(false);
            if (Button(new Rect(cx - 190, by + 76, 380, 54), "HOW TO PLAY", Palette.Gold, 22)) gm.ShowHowTo();
            if (Button(new Rect(cx - 190, by + 140, 380, 54), "RESTART MATCH", Palette.Paper, 22)) gm.PlayAgain();
            if (Button(new Rect(cx - 190, by + 204, 380, 54), "MAIN MENU", Palette.Paper, 22)) gm.ToMainMenu();
            if (Button(new Rect(cx - 190, by + 268, 380, 54),
                       Audio.Muted ? "SOUND: OFF" : "SOUND: ON",
                       Audio.Muted ? Palette.Danger : Palette.Safe, 22))
                Audio.Muted = !Audio.Muted;
            if (Button(new Rect(cx - 190, by + 332, 380, 54), "QUIT", Palette.Danger, 22)) Application.Quit();
        }

        void GameOverPanel(GameManager gm)
        {
            Fill(new Rect(0, 0, VW, VH), new Color(0.01f, 0.015f, 0.03f, 0.72f));
            var w = gm.Winner();

            var r = new Rect(VW * 0.5f - 460, VH * 0.24f, 920, 380);
            Fill(r, new Color(0.04f, 0.05f, 0.08f, 0.95f));
            Fill(new Rect(r.x, r.y, r.width, 6), w != null ? w.color : Palette.Lava);

            Text(new Rect(r.x, r.y + 30, r.width, 70),
                 w != null ? w.name + " WINS" : "DRAW", 62, w != null ? w.color : Palette.Lava,
                 TextAnchor.MiddleCenter, FontStyle.Bold);

            var me = gm.human != null ? gm.human.me : null;
            string sub = me == null ? "" :
                         (me.alive ? "You read them all." : "You finished #" + me.placement + " of " + Cfg.PlayerCount + ".");
            if (w == null) sub = "Everyone left standing died in the same round.";
            Text(new Rect(r.x, r.y + 108, r.width, 40), sub, 26, Palette.Paper, TextAnchor.MiddleCenter, FontStyle.Normal);

            Text(new Rect(r.x, r.y + 152, r.width, 34), "rounds played: " + gm.round, 20,
                 new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, FontStyle.Normal);

            if (Button(new Rect(r.x + 130, r.y + 208, 300, 62), "PLAY AGAIN   [R]", Palette.Safe, 25)) gm.PlayAgain();
            if (Button(new Rect(r.x + 490, r.y + 208, 300, 62), "MAIN MENU   [M]", Palette.Paper, 25)) gm.ToMainMenu();

            Text(new Rect(r.x, r.y + 296, r.width, 34),
                 w != null && w.isHuman ? "Last one standing." : "The trophy stays where it is.",
                 18, new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        // ------------------------------------------------------------- match hud

        void TopBar(GameManager gm)
        {
            Fill(new Rect(0, 0, VW, 74), new Color(0.03f, 0.04f, 0.06f, 0.82f));
            Fill(new Rect(0, 74, VW, 2), new Color(1f, 1f, 1f, 0.10f));

            Text(new Rect(24, 12, 400, 46), "ROUND " + Mathf.Max(1, gm.round), 34, Palette.Paper, TextAnchor.MiddleLeft, FontStyle.Bold);

            string phaseName;
            Color phaseCol;
            switch (gm.phase)
            {
                case Phase.Prep:     phaseName = "PREP  -  everyone is watching"; phaseCol = Palette.Safe; break;
                case Phase.Decision: phaseName = "COMMIT  -  secret";             phaseCol = Palette.Gold; break;
                case Phase.Reveal:   phaseName = gm.revealCaption;                phaseCol = Palette.Danger; break;
                case Phase.GameOver: phaseName = "MATCH OVER";                    phaseCol = Palette.Gold; break;
                default:             phaseName = "";                              phaseCol = Palette.Paper; break;
            }
            Text(new Rect(VW * 0.5f - 400, 10, 800, 32), phaseName, 25, phaseCol, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (gm.phaseLength > 0.01f && (gm.phase == Phase.Prep || gm.phase == Phase.Decision))
            {
                float k = Mathf.Clamp01(gm.phaseTimer / gm.phaseLength);
                var bar = new Rect(VW * 0.5f - 260, 46, 520, 12);
                Fill(bar, new Color(1f, 1f, 1f, 0.12f));
                Color c = k < 0.25f ? Palette.Danger : phaseCol;
                Fill(new Rect(bar.x, bar.y, bar.width * k, bar.height), c);
                Text(new Rect(bar.x + bar.width + 12, 40, 90, 24),
                     Mathf.CeilToInt(gm.phaseTimer) + "s", 20, c, TextAnchor.MiddleLeft, FontStyle.Bold);
            }

            int alive = gm.AliveCount();
            Text(new Rect(VW - 344, 12, 320, 46), "ALIVE  " + alive + " / " + Cfg.PlayerCount,
                 30, alive <= 4 ? Palette.Danger : Palette.Paper, TextAnchor.MiddleRight, FontStyle.Bold);

            int threshold = Cfg.DogpileThreshold(alive);
            Text(new Rect(VW - 560, 44, 536, 24),
                 "anti-dogpile: " + threshold + "+ on one target = attackers lose a tile",
                 17, new Color(1f, 1f, 1f, 0.45f), TextAnchor.MiddleRight, FontStyle.Normal);
        }

        void Feed(GameManager gm)
        {
            if (gm.feed.Count == 0) return;
            float h = 26f * gm.feed.Count + 20f;
            var r = new Rect(20, 96, 620, h);
            Fill(r, new Color(0.03f, 0.04f, 0.06f, 0.62f));
            for (int i = 0; i < gm.feed.Count; i++)
            {
                float a = Mathf.Lerp(0.45f, 1f, (i + 1) / (float)gm.feed.Count);
                Text(new Rect(r.x + 14, r.y + 10 + i * 26, r.width - 24, 24), gm.feed[i], 18,
                     new Color(Palette.Paper.r, Palette.Paper.g, Palette.Paper.b, a),
                     TextAnchor.MiddleLeft, FontStyle.Normal);
            }
        }

        void SidePanel(GameManager gm)
        {
            var me = gm.human != null ? gm.human.me : null;
            if (me == null) return;

            var r = new Rect(VW - 400, 96, 380, 330);

            if (gm.phase == Phase.Decision && me.alive)
            {
                Fill(r, new Color(0.03f, 0.04f, 0.06f, 0.78f));
                Fill(new Rect(r.x, r.y, 5, r.height), Palette.Gold);

                Text(new Rect(r.x + 18, r.y + 10, 340, 28), "YOUR COMMITMENT", 21, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);

                Row(r.x + 18, r.y + 46, "HIDE ON", "TILE " + (gm.human.hideTile + 1), me.color);

                string tgt = gm.human.targetPlayer >= 0
                    ? gm.players[gm.human.targetPlayer].name + "   TILE " + (gm.human.targetTile + 1)
                    : "- click an enemy tile -";
                Row(r.x + 18, r.y + 84, "ATTACK",
                    tgt, gm.human.targetPlayer >= 0 ? gm.players[gm.human.targetPlayer].color : Palette.Danger);

                Row(r.x + 18, r.y + 122, "GADGET", Gadgets.Name(gm.human.gadget),
                    gm.human.gadget == Gadget.None ? new Color(1, 1, 1, 0.5f) : Palette.Safe);

                float gy = r.y + 164;
                for (int g = 1; g < Gadgets.Count; g++)
                {
                    var gad = (Gadget)g;
                    int ch = me.charges[g];
                    bool sel = gm.human.gadget == gad;
                    var gr = new Rect(r.x + 18, gy, 344, 30);
                    if (sel) Fill(gr, new Color(Palette.Safe.r, Palette.Safe.g, Palette.Safe.b, 0.18f));
                    Color c = ch > 0 ? Palette.Paper : new Color(1, 1, 1, 0.25f);
                    Text(new Rect(gr.x + 4, gr.y, 26, 30), "" + g, 17, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Text(new Rect(gr.x + 28, gr.y, 92, 30), Gadgets.Name(gad), 17, c, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Text(new Rect(gr.x + 122, gr.y, 190, 30), Gadgets.Blurb(gad), 14,
                         new Color(1, 1, 1, ch > 0 ? 0.55f : 0.2f), TextAnchor.MiddleLeft, FontStyle.Normal);
                    Text(new Rect(gr.x + 306, gr.y, 38, 30), "x" + ch, 17, c, TextAnchor.MiddleRight, FontStyle.Bold);
                    gy += 30;
                }

                bool ready = gm.human.CanLock(gm.players);
                Text(new Rect(r.x + 18, r.y + r.height - 34, 344, 28),
                     ready ? "[SPACE]  LOCK IT IN" : "pick a target tile first",
                     19, ready ? Palette.Safe : new Color(1, 1, 1, 0.4f), TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else if (gm.phase == Phase.Prep && me.alive)
            {
                var pr = new Rect(r.x, r.y, r.width, 132);
                Fill(pr, new Color(0.03f, 0.04f, 0.06f, 0.7f));
                Fill(new Rect(pr.x, pr.y, 5, pr.height), me.color);
                Text(new Rect(pr.x + 18, pr.y + 10, 340, 26), "PREP", 21, me.color, TextAnchor.MiddleLeft, FontStyle.Bold);
                Text(new Rect(pr.x + 18, pr.y + 42, 344, 24), "WASD / arrows to move.", 17, Palette.Paper, TextAnchor.MiddleLeft, FontStyle.Normal);
                Text(new Rect(pr.x + 18, pr.y + 68, 344, 24), "Everyone can see you right now.", 17, new Color(1, 1, 1, 0.65f), TextAnchor.MiddleLeft, FontStyle.Normal);
                Text(new Rect(pr.x + 18, pr.y + 94, 344, 24), "So lie with your feet.", 17, Palette.Gold, TextAnchor.MiddleLeft, FontStyle.Normal);
            }
            else if (!me.alive && gm.phase != Phase.GameOver)
            {
                var pr = new Rect(r.x, r.y, r.width, 84);
                Fill(pr, new Color(0.03f, 0.04f, 0.06f, 0.7f));
                Text(new Rect(pr.x + 18, pr.y + 10, 340, 26), "SPECTATING", 21, Palette.Danger, TextAnchor.MiddleLeft, FontStyle.Bold);
                Text(new Rect(pr.x + 18, pr.y + 44, 344, 24), "You placed #" + me.placement + ". Watching it play out.",
                     16, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, FontStyle.Normal);
            }

            if (gm.scoutReveal >= 0)
            {
                var sr = new Rect(VW - 400, 440, 380, 62);
                Fill(sr, new Color(0.03f, 0.04f, 0.06f, 0.8f));
                Fill(new Rect(sr.x, sr.y, 5, sr.height), Palette.Safe);
                Text(new Rect(sr.x + 18, sr.y + 8, 344, 24), "SCOUT REPORT", 17, Palette.Safe, TextAnchor.MiddleLeft, FontStyle.Bold);
                Text(new Rect(sr.x + 18, sr.y + 32, 344, 24),
                     gm.scoutReveal + " attacked you. Names withheld.", 16, Palette.Paper, TextAnchor.MiddleLeft, FontStyle.Normal);
            }
        }

        void Row(float x, float y, string label, string value, Color c)
        {
            Text(new Rect(x, y, 110, 28), label, 15, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleLeft, FontStyle.Normal);
            Text(new Rect(x + 96, y, 250, 28), value, 19, c, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        void Roster(GameManager gm)
        {
            int n = gm.players.Count;
            float chipW = Mathf.Min(114f, (VW - 40f) / n);
            float x0 = (VW - chipW * n) * 0.5f;
            float y = VH - 96f;

            for (int i = 0; i < n; i++)
            {
                var p = gm.players[i];
                var r = new Rect(x0 + i * chipW + 2, y, chipW - 4, 72);

                Fill(r, p.alive ? new Color(0.04f, 0.05f, 0.07f, 0.88f) : new Color(0.02f, 0.02f, 0.03f, 0.8f));
                Fill(new Rect(r.x, r.y, r.width, 5), p.alive ? p.color : Palette.Dim(p.color, 0.28f));

                Color nameCol = p.alive ? Palette.Paper : new Color(1, 1, 1, 0.28f);
                if (p.isHuman && p.alive) nameCol = Palette.Gold;

                Text(new Rect(r.x + 6, r.y + 8, r.width - 12, 20), p.name, 15, nameCol, TextAnchor.MiddleLeft, FontStyle.Bold);

                if (p.alive)
                {
                    Text(new Rect(r.x + 6, r.y + 30, r.width - 12, 34), "" + p.TileCount, 30,
                         p.TileCount <= Cfg.MinTiles ? Palette.Danger : p.color, TextAnchor.MiddleLeft, FontStyle.Bold);
                    Text(new Rect(r.x + 6, r.y + 34, r.width - 12, 26), "tiles", 13,
                         new Color(1, 1, 1, 0.35f), TextAnchor.MiddleRight, FontStyle.Normal);
                }
                else
                {
                    Text(new Rect(r.x + 6, r.y + 32, r.width - 12, 30), "OUT #" + p.placement, 16,
                         new Color(1, 1, 1, 0.3f), TextAnchor.MiddleLeft, FontStyle.Bold);
                }
            }

            Text(new Rect(20, VH - 26, 900, 22),
                 "scroll = zoom   |   prep: WASD   |   commit: click your tile, then an enemy tile   |   1-4 gadget   |   SPACE lock   |   ESC pause",
                 15, new Color(1, 1, 1, 0.33f), TextAnchor.MiddleLeft, FontStyle.Normal);
        }

        void BannerText(GameManager gm)
        {
            if (!gm.BannerVisible || gm.phase == Phase.GameOver) return;
            float t = Mathf.Clamp01(gm.bannerUntil - Time.time);
            Color c = gm.bannerColor;
            c.a = Mathf.Min(1f, t * 2.4f);

            var r = new Rect(VW * 0.5f - 620, VH * 0.30f, 1240, 78);
            Fill(new Rect(r.x, r.y, r.width, r.height), new Color(0.02f, 0.03f, 0.05f, 0.55f * c.a));
            Text(r, gm.banner, 52, c, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        // ------------------------------------------------------------ world space

        void WorldLabels(GameManager gm)
        {
            var cam = ArenaCamera.I != null ? ArenaCamera.I.Cam : Camera.main;
            if (cam == null) return;

            for (int i = 0; i < gm.players.Count; i++)
            {
                var p = gm.players[i];
                Vector3 w = MeshFactory.Polar(Cfg.OuterR + 2.4f, p.zone.centerDeg, 1.4f);
                Vector2 g;
                if (!ToGui(cam, w, out g)) continue;

                var r = new Rect(g.x - 62, g.y - 20, 124, 40);
                Color c = p.alive ? p.color : new Color(1, 1, 1, 0.22f);
                Fill(new Rect(r.x, r.y, r.width, r.height), new Color(0.02f, 0.03f, 0.05f, p.alive ? 0.72f : 0.4f));
                Fill(new Rect(r.x, r.y, r.width, 3), c);
                Text(new Rect(r.x, r.y + 3, r.width, 20), p.name, 15, c, TextAnchor.MiddleCenter, FontStyle.Bold);
                Text(new Rect(r.x, r.y + 20, r.width, 18), p.alive ? p.TileCount + " tiles" : "OUT", 13,
                     new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, FontStyle.Normal);
            }

            // tile numbers, but only where they help: your zone and whoever you are aiming at
            if (gm.phase != Phase.Decision || gm.human == null || !gm.human.me.alive) return;
            NumberZone(cam, gm.human.me);
            if (gm.human.targetPlayer >= 0) NumberZone(cam, gm.players[gm.human.targetPlayer]);
            if (gm.human.hoverPlayer >= 0) NumberZone(cam, gm.players[gm.human.hoverPlayer]);
        }

        void NumberZone(Camera cam, PlayerState p)
        {
            if (!p.alive) return;
            for (int i = 0; i < p.zone.Active.Count; i++)
            {
                Vector2 g;
                if (!ToGui(cam, p.zone.Active[i].center + Vector3.up * 0.35f, out g)) continue;
                Text(new Rect(g.x - 20, g.y - 14, 40, 28), "" + (i + 1), 21, new Color(1, 1, 1, 0.85f),
                     TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        void Popups()
        {
            var cam = ArenaCamera.I != null ? ArenaCamera.I.Cam : Camera.main;
            if (cam == null) return;

            var list = Fx.Popups;
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                float k = p.age / p.life;
                Vector2 g;
                if (!ToGui(cam, p.world + Vector3.up * (k * 1.6f), out g)) continue;

                Color c = p.color;
                c.a = 1f - k * k;
                Text(new Rect(g.x - 200, g.y - 20, 400, 40), p.text, Mathf.RoundToInt(26 * p.size), c,
                     TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        bool ToGui(Camera cam, Vector3 world, out Vector2 gui)
        {
            Vector3 sp = cam.WorldToScreenPoint(world);
            gui = Vector2.zero;
            if (sp.z <= 0f) return false;
            gui = new Vector2(sp.x / s, (Screen.height - sp.y) / s);
            return true;
        }

        // ------------------------------------------------------------------ atoms

        bool Button(Rect r, string label, Color accent, int size)
        {
            bool hover = r.Contains(Event.current.mousePosition);
            if (hover) hoveredNow = label;

            Fill(r, hover ? new Color(accent.r * 0.5f, accent.g * 0.5f, accent.b * 0.5f, 0.85f)
                          : new Color(0.05f, 0.06f, 0.09f, 0.92f));
            Fill(new Rect(r.x, r.y, 5, r.height), accent);
            Fill(new Rect(r.x, r.y + r.height - 2, r.width, 2), new Color(1, 1, 1, hover ? 0.22f : 0.07f));
            Text(r, label, size, hover ? Color.white : Palette.Paper, TextAnchor.MiddleCenter, FontStyle.Bold);

            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                Audio.Play(Sfx.UiClick, 1f, 0.7f);
                return true;
            }
            return false;
        }

        void Fill(Rect r, Color c)
        {
            Color old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = old;
        }

        void Text(Rect r, string t, int size, Color c, TextAnchor anchor, FontStyle fs)
        {
            style.fontSize = size;
            style.alignment = anchor;
            style.fontStyle = fs;
            style.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.55f);
            GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), t, style);
            style.normal.textColor = c;
            GUI.Label(r, t, style);
        }
    }
}
