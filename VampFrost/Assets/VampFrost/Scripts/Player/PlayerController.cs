using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class Stats
    {
        public float maxHP = 100, regen = 0, armor = 0, moveSpeed = 5f;
        public float might = 1, cooldown = 1, area = 1, projSpeed = 1, duration = 1;
        public int amount = 0;
        public float magnet = 1.7f, crit = 0.05f, critMult = 1.6f, goldMul = 1f;
        public float dashCd = 2.5f;
    }

    public class PlayerController : MonoBehaviour
    {
        public static PlayerController I;

        public Stats S = new Stats();
        public readonly Dictionary<string, int> Passives = new();
        public float HP;
        public bool Dead;
        public bool IsInvisible { get; private set; }
        public Vector2 MoveDir { get; private set; }
        public Vector2 Facing { get; private set; } = Vector2.right;
        public WeaponSystem Weapons { get; private set; }

        SpriteRenderer sr;
        float dashT, dashCdT, invisT, invisCdT, iframes, footT;
        Vector2 dashDir;

        public float DashCdLeft => Mathf.Max(0, dashCdT);
        public float InvisCdLeft => Mathf.Max(0, invisCdT);

        public static PlayerController Create(Transform worldRoot)
        {
            var go = new GameObject("Player");
            go.transform.SetParent(worldRoot, false);
            var p = go.AddComponent<PlayerController>();
            go.AddComponent<PlayerXP>();
            return p;
        }

        void Awake()
        {
            I = this;
            sr = gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.Player();
            Weapons = gameObject.AddComponent<WeaponSystem>();
            HP = S.maxHP;
        }

        void Start() { Weapons.TryAddOrUpgrade(0); } // starting weapon: Icicle Spike
        void OnDestroy() { if (I == this) I = null; }

        void Update()
        {
            if (GameManager.I == null || GameManager.I.state != GameManager.State.Playing || Dead) return;
            float dt = Time.deltaTime;

            Vector2 inp = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (inp.sqrMagnitude > 1f) inp.Normalize();
            MoveDir = inp;
            if (inp.sqrMagnitude > .01f) Facing = inp.normalized;

            dashCdT -= dt; invisCdT -= dt; iframes -= dt;

            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.LeftShift)) && dashCdT <= 0)
            {
                dashT = .16f; dashCdT = S.dashCd;
                dashDir = inp.sqrMagnitude > .01f ? inp.normalized : Facing;
                iframes = Mathf.Max(iframes, .3f);
                GameEvents.OnPlayerDash?.Invoke();
            }

            if (Input.GetKeyDown(KeyCode.Q) && invisCdT <= 0 && invisT <= 0)
            {
                invisT = 3f; invisCdT = 10f;
                GameEvents.OnPlayerInvisibility?.Invoke();
            }
            if (invisT > 0) { invisT -= dt; IsInvisible = true; }
            else IsInvisible = false;

            Vector2 vel;
            if (dashT > 0) { dashT -= dt; vel = dashDir * S.moveSpeed * 3.4f; }
            else vel = inp * S.moveSpeed;
            transform.position += (Vector3)(vel * dt);

            if (S.regen > 0) Heal(S.regen * dt);

            if (inp.sqrMagnitude > .01f && dashT <= 0)
            {
                footT -= dt;
                if (footT <= 0) { footT = .34f; GameEvents.OnFootstep?.Invoke(); }
            }

            sr.flipX = Facing.x < 0;
            float a = IsInvisible ? .35f : (iframes > 0 && Mathf.PingPong(Time.unscaledTime * 12f, 1f) > .5f ? .45f : 1f);
            sr.color = new Color(1, 1, 1, a);
            sr.sortingOrder = Mathf.RoundToInt(-transform.position.y * 10f);
        }

        public void TakeDamage(float d)
        {
            if (Dead || iframes > 0 || dashT > 0) return;
            d = Mathf.Max(1f, d - S.armor);
            HP -= d;
            iframes = .35f;
            GameEvents.OnPlayerDamage?.Invoke(d);
            CameraRig.Shake(.13f, .15f);
            if (HP <= 0)
            {
                HP = 0; Dead = true;
                GameEvents.OnPlayerDeath?.Invoke();
            }
        }

        public void Heal(float h)
        {
            if (Dead) return;
            HP = Mathf.Min(S.maxHP, HP + h);
        }
    }

    public class PlayerXP : MonoBehaviour
    {
        public static PlayerXP I;
        public int Level = 1;
        public float XP;
        public float Next = 10;

        void Awake() { I = this; Next = NeedFor(Level); }
        void OnDestroy() { if (I == this) I = null; }

        static float NeedFor(int lvl) => 8 + lvl * 9;

        public void Add(float v)
        {
            XP += v;
            GameEvents.OnXPGained?.Invoke();
            while (XP >= Next)
            {
                XP -= Next;
                Level++;
                Next = NeedFor(Level);
                GameEvents.OnLevelUp?.Invoke(Level);
                LevelUpPanel.Enqueue();
            }
        }
    }
}
