using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    /// Infinite scrolling ground: builds 8x8-unit chunks around the player,
    /// deterministic decor per chunk. Purely cosmetic (VS-style open field).
    public class MapGenerator : MonoBehaviour
    {
        const int CHUNK = 8;
        const int VIEW = 3; // chunks in each direction

        MapDef map;
        int seed;
        readonly Dictionary<Vector2Int, GameObject> chunks = new();
        readonly List<Sprite> groundVariants = new();
        Transform holder;

        public static MapGenerator Create(Transform worldRoot, MapDef def)
        {
            var go = new GameObject("Map");
            go.transform.SetParent(worldRoot, false);
            var g = go.AddComponent<MapGenerator>();
            g.map = def;
            g.seed = def.id * 7919 + 13;
            g.holder = go.transform;
            for (int i = 0; i < 4; i++)
            {
                var real = SpriteFactory.Load($"tile_{def.ambient.ToString().ToLower()}_{i}");
                g.groundVariants.Add(real != null ? real
                    : SpriteFactory.GroundTile(i % 2 == 0 ? def.groundA : def.groundB, g.seed + i));
            }
            g.Ensure(Vector2.zero);
            return g;
        }

        void Update()
        {
            if (PlayerController.I != null) Ensure(PlayerController.I.transform.position);
        }

        void Ensure(Vector2 center)
        {
            var cc = new Vector2Int(Mathf.FloorToInt(center.x / CHUNK), Mathf.FloorToInt(center.y / CHUNK));
            for (int dx = -VIEW; dx <= VIEW; dx++)
                for (int dy = -VIEW; dy <= VIEW; dy++)
                {
                    var key = new Vector2Int(cc.x + dx, cc.y + dy);
                    if (!chunks.ContainsKey(key)) chunks[key] = Build(key);
                }
        }

        GameObject Build(Vector2Int c)
        {
            var go = new GameObject($"chunk_{c.x}_{c.y}");
            go.transform.SetParent(holder, false);
            go.transform.position = new Vector3(c.x * CHUNK, c.y * CHUNK, 0);
            var rng = new System.Random(c.x * 73856093 ^ c.y * 19349663 ^ seed);

            // ground tiles (1 unit each)
            for (int x = 0; x < CHUNK; x++)
                for (int y = 0; y < CHUNK; y++)
                {
                    var t = new GameObject("t");
                    t.transform.SetParent(go.transform, false);
                    t.transform.localPosition = new Vector3(x + .5f, y + .5f, 0);
                    var sr = t.AddComponent<SpriteRenderer>();
                    sr.sprite = groundVariants[rng.Next(groundVariants.Count)];
                    sr.sortingOrder = -5000;
                }

            // decor
            int decoCount = rng.Next(0, 4);
            for (int i = 0; i < decoCount; i++)
            {
                var d = new GameObject("deco");
                d.transform.SetParent(go.transform, false);
                float px = (float)rng.NextDouble() * CHUNK;
                float py = (float)rng.NextDouble() * CHUNK;
                d.transform.localPosition = new Vector3(px, py, 0);
                var sr = d.AddComponent<SpriteRenderer>();
                var shape = map.shapes[rng.Next(map.shapes.Length)];
                string realName = $"deco_{map.ambient.ToString().ToLower()}_{rng.Next(6)}";
                var real = SpriteFactory.Load(realName);
                sr.sprite = real != null ? real : SpriteFactory.Deco(shape, map.decoA, map.decoB);
                sr.sortingOrder = Mathf.RoundToInt(-(c.y * CHUNK + py) * 10);
            }
            return go;
        }
    }
}
