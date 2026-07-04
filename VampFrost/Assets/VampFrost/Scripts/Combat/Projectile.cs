using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class Projectile : MonoBehaviour
    {
        static readonly Stack<Projectile> pool = new();
        public static Transform Root;

        // combat
        public bool friendly;
        public float dmg, speed, life, hitRadius, hitInterval;
        public int pierce;
        public float critC, critM, frost, steal;
        // motion
        public ProjBehavior beh;
        public Vector2 dir;
        public float angle, orbRadius, orbGrow, spin, maxDist, ringR;
        Vector2 origin;
        bool returning, ringHitPlayer;
        float tick, age, baseScale;
        readonly HashSet<Enemy> hitSet = new();
        SpriteRenderer sr;

        // ---------- pooling ----------
        static Projectile Get()
        {
            Projectile p;
            if (pool.Count > 0) { p = pool.Pop(); p.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("proj");
                go.transform.SetParent(Root, false);
                p = go.AddComponent<Projectile>();
                p.sr = go.AddComponent<SpriteRenderer>();
            }
            return p;
        }

        public void ForceRelease() => Release();
        void Release() { gameObject.SetActive(false); pool.Push(this); }
        public static void ClearPool() => pool.Clear();

        // ---------- spawn ----------
        public static Projectile SpawnFriendly(Vector2 pos, Vector2 direction, ProjBehavior behavior,
            Sprite sprite, float visScale, float dmg, float speed, float life, int pierce,
            float hitRadius, float critC, float critM, float frost, float steal,
            float hitInterval = 999f, float orbRadius = 0, float orbGrow = 0,
            float startAngle = 0, float maxDist = 0, float spin = 0)
        {
            var p = Get();
            p.friendly = true;
            p.transform.position = pos;
            p.dir = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            p.beh = behavior;
            p.sr.sprite = sprite;
            p.sr.color = Color.white;
            p.baseScale = visScale;
            p.transform.localScale = Vector3.one * visScale;
            p.dmg = dmg; p.speed = speed; p.life = life; p.pierce = pierce;
            p.hitRadius = hitRadius; p.critC = critC; p.critM = critM;
            p.frost = frost; p.steal = steal;
            p.hitInterval = hitInterval; p.tick = 0;
            p.orbRadius = orbRadius; p.orbGrow = orbGrow;
            p.angle = startAngle; p.maxDist = maxDist; p.spin = spin;
            p.origin = pos; p.returning = false;
            p.ringR = .1f; p.ringHitPlayer = false; p.age = 0;
            p.hitSet.Clear();
            p.sr.sortingOrder = behavior == ProjBehavior.Pool ? -200 : 500;
            p.FaceDir();
            if (behavior == ProjBehavior.Blast)
            {
                p.DoBlast();
                GameEvents.OnExplosion?.Invoke();
                CameraRig.Shake(.09f, .12f);
            }
            if (behavior == ProjBehavior.Fang) p.DoBlast();
            return p;
        }

        public static void SpawnHostile(Vector2 pos, Vector2 direction, float dmg, float speed,
            float life, Color col, float sizeScale, ProjBehavior behavior = ProjBehavior.Straight)
        {
            var p = Get();
            p.friendly = false;
            p.transform.position = pos;
            p.dir = direction.sqrMagnitude > .001f ? direction.normalized : Vector2.right;
            p.beh = behavior;
            p.sr.sprite = behavior == ProjBehavior.Ring
                ? SpriteFactory.RingSprite(col, 48, 4)
                : SpriteFactory.Projectile(ProjShape.Orb, col, .8f);
            p.sr.color = Color.white;
            p.baseScale = sizeScale;
            p.transform.localScale = Vector3.one * sizeScale;
            p.dmg = dmg; p.speed = speed; p.life = life;
            p.hitRadius = .22f * sizeScale;
            p.pierce = 0; p.ringR = .1f; p.ringHitPlayer = false;
            p.age = 0; p.spin = 0;
            p.hitSet.Clear();
            p.sr.sortingOrder = 500;
            p.FaceDir();
        }

        void FaceDir()
        {
            if (beh == ProjBehavior.Straight || beh == ProjBehavior.Boomerang || beh == ProjBehavior.Fang)
                transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            else transform.rotation = Quaternion.identity;
        }

        // ---------- update ----------
        void Update()
        {
            var gm = GameManager.I;
            if (gm == null) return;
            if (gm.state != GameManager.State.Playing) return;
            float dt = Time.deltaTime;
            age += dt;
            var pc = PlayerController.I;
            Vector2 pos = transform.position;

            switch (beh)
            {
                case ProjBehavior.Straight:
                    pos += dir * speed * dt;
                    life -= dt;
                    break;

                case ProjBehavior.Boomerang:
                    if (!returning)
                    {
                        pos += dir * speed * dt;
                        if ((pos - origin).sqrMagnitude > maxDist * maxDist)
                        { returning = true; hitSet.Clear(); }
                    }
                    else if (pc != null)
                    {
                        Vector2 back = ((Vector2)pc.transform.position - pos);
                        if (back.sqrMagnitude < .35f) { Release(); return; }
                        pos += back.normalized * speed * 1.25f * dt;
                    }
                    else { Release(); return; }
                    life -= dt;
                    break;

                case ProjBehavior.Orbit:
                case ProjBehavior.OrbitOut:
                case ProjBehavior.ArcSwing:
                    if (pc == null) { Release(); return; }
                    angle += speed * dt; // degrees/sec
                    if (beh == ProjBehavior.OrbitOut) orbRadius += orbGrow * dt;
                    float rad = angle * Mathf.Deg2Rad;
                    pos = (Vector2)pc.transform.position +
                          new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * orbRadius;
                    transform.rotation = Quaternion.Euler(0, 0, angle + 90f);
                    if (beh != ProjBehavior.Orbit) life -= dt;
                    break;

                case ProjBehavior.Blast:
                case ProjBehavior.Fang:
                    life -= dt;
                    float k = 1f - Mathf.Clamp01(life / .25f);
                    transform.localScale = Vector3.one * baseScale * Mathf.Lerp(.4f, 1.1f, k);
                    sr.color = new Color(1, 1, 1, 1f - k * .9f);
                    break;

                case ProjBehavior.Pool:
                    life -= dt;
                    sr.color = new Color(1, 1, 1, .55f + .25f * Mathf.Sin(age * 6f));
                    break;

                case ProjBehavior.Ring:
                    ringR += speed * dt;
                    life -= dt;
                    transform.localScale = Vector3.one * (ringR * 2f / 1.5625f);
                    sr.color = new Color(1, 1, 1, Mathf.Clamp01(life * 2.5f));
                    break;
            }

            if (spin != 0) transform.Rotate(0, 0, spin * dt);
            transform.position = pos;

            // ---------- collisions ----------
            if (friendly) HitEnemies(pos, dt);
            else HitPlayer(pos, pc);

            if (life <= 0 && !(beh == ProjBehavior.Orbit)) Release();
        }

        void HitEnemies(Vector2 pos, float dt)
        {
            switch (beh)
            {
                case ProjBehavior.Straight:
                case ProjBehavior.Boomerang:
                    for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                    {
                        var e = Enemy.Active[i];
                        if (e == null || hitSet.Contains(e)) continue;
                        float r = hitRadius + e.Radius;
                        if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
                        {
                            hitSet.Add(e);
                            e.TakeDamage(dmg, critC, critM, frost, steal, pos);
                            if (--pierce < 0 && beh == ProjBehavior.Straight) { Release(); return; }
                        }
                    }
                    break;

                case ProjBehavior.Orbit:
                case ProjBehavior.OrbitOut:
                    tick -= dt;
                    if (tick <= 0)
                    {
                        bool any = false;
                        for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                        {
                            var e = Enemy.Active[i];
                            if (e == null) continue;
                            float r = hitRadius + e.Radius;
                            if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
                            { e.TakeDamage(dmg, critC, critM, frost, steal, pos); any = true; }
                        }
                        if (any) tick = hitInterval;
                    }
                    break;

                case ProjBehavior.ArcSwing:
                    for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                    {
                        var e = Enemy.Active[i];
                        if (e == null || hitSet.Contains(e)) continue;
                        float r = hitRadius + e.Radius;
                        if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
                        { hitSet.Add(e); e.TakeDamage(dmg, critC, critM, frost, steal, pos); }
                    }
                    break;

                case ProjBehavior.Pool:
                    tick -= dt;
                    if (tick <= 0)
                    {
                        tick = hitInterval;
                        for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                        {
                            var e = Enemy.Active[i];
                            if (e == null) continue;
                            float r = hitRadius + e.Radius;
                            if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
                                e.TakeDamage(dmg, critC, critM, frost, steal, pos);
                        }
                    }
                    break;

                case ProjBehavior.Ring:
                    for (int i = Enemy.Active.Count - 1; i >= 0; i--)
                    {
                        var e = Enemy.Active[i];
                        if (e == null || hitSet.Contains(e)) continue;
                        float d = Vector2.Distance(e.transform.position, pos);
                        if (Mathf.Abs(d - ringR) < .45f + e.Radius)
                        { hitSet.Add(e); e.TakeDamage(dmg, critC, critM, frost, steal, pos); }
                    }
                    break;
            }
        }

        void DoBlast()
        {
            Vector2 pos = transform.position;
            for (int i = Enemy.Active.Count - 1; i >= 0; i--)
            {
                var e = Enemy.Active[i];
                if (e == null) continue;
                float r = hitRadius + e.Radius;
                if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
                    e.TakeDamage(dmg, critC, critM, frost, steal, pos);
            }
        }

        void HitPlayer(Vector2 pos, PlayerController pc)
        {
            if (pc == null || pc.Dead) { if (beh != ProjBehavior.Ring) return; }
            if (pc == null) return;

            if (beh == ProjBehavior.Ring)
            {
                if (ringHitPlayer) return;
                float d = Vector2.Distance(pc.transform.position, pos);
                if (Mathf.Abs(d - ringR) < .5f)
                { ringHitPlayer = true; pc.TakeDamage(dmg); }
            }
            else
            {
                float r = hitRadius + .3f;
                if (((Vector2)pc.transform.position - pos).sqrMagnitude < r * r)
                { pc.TakeDamage(dmg); Release(); }
            }
        }
    }
}
