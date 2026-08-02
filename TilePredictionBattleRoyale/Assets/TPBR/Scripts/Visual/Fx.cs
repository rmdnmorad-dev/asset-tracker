using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    public class Popup
    {
        public Vector3 world;
        public string text;
        public Color color;
        public float age, life, size;
    }

    /// Lightweight effects: sparks, shockwave rings, screen shake and the floating
    /// text the reveal phase leans on. Hand-rolled instead of ParticleSystem so it
    /// behaves identically in Built-in RP and URP with no asset setup.
    public class Fx : MonoBehaviour
    {
        static Fx I;

        public static float Shake;
        public static readonly List<Popup> Popups = new List<Popup>();

        struct Spark
        {
            public Transform tr;
            public Vector3 vel;
            public float life, age, size;
            public Color color;
        }

        struct Ring
        {
            public Transform tr;
            public Material mat;
            public float age, life, maxR;
            public Color color;
        }

        readonly List<Spark> sparks = new List<Spark>();
        readonly List<Ring> rings = new List<Ring>();
        readonly Stack<Transform> sparkPool = new Stack<Transform>();
        readonly Stack<Transform> ringPool = new Stack<Transform>();

        Material sparkMat;
        Mesh sparkMesh, ringMesh;
        MaterialPropertyBlock mpb;

        public static void Init(Transform parent = null)
        {
            if (I != null) return;
            var go = new GameObject("Fx");
            if (parent != null) go.transform.SetParent(parent, false);
            I = go.AddComponent<Fx>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            I = null;
            Shake = 0f;
            Popups.Clear();
        }

        void Awake()
        {
            sparkMat = Mat.Unlit(Color.white);
            sparkMesh = MeshFactory.Box(new Vector3(0.16f, 0.16f, 0.16f));
            ringMesh = MeshFactory.Disc(0.82f, 1f, 48);
            mpb = new MaterialPropertyBlock();
        }

        // ------------------------------------------------------------- public api

        public static void Burst(Vector3 pos, Color c, int count = 18, float speed = 5f)
        {
            if (I == null) return;
            I.SpawnBurst(pos, c, count, speed);
        }

        public static void LavaBurst(Vector3 pos)
        {
            Burst(pos + Vector3.up * 0.2f, Palette.Lava, 22, 4.5f);
            Wave(pos, Palette.Lava, 2.6f, 0.7f);
        }

        public static void Impact(Vector3 pos, Color c)
        {
            Burst(pos + Vector3.up * 0.3f, c, 30, 7f);
            Burst(pos + Vector3.up * 0.3f, Color.white, 12, 9f);
            Wave(pos, c, 3.4f, 0.55f);
            Shake = Mathf.Max(Shake, 0.55f);
        }

        public static void Wave(Vector3 pos, Color c, float maxRadius, float life)
        {
            if (I == null) return;
            I.SpawnRing(pos, c, maxRadius, life);
        }

        public static void Say(Vector3 world, string text, Color c, float life = 1.6f, float size = 1f)
        {
            Popups.Add(new Popup { world = world, text = text, color = c, life = life, size = size });
        }

        public static void Clear()
        {
            Popups.Clear();
            Shake = 0f;
        }

        // ------------------------------------------------------------- internals

        void SpawnBurst(Vector3 pos, Color c, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                Transform tr = sparkPool.Count > 0 ? sparkPool.Pop() : NewSpark();
                tr.gameObject.SetActive(true);
                tr.position = pos;
                float s = Random.Range(0.5f, 1.4f);
                tr.localScale = new Vector3(s, s, s);
                tr.rotation = Random.rotation;

                Vector3 dir = Random.insideUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 1.4f + 0.35f;
                sparks.Add(new Spark
                {
                    tr = tr,
                    vel = dir.normalized * speed * Random.Range(0.55f, 1.35f),
                    life = Random.Range(0.45f, 0.95f),
                    age = 0f,
                    size = s,
                    color = Color.Lerp(c, Color.white, Random.value * 0.4f)
                });
            }
        }

        Transform NewSpark()
        {
            var go = new GameObject("spark");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = sparkMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = sparkMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void SpawnRing(Vector3 pos, Color c, float maxR, float life)
        {
            Transform tr = ringPool.Count > 0 ? ringPool.Pop() : NewRing();
            tr.gameObject.SetActive(true);
            tr.position = pos + Vector3.up * 0.06f;
            tr.localScale = new Vector3(0.2f, 1f, 0.2f);
            var mr = tr.GetComponent<MeshRenderer>();
            rings.Add(new Ring { tr = tr, mat = mr.sharedMaterial, age = 0f, life = life, maxR = maxR, color = c });
        }

        Transform NewRing()
        {
            var go = new GameObject("ring");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat.Transparent(Color.white);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            Shake = Mathf.Max(0f, Shake - dt * 2.2f);

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.age += dt;
                if (s.age >= s.life || s.tr == null)
                {
                    if (s.tr != null) { s.tr.gameObject.SetActive(false); sparkPool.Push(s.tr); }
                    sparks.RemoveAt(i);
                    continue;
                }
                s.vel += Vector3.down * (18f * dt);
                s.tr.position += s.vel * dt;
                s.tr.Rotate(240f * dt, 180f * dt, 0f);
                float k = 1f - s.age / s.life;
                float sc = s.size * k;
                s.tr.localScale = new Vector3(sc, sc, sc);

                mpb.Clear();
                Color c = s.color;
                mpb.SetColor("_BaseColor", c);
                mpb.SetColor("_Color", c);
                s.tr.GetComponent<MeshRenderer>().SetPropertyBlock(mpb);

                sparks[i] = s;
            }

            for (int i = rings.Count - 1; i >= 0; i--)
            {
                var r = rings[i];
                r.age += dt;
                if (r.age >= r.life || r.tr == null)
                {
                    if (r.tr != null) { r.tr.gameObject.SetActive(false); ringPool.Push(r.tr); }
                    rings.RemoveAt(i);
                    continue;
                }
                float k = r.age / r.life;
                float rad = Mathf.Lerp(0.2f, r.maxR, 1f - (1f - k) * (1f - k));
                r.tr.localScale = new Vector3(rad, 1f, rad);
                Color c = r.color;
                c.a = 1f - k;
                Mat.Tint(r.mat, c);
                rings[i] = r;
            }

            for (int i = Popups.Count - 1; i >= 0; i--)
            {
                Popups[i].age += dt;
                if (Popups[i].age >= Popups[i].life) Popups.RemoveAt(i);
            }
        }
    }
}
