using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    /// One tile of one player's zone. Identity is the (row, col) grid cell - the
    /// index a player sees during targeting is the position in <see cref="Zone.Active"/>,
    /// which only changes between rounds, never inside one.
    public class TileCell
    {
        public int row, col;
        public float rIn, rOut, aStart, aEnd;
        public Vector3 center;
        public bool alive = true;

        public Transform root;
        public Transform outline;
        public MeshRenderer topRenderer;
        public Material topMat;

        public float aMid { get { return (aStart + aEnd) * 0.5f; } }
    }

    /// A single player's private tile area: a slice of the arena ring.
    public class Zone
    {
        public int owner;
        public float centerDeg;
        public float aStart, aEnd;

        public readonly List<TileCell> Grid = new List<TileCell>();     // every cell ever built
        public readonly List<TileCell> Active = new List<TileCell>();   // still standing, display order
        readonly List<TileCell> removalOrder = new List<TileCell>();    // lava eats in this order

        public Transform root;

        public int TileCount { get { return Active.Count; } }
        public bool AtMinimum { get { return Active.Count <= Cfg.MinTiles; } }

        public void Build(int ownerIndex)
        {
            owner = ownerIndex;
            centerDeg = ownerIndex * Cfg.ZoneStepDeg;
            aStart = centerDeg - Cfg.ZoneSpanDeg * 0.5f;
            aEnd = centerDeg + Cfg.ZoneSpanDeg * 0.5f;

            for (int r = 0; r < Cfg.TileRows; r++)
            {
                for (int c = 0; c < Cfg.TileCols; c++)
                {
                    var cell = new TileCell
                    {
                        row = r,
                        col = c,
                        rIn = Cfg.InnerR + r * Cfg.RowDepth,
                        rOut = Cfg.InnerR + (r + 1) * Cfg.RowDepth,
                        aStart = aStart + c * Cfg.ColSpanDeg,
                        aEnd = aStart + (c + 1) * Cfg.ColSpanDeg,
                    };
                    cell.center = MeshFactory.Polar((cell.rIn + cell.rOut) * 0.5f,
                                                    (cell.aStart + cell.aEnd) * 0.5f);
                    Grid.Add(cell);
                    Active.Add(cell);
                }
            }

            // Lava creeps inward: the outermost row goes first, and the last row
            // standing is row 0 - exactly Cfg.MinTiles tiles, the 50/50 endgame.
            for (int r = Cfg.TileRows - 1; r >= 1; r--)
                for (int c = 0; c < Cfg.TileCols; c++)
                    removalOrder.Add(Cell(r, c));
        }

        public TileCell Cell(int row, int col)
        {
            for (int i = 0; i < Grid.Count; i++)
                if (Grid[i].row == row && Grid[i].col == col) return Grid[i];
            return null;
        }

        public TileCell Tile(int activeIndex)
        {
            if (activeIndex < 0 || activeIndex >= Active.Count) return null;
            return Active[activeIndex];
        }

        public int IndexOf(TileCell cell) { return Active.IndexOf(cell); }

        public Vector3 CenterOf(int activeIndex)
        {
            var t = Tile(activeIndex);
            return t != null ? t.center : MeshFactory.Polar((Cfg.InnerR + Cfg.OuterR) * 0.5f, centerDeg);
        }

        /// Removes the next tile the lava would claim. Returns null when the zone is
        /// already at the two-tile floor, which is a hard rule, not a soft one.
        public TileCell RemoveNextTile()
        {
            if (AtMinimum) return null;
            for (int i = 0; i < removalOrder.Count; i++)
            {
                var c = removalOrder[i];
                if (!c.alive) continue;
                c.alive = false;
                Active.Remove(c);
                return c;
            }
            return null;
        }

        /// True when the point lies inside a still-standing tile of this zone.
        public TileCell CellAt(Vector3 world)
        {
            float r = new Vector2(world.x, world.z).magnitude;
            if (r < Cfg.InnerR || r > Cfg.OuterR) return null;

            float a = MeshFactory.AngleOf(world);
            float delta = Mathf.DeltaAngle(centerDeg, a);
            if (Mathf.Abs(delta) > Cfg.ZoneSpanDeg * 0.5f) return null;

            int row = Mathf.Clamp((int)((r - Cfg.InnerR) / Cfg.RowDepth), 0, Cfg.TileRows - 1);
            float local = delta + Cfg.ZoneSpanDeg * 0.5f;
            int col = Mathf.Clamp((int)(local / Cfg.ColSpanDeg), 0, Cfg.TileCols - 1);

            var cell = Cell(row, col);
            return (cell != null && cell.alive) ? cell : null;
        }

        /// Keeps a walking avatar inside its own zone and off dead tiles.
        public Vector3 Clamp(Vector3 world)
        {
            if (Active.Count == 0) return MeshFactory.Polar(Cfg.InnerR, centerDeg);

            float r = new Vector2(world.x, world.z).magnitude;
            float a = MeshFactory.AngleOf(world);
            float delta = Mathf.DeltaAngle(centerDeg, a);

            float margin = 0.28f;
            float halfSpan = Cfg.ZoneSpanDeg * 0.5f;
            float angMargin = Mathf.Rad2Deg * (margin / Mathf.Max(r, 1f));
            delta = Mathf.Clamp(delta, -halfSpan + angMargin, halfSpan - angMargin);
            r = Mathf.Clamp(r, Cfg.InnerR + margin, Cfg.OuterR - margin);

            Vector3 p = MeshFactory.Polar(r, centerDeg + delta);
            if (CellAt(p) != null) return p;

            // landed on a burnt-out tile - slide to the nearest living one
            TileCell best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < Active.Count; i++)
            {
                float d = (Active[i].center - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = Active[i]; }
            }
            return best != null ? best.center : p;
        }
    }
}
