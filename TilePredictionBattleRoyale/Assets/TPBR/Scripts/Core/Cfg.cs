namespace TPBR
{
    /// Every tunable number in the prototype lives here.
    public static class Cfg
    {
        // ---- roster -------------------------------------------------------
        public const int PlayerCount = 16;
        // Zone 8 sits at 180 deg, which is the bottom of the screen under the fixed
        // camera - the most readable seat in the house, so that is yours.
        public const int HumanIndex  = 8;

        // ---- zone / tile layout -------------------------------------------
        public const int TileRows   = 4;       // radial slices  (outer rows die first)
        public const int TileCols   = 2;       // angular slices
        public const int StartTiles = TileRows * TileCols;   // 8
        public const int MinTiles   = 2;       // "no player can ever go below 2"

        public const float InnerR     = 8.5f;  // inner edge of the zone ring
        public const float OuterR     = 15.5f; // outer edge of the zone ring
        public const float ZoneGapDeg = 2.5f;  // dead space between neighbouring zones
        public const float TileH      = 0.45f; // tile extrusion depth
        public const float TileLift   = 0.10f; // gap between tile top and outline

        // ---- round timing --------------------------------------------------
        public const float PrepSeconds     = 10f;
        public const float DecisionSeconds = 15f;

        // ---- lava / shrink --------------------------------------------------
        public const int LavaFirstRound  = 3;  // first round lava eats a tile
        public const int LavaEveryRounds = 2;  // ...and every N rounds after that

        // ---- anti-dogpiling --------------------------------------------------
        // 16..11 alive -> 4+ attackers on one target penalises all of them.
        // 10 or fewer   -> 3+ attackers does.
        public const int DogpileAliveBreak    = 11;
        public const int DogpileThresholdHigh = 4;
        public const int DogpileThresholdLow  = 3;
        public const int DogpilePenaltyTiles  = 1;

        // ---- gadgets ---------------------------------------------------------
        public const int SplashCharges = 2;
        public const int ShieldCharges = 1;
        public const int DecoyCharges  = 1;
        public const int ScoutCharges  = 2;

        // ---- reveal beat lengths (seconds) ------------------------------------
        public const float BeatLock    = 1.10f;
        public const float BeatIncoming= 1.30f;
        public const float BeatImpact  = 1.40f;
        public const float BeatDogpile = 1.80f;
        public const float BeatLava    = 1.60f;
        public const float BeatSummary = 2.20f;

        // ---- derived ----------------------------------------------------------
        public const float ZoneStepDeg  = 360f / PlayerCount;              // 22.5
        public const float ZoneSpanDeg  = ZoneStepDeg - ZoneGapDeg;        // 20.0
        public const float ColSpanDeg   = ZoneSpanDeg / TileCols;
        public const float RowDepth     = (OuterR - InnerR) / TileRows;

        public static int DogpileThreshold(int aliveAtRoundStart)
            => aliveAtRoundStart >= DogpileAliveBreak ? DogpileThresholdHigh : DogpileThresholdLow;
    }
}
