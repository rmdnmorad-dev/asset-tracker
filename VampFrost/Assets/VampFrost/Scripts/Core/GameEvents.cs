using System;
using UnityEngine;

namespace VampFrost
{
    /// Central event bus. ALL gameplay->audio/UI/FX communication flows through here.
    /// No gameplay script ever calls AudioManager directly (per audio spec).
    public static class GameEvents
    {
        // ---- Enemies ----
        public static Action<Enemy> OnEnemySpawn;
        public static Action<Enemy> OnEnemyDeath;
        public static Action<Vector2> OnEnemyTelegraph;   // ranged wind-up
        public static Action OnEnemyTick;                 // occasional movement tick

        // ---- Bosses ----
        public static Action<Boss> OnBossSpawn;
        public static Action<int> OnBossPhaseChange;      // new phase (1..3)
        public static Action OnBossHeavy;                 // heavy attack / slam
        public static Action OnBossDeath;

        // ---- Player ----
        public static Action<float> OnPlayerDamage;
        public static Action OnPlayerDeath;
        public static Action OnPlayerDash;
        public static Action OnPlayerInvisibility;
        public static Action OnFootstep;
        public static Action<int> OnLevelUp;              // new level
        public static Action OnXPGained;
        public static Action<int> OnGoldGained;
        public static Action OnHealthPickup;

        // ---- Combat ----
        public static Action<int> OnWeaponFire;           // weapon id
        public static Action<bool> OnHit;                 // crit?
        public static Action OnFreezeApplied;
        public static Action OnExplosion;
        public static Action OnChestOpen;

        // ---- Flow ----
        public static Action<int> OnWaveStart;
        public static Action<int> OnWaveEnd;
        public static Action OnRunStart;
        public static Action OnRunEnd;
        public static Action OnVictory;
        public static Action OnGameOver;
        public static Action<bool> OnPause;               // true = paused

        // ---- UI ----
        public static Action OnUIHover, OnUIClick, OnUIConfirm, OnUICancel,
                             OnUIError, OnUIOpen, OnUIClose, OnUINotify;
    }
}
