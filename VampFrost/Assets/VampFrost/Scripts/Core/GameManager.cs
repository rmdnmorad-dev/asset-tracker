using System.Collections;
using UnityEngine;

namespace VampFrost
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager I;
        public enum State { Menu, Playing, LevelUp, Paused, GameOver, Victory }

        public State state = State.Menu;
        public const int MaxWave = 20;
        public const float WaveDur = 45f;

        public int mapId;
        public MapDef Map => MapDefs.All[mapId];
        public float runTime;
        public int wave = 1;
        public int goldRun;

        Transform worldRoot;
        bool curBossMini;

        public static Transform World => I != null ? I.worldRoot : null;

        void Awake()
        {
            I = this;
            GameEvents.OnPlayerDeath += HandlePlayerDeath;
            GameEvents.OnBossSpawn += b => curBossMini = b.Mini;
            GameEvents.OnBossDeath += HandleBossDeath;
        }

        void OnDestroy()
        {
            if (I == this) I = null;
            GameEvents.OnPlayerDeath -= HandlePlayerDeath;
            GameEvents.OnBossDeath -= HandleBossDeath;
        }

        void Update()
        {
            switch (state)
            {
                case State.Playing:
                    runTime += Time.deltaTime;
                    wave = Mathf.Clamp(1 + (int)(runTime / WaveDur), 1, MaxWave);
                    if (Input.GetKeyDown(KeyCode.Escape) &&
                        PlayerController.I != null && !PlayerController.I.Dead)
                        SetPause(true);
                    break;
                case State.Paused:
                    if (Input.GetKeyDown(KeyCode.Escape)) SetPause(false);
                    break;
            }
        }

        // ---------------- run lifecycle ----------------
        public void StartRun(int map)
        {
            mapId = map;
            TearDownWorld();

            Time.timeScale = 1f;
            runTime = 0; wave = 1; goldRun = 0; curBossMini = false;

            worldRoot = new GameObject("World").transform;
            Projectile.Root = worldRoot;
            FX.Init(worldRoot);
            MapGenerator.Create(worldRoot, Map);

            var player = PlayerController.Create(worldRoot);
            if (CameraRig.I != null)
            {
                CameraRig.I.transform.position = new Vector3(0, 0, -10);
                CameraRig.I.SetTarget(player.transform);
                CameraRig.I.SetBackground(Map.bg);
            }

            WaveSpawner.Create(worldRoot);

            Menus.HideAll();
            LevelUpPanel.ResetQueue();
            HUD.Show();

            state = State.Playing;
            GameEvents.OnRunStart?.Invoke();
        }

        void TearDownWorld()
        {
            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = null;
            Enemy.ClearPool();
            Projectile.ClearPool();
            XPGem.ClearPool();
            GoldPickup.ClearPool();
        }

        public void SetPause(bool on)
        {
            if (on && state != State.Playing) return;
            if (!on && state != State.Paused) return;
            state = on ? State.Paused : State.Playing;
            Time.timeScale = on ? 0f : 1f;
            Menus.ShowPause(on);
            GameEvents.OnPause?.Invoke(on);
        }

        public void EndToMenu()
        {
            Time.timeScale = 1f;
            if (state == State.Paused) GameEvents.OnPause?.Invoke(false);
            FinishRunPersist();
            TearDownWorld();
            LevelUpPanel.ResetQueue();
            HUD.Hide();
            Menus.HideAll();
            state = State.Menu;
            if (CameraRig.I != null)
            {
                CameraRig.I.SetTarget(null);
                CameraRig.I.SetBackground(new Color(.05f, .06f, .09f));
            }
            GameEvents.OnRunEnd?.Invoke();
            Menus.ShowMain();
        }

        bool persisted;
        void FinishRunPersist()
        {
            if (persisted || runTime <= 0) { persisted = false; return; }
            SaveSystem.Data.gold += goldRun;
            SaveSystem.Data.runsPlayed++;
            SaveSystem.Save();
            persisted = false;
            runTime = 0;
        }

        public void AddGold(int v)
        {
            goldRun += v;
            GameEvents.OnGoldGained?.Invoke(v);
        }

        // ---------------- endings ----------------
        void HandlePlayerDeath()
        {
            if (state != State.Playing && state != State.LevelUp) return;
            StartCoroutine(GameOverSeq());
        }

        IEnumerator GameOverSeq()
        {
            GameEvents.OnGameOver?.Invoke();
            Time.timeScale = .3f;
            yield return new WaitForSecondsRealtime(1.1f);
            Time.timeScale = 0f;
            state = State.GameOver;
            SaveSystem.Data.gold += goldRun;
            SaveSystem.Data.runsPlayed++;
            SaveSystem.Save();
            persisted = true;
            HUD.Hide();
            Menus.ShowEnd(false);
        }

        void HandleBossDeath()
        {
            if (curBossMini) { curBossMini = false; return; }
            if (state != State.Playing) return;
            StartCoroutine(VictorySeq());
        }

        IEnumerator VictorySeq()
        {
            GameEvents.OnVictory?.Invoke();
            yield return new WaitForSecondsRealtime(1.6f);
            Time.timeScale = 0f;
            state = State.Victory;
            SaveSystem.Data.gold += goldRun;
            SaveSystem.Data.runsPlayed++;
            SaveSystem.Save();
            persisted = true;
            HUD.Hide();
            Menus.ShowEnd(true);
        }
    }
}
