using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public enum DecoShape { Tombstone, DeadTree, Rock, Pillar, Cactus, Barrel, Bush, Cross, Fence, Car }
    public enum ProjShape { Shard, Orb, Blade, Star, Fang, RingO, Disc, Spear, Pool, Bolt }

    /// Loads user sprites from Resources/Sprites/<name>; falls back to generated
    /// pixel placeholders so the whole game runs before any art is imported.
    public static class SpriteFactory
    {
        static readonly Dictionary<string, Sprite> cache = new();
        public const float PPU = 32f;

        public static Sprite White
        {
            get
            {
                if (!cache.TryGetValue("__white", out var s))
                {
                    var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    t.SetPixels32(new[] { new Color32(255,255,255,255), new Color32(255,255,255,255),
                                          new Color32(255,255,255,255), new Color32(255,255,255,255) });
                    t.Apply();
                    s = Sprite.Create(t, new Rect(0, 0, 2, 2), new Vector2(.5f, .5f), 2);
                    cache["__white"] = s;
                }
                return s;
            }
        }

        /// Try Resources/Sprites/<name> (drop your sliced sheets here, see README).
        public static Sprite Load(string name)
        {
            if (cache.TryGetValue("res_" + name, out var c)) return c;
            var s = Resources.Load<Sprite>("Sprites/" + name);
            if (s != null) cache["res_" + name] = s;
            return s;
        }

        // ---------------- texture helpers ----------------
        static Texture2D Tex(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false)
            { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            t.SetPixels32(px);
            return t;
        }
        static void Px(Texture2D t, int x, int y, Color c)
        { if (x >= 0 && y >= 0 && x < t.width && y < t.height) t.SetPixel(x, y, c); }

        static void FillRect(Texture2D t, int x, int y, int w, int h, Color c)
        { for (int i = x; i < x + w; i++) for (int j = y; j < y + h; j++) Px(t, i, j, c); }

        static void FillCircle(Texture2D t, float cx, float cy, float r, Color c)
        {
            int x0 = Mathf.FloorToInt(cx - r), x1 = Mathf.CeilToInt(cx + r);
            int y0 = Mathf.FloorToInt(cy - r), y1 = Mathf.CeilToInt(cy + r);
            for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Px(t, x, y, c);
        }

        static void Outline(Texture2D t, Color oc)
        {
            int w = t.width, h = t.height;
            var src = t.GetPixels32();
            for (int x = 0; x < w; x++) for (int y = 0; y < h; y++)
            {
                if (src[y * w + x].a > 10) continue;
                bool edge =
                    (x > 0 && src[y * w + x - 1].a > 10) || (x < w - 1 && src[y * w + x + 1].a > 10) ||
                    (y > 0 && src[(y - 1) * w + x].a > 10) || (y < h - 1 && src[(y + 1) * w + x].a > 10);
                if (edge) Px(t, x, y, oc);
            }
        }

        static Sprite Bake(string key, Texture2D t, float ppu = PPU, float pivotY = .5f)
        {
            t.Apply();
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, pivotY), ppu);
            cache[key] = s;
            return s;
        }

        // ---------------- generated sprites ----------------
        public static Sprite Solid(Color c, int w = 8, int h = 8)
        {
            string k = $"sol{c}{w}x{h}";
            if (cache.TryGetValue(k, out var s)) return s;
            var t = Tex(w, h); FillRect(t, 0, 0, w, h, c);
            return Bake(k, t);
        }

        public static Sprite Circle(Color c, int d = 16, bool outline = true)
        {
            string k = $"cir{c}{d}{outline}";
            if (cache.TryGetValue(k, out var s)) return s;
            var t = Tex(d + 2, d + 2);
            FillCircle(t, (d + 1) / 2f, (d + 1) / 2f, d / 2f, c);
            FillCircle(t, (d + 1) / 2f - d * .12f, (d + 1) / 2f + d * .12f, d * .22f,
                       Color.Lerp(c, Color.white, .35f));
            if (outline) Outline(t, new Color(0, 0, 0, .9f));
            return Bake(k, t);
        }

        public static Sprite RingSprite(Color c, int d = 48, int thick = 3)
        {
            string k = $"rng{c}{d}{thick}";
            if (cache.TryGetValue(k, out var s)) return s;
            var t = Tex(d + 2, d + 2);
            float cx = (d + 1) / 2f, r = d / 2f;
            for (int x = 0; x < t.width; x++) for (int y = 0; y < t.height; y++)
            {
                float dd = Mathf.Sqrt((x - cx) * (x - cx) + (y - cx) * (y - cx));
                if (dd <= r && dd >= r - thick) Px(t, x, y, c);
            }
            return Bake(k, t);
        }

        public static Sprite Player()
        {
            var real = Load("Player");
            if (real != null) return real;
            if (cache.TryGetValue("plc_player", out var s)) return s;
            var t = Tex(20, 30);
            var cloak = new Color(.10f, .10f, .16f);
            var armor = new Color(.28f, .34f, .45f);
            var skin = new Color(.82f, .80f, .84f);
            var red = new Color(.75f, .08f, .12f);
            FillRect(t, 4, 2, 12, 16, cloak);              // cloak
            FillRect(t, 6, 4, 8, 12, armor);               // body
            FillCircle(t, 10, 21, 4.4f, skin);             // head
            FillRect(t, 5, 14, 10, 2, new Color(.16f,.2f,.3f)); // collar
            Px(t, 8, 22, red); Px(t, 12, 22, red);         // eyes
            FillRect(t, 3, 1, 3, 14, new Color(.4f,.05f,.08f)); // cape edge
            FillRect(t, 14, 1, 3, 14, new Color(.4f,.05f,.08f));
            Outline(t, new Color(0, 0, 0, .95f));
            return Bake("plc_player", t, PPU, .12f);
        }

        public static Sprite Mob(MobDef d)
        {
            var real = Load("mob_" + d.key);
            if (real != null) return real;
            return Circle(d.color, Mathf.RoundToInt(14 * d.size));
        }

        public static Sprite BossSprite(BossDef d)
        {
            var real = Load("boss_" + d.key);
            if (real != null) return real;
            string k = "plc_boss_" + d.key;
            if (cache.TryGetValue(k, out var s)) return s;
            int sz = Mathf.RoundToInt(22 * d.size);
            var t = Tex(sz + 6, sz + 10);
            FillCircle(t, (sz + 5) / 2f, (sz + 5) / 2f, sz / 2f, d.color);
            FillCircle(t, (sz + 5) / 2f, sz * .78f, sz * .22f, Color.Lerp(d.color, Color.black, .4f));
            // horns
            FillRect(t, 3, sz - 2, 3, 8, Color.Lerp(d.color, Color.black, .5f));
            FillRect(t, sz, sz - 2, 3, 8, Color.Lerp(d.color, Color.black, .5f));
            Outline(t, Color.black);
            return Bake(k, t);
        }

        public static Sprite Projectile(ProjShape sh, Color c, float scale = 1f)
        {
            string k = $"prj{sh}{c}{scale:F2}";
            if (cache.TryGetValue(k, out var s)) return s;
            int u = Mathf.Max(4, Mathf.RoundToInt(10 * scale));
            Texture2D t;
            switch (sh)
            {
                case ProjShape.Shard:
                case ProjShape.Spear:
                    t = Tex(u * 2, u);
                    for (int x = 0; x < u * 2; x++)
                    {
                        int hh = Mathf.Max(1, Mathf.RoundToInt(u * .5f * (1f - x / (u * 2f))));
                        FillRect(t, x, u / 2 - hh, 1, hh * 2, c);
                    }
                    FillRect(t, 0, u / 2 - 1, u, 2, Color.Lerp(c, Color.white, .5f));
                    break;
                case ProjShape.Blade:
                    t = Tex(u * 2, u * 2);
                    for (int x = 0; x < u * 2; x++)
                    {
                        float a = x / (u * 2f) * Mathf.PI;
                        int y = Mathf.RoundToInt(Mathf.Sin(a) * u * .9f) + u / 3;
                        FillRect(t, x, y, 1, Mathf.Max(2, u / 3), c);
                    }
                    break;
                case ProjShape.Star:
                    t = Tex(u * 2, u * 2);
                    FillRect(t, u - 1, 0, 3, u * 2, c);
                    FillRect(t, 0, u - 1, u * 2, 3, c);
                    for (int i = 0; i < u * 2; i++) { Px(t, i, i, c); Px(t, i, u * 2 - 1 - i, c); }
                    break;
                case ProjShape.Fang:
                    t = Tex(u * 2, u);
                    for (int i = 0; i < 4; i++)
                        for (int x = 0; x < u / 2; x++)
                        { int hh = Mathf.Max(1, (u / 2 - x) / 2); FillRect(t, i * (u / 2) + x, 0, 1, hh + 2, c); }
                    break;
                case ProjShape.RingO:
                    return RingSprite(c, u * 4, Mathf.Max(2, u / 4));
                case ProjShape.Disc:
                    t = Tex(u * 2, u * 2);
                    FillCircle(t, u, u, u * .9f, c);
                    FillCircle(t, u, u, u * .45f, Color.Lerp(c, Color.black, .4f));
                    break;
                case ProjShape.Pool:
                    t = Tex(u * 4, u * 2);
                    for (int x = 0; x < u * 4; x++) for (int y = 0; y < u * 2; y++)
                    {
                        float nx = (x - u * 2f) / (u * 2f), ny = (y - u) / (u * .9f);
                        if (nx * nx + ny * ny <= 1f) Px(t, x, y, new Color(c.r, c.g, c.b, .8f));
                    }
                    break;
                case ProjShape.Bolt:
                    t = Tex(u * 2, u / 2 + 2);
                    FillRect(t, 0, 1, u * 2, u / 2, c);
                    FillRect(t, u, 0, u, u / 2 + 2, Color.Lerp(c, Color.white, .4f));
                    break;
                default: // Orb
                    return Circle(c, u * 2, false);
            }
            Outline(t, new Color(0, 0, 0, .8f));
            return Bake(k, t);
        }

        public static Sprite Deco(DecoShape sh, Color a, Color b)
        {
            string k = $"dec{sh}{a}{b}";
            if (cache.TryGetValue(k, out var s)) return s;
            Texture2D t;
            switch (sh)
            {
                case DecoShape.Tombstone:
                    t = Tex(14, 18);
                    FillRect(t, 2, 0, 10, 12, a);
                    FillCircle(t, 7, 12, 5, a);
                    FillRect(t, 5, 5, 4, 1, b); FillRect(t, 5, 8, 4, 1, b);
                    break;
                case DecoShape.Cross:
                    t = Tex(12, 18);
                    FillRect(t, 5, 0, 2, 16, a); FillRect(t, 2, 10, 8, 2, a);
                    break;
                case DecoShape.DeadTree:
                    t = Tex(24, 34);
                    FillRect(t, 11, 0, 3, 16, a);
                    FillRect(t, 6, 14, 4, 2, a); FillRect(t, 14, 18, 5, 2, a);
                    FillRect(t, 4, 20, 3, 2, a); FillRect(t, 17, 24, 3, 2, a);
                    FillRect(t, 11, 16, 2, 12, a);
                    FillRect(t, 8, 26, 2, 4, a); FillRect(t, 14, 28, 2, 4, a);
                    break;
                case DecoShape.Rock:
                    t = Tex(16, 12);
                    FillCircle(t, 6, 5, 5, a); FillCircle(t, 11, 4, 3.5f, Color.Lerp(a, b, .5f));
                    break;
                case DecoShape.Pillar:
                    t = Tex(12, 30);
                    FillRect(t, 3, 0, 6, 26, a);
                    FillRect(t, 1, 24, 10, 3, b); FillRect(t, 1, 0, 10, 2, b);
                    FillRect(t, 4, 4, 1, 18, Color.Lerp(a, Color.black, .3f));
                    break;
                case DecoShape.Cactus:
                    t = Tex(16, 24);
                    FillRect(t, 6, 0, 4, 20, a);
                    FillRect(t, 2, 8, 4, 2, a); FillRect(t, 2, 8, 2, 6, a);
                    FillRect(t, 10, 12, 4, 2, a); FillRect(t, 12, 12, 2, 5, a);
                    break;
                case DecoShape.Barrel:
                    t = Tex(12, 14);
                    FillRect(t, 1, 0, 10, 12, a);
                    FillRect(t, 1, 3, 10, 1, b); FillRect(t, 1, 8, 10, 1, b);
                    break;
                case DecoShape.Bush:
                    t = Tex(18, 12);
                    FillCircle(t, 6, 5, 5, a); FillCircle(t, 12, 5, 5, a); FillCircle(t, 9, 7, 4, Color.Lerp(a, b, .4f));
                    break;
                case DecoShape.Fence:
                    t = Tex(24, 14);
                    for (int i = 0; i < 4; i++) FillRect(t, 1 + i * 6, 0, 2, 12, a);
                    FillRect(t, 0, 4, 24, 2, b); FillRect(t, 0, 9, 24, 2, b);
                    break;
                default: // Car
                    t = Tex(28, 14);
                    FillRect(t, 1, 3, 26, 7, a);
                    FillRect(t, 6, 8, 14, 4, Color.Lerp(a, Color.black, .3f));
                    FillCircle(t, 6, 2, 2.5f, Color.black); FillCircle(t, 21, 2, 2.5f, Color.black);
                    FillRect(t, 8, 8, 4, 3, b); FillRect(t, 15, 8, 4, 3, b);
                    break;
            }
            Outline(t, new Color(0, 0, 0, .85f));
            return Bake(k, t, PPU, .1f);
        }

        public static Sprite GroundTile(Color baseCol, int seed)
        {
            string k = $"gnd{baseCol}{seed}";
            if (cache.TryGetValue(k, out var s)) return s;
            var rng = new System.Random(seed);
            var t = Tex(16, 16);
            for (int x = 0; x < 16; x++) for (int y = 0; y < 16; y++)
            {
                float v = (float)(rng.NextDouble() * .08 - .04);
                Px(t, x, y, new Color(
                    Mathf.Clamp01(baseCol.r + v),
                    Mathf.Clamp01(baseCol.g + v),
                    Mathf.Clamp01(baseCol.b + v)));
            }
            for (int i = 0; i < 5; i++)
                Px(t, rng.Next(16), rng.Next(16), Color.Lerp(baseCol, Color.black, .25f));
            return Bake(k, t, 16f);
        }

        public static Sprite Gem(Color c) => Projectile(ProjShape.Shard, c, .6f);
        public static Sprite Chest()
        {
            var real = Load("Chest"); if (real != null) return real;
            if (cache.TryGetValue("plc_chest", out var s)) return s;
            var t = Tex(18, 14);
            var wood = new Color(.45f, .28f, .14f);
            FillRect(t, 1, 0, 16, 10, wood);
            FillRect(t, 1, 9, 16, 3, Color.Lerp(wood, Color.black, .3f));
            FillRect(t, 8, 4, 2, 4, new Color(.9f, .8f, .3f));
            FillRect(t, 1, 5, 16, 1, new Color(.2f, .12f, .06f));
            Outline(t, Color.black);
            return Bake("plc_chest", t, PPU, .1f);
        }
    }
}
