using UnityEngine;

namespace TPBR
{
    public enum Gadget { None = 0, Splash = 1, Shield = 2, Decoy = 3, Scout = 4 }

    public static class Gadgets
    {
        public const int Count = 5;

        public static string Name(Gadget g)
        {
            switch (g)
            {
                case Gadget.Splash: return "SPLASH";
                case Gadget.Shield: return "SHIELD";
                case Gadget.Decoy:  return "DECOY";
                case Gadget.Scout:  return "SCOUT";
                default:            return "NONE";
            }
        }

        public static string Blurb(Gadget g)
        {
            switch (g)
            {
                case Gadget.Splash: return "your attack also hits the tiles next to it";
                case Gadget.Shield: return "survive one hit this round";
                case Gadget.Decoy:  return "if hit, lose a tile instead of dying";
                case Gadget.Scout:  return "learn how many players attacked you (not who)";
                default:            return "no gadget";
            }
        }

        public static bool IsAttack(Gadget g) { return g == Gadget.Splash; }

        public static int StartCharges(Gadget g)
        {
            switch (g)
            {
                case Gadget.Splash: return Cfg.SplashCharges;
                case Gadget.Shield: return Cfg.ShieldCharges;
                case Gadget.Decoy:  return Cfg.DecoyCharges;
                case Gadget.Scout:  return Cfg.ScoutCharges;
                default:            return 0;
            }
        }
    }

    /// One player's secret commitment for a round. Nothing in here is ever shown
    /// to another player before the reveal, and the attacker's identity is never
    /// shown at all - only the public outcome is.
    public struct Decision
    {
        public int hideTile;      // active tile index inside my own zone
        public int targetPlayer;  // who I attack (-1 = nobody left to attack)
        public int targetTile;    // active tile index inside their zone
        public Gadget gadget;
        public bool locked;

        public static Decision Empty
        {
            get
            {
                Decision d;
                d.hideTile = 0;
                d.targetPlayer = -1;
                d.targetTile = 0;
                d.gadget = Gadget.None;
                d.locked = false;
                return d;
            }
        }
    }

    public class PlayerState
    {
        public int index;
        public string name;
        public Color color;
        public bool isHuman;

        public bool alive = true;
        public int deathRound = -1;
        public int placement = 0;       // 1 = winner

        public Zone zone;
        public Avatar avatar;

        public Decision decision = Decision.Empty;
        public int[] charges = new int[Gadgets.Count];

        // --- habit memory -------------------------------------------------
        // Keyed by grid cell (row * Cols + col) so the numbers stay meaningful
        // even after the zone shrinks and active indices shift underneath.
        public float[] hideHistory = new float[Cfg.TileRows * Cfg.TileCols];
        public float[] prepDwell   = new float[Cfg.TileRows * Cfg.TileCols];

        // --- per-round scratch ---------------------------------------------
        public int lastHideTile = -1;
        public int incomingCount;        // how many attacks landed on me (Scout reveals this)
        public bool usedShieldThisRound;
        public bool usedDecoyThisRound;

        public PlayerState(int i, string n, bool human)
        {
            index = i;
            name = n;
            isHuman = human;
            color = Palette.Of(i);
            for (int g = 0; g < Gadgets.Count; g++)
                charges[g] = Gadgets.StartCharges((Gadget)g);
        }

        public int TileCount { get { return zone != null ? zone.TileCount : 0; } }

        public bool Has(Gadget g) { return g != Gadget.None && charges[(int)g] > 0; }

        public void Spend(Gadget g) { if (Has(g)) charges[(int)g]--; }

        public static int Key(TileCell c) { return c.row * Cfg.TileCols + c.col; }

        public void RememberHide(TileCell c)
        {
            if (c == null) return;
            // decay old habits so recent behaviour reads louder than ancient rounds
            for (int i = 0; i < hideHistory.Length; i++) hideHistory[i] *= 0.72f;
            hideHistory[Key(c)] += 1f;
        }

        public void RememberDwell(TileCell c, float dt)
        {
            if (c == null) return;
            prepDwell[Key(c)] += dt;
        }

        public void DecayDwell()
        {
            for (int i = 0; i < prepDwell.Length; i++) prepDwell[i] *= 0.55f;
        }
    }
}
