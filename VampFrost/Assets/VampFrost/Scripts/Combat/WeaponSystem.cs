using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class WeaponInst
    {
        public int id, lvl = 1;
        public float cdT;
        public readonly List<Projectile> orbs = new();
        public WeaponDef D => WeaponDefs.All[id];

        public float DmgMul => 1f + .18f * (lvl - 1);
        public float AreaMul => 1f + .08f * (lvl - 1);
        public int Count(Stats s) => D.count
            + (D.grow ? (lvl >= 3 ? 1 : 0) + (lvl >= 6 ? 1 : 0) : 0)
            + (D.usesAmount ? s.amount : 0);
        public float Cd(Stats s) => D.cd * Mathf.Pow(.96f, lvl - 1) * s.cooldown;
        public float Dmg(Stats s) => D.dmg * DmgMul * s.might;
        public float Spd(Stats s) => D.speed * s.projSpeed;
        public float Area(Stats s) => D.area * AreaMul * s.area;
        public float Life(Stats s) => D.life *
            ((D.beh == ProjBehavior.Pool || D.beh == ProjBehavior.OrbitOut) ? s.duration : 1f);
    }

    public class WeaponSystem : MonoBehaviour
    {
        public const int MaxSlots = 6;
        public readonly List<WeaponInst> Items = new();
        PlayerController pc;

        void Awake()
        {
            pc = GetComponent<PlayerController>();
            GameEvents.OnPlayerDash += OnDash;
        }
        void OnDestroy() { GameEvents.OnPlayerDash -= OnDash; }

        public bool Owns(int id) { foreach (var w in Items) if (w.id == id) return true; return false; }
        public int LevelOf(int id) { foreach (var w in Items) if (w.id == id) return w.lvl; return 0; }
        public WeaponInst GetInst(int id) { foreach (var w in Items) if (w.id == id) return w; return null; }

        public bool CanTake(int id)
        {
            var w = GetInst(id);
            if (w != null) return w.lvl < WeaponDef.MaxLevel;
            return Items.Count < MaxSlots;
        }

        /// Adds the weapon or upgrades it. Returns the resulting level.
        public int TryAddOrUpgrade(int id)
        {
            var w = GetInst(id);
            if (w == null)
            {
                if (Items.Count >= MaxSlots) return 0;
                w = new WeaponInst { id = id };
                Items.Add(w);
            }
            else if (w.lvl < WeaponDef.MaxLevel) w.lvl++;

            if (id == 10) pc.S.dashCd = 2.5f * Mathf.Pow(.9f, w.lvl); // Bite Dash
            if (w.D.beh == ProjBehavior.Orbit) RespawnOrbs(w);
            return w.lvl;
        }

        void Update()
        {
            if (GameManager.I == null || GameManager.I.state != GameManager.State.Playing || pc.Dead) return;
            float dt = Time.deltaTime;
            var s = pc.S;

            foreach (var w in Items)
            {
                if (w.D.beh == ProjBehavior.Orbit) { MaintainOrbs(w); continue; }
                if (w.D.beh == ProjBehavior.Fang) continue; // dash-triggered
                w.cdT -= dt;
                if (w.cdT <= 0)
                {
                    w.cdT = w.Cd(s);
                    Fire(w);
                }
            }
        }

        // ---------- targeting ----------
        Enemy Nearest(float maxRange = 14f)
        {
            Enemy best = null; float bd = maxRange * maxRange;
            Vector2 p = pc.transform.position;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                var e = Enemy.Active[i]; if (e == null) continue;
                float d = ((Vector2)e.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = e; }
            }
            return best;
        }

        Enemy RandomEnemy(float maxRange = 9f)
        {
            Vector2 p = pc.transform.position;
            float r2 = maxRange * maxRange;
            Enemy pick = null; int seen = 0;
            for (int i = 0; i < Enemy.Active.Count; i++)
            {
                var e = Enemy.Active[i]; if (e == null) continue;
                if (((Vector2)e.transform.position - p).sqrMagnitude > r2) continue;
                seen++;
                if (Random.Range(0, seen) == 0) pick = e;
            }
            return pick;
        }

        Vector2 AimDir(WeaponInst w, out Vector2 targetPos)
        {
            Vector2 p = pc.transform.position;
            targetPos = p;
            switch (w.D.aim)
            {
                case AimMode.Nearest:
                    var n = Nearest();
                    if (n != null) { targetPos = n.transform.position; return (targetPos - p).normalized; }
                    return pc.Facing;
                case AimMode.RandomEnemy:
                    var r = RandomEnemy();
                    if (r != null) { targetPos = r.transform.position; return (targetPos - p).normalized; }
                    targetPos = p + pc.Facing * 2f;
                    return pc.Facing;
                case AimMode.MoveDir:
                    return pc.MoveDir.sqrMagnitude > .01f ? pc.MoveDir.normalized : pc.Facing;
                default:
                    return pc.Facing;
            }
        }

        static Vector2 Rot(Vector2 v, float deg)
        {
            float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ---------- fire ----------
        void Fire(WeaponInst w)
        {
            var s = pc.S;
            var d = w.D;
            Vector2 p = pc.transform.position;
            int count = w.Count(s);
            var sprite = SpriteFactory.Projectile(d.shape, d.col, d.scale);
            float hitR = .28f * d.scale;

            GameEvents.OnWeaponFire?.Invoke(w.id);

            switch (d.beh)
            {
                case ProjBehavior.Straight:
                {
                    Vector2 baseDir = AimDir(w, out _);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = d.aim == AimMode.RandomDir
                            ? Rot(Vector2.right, Random.value * 360f)
                            : Rot(baseDir, (i - (count - 1) * .5f) * 10f);
                        Projectile.SpawnFriendly(p, dir, d.beh, sprite, d.scale,
                            w.Dmg(s), w.Spd(s), d.life, d.pierce, hitR,
                            s.crit, s.critMult, d.frost, d.steal,
                            spin: d.shape == ProjShape.Star || d.shape == ProjShape.Disc ? 720f : 0f);
                    }
                    break;
                }
                case ProjBehavior.Boomerang:
                {
                    Vector2 baseDir = AimDir(w, out _);
                    for (int i = 0; i < count; i++)
                    {
                        Vector2 dir = Rot(baseDir, (i - (count - 1) * .5f) * 24f);
                        Projectile.SpawnFriendly(p, dir, d.beh, sprite, d.scale,
                            w.Dmg(s), w.Spd(s), 4f, 99, hitR,
                            s.crit, s.critMult, d.frost, d.steal,
                            maxDist: 4.5f * s.area * w.AreaMul, spin: 900f);
                    }
                    break;
                }
                case ProjBehavior.ArcSwing:
                {
                    float faceA = Mathf.Atan2(pc.Facing.y, pc.Facing.x) * Mathf.Rad2Deg;
                    float radius = 1.7f * w.Area(s);
                    for (int i = 0; i < count; i++)
                    {
                        float start = faceA - 70f + i * (360f / count);
                        Projectile.SpawnFriendly(p, pc.Facing, d.beh, sprite, d.scale,
                            w.Dmg(s), d.speed, d.life, 99, .45f * d.scale,
                            s.crit, s.critMult, d.frost, d.steal,
                            orbRadius: radius, startAngle: start);
                    }
                    break;
                }
                case ProjBehavior.Blast:
                {
                    AimDir(w, out Vector2 tp);
                    Projectile.SpawnFriendly(tp, Vector2.right, d.beh, sprite,
                        w.Area(s) * 1.5f, w.Dmg(s), 0, d.life, 99, w.Area(s),
                        s.crit, s.critMult, d.frost, d.steal);
                    break;
                }
                case ProjBehavior.Pool:
                {
                    AimDir(w, out Vector2 tp);
                    Projectile.SpawnFriendly(tp, Vector2.right, d.beh, sprite,
                        w.Area(s) * 1.6f, w.Dmg(s), 0, w.Life(s), 99, w.Area(s),
                        s.crit, s.critMult, d.frost, d.steal, hitInterval: .5f);
                    break;
                }
                case ProjBehavior.OrbitOut:
                {
                    for (int i = 0; i < count; i++)
                        Projectile.SpawnFriendly(p, Vector2.right, d.beh, sprite, d.scale,
                            w.Dmg(s), d.speed, w.Life(s), 99, .4f * d.scale,
                            s.crit, s.critMult, d.frost, d.steal,
                            hitInterval: .3f, orbRadius: .3f, orbGrow: 2.2f,
                            startAngle: i * (360f / count));
                    break;
                }
                case ProjBehavior.Ring:
                {
                    float maxR = 5f * w.Area(s);
                    float spd = w.Spd(s);
                    Projectile.SpawnFriendly(p, Vector2.right, d.beh,
                        SpriteFactory.RingSprite(d.col, 48, 3), 1f,
                        w.Dmg(s), spd, maxR / spd, 99, 0,
                        s.crit, s.critMult, d.frost, d.steal);
                    break;
                }
            }
        }

        void OnDash()
        {
            var w = GetInst(10);
            if (w == null || pc == null) return;
            var s = pc.S;
            var d = w.D;
            Vector2 pos = (Vector2)pc.transform.position + pc.Facing * 1.1f;
            GameEvents.OnWeaponFire?.Invoke(10);
            Projectile.SpawnFriendly(pos, pc.Facing, ProjBehavior.Fang,
                SpriteFactory.Projectile(d.shape, d.col, 1.4f), 1.4f,
                w.Dmg(s), 0, d.life, 99, w.Area(s),
                s.crit, s.critMult, d.frost, d.steal);
        }

        // ---------- orbit upkeep ----------
        void RespawnOrbs(WeaponInst w)
        {
            foreach (var o in w.orbs) if (o != null) o.ForceRelease();
            w.orbs.Clear();
        }

        void MaintainOrbs(WeaponInst w)
        {
            var s = pc.S;
            int need = w.Count(s);
            w.orbs.RemoveAll(o => o == null || !o.gameObject.activeSelf);
            while (w.orbs.Count < need)
            {
                var d = w.D;
                var pr = Projectile.SpawnFriendly(pc.transform.position, Vector2.right,
                    ProjBehavior.Orbit,
                    SpriteFactory.Projectile(d.shape, d.col, 1f), 1f,
                    w.Dmg(s), d.speed, 0, 99, .35f,
                    s.crit, s.critMult, d.frost, d.steal,
                    hitInterval: .35f, orbRadius: 1.6f,
                    startAngle: w.orbs.Count * (360f / Mathf.Max(1, need)));
                w.orbs.Add(pr);
            }
            float r = 1.6f * w.Area(s);
            float dmg = w.Dmg(s);
            for (int i = 0; i < w.orbs.Count; i++)
            { w.orbs[i].orbRadius = r; w.orbs[i].dmg = dmg; }
        }
    }
}
