using UnityEngine;
using UnityEngine.UI;

namespace VampFrost
{
    public class HUD : MonoBehaviour
    {
        public static HUD I;

        GameObject root;
        RectTransform hpFill, xpFill, bossFill;
        Text hpTxt, lvlTxt, timerTxt, waveTxt, goldTxt, weaponsTxt, bossName, toastTxt, hintTxt, cdTxt;
        GameObject bossPanel;
        float toastT, hintT = 12f;
        Boss boss;

        public static HUD Create()
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(UIBuilder.Root.transform, false);
            var h = go.AddComponent<HUD>();
            I = h;
            h.Build(go.transform);
            go.SetActive(false);
            return h;
        }

        void OnDestroy() { if (I == this) I = null; Unsub(); }

        void Build(Transform t)
        {
            root = t.gameObject;
            var rt = root.GetComponent<RectTransform>() ?? root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var TL = new Vector2(0f, 1f);
            var TR = new Vector2(1f, 1f);
            var TC = new Vector2(.5f, 1f);
            var BC = new Vector2(.5f, 0f);
            var BL = new Vector2(0f, 0f);

            // HP + XP (top-left)
            hpFill = UIBuilder.Bar(t, new Vector2(320, 26), new Vector2(24, -24),
                new Color(.08f, .05f, .07f, .9f), UIBuilder.BloodRed, TL);
            hpTxt = UIBuilder.Txt(t, "100/100", 18, Color.white, new Vector2(24, -24),
                new Vector2(320, 26), TextAnchor.MiddleCenter, TL);
            xpFill = UIBuilder.Bar(t, new Vector2(320, 14), new Vector2(24, -56),
                new Color(.06f, .05f, .1f, .9f), new Color(.55f, .3f, .95f), TL);
            lvlTxt = UIBuilder.Txt(t, "Lv 1", 18, Color.white, new Vector2(352, -52),
                new Vector2(80, 24), TextAnchor.MiddleLeft, TL);
            weaponsTxt = UIBuilder.Txt(t, "", 17, new Color(.85f, .92f, 1f), new Vector2(24, -84),
                new Vector2(340, 300), TextAnchor.UpperLeft, TL);
            cdTxt = UIBuilder.Txt(t, "", 16, new Color(.7f, .85f, 1f), new Vector2(24, 20),
                new Vector2(400, 24), TextAnchor.LowerLeft, BL);

            // Timer + wave (top-center)
            timerTxt = UIBuilder.Txt(t, "00:00", 44, Color.white, new Vector2(0, -30),
                new Vector2(220, 50), TextAnchor.MiddleCenter, TC, FontStyle.Bold);
            waveTxt = UIBuilder.Txt(t, "Wave 1 / 20", 22, UIBuilder.Frost, new Vector2(0, -72),
                new Vector2(240, 30), TextAnchor.MiddleCenter, TC);

            // Gold (top-right)
            goldTxt = UIBuilder.Txt(t, "0 g", 24, new Color(.95f, .8f, .25f), new Vector2(-28, -28),
                new Vector2(200, 32), TextAnchor.MiddleRight, TR);

            // Boss bar (bottom-center)
            bossPanel = UIBuilder.Panel(t, "boss", new Vector2(720, 60), new Vector2(0, 46),
                new Color(0, 0, 0, 0), BC).gameObject;
            bossName = UIBuilder.Txt(bossPanel.transform, "BOSS", 22, new Color(1f, .6f, .3f),
                new Vector2(0, 18), new Vector2(720, 26), TextAnchor.MiddleCenter,
                new Vector2(.5f, .5f), FontStyle.Bold);
            bossFill = UIBuilder.Bar(bossPanel.transform, new Vector2(700, 20), new Vector2(0, -10),
                new Color(.1f, .04f, .04f, .95f), new Color(1f, .45f, .15f));
            bossPanel.SetActive(false);

            // Toast + hint
            toastTxt = UIBuilder.Txt(t, "", 24, UIBuilder.Frost, new Vector2(0, 130),
                new Vector2(900, 32), TextAnchor.MiddleCenter, BC, FontStyle.Bold);
            hintTxt = UIBuilder.Txt(t, "WASD move   ·   SPACE dash   ·   Q invisibility   ·   ESC pause",
                18, new Color(1, 1, 1, .55f), new Vector2(0, 90),
                new Vector2(900, 26), TextAnchor.MiddleCenter, BC);

            Sub();
        }

        void Sub()
        {
            GameEvents.OnBossSpawn += OnBossSpawn;
            GameEvents.OnBossDeath += OnBossDeath;
            GameEvents.OnWaveStart += OnWave;
        }
        void Unsub()
        {
            GameEvents.OnBossSpawn -= OnBossSpawn;
            GameEvents.OnBossDeath -= OnBossDeath;
            GameEvents.OnWaveStart -= OnWave;
        }

        void OnBossSpawn(Boss b) { boss = b; bossName.text = b.B.name; bossPanel.SetActive(true); }
        void OnBossDeath() { boss = null; bossPanel.SetActive(false); }
        void OnWave(int w) { if (w > 1) Toast($"Wave {w}"); }

        public static void Show()
        {
            if (I == null) return;
            I.root.SetActive(true);
            I.hintT = 12f; I.toastT = 0;
            I.boss = null; I.bossPanel.SetActive(false); // clear stale boss bar from a previous run
        }
        public static void Hide() { if (I != null) I.root.SetActive(false); }

        public void Toast(string s)
        {
            toastTxt.text = s; toastT = 2.4f;
            GameEvents.OnUINotify?.Invoke();
        }

        void Update()
        {
            var gm = GameManager.I;
            var pc = PlayerController.I;
            if (gm == null || pc == null) return;

            hpFill.localScale = new Vector3(Mathf.Clamp01(pc.HP / pc.S.maxHP), 1, 1);
            hpTxt.text = $"{Mathf.CeilToInt(pc.HP)}/{Mathf.CeilToInt(pc.S.maxHP)}";

            var xp = PlayerXP.I;
            if (xp != null)
            {
                xpFill.localScale = new Vector3(Mathf.Clamp01(xp.XP / xp.Next), 1, 1);
                lvlTxt.text = "Lv " + xp.Level;
            }

            int m = (int)(gm.runTime / 60f), s = (int)(gm.runTime % 60f);
            timerTxt.text = $"{m:00}:{s:00}";
            waveTxt.text = $"Wave {gm.wave} / {GameManager.MaxWave}";
            goldTxt.text = gm.goldRun + " g";

            var sb = new System.Text.StringBuilder();
            foreach (var w in pc.Weapons.Items)
                sb.AppendLine($"{w.D.name}  Lv{w.lvl}");
            weaponsTxt.text = sb.ToString();

            string dash = pc.DashCdLeft > 0 ? $"Dash {pc.DashCdLeft:0.0}s" : "Dash READY";
            string inv = pc.InvisCdLeft > 0 ? $"Invis {pc.InvisCdLeft:0.0}s" : "Invis READY";
            cdTxt.text = dash + "     " + inv;

            if (boss != null)
                bossFill.localScale = new Vector3(Mathf.Clamp01(boss.HP / boss.MaxHP), 1, 1);

            if (toastT > 0)
            {
                toastT -= Time.unscaledDeltaTime;
                var c = toastTxt.color; c.a = Mathf.Clamp01(toastT / .6f); toastTxt.color = c;
            }
            if (hintT > 0)
            {
                hintT -= Time.unscaledDeltaTime;
                var c = hintTxt.color; c.a = Mathf.Clamp01(hintT / 3f) * .55f; hintTxt.color = c;
            }
        }
    }
}
