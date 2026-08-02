using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    public enum TileLook
    {
        Idle,        // a normal, safe tile
        Dead,        // the zone's owner is eliminated
        Owned,       // one of your own tiles
        Standing,    // the tile you have committed to hide on
        Aimed,       // the tile you are attacking
        Incoming,    // an attack is about to land here (reveal only)
        Burning,     // lava will claim this next round
        Hit          // an attack just landed here
    }

    /// Builds and owns every piece of world geometry: floor, tiles, outlines,
    /// lava ring and the centre trophy. All of it is generated at runtime.
    public class Arena : MonoBehaviour
    {
        public Zone[] zones;

        /// Set by GameManager so eliminated zones can grey themselves out.
        public System.Func<int, bool> IsAlive;

        Transform lavaRing, trophyCup;
        Material lavaMat, trophyMat;
        readonly List<CrumbleAnim> crumbles = new List<CrumbleAnim>();
        float t;

        class CrumbleAnim
        {
            public TileCell cell;
            public float age;
            public Vector3 from;
        }

        // ------------------------------------------------------------------ build

        public void Build()
        {
            BuildEnvironment();

            zones = new Zone[Cfg.PlayerCount];
            for (int i = 0; i < Cfg.PlayerCount; i++)
            {
                var z = new Zone();
                z.Build(i);

                var zroot = new GameObject("Zone_" + i);
                zroot.transform.SetParent(transform, false);
                z.root = zroot.transform;

                Color c = Palette.Of(i);
                for (int k = 0; k < z.Grid.Count; k++) BuildTile(z, z.Grid[k], c);

                zones[i] = z;
            }
        }

        void BuildTile(Zone z, TileCell cell, Color owner)
        {
            float midR = (cell.rIn + cell.rOut) * 0.5f;
            float inset = 0.11f;
            float angInset = Mathf.Rad2Deg * (inset / Mathf.Max(midR, 0.01f));

            var root = new GameObject(string.Format("Tile_{0}_{1}", cell.row, cell.col));
            root.transform.SetParent(z.root, false);
            cell.root = root.transform;

            // dark backing plate, slightly larger - reads as a crisp outline top-down
            var outline = new GameObject("Outline");
            outline.transform.SetParent(root.transform, false);
            outline.transform.localPosition = new Vector3(0f, -0.05f, 0f);
            outline.AddComponent<MeshFilter>().sharedMesh =
                MeshFactory.Sector(cell.rIn, cell.rOut, cell.aStart, cell.aEnd, Cfg.TileH * 1.05f);
            var omr = outline.AddComponent<MeshRenderer>();
            omr.sharedMaterial = Mat.Lit(Palette.TileDark, 0f);
            omr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cell.outline = outline.transform;

            var top = new GameObject("Top");
            top.transform.SetParent(root.transform, false);
            top.AddComponent<MeshFilter>().sharedMesh =
                MeshFactory.Sector(cell.rIn + inset, cell.rOut - inset,
                                   cell.aStart + angInset, cell.aEnd - angInset, Cfg.TileH);
            cell.topMat = Mat.Lit(TintFor(TileLook.Idle, owner), 0.1f);
            cell.topRenderer = top.AddComponent<MeshRenderer>();
            cell.topRenderer.sharedMaterial = cell.topMat;
            cell.topRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            SetLook(cell, TileLook.Idle, owner);
        }

        void BuildEnvironment()
        {
            // floor under the ring
            var floor = new GameObject("Floor");
            floor.transform.SetParent(transform, false);
            floor.transform.localPosition = new Vector3(0f, -Cfg.TileH - 0.12f, 0f);
            floor.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Disc(0f, Cfg.OuterR + 1.2f, 96);
            floor.AddComponent<MeshRenderer>().sharedMaterial = Mat.Lit(Palette.Floor, 0.05f);

            // lava sea beyond the arena edge
            var lava = new GameObject("LavaRing");
            lava.transform.SetParent(transform, false);
            lava.transform.localPosition = new Vector3(0f, -Cfg.TileH - 0.35f, 0f);
            lava.AddComponent<MeshFilter>().sharedMesh = MeshFactory.Disc(Cfg.OuterR + 0.4f, 44f, 128);
            lavaMat = Mat.Lit(Palette.LavaDeep, 0f);
            Mat.Emissive(lavaMat, Palette.Lava * 1.4f);
            lava.AddComponent<MeshRenderer>().sharedMaterial = lavaMat;
            lavaRing = lava.transform;

            BuildTrophy();
        }

        void BuildTrophy()
        {
            var root = new GameObject("Trophy");
            root.transform.SetParent(transform, false);

            var stone = Mat.Lit(new Color(0.16f, 0.19f, 0.24f), 0.05f);
            trophyMat = Mat.Lit(Palette.Gold, 0.85f, 0.9f);
            Mat.Emissive(trophyMat, Palette.Gold * 0.5f);

            Add(root.transform, MeshFactory.Cone(4.2f, 3.6f, 0.5f, 40), stone, new Vector3(0f, -Cfg.TileH, 0f));
            Add(root.transform, MeshFactory.Cone(2.9f, 2.4f, 0.6f, 36), stone, new Vector3(0f, -Cfg.TileH + 0.5f, 0f));

            var cup = new GameObject("Cup");
            cup.transform.SetParent(root.transform, false);
            cup.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            trophyCup = cup.transform;
            Add(cup.transform, MeshFactory.Cone(0.55f, 0.35f, 0.35f, 24), trophyMat, Vector3.zero);
            Add(cup.transform, MeshFactory.Cone(0.16f, 0.16f, 0.55f, 16), trophyMat, new Vector3(0f, 0.35f, 0f));
            Add(cup.transform, MeshFactory.Cone(0.42f, 0.95f, 1.05f, 24), trophyMat, new Vector3(0f, 0.9f, 0f));
        }

        static void Add(Transform parent, Mesh m, Material mat, Vector3 pos)
        {
            var go = new GameObject(m.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.AddComponent<MeshFilter>().sharedMesh = m;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }

        // ------------------------------------------------------------------ look

        public static Color TintFor(TileLook look, Color owner)
        {
            switch (look)
            {
                case TileLook.Dead:     return Color.Lerp(Palette.TileDark, Palette.Floor, 0.45f);
                case TileLook.Owned:    return Color.Lerp(owner, Palette.Floor, 0.30f);
                case TileLook.Standing: return Color.Lerp(owner, Color.white, 0.45f);
                case TileLook.Aimed:    return Color.Lerp(Palette.Danger, Color.white, 0.25f);
                case TileLook.Incoming: return Color.Lerp(Palette.Danger, Palette.Floor, 0.35f);
                case TileLook.Burning:  return Color.Lerp(Palette.Lava, Palette.Floor, 0.25f);
                case TileLook.Hit:      return Color.white;
                default:                return Color.Lerp(owner, Palette.Floor, 0.62f);
            }
        }

        public void SetLook(TileCell cell, TileLook look, Color owner)
        {
            if (cell == null || cell.topMat == null) return;
            Color c = TintFor(look, owner);
            Mat.Tint(cell.topMat, c);

            float e = 0f;
            switch (look)
            {
                case TileLook.Standing: e = 0.55f; break;
                case TileLook.Aimed:    e = 1.10f; break;
                case TileLook.Incoming: e = 0.85f; break;
                case TileLook.Burning:  e = 1.25f; break;
                case TileLook.Hit:      e = 2.60f; break;
            }
            Mat.Emissive(cell.topMat, c * e);
        }

        /// Repaints an entire zone back to its resting state.
        public void ResetZoneLook(int player)
        {
            var z = zones[player];
            Color c = Palette.Of(player);
            bool dead = IsAlive != null && !IsAlive(player);
            for (int i = 0; i < z.Active.Count; i++)
                SetLook(z.Active[i], dead ? TileLook.Dead : TileLook.Idle, c);
        }

        public void ResetAllLooks()
        {
            for (int i = 0; i < zones.Length; i++) ResetZoneLook(i);
        }

        /// Highlights the tiles the lava is going to take next, a round early.
        public void PreviewLava()
        {
            for (int i = 0; i < zones.Length; i++)
            {
                var z = zones[i];
                if (z.AtMinimum) continue;
                if (IsAlive != null && !IsAlive(i)) continue;
                for (int k = 0; k < z.Active.Count; k++)
                {
                    // the next victim is the outermost surviving tile
                    if (k == NextVictimIndex(z)) SetLook(z.Active[k], TileLook.Burning, Palette.Of(i));
                }
            }
        }

        public static int NextVictimIndex(Zone z)
        {
            int best = -1, bestRow = -1;
            for (int i = 0; i < z.Active.Count; i++)
            {
                if (z.Active[i].row > bestRow) { bestRow = z.Active[i].row; best = i; }
            }
            return bestRow <= 0 ? -1 : best;
        }

        // ------------------------------------------------------------ destruction

        public void Crumble(TileCell cell)
        {
            if (cell == null || cell.root == null) return;
            Mat.Tint(cell.topMat, Palette.Lava);
            Mat.Emissive(cell.topMat, Palette.Lava * 3f);
            crumbles.Add(new CrumbleAnim { cell = cell, age = 0f, from = cell.root.localPosition });
            Fx.LavaBurst(cell.center);
        }

        void Update()
        {
            t += Time.deltaTime;

            if (lavaMat != null)
            {
                float pulse = 1.15f + Mathf.Sin(t * 1.6f) * 0.35f;
                Mat.Emissive(lavaMat, Palette.Lava * pulse);
            }
            if (lavaRing != null)
            {
                float s = 1f + Mathf.Sin(t * 0.7f) * 0.006f;
                lavaRing.localScale = new Vector3(s, 1f, s);
            }
            if (trophyCup != null)
            {
                trophyCup.localRotation = Quaternion.Euler(0f, t * 34f, 0f);
                trophyCup.localPosition = new Vector3(0f, 1.1f + Mathf.Sin(t * 1.4f) * 0.12f, 0f);
            }

            for (int i = crumbles.Count - 1; i >= 0; i--)
            {
                var c = crumbles[i];
                c.age += Time.deltaTime;
                float k = Mathf.Clamp01(c.age / 0.85f);
                float drop = k * k * 4.5f;
                if (c.cell.root != null)
                {
                    c.cell.root.localPosition = c.from + new Vector3(0f, -drop, 0f);
                    c.cell.root.localRotation = Quaternion.Euler(k * 22f * Mathf.Sin(c.cell.aMid), 0f, k * 14f);
                }
                if (k >= 1f)
                {
                    if (c.cell.root != null) c.cell.root.gameObject.SetActive(false);
                    crumbles.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------ picking

        /// Which tile is under this screen ray? Solved analytically on the y = 0
        /// plane - no colliders anywhere in the arena.
        public bool Pick(Ray ray, out int player, out int tileIndex)
        {
            player = -1;
            tileIndex = -1;

            var plane = new Plane(Vector3.up, Vector3.zero);
            float dist;
            if (!plane.Raycast(ray, out dist)) return false;

            Vector3 p = ray.GetPoint(dist);
            float r = new Vector2(p.x, p.z).magnitude;
            if (r < Cfg.InnerR || r > Cfg.OuterR) return false;

            float a = MeshFactory.AngleOf(p);
            int z = Mathf.RoundToInt(a / Cfg.ZoneStepDeg) % Cfg.PlayerCount;
            if (z < 0) z += Cfg.PlayerCount;

            var cell = zones[z].CellAt(p);
            if (cell == null) return false;

            player = z;
            tileIndex = zones[z].IndexOf(cell);
            return tileIndex >= 0;
        }
    }
}
