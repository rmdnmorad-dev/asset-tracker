using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    /// Procedural meshes. No art files anywhere in this project - every shape the
    /// game draws is generated here at runtime.
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// Angle convention used across the whole game:
        ///   x = r * sin(deg),  z = r * cos(deg)
        /// so 0 deg points at +Z and the angle increases clockwise seen from above.
        public static Vector3 Polar(float r, float deg, float y = 0f)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(r * Mathf.Sin(a), y, r * Mathf.Cos(a));
        }

        public static float AngleOf(Vector3 p)
        {
            float d = Mathf.Atan2(p.x, p.z) * Mathf.Rad2Deg;
            return d < 0f ? d + 360f : d;
        }

        /// Annular sector ("wedge") prism: the shape of one tile.
        /// Top face sits at y = 0, the body extrudes down to y = -height.
        public static Mesh Sector(float rIn, float rOut, float aStart, float aEnd, float height, int seg = 6)
        {
            string key = string.Format("sec|{0:F3}|{1:F3}|{2:F3}|{3:F3}|{4:F3}|{5}",
                                       rIn, rOut, aStart, aEnd, height, seg);
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var v = new List<Vector3>();
            var t = new List<int>();
            float yB = -height;

            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.Lerp(aStart, aEnd, i / (float)seg);
                float a1 = Mathf.Lerp(aStart, aEnd, (i + 1) / (float)seg);

                Vector3 i0T = Polar(rIn, a0), i1T = Polar(rIn, a1);
                Vector3 o0T = Polar(rOut, a0), o1T = Polar(rOut, a1);
                Vector3 i0B = Polar(rIn, a0, yB), i1B = Polar(rIn, a1, yB);
                Vector3 o0B = Polar(rOut, a0, yB), o1B = Polar(rOut, a1, yB);

                Quad(v, t, i0T, o0T, o1T, i1T);   // top   (+Y)
                Quad(v, t, i0B, i1B, o1B, o0B);   // bottom(-Y)
                Quad(v, t, o0T, o0B, o1B, o1T);   // outer wall
                Quad(v, t, i0T, i1T, i1B, i0B);   // inner wall
            }

            // the two flat end caps
            {
                Vector3 i0T = Polar(rIn, aStart), o0T = Polar(rOut, aStart);
                Vector3 i0B = Polar(rIn, aStart, yB), o0B = Polar(rOut, aStart, yB);
                Quad(v, t, i0T, i0B, o0B, o0T);

                Vector3 i1T = Polar(rIn, aEnd), o1T = Polar(rOut, aEnd);
                Vector3 i1B = Polar(rIn, aEnd, yB), o1B = Polar(rOut, aEnd, yB);
                Quad(v, t, i1T, o1T, o1B, i1B);
            }

            var m = Build(v, t, key);
            cache[key] = m;
            return m;
        }

        /// Flat disc on the y = 0 plane, used for the floor, lava pool and shockwave rings.
        public static Mesh Disc(float rIn, float rOut, int seg = 72)
        {
            string key = string.Format("disc|{0:F2}|{1:F2}|{2}", rIn, rOut, seg);
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = 360f * i / seg, a1 = 360f * (i + 1) / seg;
                Quad(v, t, Polar(rIn, a0), Polar(rOut, a0), Polar(rOut, a1), Polar(rIn, a1));
            }
            var m = Build(v, t, key);
            cache[key] = m;
            return m;
        }

        /// Simple box centred on the origin.
        public static Mesh Box(Vector3 size)
        {
            string key = "box|" + size;
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            Vector3 h = size * 0.5f;
            var v = new List<Vector3>();
            var t = new List<int>();
            Vector3 a = new Vector3(-h.x, -h.y, -h.z), b = new Vector3(h.x, -h.y, -h.z);
            Vector3 c = new Vector3(h.x, -h.y, h.z), d = new Vector3(-h.x, -h.y, h.z);
            Vector3 e = new Vector3(-h.x, h.y, -h.z), f = new Vector3(h.x, h.y, -h.z);
            Vector3 g = new Vector3(h.x, h.y, h.z), i = new Vector3(-h.x, h.y, h.z);

            Quad(v, t, e, i, g, f);   // top
            Quad(v, t, a, b, c, d);   // bottom
            Quad(v, t, a, e, f, b);   // -Z
            Quad(v, t, c, g, i, d);   // +Z
            Quad(v, t, b, f, g, c);   // +X
            Quad(v, t, d, i, e, a);   // -X

            var m = Build(v, t, key);
            cache[key] = m;
            return m;
        }

        /// Cylinder / truncated cone, base at y = 0 growing upward.
        public static Mesh Cone(float rBottom, float rTop, float height, int seg = 20)
        {
            string key = string.Format("cone|{0:F2}|{1:F2}|{2:F2}|{3}", rBottom, rTop, height, seg);
            Mesh cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var v = new List<Vector3>();
            var t = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = 360f * i / seg, a1 = 360f * (i + 1) / seg;
                Vector3 b0 = Polar(rBottom, a0), b1 = Polar(rBottom, a1);
                Vector3 t0 = Polar(rTop, a0, height), t1 = Polar(rTop, a1, height);
                Quad(v, t, b0, b1, t1, t0);                    // side, normal points outward
                Tri(v, t, new Vector3(0f, height, 0f), t0, t1); // top cap fan, +Y
                Tri(v, t, Vector3.zero, b1, b0);               // bottom cap fan, -Y
            }
            var m = Build(v, t, key);
            cache[key] = m;
            return m;
        }

        // ---------------------------------------------------------------- utils

        /// Winding order is (v0 -> v1 -> v2 -> v3) around the face, chosen so the
        /// resulting normal points out of the solid. See the callers above.
        static void Quad(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int n = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            t.Add(n); t.Add(n + 1); t.Add(n + 2);
            t.Add(n); t.Add(n + 2); t.Add(n + 3);
        }

        static void Tri(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, Vector3 c)
        {
            int n = v.Count;
            v.Add(a); v.Add(b); v.Add(c);
            t.Add(n); t.Add(n + 1); t.Add(n + 2);
        }

        static Mesh Build(List<Vector3> v, List<int> t, string name)
        {
            var m = new Mesh();
            m.name = name;
            m.indexFormat = v.Count > 65000
                ? UnityEngine.Rendering.IndexFormat.UInt32
                : UnityEngine.Rendering.IndexFormat.UInt16;
            m.SetVertices(v);
            m.SetTriangles(t, 0);

            var uv = new Vector2[v.Count];
            for (int i = 0; i < v.Count; i++) uv[i] = new Vector2(v[i].x * 0.1f, v[i].z * 0.1f);
            m.uv = uv;

            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }
    }
}
