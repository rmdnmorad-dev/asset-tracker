using System.Collections;
using UnityEngine;

namespace VampFrost
{
    public class Boss : Enemy
    {
        public static Boss Current;

        public BossDef B;
        public bool Mini;
        public int Phase { get; private set; } = 1;

        float invulnT;
        float tSummon, tRadial, tBurst, tCharge, tTele, tRing, tAoe;
        float chargeT; Vector2 chargeDir;
        BossPhase P => B.phases[Phase - 1];

        public static Boss Spawn(BossDef def, Vector2 pos, bool mini)
        {
            var go = new GameObject("Boss_" + def.key);
            go.transform.SetParent(GameManager.World, false);
            var b = go.AddComponent<Boss>();
            b.sr = go.AddComponent<SpriteRenderer>();
            b.B = def; b.Mini = mini; b.IsBoss = true;
            b.transform.position = pos;

            b.MaxHP = b.HP = def.hp * (mini ? 1f : 1f);
            b.Dmg = def.dmg; b.Speed = def.speed; b.Radius = .55f * def.size;
            b.sr.sprite = SpriteFactory.BossSprite(def);
            b.sr.color = Color.white;
            b.D = new MobDef { key = def.key, name = def.name, color = def.color, xp = 30 };

            Active.Add(b);
            Current = b;
            GameEvents.OnBossSpawn?.Invoke(b);
            CameraRig.Shake(.25f, .4f);
            return b;
        }

        protected override void Update()
        {
            if (GameManager.I == null || GameManager.I.state != GameManager.State.Playing) return;
            var pc = PlayerController.I; if (pc == null) return;
            float dt = Time.deltaTime;
            invulnT -= dt; hitFlashT -= dt;

            Vector2 pos = transform.position;
            Vector2 toP = (Vector2)pc.transform.position - pos;
            float dist = toP.magnitude;
            Vector2 dir = dist > .001f ? toP / dist : Vector2.right;

            float spd = Speed * P.speedMul;

            if (chargeT > 0)
            {
                chargeT -= dt;
                pos += chargeDir * spd * 4.2f * dt;
                if (!pc.IsInvisible && dist < Radius + .35f && atkCd <= 0)
                { atkCd = .5f; pc.TakeDamage(Dmg * 1.5f); }
            }
            else
            {
                if (!pc.IsInvisible) pos += dir * spd * dt;
                atkCd -= dt;
                if (!pc.IsInvisible && dist < Radius + .35f && atkCd <= 0)
                { atkCd = .7f; pc.TakeDamage(Dmg); }
            }
            transform.position = pos;

            // ---- pattern timers ----
            TickPattern(ref tSummon, P.summonEvery, dt, () =>
            {
                int n = 2 + Phase;
                for (int i = 0; i < n; i++)
                    Enemy.Spawn(EnemyDefs.Mobs[P.summonId],
                        pos + Random.insideUnitCircle.normalized * 1.6f,
                        GameManager.I.wave, false);
            });

            TickPattern(ref tRadial, P.radialEvery, dt, () =>
            {
                for (int i = 0; i < P.radialCount; i++)
                {
                    float a = i * Mathf.PI * 2f / P.radialCount + Time.time;
                    Projectile.SpawnHostile(pos, new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                        Dmg * .7f, 5.5f, 3.5f, B.color, 1.1f);
                }
            });

            TickPattern(ref tBurst, P.burstEvery, dt, () =>
            {
                for (int i = 0; i < P.burstCount; i++)
                {
                    float spread = (i - (P.burstCount - 1) * .5f) * .12f;
                    Vector2 d = Rot(dir, spread);
                    Projectile.SpawnHostile(pos, d, Dmg * .6f, 8f, 2.5f,
                        Color.Lerp(B.color, Color.white, .3f), .9f);
                }
            });

            TickPattern(ref tCharge, P.chargeEvery, dt, () =>
            {
                chargeDir = dir; chargeT = .55f;
                GameEvents.OnBossHeavy?.Invoke();
                CameraRig.Shake(.18f, .3f);
            });

            TickPattern(ref tTele, P.teleportEvery, dt, () =>
            {
                GameEvents.OnEnemyTelegraph?.Invoke(pos);
                float a = Random.value * Mathf.PI * 2f;
                transform.position = (Vector2)pc.transform.position +
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2.5f, 4f);
            });

            TickPattern(ref tRing, P.ringEvery, dt, () =>
            {
                Projectile.SpawnHostile(pos, Vector2.right, Dmg * .8f, 6f, 1.4f,
                    B.color, 1f, ProjBehavior.Ring);
                GameEvents.OnBossHeavy?.Invoke();
            });

            TickPattern(ref tAoe, P.aoeEvery, dt, () => StartCoroutine(AoeAtPlayer()));

            // visuals
            sr.flipX = dir.x < 0;
            Color c = invulnT > 0 ? Color.Lerp(Color.white, B.color, Mathf.PingPong(Time.time * 10, 1))
                                  : (hitFlashT > 0 ? Color.white * 1.5f : Color.white);
            sr.color = c;
            sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f) + 1;
        }

        static void TickPattern(ref float t, float every, float dt, System.Action fire)
        {
            if (every <= 0) return;
            t += dt;
            if (t >= every) { t = 0; fire(); }
        }

        static Vector2 Rot(Vector2 v, float rad)
        {
            float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        IEnumerator AoeAtPlayer()
        {
            var pc = PlayerController.I; if (pc == null) yield break;
            Vector2 target = pc.transform.position;
            float radius = 1.9f;

            var marker = new GameObject("aoe");
            marker.transform.SetParent(GameManager.World, false);
            marker.transform.position = target;
            var msr = marker.AddComponent<SpriteRenderer>();
            msr.sprite = SpriteFactory.RingSprite(new Color(1f, .3f, .2f, .8f), 48, 4);
            msr.sortingOrder = -100;

            float warn = .9f, t = 0;
            while (t < warn)
            {
                t += Time.deltaTime;
                marker.transform.localScale = Vector3.one * (radius * 2f / 1.5f) * Mathf.Lerp(.2f, 1f, t / warn);
                yield return null;
            }
            GameEvents.OnBossHeavy?.Invoke();
            GameEvents.OnExplosion?.Invoke();
            CameraRig.Shake(.22f, .25f);
            if (pc != null && !pc.IsInvisible &&
                Vector2.Distance(pc.transform.position, target) < radius)
                pc.TakeDamage(Dmg * 1.2f);
            Destroy(marker);
        }

        public override void TakeDamage(float dmg, float critC, float critM, float frost, float steal, Vector2 srcPos)
        {
            if (invulnT > 0) return;
            base.TakeDamage(dmg, critC, critM, frost * .3f, steal, srcPos); // bosses resist freeze
            knock = Vector2.zero;

            if (HP > 0)
            {
                float f = HP / MaxHP;
                int wanted = f > 2f / 3f ? 1 : f > 1f / 3f ? 2 : 3;
                if (wanted > Phase)
                {
                    Phase = wanted;
                    invulnT = 1f;
                    GameEvents.OnBossPhaseChange?.Invoke(Phase);
                    CameraRig.Shake(.2f, .5f);
                }
            }
        }

        protected override void Die()
        {
            for (int i = 0; i < (Mini ? 8 : 20); i++)
                XPGem.Spawn((Vector2)transform.position + Random.insideUnitCircle * 1.2f, 6f);
            GoldPickup.Spawn(transform.position, Mini ? 40 : 120);
            ChestPickup.Spawn(transform.position);
            if (Current == this) Current = null;
            GameEvents.OnBossDeath?.Invoke();
            CameraRig.Shake(.35f, .6f);
            Active.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() { if (Current == this) Current = null; }
    }
}
