using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    /// The opponents. Each bot has a personality that produces a *readable* habit
    /// during prep and then either honours or breaks it at commit time - that is
    /// what makes watching the arena worth doing.
    public class BotBrain
    {
        public PlayerState me;
        public string personality;

        float skill;       // 0..1 - how hard it reads your habits
        float sticky;      // 0..1 - how often it returns to its favourite tile
        float honesty;     // 0..1 - chance its prep dwell matches where it actually hides
        float aggression;  // 0..1 - gadget appetite
        float spread;      // 0..1 - willingness to skip the obvious target (dodges the dogpile rule)

        int favouriteKey;
        Vector3 wanderTarget;
        float wanderTimer;

        static readonly string[] Kinds = { "CAMPER", "DRIFTER", "EDGE-LORD", "TRICKSTER", "MIRROR", "HUNTER" };

        public BotBrain(PlayerState p)
        {
            me = p;
            int kind = Random.Range(0, Kinds.Length);
            personality = Kinds[kind];

            skill      = Random.Range(0.35f, 0.9f);
            sticky     = 0.2f;
            honesty    = 0.5f;
            aggression = Random.Range(0.25f, 0.8f);
            spread     = Random.Range(0.25f, 0.75f);

            switch (personality)
            {
                case "CAMPER":    sticky = 0.75f; honesty = 0.70f; break;
                case "DRIFTER":   sticky = 0.15f; honesty = 0.45f; break;
                case "EDGE-LORD": sticky = 0.45f; honesty = 0.60f; break;
                case "TRICKSTER": sticky = 0.05f; honesty = 0.12f; break;   // prep is pure theatre
                case "MIRROR":    sticky = 0.35f; honesty = 0.50f; break;
                case "HUNTER":    sticky = 0.30f; honesty = 0.55f; skill = Mathf.Min(1f, skill + 0.2f); break;
            }

            favouriteKey = Random.Range(0, Cfg.TileRows * Cfg.TileCols);
            wanderTarget = me.zone != null ? me.zone.CenterOf(0) : Vector3.zero;
        }

        // ------------------------------------------------------------ prep phase

        public Vector3 TickPrep(float dt, Vector3 current)
        {
            wanderTimer -= dt;
            if (wanderTimer <= 0f || (current - wanderTarget).sqrMagnitude < 0.10f)
            {
                wanderTarget = PickWanderPoint();
                wanderTimer = Random.Range(0.55f, 1.7f);
            }

            float speed = personality == "TRICKSTER" ? 5.4f : 4.2f;
            Vector3 next = Vector3.MoveTowards(current, wanderTarget, speed * dt);
            return me.zone.Clamp(next);
        }

        Vector3 PickWanderPoint()
        {
            var z = me.zone;
            if (z.Active.Count == 0) return Vector3.zero;

            TileCell cell;
            if (Random.value < sticky)
            {
                cell = FindByKey(z, favouriteKey);
                if (cell == null) cell = z.Active[Random.Range(0, z.Active.Count)];
            }
            else if (personality == "EDGE-LORD")
            {
                // gravitates to the rim, which is exactly where lava is coming from
                cell = z.Active[z.Active.Count - 1];
                if (Random.value < 0.35f) cell = z.Active[Random.Range(0, z.Active.Count)];
            }
            else
            {
                cell = z.Active[Random.Range(0, z.Active.Count)];
            }

            return RandomPointIn(cell);
        }

        public static Vector3 RandomPointIn(TileCell c)
        {
            float m = 0.3f;
            float r = Random.Range(c.rIn + m, c.rOut - m);
            float angM = Mathf.Rad2Deg * (m / Mathf.Max(r, 1f));
            float a = Random.Range(c.aStart + angM, c.aEnd - angM);
            return MeshFactory.Polar(r, a);
        }

        static TileCell FindByKey(Zone z, int key)
        {
            for (int i = 0; i < z.Active.Count; i++)
                if (PlayerState.Key(z.Active[i]) == key) return z.Active[i];
            return null;
        }

        // -------------------------------------------------------- decision phase

        public void Decide(List<PlayerState> players, int round, Vector3 prepEndPos)
        {
            var d = Decision.Empty;
            d.hideTile = PickHide(prepEndPos);

            int target = PickTarget(players);
            d.targetPlayer = target;
            d.targetTile = target >= 0 ? PredictTile(players[target]) : 0;
            d.gadget = PickGadget(players, target);
            d.locked = true;

            me.decision = d;
        }

        int PickHide(Vector3 prepEndPos)
        {
            var z = me.zone;
            if (z.Active.Count == 0) return 0;
            if (z.Active.Count <= Cfg.MinTiles) return Random.Range(0, z.Active.Count);   // pure 50/50 endgame

            // "honesty" is the bluff dial: an honest bot ends up where it was
            // loitering, a trickster spent the whole prep phase lying to you.
            if (Random.value < honesty)
            {
                var cell = z.CellAt(prepEndPos);
                if (cell != null)
                {
                    int idx = z.IndexOf(cell);
                    if (idx >= 0 && !(idx == me.lastHideTile && Random.value < 0.6f)) return idx;
                }
            }

            if (Random.value < sticky)
            {
                var fav = FindByKey(z, favouriteKey);
                if (fav != null) return z.IndexOf(fav);
            }

            // otherwise avoid repeating the previous hiding spot
            int pick = Random.Range(0, z.Active.Count);
            if (pick == me.lastHideTile && z.Active.Count > 1)
                pick = (pick + 1 + Random.Range(0, z.Active.Count - 1)) % z.Active.Count;
            return pick;
        }

        int PickTarget(List<PlayerState> players)
        {
            var candidates = new List<PlayerState>();
            for (int i = 0; i < players.Count; i++)
                if (players[i].alive && players[i].index != me.index) candidates.Add(players[i]);
            if (candidates.Count == 0) return -1;

            // Spreading fire is not altruism - piling on trips the anti-dogpile rule
            // and costs the attacker a tile, so good bots deliberately scatter.
            if (Random.value < spread)
                return candidates[Random.Range(0, candidates.Count)].index;

            float best = float.MinValue;
            int bestIdx = candidates[0].index;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                // Fewer tiles = easier to guess right, so wounded players draw fire.
                // Deliberately no "prefer the human" term here: 15 bots applying the
                // same identity bias compound into a permanent pile-on that halves the
                // player's win rate. Heat on the player has to be earned by tile count.
                float score = (Cfg.StartTiles - c.TileCount) * 1.2f + Random.Range(0f, 2.4f);
                if (score > best) { best = score; bestIdx = c.index; }
            }
            return bestIdx;
        }

        /// Reads the target's habits: where they have hidden before, blended with
        /// where they spent the prep phase standing around.
        int PredictTile(PlayerState target)
        {
            var z = target.zone;
            if (z.Active.Count == 0) return 0;
            if (z.Active.Count <= 1) return 0;

            var weights = new float[z.Active.Count];
            float total = 0f;
            for (int i = 0; i < z.Active.Count; i++)
            {
                int key = PlayerState.Key(z.Active[i]);
                float habit = target.hideHistory[key] * 0.62f + target.prepDwell[key] * 0.38f;
                // skill blends the read against a flat guess
                float w = Mathf.Lerp(1f, 0.15f + habit * 2.2f, skill) + 0.001f;
                weights[i] = w;
                total += w;
            }

            float roll = Random.value * total;
            for (int i = 0; i < weights.Length; i++)
            {
                roll -= weights[i];
                if (roll <= 0f) return i;
            }
            return z.Active.Count - 1;
        }

        Gadget PickGadget(List<PlayerState> players, int target)
        {
            bool endgame = me.TileCount <= Cfg.MinTiles + 1;

            if (endgame && me.Has(Gadget.Shield) && Random.value < 0.55f) return Gadget.Shield;
            if (endgame && me.Has(Gadget.Decoy) && Random.value < 0.35f) return Gadget.Decoy;

            if (target >= 0 && me.Has(Gadget.Splash))
            {
                var t = players[target];
                // splash is worth most when the target's zone is small
                float chance = aggression * (t.TileCount <= 4 ? 0.7f : 0.35f);
                if (Random.value < chance) return Gadget.Splash;
            }

            if (me.Has(Gadget.Scout) && Random.value < 0.14f) return Gadget.Scout;
            if (me.Has(Gadget.Shield) && Random.value < 0.10f) return Gadget.Shield;

            return Gadget.None;
        }
    }
}
