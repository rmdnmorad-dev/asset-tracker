using UnityEngine;

namespace TPBR
{
    /// Placeholder character built from procedural primitives. Swap the meshes in
    /// <see cref="Build"/> for real art later - nothing else reads them.
    public class Avatar : MonoBehaviour
    {
        public int owner;
        public Transform body, head, visor, beak, ring;
        Material bodyMat, ringMat;
        Color baseColor;

        float bob;
        Vector3 facing = Vector3.forward;

        // hop animation
        bool hopping;
        Vector3 hopFrom, hopTo;
        float hopT, hopDur = 0.55f;

        bool dying;
        float dieT;

        public bool Busy { get { return hopping || dying; } }

        public static Avatar Create(Transform parent, int owner, Color c)
        {
            var go = new GameObject("Avatar_" + owner);
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<Avatar>();
            a.owner = owner;
            a.baseColor = c;
            a.Build();
            return a;
        }

        void Build()
        {
            bodyMat = Mat.Lit(baseColor, 0.2f);
            var darkMat = Mat.Lit(new Color(0.10f, 0.11f, 0.15f), 0.3f);
            ringMat = Mat.Unlit(baseColor);

            ring = Piece("Ring", MeshFactory.Disc(0.52f, 0.68f, 24), ringMat).transform;
            ring.localPosition = new Vector3(0f, 0.03f, 0f);

            body = Piece("Body", MeshFactory.Cone(0.42f, 0.34f, 0.78f, 16), bodyMat).transform;
            body.localPosition = Vector3.zero;

            head = Piece("Head", MeshFactory.Box(new Vector3(0.56f, 0.5f, 0.52f)), bodyMat).transform;
            head.localPosition = new Vector3(0f, 1.05f, 0f);

            visor = Piece("Visor", MeshFactory.Box(new Vector3(0.44f, 0.17f, 0.1f)), darkMat).transform;
            visor.localPosition = new Vector3(0f, 1.08f, 0.26f);

            // the nose is the readability trick: it tells you which way a player
            // is looking during prep, which is most of the bluffing information.
            beak = Piece("Beak", MeshFactory.Box(new Vector3(0.14f, 0.14f, 0.3f)), darkMat).transform;
            beak.localPosition = new Vector3(0f, 0.92f, 0.34f);
        }

        GameObject Piece(string n, Mesh m, Material mat)
        {
            var go = new GameObject(n);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            mr.receiveShadows = false;
            return go;
        }

        public void Warp(Vector3 pos)
        {
            transform.position = pos;
            hopping = false;
        }

        public void MoveTo(Vector3 pos, float dt)
        {
            Vector3 delta = pos - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude > 0.0004f)
            {
                facing = Vector3.Slerp(facing, delta.normalized, 1f - Mathf.Exp(-14f * dt));
                bob += delta.magnitude * 7f;
            }
            else
            {
                bob += dt * 1.5f;
            }
            transform.position = pos;
        }

        public void Hop(Vector3 to, float duration = 0.55f)
        {
            hopFrom = transform.position;
            hopTo = to;
            hopDur = duration;
            hopT = 0f;
            hopping = true;
        }

        public void Die()
        {
            if (dying) return;
            dying = true;
            dieT = 0f;
            hopping = false;
        }

        public void SetDimmed(bool dim)
        {
            Color c = dim ? Palette.Dim(baseColor, 0.35f) : baseColor;
            Mat.Tint(bodyMat, c);
            Mat.Tint(ringMat, dim ? Palette.Dim(baseColor, 0.3f) : baseColor);
        }

        public void SetRingHighlight(bool on)
        {
            ring.localScale = on ? new Vector3(1.35f, 1f, 1.35f) : Vector3.one;
            Mat.Tint(ringMat, on ? Color.Lerp(baseColor, Color.white, 0.55f) : baseColor);
        }

        void Update()
        {
            float dt = Time.deltaTime;

            if (hopping)
            {
                hopT += dt / Mathf.Max(hopDur, 0.01f);
                float t = Mathf.Clamp01(hopT);
                float e = t * t * (3f - 2f * t);
                Vector3 p = Vector3.Lerp(hopFrom, hopTo, e);
                p.y += Mathf.Sin(t * Mathf.PI) * 1.15f;
                transform.position = p;

                Vector3 d = hopTo - hopFrom;
                d.y = 0f;
                if (d.sqrMagnitude > 0.001f) facing = d.normalized;
                if (t >= 1f) { hopping = false; transform.position = hopTo; }
            }

            if (dying)
            {
                dieT += dt;
                float t = Mathf.Clamp01(dieT / 0.9f);
                transform.position += Vector3.up * (dt * (3.2f - t * 3.4f));
                transform.Rotate(Vector3.up, 720f * dt, Space.World);
                float s = Mathf.Max(0f, 1f - t);
                transform.localScale = new Vector3(s, s, s);
                if (t >= 1f) gameObject.SetActive(false);
                return;
            }

            if (!hopping) transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            else transform.rotation = Quaternion.LookRotation(facing, Vector3.up);

            float b = Mathf.Sin(bob) * 0.06f;
            if (body != null) body.localPosition = new Vector3(0f, Mathf.Abs(b), 0f);
            if (head != null) head.localPosition = new Vector3(0f, 1.05f + b * 0.7f, 0f);
            if (visor != null) visor.localPosition = new Vector3(0f, 1.08f + b * 0.7f, 0.26f);
            if (beak != null) beak.localPosition = new Vector3(0f, 0.92f + b * 0.7f, 0.34f);
        }
    }
}
