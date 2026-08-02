using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    public class AttackRecord
    {
        public int attacker;
        public int target;
        public int targetTile;
        public Gadget gadget;
        public readonly List<int> hitTiles = new List<int>();   // active indices in the target's zone
        public bool killed;
    }

    /// Everything that happens in one simultaneous resolution, computed up front as
    /// pure data. The reveal phase then plays it back beat by beat - it never
    /// re-derives anything, so what you watch is exactly what was resolved.
    public class RoundResult
    {
        public int round;
        public int aliveAtStart;
        public int dogpileThreshold;

        public readonly List<AttackRecord> attacks = new List<AttackRecord>();
        public readonly Dictionary<int, List<int>> attackersByTarget = new Dictionary<int, List<int>>();
        public readonly Dictionary<int, HashSet<int>> hitTilesByZone = new Dictionary<int, HashSet<int>>();

        public readonly List<int> dogpiledTargets = new List<int>();
        public readonly List<int> penalisedAttackers = new List<int>();

        public readonly List<int> deaths = new List<int>();
        public readonly List<int> shieldSaves = new List<int>();
        public readonly List<int> decoySaves = new List<int>();
        public readonly List<int> scoutUsers = new List<int>();

        public bool lavaThisRound;
        public readonly List<int> lavaLosers = new List<int>();

        /// player -> how many tiles they lose this round from penalties + decoy
        public readonly Dictionary<int, int> instantTileLoss = new Dictionary<int, int>();

        public int AttackersOn(int target)
        {
            List<int> l;
            return attackersByTarget.TryGetValue(target, out l) ? l.Count : 0;
        }

        public bool WasHit(int zone, int tileIndex)
        {
            HashSet<int> s;
            return hitTilesByZone.TryGetValue(zone, out s) && s.Contains(tileIndex);
        }

        public int TileLossFor(int player)
        {
            int n;
            return instantTileLoss.TryGetValue(player, out n) ? n : 0;
        }
    }

    public static class Resolver
    {
        /// Pure: reads the locked decisions, writes no state. Call the Apply* methods
        /// on GameManager to actually mutate the world during the reveal.
        public static RoundResult Resolve(List<PlayerState> players, int round, bool lavaThisRound)
        {
            var res = new RoundResult { round = round, lavaThisRound = lavaThisRound };

            var alive = new List<PlayerState>();
            for (int i = 0; i < players.Count; i++) if (players[i].alive) alive.Add(players[i]);
            res.aliveAtStart = alive.Count;
            res.dogpileThreshold = Cfg.DogpileThreshold(res.aliveAtStart);

            // ---- 1. collect attacks -------------------------------------------
            for (int i = 0; i < alive.Count; i++)
            {
                var p = alive[i];
                var d = p.decision;
                if (d.targetPlayer < 0 || d.targetPlayer >= players.Count) continue;

                var target = players[d.targetPlayer];
                if (!target.alive || target.index == p.index) continue;

                var rec = new AttackRecord
                {
                    attacker = p.index,
                    target = target.index,
                    targetTile = d.targetTile,
                    gadget = d.gadget
                };

                rec.hitTiles.Add(d.targetTile);
                if (d.gadget == Gadget.Splash && p.Has(Gadget.Splash))
                {
                    var extra = Neighbours(target.zone, d.targetTile);
                    for (int k = 0; k < extra.Count; k++)
                        if (!rec.hitTiles.Contains(extra[k])) rec.hitTiles.Add(extra[k]);
                }

                res.attacks.Add(rec);

                List<int> list;
                if (!res.attackersByTarget.TryGetValue(target.index, out list))
                {
                    list = new List<int>();
                    res.attackersByTarget[target.index] = list;
                }
                list.Add(p.index);

                HashSet<int> hits;
                if (!res.hitTilesByZone.TryGetValue(target.index, out hits))
                {
                    hits = new HashSet<int>();
                    res.hitTilesByZone[target.index] = hits;
                }
                for (int k = 0; k < rec.hitTiles.Count; k++) hits.Add(rec.hitTiles[k]);
            }

            // ---- 2. who is standing in the blast ------------------------------
            for (int i = 0; i < alive.Count; i++)
            {
                var p = alive[i];
                p.incomingCount = res.AttackersOn(p.index);
                if (p.decision.gadget == Gadget.Scout && p.Has(Gadget.Scout)) res.scoutUsers.Add(p.index);

                if (!res.WasHit(p.index, p.decision.hideTile)) continue;

                if (p.decision.gadget == Gadget.Shield && p.Has(Gadget.Shield))
                {
                    res.shieldSaves.Add(p.index);
                }
                else if (p.decision.gadget == Gadget.Decoy && p.Has(Gadget.Decoy))
                {
                    res.decoySaves.Add(p.index);
                    AddLoss(res, p.index, 1);
                }
                else
                {
                    res.deaths.Add(p.index);
                }
            }

            for (int i = 0; i < res.attacks.Count; i++)
            {
                var a = res.attacks[i];
                a.killed = res.deaths.Contains(a.target);
            }

            // ---- 3. anti-dogpiling --------------------------------------------
            // Threshold is 4 while 11+ are alive, 3 once 10 or fewer remain.
            // The penalty lands on every attacker who piled on, and it lands even
            // when the target dies - that is the whole point of the rule.
            foreach (var kv in res.attackersByTarget)
            {
                if (kv.Value.Count < res.dogpileThreshold) continue;

                res.dogpiledTargets.Add(kv.Key);
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    int attacker = kv.Value[i];
                    if (res.deaths.Contains(attacker)) continue;   // already eliminated
                    if (res.penalisedAttackers.Contains(attacker)) continue;
                    res.penalisedAttackers.Add(attacker);
                    AddLoss(res, attacker, Cfg.DogpilePenaltyTiles);
                }
            }

            // ---- 4. lava --------------------------------------------------------
            if (lavaThisRound)
            {
                for (int i = 0; i < alive.Count; i++)
                {
                    var p = alive[i];
                    if (res.deaths.Contains(p.index)) continue;
                    if (p.zone.AtMinimum) continue;      // the 2-tile floor is absolute
                    res.lavaLosers.Add(p.index);
                }
            }

            return res;
        }

        static void AddLoss(RoundResult r, int player, int n)
        {
            int cur;
            r.instantTileLoss.TryGetValue(player, out cur);
            r.instantTileLoss[player] = cur + n;
        }

        /// Orthogonal neighbours of a tile inside one zone, as active indices.
        public static List<int> Neighbours(Zone z, int activeIndex)
        {
            var outp = new List<int>();
            var c = z.Tile(activeIndex);
            if (c == null) return outp;

            TryAdd(z, outp, c.row + 1, c.col);
            TryAdd(z, outp, c.row - 1, c.col);
            TryAdd(z, outp, c.row, c.col + 1);
            TryAdd(z, outp, c.row, c.col - 1);
            return outp;
        }

        static void TryAdd(Zone z, List<int> outp, int row, int col)
        {
            if (row < 0 || row >= Cfg.TileRows || col < 0 || col >= Cfg.TileCols) return;
            var cell = z.Cell(row, col);
            if (cell == null || !cell.alive) return;
            int idx = z.IndexOf(cell);
            if (idx >= 0) outp.Add(idx);
        }
    }
}
