using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VampFrost
{
    public static class Upgrades
    {
        public class Choice
        {
            public string title, desc;
            public System.Action apply;
        }

        class Passive
        {
            public string key, title, desc;
            public System.Action<Stats> apply;
            public int cap = 5, weight = 6;
        }

        static readonly Passive[] passives =
        {
            new Passive{ key="hp",    title="Vampiric Vigor", desc="+20% Max HP, heal 20%",
                apply=s=>{ s.maxHP*=1.2f; PlayerController.I?.Heal(s.maxHP*.2f);} },
            new Passive{ key="regen", title="Cold Blood",     desc="+0.4 HP/s regen",
                apply=s=>s.regen+=.4f },
            new Passive{ key="armor", title="Frost Plating",  desc="+1 Armor",
                apply=s=>s.armor+=1f },
            new Passive{ key="speed", title="Night Stride",   desc="+8% Move speed",
                apply=s=>s.moveSpeed*=1.08f },
            new Passive{ key="might", title="Crimson Might",  desc="+12% Damage",
                apply=s=>s.might*=1.12f },
            new Passive{ key="cd",    title="Swift Rituals",  desc="-6% Cooldowns",
                apply=s=>s.cooldown*=.94f },
            new Passive{ key="area",  title="Widened Frost",  desc="+10% Area",
                apply=s=>s.area*=1.10f },
            new Passive{ key="pspd",  title="Hunter's Edge",  desc="+10% Projectile speed",
                apply=s=>s.projSpeed*=1.10f },
            new Passive{ key="dur",   title="Lingering Chill",desc="+15% Effect duration",
                apply=s=>s.duration*=1.15f },
            new Passive{ key="mag",   title="Blood Scent",    desc="+35% Pickup range",
                apply=s=>s.magnet*=1.35f },
            new Passive{ key="crit",  title="Killer Instinct",desc="+5% Crit chance",
                apply=s=>s.crit+=.05f },
            new Passive{ key="gold",  title="Greed",          desc="+20% Gold gain",
                apply=s=>s.goldMul*=1.2f },
            new Passive{ key="amt",   title="Echoing Strike", desc="+1 Projectile (all)",
                apply=s=>s.amount+=1, cap=2, weight=2 },
        };

        static int PassiveLv(string key)
        {
            var p = PlayerController.I;
            return p != null && p.Passives.TryGetValue(key, out var v) ? v : 0;
        }

        public static List<Choice> Roll(int n)
        {
            var pc = PlayerController.I;
            var ws = pc.Weapons;
            var pool = new List<(Choice c, int w)>();

            for (int id = 0; id < WeaponDefs.All.Length; id++)
            {
                int wid = id;
                var d = WeaponDefs.All[id];
                int lv = ws.LevelOf(id);
                if (lv == 0 && ws.Items.Count < WeaponSystem.MaxSlots)
                    pool.Add((new Choice
                    {
                        title = "NEW  " + d.name,
                        desc = d.desc,
                        apply = () => ws.TryAddOrUpgrade(wid)
                    }, 8));
                else if (lv > 0 && lv < WeaponDef.MaxLevel)
                    pool.Add((new Choice
                    {
                        title = $"{d.name}  Lv{lv + 1}",
                        desc = "+18% damage" + (d.grow && (lv + 1 == 3 || lv + 1 == 6) ? ", +1 projectile" : ""),
                        apply = () => ws.TryAddOrUpgrade(wid)
                    }, 10));
            }

            foreach (var p in passives)
            {
                var pp = p;
                int lv = PassiveLv(p.key);
                if (lv >= p.cap) continue;
                pool.Add((new Choice
                {
                    title = $"{p.title}  {new string('I', lv + 1)}",
                    desc = p.desc,
                    apply = () =>
                    {
                        pp.apply(pc.S);
                        pc.Passives[pp.key] = PassiveLv(pp.key) + 1;
                    }
                }, p.weight));
            }

            var picks = new List<Choice>();
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                int total = 0; foreach (var e in pool) total += e.w;
                int r = Random.Range(0, total);
                for (int j = 0; j < pool.Count; j++)
                {
                    r -= pool[j].w;
                    if (r < 0) { picks.Add(pool[j].c); pool.RemoveAt(j); break; }
                }
            }
            return picks;
        }

        public static string GrantRandom()
        {
            var one = Roll(1);
            if (one.Count == 0) return "Gold!";
            one[0].apply();
            return one[0].title;
        }
    }

    public class LevelUpPanel : MonoBehaviour
    {
        static LevelUpPanel I;
        static int pending;

        GameObject root;
        readonly Button[] cards = new Button[3];
        readonly Text[] titles = new Text[3];
        readonly Text[] descs = new Text[3];
        List<Upgrades.Choice> choices;

        public static void Init()
        {
            var go = new GameObject("LevelUpPanel");
            go.transform.SetParent(UIBuilder.Root.transform, false);
            I = go.AddComponent<LevelUpPanel>();
            I.Build(go);
            go.SetActive(false);
        }

        void Build(GameObject go)
        {
            root = go;
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var dim = UIBuilder.Stretch(go.transform, "dim", new Color(0, 0, 0, .72f));
            dim.raycastTarget = true;

            UIBuilder.Txt(go.transform, "LEVEL UP!", 56, UIBuilder.Frost,
                new Vector2(0, 260), new Vector2(600, 70),
                TextAnchor.MiddleCenter, null, FontStyle.Bold);

            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                float x = (i - 1) * 330f;
                var b = UIBuilder.Btn(go.transform, "", new Vector2(300, 340),
                    new Vector2(x, -10), () => Pick(idx), null, 22,
                    new Color(.09f, .13f, .22f, .98f));
                cards[i] = b;
                titles[i] = UIBuilder.Txt(b.transform, "", 24, UIBuilder.Frost,
                    new Vector2(0, 110), new Vector2(280, 90),
                    TextAnchor.MiddleCenter, null, FontStyle.Bold);
                descs[i] = UIBuilder.Txt(b.transform, "", 19, new Color(.9f, .93f, 1f),
                    new Vector2(0, -30), new Vector2(260, 180), TextAnchor.UpperCenter);
                descs[i].horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }

        public static void Enqueue()
        {
            pending++;
            if (I != null && !I.root.activeSelf) I.Open();
        }

        void Open()
        {
            GameManager.I.state = GameManager.State.LevelUp;
            Time.timeScale = 0f;
            GameEvents.OnUIOpen?.Invoke();
            choices = Upgrades.Roll(3);
            for (int i = 0; i < 3; i++)
            {
                bool has = i < choices.Count;
                cards[i].gameObject.SetActive(has);
                if (!has) continue;
                titles[i].text = choices[i].title;
                descs[i].text = choices[i].desc;
            }
            root.SetActive(true);
        }

        void Pick(int i)
        {
            if (i < choices.Count) choices[i].apply();
            GameEvents.OnUIConfirm?.Invoke();
            pending--;
            if (pending > 0) { Open(); return; }
            root.SetActive(false);
            Time.timeScale = 1f;
            GameManager.I.state = GameManager.State.Playing;
        }

        public static void ResetQueue() { pending = 0; if (I != null) I.root.SetActive(false); }
    }
}
