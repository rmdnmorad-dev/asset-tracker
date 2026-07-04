using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace VampFrost
{
    /// Central audio brain. Listens ONLY to GameEvents (no gameplay script calls
    /// audio directly), pools AudioSources, runs the Intensity Controller and
    /// feeds the adaptive music system every frame.
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager I;

        ProceduralMusicSystem music;
        AudioSource ambient;
        AudioSource[] sfxPool;
        AudioSource[] uiPool;
        int sfxIdx, uiIdx;

        AudioMixer mixer;
        AudioMixerGroup gMusic, gSfx, gUi;

        readonly Dictionary<SfxId, float> nextOk = new();

        float intensity;      // 0..1 global Audio Intensity Controller
        float combat01;       // nearby enemy density
        bool paused;
        MapDef curMap;

        public float Intensity => intensity;

        // ------------------------------------------------------------------
        void Awake()
        {
            if (I != null) { Destroy(gameObject); return; }
            I = this;

            Sfx.Init();

            // Optional mixer: drop one at Assets/Resources/Audio/VampFrostMixer
            // with groups Master > Music/SFX/UI and exposed params
            // MasterVol / MusicVol / SFXVol / UIVol. Everything also works
            // without it (direct volume path).
            mixer = Resources.Load<AudioMixer>("Audio/VampFrostMixer");
            if (mixer != null)
            {
                gMusic = FindGroup("Music");
                gSfx = FindGroup("SFX");
                gUi = FindGroup("UI");
                Debug.Log("[VampFrost] AudioMixer found - routing through mixer groups.");
            }

            music = ProceduralMusicSystem.Create(transform, gMusic);

            ambient = gameObject.AddComponent<AudioSource>();
            ambient.loop = true; ambient.playOnAwake = false;
            ambient.spatialBlend = 0f; ambient.ignoreListenerPause = true;
            if (gSfx != null) ambient.outputAudioMixerGroup = gSfx;

            sfxPool = new AudioSource[18];
            for (int i = 0; i < sfxPool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                if (gSfx != null) s.outputAudioMixerGroup = gSfx;
                sfxPool[i] = s;
            }

            uiPool = new AudioSource[4];
            for (int i = 0; i < uiPool.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false; s.spatialBlend = 0f;
                s.ignoreListenerPause = true;
                if (gUi != null) s.outputAudioMixerGroup = gUi;
                uiPool[i] = s;
            }

            Subscribe();
            ApplyVolumes();
        }

        void OnDestroy() { if (I == this) I = null; }

        AudioMixerGroup FindGroup(string n)
        {
            var g = mixer.FindMatchingGroups(n);
            return g != null && g.Length > 0 ? g[0] : null;
        }

        // ------------------------------------------------------------------
        //  user volume (mixer path uses dB params; direct path multiplies)
        // ------------------------------------------------------------------
        static float Db(float v) => v <= .0001f ? -80f : Mathf.Log10(v) * 20f;

        float UserMaster => SaveSystem.Data.muteMaster ? 0f : SaveSystem.Data.master;
        float UserSfx => (SaveSystem.Data.muteSfx ? 0f : SaveSystem.Data.sfx) * UserMaster;
        float UserUi => (SaveSystem.Data.muteUi ? 0f : SaveSystem.Data.ui) * UserMaster;
        float UserMusic => (SaveSystem.Data.muteMusic ? 0f : SaveSystem.Data.music) * UserMaster;

        public void ApplyVolumes()
        {
            if (mixer == null) return; // direct path is applied live every frame/shot
            var d = SaveSystem.Data;
            mixer.SetFloat("MasterVol", Db(d.muteMaster ? 0 : d.master));
            mixer.SetFloat("MusicVol", Db(d.muteMusic ? 0 : d.music));
            mixer.SetFloat("SFXVol", Db(d.muteSfx ? 0 : d.sfx));
            mixer.SetFloat("UIVol", Db(d.muteUi ? 0 : d.ui));
        }

        // ------------------------------------------------------------------
        //  playback helpers (pooled, throttled, pitch-jittered)
        // ------------------------------------------------------------------
        void Play(SfxId id, float vol, float pitch = 1f, float jitter = .05f, float minGap = 0f)
        {
            if (minGap > 0f)
            {
                if (nextOk.TryGetValue(id, out var t) && Time.unscaledTime < t) return;
                nextOk[id] = Time.unscaledTime + minGap;
            }
            var clip = Sfx.Get(id);
            var s = sfxPool[sfxIdx = (sfxIdx + 1) % sfxPool.Length];
            s.pitch = pitch * (1f + Random.Range(-jitter, jitter));
            s.PlayOneShot(clip, vol * (mixer != null ? 1f : UserSfx));
        }

        void PlayUI(SfxId id, float vol, float pitch = 1f)
        {
            var s = uiPool[uiIdx = (uiIdx + 1) % uiPool.Length];
            s.pitch = pitch;
            s.PlayOneShot(Sfx.Get(id), vol * (mixer != null ? 1f : UserUi));
        }

        // ------------------------------------------------------------------
        //  EVENT WIRING - the entire game talks to audio through these
        // ------------------------------------------------------------------
        void Subscribe()
        {
            // player
            GameEvents.OnFootstep += () => Play(SfxId.Footstep, .32f, 1f, .12f);
            GameEvents.OnPlayerDash += () => Play(SfxId.Dash, .6f);
            GameEvents.OnPlayerInvisibility += () => Play(SfxId.Invis, .55f);
            GameEvents.OnPlayerDamage += _ => Play(SfxId.PlayerHurt, .7f);
            GameEvents.OnPlayerDeath += () => Play(SfxId.PlayerDeath, .9f);
            GameEvents.OnLevelUp += _ => Play(SfxId.LevelUp, .8f);
            GameEvents.OnXPGained += () => Play(SfxId.XP, .16f, 1f, .15f, .09f);
            GameEvents.OnGoldGained += _ => Play(SfxId.Coin, .4f, 1f, .1f, .08f);
            GameEvents.OnHealthPickup += () => Play(SfxId.Heal, .55f);

            // combat
            GameEvents.OnWeaponFire += id => Play(SfxId.WeaponFire, .38f,
                id >= 0 && id < WeaponDefs.All.Length ? WeaponDefs.All[id].pitch : 1f,
                .05f, .03f);
            GameEvents.OnHit += crit =>
            {
                if (crit) Play(SfxId.Crit, .5f, 1f, .08f, .06f);
                else Play(SfxId.Impact, .42f, 1f, .1f, .045f);
            };
            GameEvents.OnFreezeApplied += () => Play(SfxId.Freeze, .5f, 1f, .06f, .12f);
            GameEvents.OnExplosion += () => Play(SfxId.BossHeavy, .5f, 1.35f, .1f, .08f);
            GameEvents.OnChestOpen += () => Play(SfxId.Chest, .75f);

            // enemies
            GameEvents.OnEnemyTick += () => Play(SfxId.EnemyTick, .22f, 1f, .2f, .3f);
            GameEvents.OnEnemyTelegraph += _ => Play(SfxId.Telegraph, .38f, 1f, .08f, .25f);
            GameEvents.OnEnemyDeath += _ => Play(SfxId.EnemyDeath, .38f, 1f, .12f, .055f);

            // bosses
            GameEvents.OnBossSpawn += _ => Play(SfxId.BossRoar, .95f);
            GameEvents.OnBossPhaseChange += p =>
            {
                Play(SfxId.BossPhase, .9f);
                Play(SfxId.BossRoar, .55f, 1.1f);
                StartCoroutine(music.PhaseTransition());
            };
            GameEvents.OnBossHeavy += () => Play(SfxId.BossHeavy, .9f, 1f, .06f, .15f);
            GameEvents.OnBossDeath += () => Play(SfxId.BossRoar, .8f, .7f);

            // flow
            GameEvents.OnWaveStart += w => { if (w > 1) Play(SfxId.WaveSting, .55f); };
            GameEvents.OnRunStart += () =>
            {
                curMap = GameManager.I.Map;
                ambient.clip = EnvironmentAudio.Get(curMap.ambient);
                ambient.Play();
                music.StartRun(curMap);
            };
            GameEvents.OnRunEnd += () => { music.StopRun(); ambient.Stop(); };
            GameEvents.OnGameOver += () =>
            { Play(SfxId.GameOver, .9f); music.StopRun(); ambient.Stop(); };
            GameEvents.OnVictory += () =>
            { Play(SfxId.Victory, .9f); music.StopRun(); ambient.Stop(); };
            GameEvents.OnPause += p => { paused = p; AudioListener.pause = p; };

            // UI
            GameEvents.OnUIHover += () => PlayUI(SfxId.UIHover, .3f);
            GameEvents.OnUIClick += () => PlayUI(SfxId.UIClick, .5f);
            GameEvents.OnUIConfirm += () => PlayUI(SfxId.UIConfirm, .55f);
            GameEvents.OnUICancel += () => PlayUI(SfxId.UICancel, .5f);
            GameEvents.OnUIError += () => PlayUI(SfxId.UIError, .55f);
            GameEvents.OnUIOpen += () => PlayUI(SfxId.UIOpen, .45f);
            GameEvents.OnUIClose += () => PlayUI(SfxId.UIClose, .45f);
            GameEvents.OnUINotify += () => PlayUI(SfxId.UINotify, .45f);
        }

        // ------------------------------------------------------------------
        //  INTENSITY CONTROLLER + per-frame dynamics
        // ------------------------------------------------------------------
        void Update()
        {
            float udt = Time.unscaledDeltaTime;
            var gm = GameManager.I;
            var pc = PlayerController.I;

            bool inRun = gm != null &&
                (gm.state == GameManager.State.Playing ||
                 gm.state == GameManager.State.LevelUp ||
                 gm.state == GameManager.State.Paused);

            int enemies = Enemy.Active.Count;
            int near = 0;
            if (pc != null)
            {
                Vector2 p = pc.transform.position;
                for (int i = 0; i < Enemy.Active.Count; i++)
                {
                    var e = Enemy.Active[i];
                    if (e != null && ((Vector2)e.transform.position - p).sqrMagnitude < 64f)
                        near++;
                }
            }

            float hp01 = pc != null && pc.S.maxHP > 0 ? Mathf.Clamp01(pc.HP / pc.S.maxHP) : 1f;
            bool bossA = Boss.Current != null;
            int phase = bossA ? Boss.Current.Phase : 0;
            float wave01 = gm != null ? gm.wave / (float)GameManager.MaxWave : 0f;

            float target = !inRun ? 0f : Mathf.Clamp01(
                .50f * Mathf.Clamp01(enemies / 45f) +
                .30f * Mathf.Clamp01(near / 12f) +
                .25f * (1f - hp01) +
                (bossA ? .35f : 0f) +
                .08f * wave01);

            intensity = Mathf.MoveTowards(intensity, target,
                (target > intensity ? 1.1f : .35f) * udt);

            float cTarget = Mathf.Clamp01(near / 10f);
            combat01 = Mathf.MoveTowards(combat01, cTarget,
                (cTarget > combat01 ? 1.6f : .6f) * udt);

            music.Tick(udt, intensity, combat01, hp01, bossA, phase,
                gm != null && gm.state == GameManager.State.Paused,
                curMap != null ? curMap.reverb : AudioReverbPreset.Off,
                mixer != null ? 1f : UserMusic);

            ambient.volume = .8f * (mixer != null ? 1f : UserSfx)
                             * (paused ? .35f : 1f)
                             * (inRun ? 1f : 0f);
        }
    }
}
