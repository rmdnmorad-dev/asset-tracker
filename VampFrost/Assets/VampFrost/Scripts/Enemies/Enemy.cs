using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class Enemy : MonoBehaviour
    {
        public static readonly List<Enemy> Active = new();
        static readonly Stack<Enemy> pool = new();

        public MobDef D;
        public float HP, MaxHP, Dmg, Speed, Radius;
        public bool Elite;
        public bool IsBoss;

        protected SpriteRenderer sr;
        protected float atkCd, hitFlashT, slowT, freezeT, frostStackT;
        protected int frostStacks;
        protected Vector2 knock;
        float fireT; bool telegraphed;
        float tickT;
        static int sepCursor;

        // ---------- spawning / pooling ----------
        public static Enemy Spawn(MobDef d, Vector2 pos, int wave, bool elite)
        {
            Enemy e;
            if (pool.Count > 0) { e = pool.Pop(); e.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("Enemy");
                go.transform.SetParent(GameManager.World, false);
                e = go.AddComponent<Enemy>();
                e.sr = go.AddComponent<SpriteRenderer>();
            }
            e.transform.position = pos;
            e.Init(d, wave, elite);
            Active.Add(e);
            GameEvents.OnEnemySpawn?.Invoke(e);
            return e;
        }

        protected virtual void Init(MobDef d, int wave, bool elite)
        {
            D = d; Elite = elite; IsBoss = false;
            float hs = 1f + (wave - 1) * 0.45f;
            float ds = 1f + (wave - 1) * 0.16f;
            MaxHP = HP = d.hp * hs * (elite ? 10f : 1f);
            Dmg = d.dmg * ds * (elite ? 1.8f : 1f);
            Speed = d.speed * (1f + wave * 0.015f) * (elite ? 1.05f : 1f);
            Radius = d.radius * (elite ? 1.5f : 1f);
            sr.sprite = SpriteFactory.Mob(d);
            transform.localScale = Vector3.one * (elite ? 1.5f : 1f);
            sr.color = Color.white;
            atkCd = hitFlashT = slowT = freezeT = frostStackT = knock.x = knock.y = 0;
            frostStacks = 0; telegraphed = false;
            fireT = d.ranged ? Random.Range(1f, d.fireCd) : 0;
            tickT = Random.Range(1f, 4f);
        }

        void OnDisable() { Active.Remove(this); }
        protected void Release()
        {
            gameObject.SetActive(false);
            if (!IsBoss) pool.Push(this);
        }
        public static void ClearPool() { pool.Clear(); Active.Clear(); }

        // ---------- behaviour ----------
        protected virtual void Update()
        {
            if (GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            var pc = PlayerController.I;
            if (pc == null) return;
            float dt = Time.deltaTime;

            hitFlashT -= dt; slowT -= dt; frostStackT -= dt; atkCd -= dt;
            if (frostStackT <= 0) frostStacks = 0;
            if (freezeT > 0) freezeT -= dt;

            Vector2 pos = transform.position;
            Vector2 toPlayer = (Vector2)pc.transform.position - pos;
            float dist = toPlayer.magnitude;
            Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.right;

            // occasional creepy movement tick
            tickT -= dt;
            if (tickT <= 0 && dist < 9f) { tickT = Random.Range(2.5f, 6f); GameEvents.OnEnemyTick?.Invoke(); }

            if (freezeT <= 0)
            {
                float spd = Speed * (slowT > 0 ? 0.45f : 1f);
                Vector2 move;

                if (pc.IsInvisible)
                    move = new Vector2(Mathf.Sin(Time.time * .7f + GetInstanceID()), Mathf.Cos(Time.time * .6f + GetInstanceID())) * .4f;
                else if (D.ranged && dist < D.range * .55f)
                    move = -dir; // backpedal
                else if (D.ranged && dist < D.range)
                    move = Vector2.Perpendicular(dir) * ((GetInstanceID() & 1) == 0 ? 1 : -1) * .5f;
                else
                    move = dir;

                // cheap separation: test one random neighbour per frame
                if (Active.Count > 1)
                {
                    sepCursor = (sepCursor + 1) % Active.Count;
                    var o = Active[sepCursor];
                    if (o != this && o != null)
                    {
                        Vector2 d2 = pos - (Vector2)o.transform.position;
                        float m = d2.magnitude, min = (Radius + o.Radius) * 1.4f;
                        if (m > 0.001f && m < min) move += d2 / m * (min - m) * 2f;
                    }
                }

                pos += (move.normalized * spd + knock) * dt;
                transform.position = pos;

                // ranged fire
                if (D.ranged && !pc.IsInvisible && dist < D.range)
                {
                    fireT -= dt;
                    if (fireT <= .45f && !telegraphed)
                    { telegraphed = true; GameEvents.OnEnemyTelegraph?.Invoke(pos); }
                    if (fireT <= 0)
                    {
                        telegraphed = false; fireT = D.fireCd;
                        Projectile.SpawnHostile(pos, dir, Dmg, D.projSpeed, 3f,
                            Color.Lerp(D.color, Color.white, .3f), 1f);
                    }
                }

                // contact damage
                if (!pc.IsInvisible && dist < Radius + .32f && atkCd <= 0)
                { atkCd = .65f; pc.TakeDamage(Dmg); }
            }

            knock = Vector2.MoveTowards(knock, Vector2.zero, 18f * dt);

            // teleport back if far off-screen (VS style)
            if (dist > 26f)
            {
                float a = Random.value * Mathf.PI * 2f;
                transform.position = (Vector2)pc.transform.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 13f;
            }

            // visuals
            sr.flipX = dir.x < 0;
            Color baseC = Color.white;
            if (Elite) baseC = new Color(1f, .85f, .6f);
            if (freezeT > 0) baseC = new Color(.55f, .85f, 1f);
            else if (slowT > 0) baseC = Color.Lerp(baseC, new Color(.7f, .9f, 1f), .5f);
            if (telegraphed) baseC = Color.Lerp(baseC, new Color(1f, .4f, .3f), Mathf.PingPong(Time.time * 8f, 1f));
            sr.color = hitFlashT > 0 ? Color.Lerp(baseC, Color.white, 3f) : baseC;
            sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f);
        }

        // ---------- damage ----------
        public virtual void TakeDamage(float dmg, float critC, float critM, float frost, float steal, Vector2 srcPos)
        {
            bool crit = Random.value < critC;
            float final = dmg * (crit ? critM : 1f);
            HP -= final;
            hitFlashT = .07f;
            GameEvents.OnHit?.Invoke(crit);
            FX.Damage(transform.position, Mathf.RoundToInt(final), crit);

            if (steal > 0 && PlayerController.I != null)
                PlayerController.I.Heal(final * steal);

            Vector2 kd = ((Vector2)transform.position - srcPos).normalized;
            knock += kd * (crit ? 4.5f : 2.5f) / Mathf.Max(1f, transform.localScale.x);

            if (frost > 0 && Random.value < frost)
            {
                slowT = Mathf.Max(slowT, 2f);
                frostStacks++; frostStackT = 2f;
                if (frostStacks >= 3 && freezeT <= 0)
                { freezeT = 1.2f; frostStacks = 0; GameEvents.OnFreezeApplied?.Invoke(); }
            }
            if (HP <= 0) Die();
        }

        protected virtual void Die()
        {
            int wave = GameManager.I != null ? GameManager.I.wave : 1;
            XPGem.Spawn(transform.position, D.xp * (1f + wave * .15f));
            if (Random.value < .08f) GoldPickup.Spawn(transform.position, 1 + wave / 5);
            if (Random.value < .02f) HealthPickup.Spawn(transform.position);
            if (Elite) ChestPickup.Spawn(transform.position);
            GameEvents.OnEnemyDeath?.Invoke(this);
            Release();
        }
    }
}
