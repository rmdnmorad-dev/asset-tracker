using UnityEngine;
using UnityEngine.UI;

namespace VampFrost
{
    public static class Menus
    {
        static GameObject main, mapSel, settings, pause, end;
        static Text goldTxt, endTitle, endStats;
        static bool settingsFromPause;

        public static void Init()
        {
            var t = UIBuilder.Root.transform;
            BuildMain(t);
            BuildMapSelect(t);
            BuildSettings(t);
            BuildPause(t);
            BuildEnd(t);
            HideAll();
        }

        static GameObject Screen(Transform parent, string name, Color dim)
        {
            var go = new GameObject(name);
            var rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            UIBuilder.Stretch(go.transform, "dim", dim).raycastTarget = true;
            return go;
        }

        // ---------------- MAIN ----------------
        static void BuildMain(Transform parent)
        {
            main = Screen(parent, "MainMenu", new Color(.03f, .04f, .08f, 1f));
            var t = main.transform;

            UIBuilder.Txt(t, "VampFrost", 96, UIBuilder.BloodRed,
                new Vector2(4, 246), new Vector2(800, 110),
                TextAnchor.MiddleCenter, null, FontStyle.Bold);
            UIBuilder.Txt(t, "VampFrost", 96, UIBuilder.Frost,
                new Vector2(0, 250), new Vector2(800, 110),
                TextAnchor.MiddleCenter, null, FontStyle.Bold);
            UIBuilder.Txt(t, "a frozen-blood survivors run", 22,
                new Color(.7f, .8f, .95f, .8f), new Vector2(0, 178),
                new Vector2(700, 30));

            UIBuilder.Btn(t, "PLAY", new Vector2(340, 74), new Vector2(0, 60),
                () => { GameEvents.OnUIOpen?.Invoke(); Show(mapSel); }, null, 34);
            UIBuilder.Btn(t, "SETTINGS", new Vector2(340, 64), new Vector2(0, -32),
                () => { settingsFromPause = false; GameEvents.OnUIOpen?.Invoke(); Show(settings); });
            UIBuilder.Btn(t, "QUIT", new Vector2(340, 64), new Vector2(0, -118),
                () => { SaveSystem.Save(); Application.Quit(); });

            goldTxt = UIBuilder.Txt(t, "", 24, new Color(.95f, .8f, .25f),
                new Vector2(0, -210), new Vector2(500, 32));
        }

        // ---------------- MAP SELECT ----------------
        static void BuildMapSelect(Transform parent)
        {
            mapSel = Screen(parent, "MapSelect", new Color(.03f, .04f, .08f, 1f));
            var t = mapSel.transform;
            UIBuilder.Txt(t, "CHOOSE YOUR HUNT", 44, UIBuilder.Frost,
                new Vector2(0, 300), new Vector2(800, 60),
                TextAnchor.MiddleCenter, null, FontStyle.Bold);

            for (int i = 0; i < MapDefs.All.Length; i++)
            {
                int id = i;
                var m = MapDefs.All[i];
                float x = (i % 3 - 1) * 400f;
                float y = i < 3 ? 110f : -80f;
                var b = UIBuilder.Btn(t, "", new Vector2(360, 150), new Vector2(x, y),
                    () => GameManager.I.StartRun(id), null, 24,
                    new Color(m.groundA.r * .6f + .05f, m.groundA.g * .6f + .06f, m.groundA.b * .6f + .1f, 1f));
                UIBuilder.Txt(b.transform, m.name, 27, Color.white,
                    new Vector2(0, 30), new Vector2(340, 40),
                    TextAnchor.MiddleCenter, null, FontStyle.Bold);
                UIBuilder.Txt(b.transform, "vs " + EnemyDefs.Bosses[m.bossId].name, 18,
                    new Color(1f, .65f, .45f), new Vector2(0, -6), new Vector2(340, 26));
                int best = SaveSystem.Data.bestWave[i];
                UIBuilder.Txt(b.transform,
                    best > 0 ? $"best: wave {best}" : "unexplored", 17,
                    new Color(.75f, .85f, 1f, .8f), new Vector2(0, -40), new Vector2(340, 24));
            }

            UIBuilder.Btn(t, "BACK", new Vector2(220, 56), new Vector2(0, -280),
                () => { GameEvents.OnUIClose?.Invoke(); Show(main); });
        }

        // ---------------- SETTINGS ----------------
        static void BuildSettings(Transform parent)
        {
            settings = Screen(parent, "Settings", new Color(.02f, .03f, .06f, .96f));
            var t = settings.transform;
            UIBuilder.Panel(t, "box", new Vector2(560, 460), Vector2.zero, UIBuilder.PanelDark);
            UIBuilder.Txt(t, "SETTINGS", 40, UIBuilder.Frost, new Vector2(0, 178),
                new Vector2(400, 50), TextAnchor.MiddleCenter, null, FontStyle.Bold);

            var d = SaveSystem.Data;
            UIBuilder.SliderRow(t, "Master", new Vector2(0, 100), d.master,
                v => { d.master = v; AudioManager.I?.ApplyVolumes(); });
            UIBuilder.SliderRow(t, "Music", new Vector2(0, 40), d.music,
                v => { d.music = v; AudioManager.I?.ApplyVolumes(); });
            UIBuilder.SliderRow(t, "SFX", new Vector2(0, -20), d.sfx,
                v => { d.sfx = v; AudioManager.I?.ApplyVolumes(); });
            UIBuilder.SliderRow(t, "UI", new Vector2(0, -80), d.ui,
                v => { d.ui = v; AudioManager.I?.ApplyVolumes(); });

            UIBuilder.Btn(t, "BACK", new Vector2(220, 56), new Vector2(0, -168), () =>
            {
                SaveSystem.Save();
                GameEvents.OnUIClose?.Invoke();
                if (settingsFromPause) Show(pause); else Show(main);
            });
        }

        // ---------------- PAUSE ----------------
        static void BuildPause(Transform parent)
        {
            pause = Screen(parent, "Pause", new Color(0, 0, 0, .66f));
            var t = pause.transform;
            UIBuilder.Panel(t, "box", new Vector2(480, 420), Vector2.zero, UIBuilder.PanelDark);
            UIBuilder.Txt(t, "GAME PAUSED", 42, UIBuilder.Frost, new Vector2(0, 140),
                new Vector2(460, 60), TextAnchor.MiddleCenter, null, FontStyle.Bold);
            UIBuilder.Btn(t, "RESUME", new Vector2(320, 62), new Vector2(0, 46),
                () => GameManager.I.SetPause(false));
            UIBuilder.Btn(t, "SETTINGS", new Vector2(320, 62), new Vector2(0, -34), () =>
            {
                settingsFromPause = true;
                GameEvents.OnUIOpen?.Invoke();
                pause.SetActive(false);
                settings.SetActive(true);
            });
            UIBuilder.Btn(t, "QUIT TO MENU", new Vector2(320, 62), new Vector2(0, -114),
                () => GameManager.I.EndToMenu());
        }

        // ---------------- END SCREEN ----------------
        static void BuildEnd(Transform parent)
        {
            end = Screen(parent, "End", new Color(0, 0, 0, .8f));
            var t = end.transform;
            UIBuilder.Panel(t, "box", new Vector2(560, 420), Vector2.zero, UIBuilder.PanelDark);
            endTitle = UIBuilder.Txt(t, "VICTORY", 56, UIBuilder.Frost, new Vector2(0, 120),
                new Vector2(540, 70), TextAnchor.MiddleCenter, null, FontStyle.Bold);
            endStats = UIBuilder.Txt(t, "", 24, Color.white, new Vector2(0, -6),
                new Vector2(500, 160), TextAnchor.MiddleCenter);
            UIBuilder.Btn(t, "CONTINUE", new Vector2(300, 62), new Vector2(0, -140),
                () => GameManager.I.EndToMenu());
        }

        // ---------------- API ----------------
        static void Show(GameObject g)
        {
            main.SetActive(g == main);
            mapSel.SetActive(g == mapSel);
            settings.SetActive(g == settings);
            pause.SetActive(g == pause);
            end.SetActive(g == end);
        }

        public static void ShowMain()
        {
            RefreshMapSelect();
            goldTxt.text = "Blood Gold: " + SaveSystem.Data.gold;
            Show(main);
        }

        static void RefreshMapSelect()
        {
            // cheap: rebuild best-wave labels by tearing down and rebuilding map select
            Object.Destroy(mapSel);
            BuildMapSelect(UIBuilder.Root.transform);
            mapSel.SetActive(false);
        }

        public static void ShowPause(bool on)
        {
            if (on) Show(pause);
            else { pause.SetActive(false); settings.SetActive(false); }
        }

        public static void ShowEnd(bool victory)
        {
            var gm = GameManager.I;
            endTitle.text = victory ? "VICTORY" : "YOU DIED";
            endTitle.color = victory ? UIBuilder.Frost : UIBuilder.BloodRed;
            int m = (int)(gm.runTime / 60f), s = (int)(gm.runTime % 60f);
            endStats.text = $"Map: {gm.Map.name}\nSurvived: {m:00}:{s:00}   ·   Wave {gm.wave}\nGold earned: {gm.goldRun}";
            Show(end);
        }

        public static void HideAll()
        {
            main.SetActive(false); mapSel.SetActive(false);
            settings.SetActive(false); pause.SetActive(false); end.SetActive(false);
        }
    }
}
