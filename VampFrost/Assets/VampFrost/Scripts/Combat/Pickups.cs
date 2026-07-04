using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class XPGem : MonoBehaviour
    {
        static readonly Stack<XPGem> pool = new();
        float value; SpriteRenderer sr; float pullSpd;

        public static void Spawn(Vector2 pos, float value)
        {
            XPGem g;
            if (pool.Count > 0) { g = pool.Pop(); g.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("xp");
                go.transform.SetParent(GameManager.World, false);
                g = go.AddComponent<XPGem>();
                g.sr = go.AddComponent<SpriteRenderer>();
            }
            g.transform.position = pos + Random.insideUnitCircle * .15f;
            g.value = value;
            bool big = value >= 5f;
            g.sr.sprite = SpriteFactory.Circle(big ? new Color(.55f, .3f, .95f) : new Color(.4f, .75f, 1f), big ? 10 : 7, false);
            g.sr.sortingOrder = 50;
            g.pullSpd = 0;
        }

        public static void ClearPool() => pool.Clear();

        void Update()
        {
            var pc = PlayerController.I;
            if (pc == null || pc.Dead || GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            Vector2 to = (Vector2)pc.transform.position - (Vector2)transform.position;
            float d = to.magnitude;
            if (d < pc.S.magnet)
            {
                pullSpd = Mathf.Min(14f, pullSpd + 40f * Time.deltaTime);
                transform.position += (Vector3)(to.normalized * pullSpd * Time.deltaTime);
            }
            if (d < .35f)
            {
                PlayerXP.I?.Add(value);
                gameObject.SetActive(false);
                pool.Push(this);
            }
        }
    }

    public class GoldPickup : MonoBehaviour
    {
        static readonly Stack<GoldPickup> pool = new();
        int value; float pullSpd;

        public static void Spawn(Vector2 pos, int value)
        {
            GoldPickup g;
            if (pool.Count > 0) { g = pool.Pop(); g.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("gold");
                go.transform.SetParent(GameManager.World, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = SpriteFactory.Circle(new Color(.95f, .8f, .25f), 8, true);
                sr.sortingOrder = 51;
                g = go.AddComponent<GoldPickup>();
            }
            g.transform.position = pos + Random.insideUnitCircle * .2f;
            g.value = value; g.pullSpd = 0;
        }

        public static void ClearPool() => pool.Clear();

        void Update()
        {
            var pc = PlayerController.I;
            if (pc == null || pc.Dead || GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            Vector2 to = (Vector2)pc.transform.position - (Vector2)transform.position;
            float d = to.magnitude;
            if (d < pc.S.magnet)
            {
                pullSpd = Mathf.Min(13f, pullSpd + 36f * Time.deltaTime);
                transform.position += (Vector3)(to.normalized * pullSpd * Time.deltaTime);
            }
            if (d < .35f)
            {
                GameManager.I.AddGold(Mathf.RoundToInt(value * pc.S.goldMul));
                gameObject.SetActive(false);
                pool.Push(this);
            }
        }
    }

    public class HealthPickup : MonoBehaviour
    {
        public static void Spawn(Vector2 pos)
        {
            var go = new GameObject("hp");
            go.transform.SetParent(GameManager.World, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Circle(new Color(.95f, .25f, .3f), 10, true);
            sr.sortingOrder = 51;
            go.AddComponent<HealthPickup>().transform.position = pos;
        }

        void Update()
        {
            var pc = PlayerController.I;
            if (pc == null || pc.Dead || GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            if (Vector2.Distance(pc.transform.position, transform.position) < .45f)
            {
                pc.Heal(pc.S.maxHP * .25f);
                GameEvents.OnHealthPickup?.Invoke();
                Destroy(gameObject);
            }
        }
    }

    public class ChestPickup : MonoBehaviour
    {
        public static void Spawn(Vector2 pos)
        {
            var go = new GameObject("chest");
            go.transform.SetParent(GameManager.World, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Chest();
            sr.sortingOrder = Mathf.RoundToInt(-pos.y * 10f);
            go.AddComponent<ChestPickup>().transform.position = pos;
        }

        void Update()
        {
            var pc = PlayerController.I;
            if (pc == null || pc.Dead || GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            if (Vector2.Distance(pc.transform.position, transform.position) < .6f)
            {
                GameEvents.OnChestOpen?.Invoke();
                int rolls = Random.value < .05f ? 3 : Random.value < .3f ? 2 : 1;
                string txt = "";
                for (int i = 0; i < rolls; i++)
                {
                    string got = Upgrades.GrantRandom();
                    txt += (i > 0 ? "  +  " : "") + got;
                }
                GameManager.I.AddGold(25);
                HUD.I?.Toast("Chest: " + txt);
                Destroy(gameObject);
            }
        }
    }
}
