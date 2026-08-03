using System.Collections;
using UnityEngine;

namespace TPBR
{
    /// Entry point. Open ANY scene - even a completely empty one - and press Play.
    /// Everything below is generated at runtime: no prefabs, no materials, no
    /// meshes, no scene references, nothing to wire up in the inspector.
    ///
    /// (Dropping this component on a GameObject yourself also works and takes
    /// priority over the automatic boot.)
    public class Bootstrap : MonoBehaviour
    {
        static bool booted;
        static Bootstrap instance;
        static GameObject root;

        /// Set before Restart() to drop straight into a new match instead of the
        /// title screen (what "PLAY AGAIN" on the results screen does).
        public static bool AutoStart;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            booted = false;
            instance = null;
            root = null;
            AutoStart = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (booted) return;                       // a hand-placed Bootstrap already ran
            new GameObject("Bootstrap").AddComponent<Bootstrap>();
        }

        void Awake()
        {
            if (booted && instance != null && instance != this) { Destroy(gameObject); return; }
            booted = true;
            instance = this;
            DontDestroyOnLoad(gameObject);
            BuildWorld();
        }

        void BuildWorld()
        {
            Time.timeScale = 1f;
            root = new GameObject("TPBR");

            // Audio hangs off the persistent Bootstrap object, not the match root:
            // regenerating every synthesised clip on each restart would cost a hitch
            // for no benefit.
            Audio.Init(transform);

            SetupLighting(root.transform);
            Fx.Init(root.transform);
            ArenaCamera.Create(root.transform);

            var arenaGo = new GameObject("Arena");
            arenaGo.transform.SetParent(root.transform, false);
            var arena = arenaGo.AddComponent<Arena>();
            arena.Build();

            var gmGo = new GameObject("GameManager");
            gmGo.transform.SetParent(root.transform, false);
            var gm = gmGo.AddComponent<GameManager>();
            gm.arena = arena;
            gm.Begin();

            Hud.Create(root.transform);

            if (AutoStart) { AutoStart = false; gm.StartMatch(); }

            Debug.Log("[TPBR] Booted. Arena, 16 players, audio, FX and UI all generated at runtime.");
        }

        public static void Restart()
        {
            if (instance != null) instance.StartCoroutine(instance.Rebuild());
        }

        IEnumerator Rebuild()
        {
            if (root != null) Destroy(root);
            Fx.Clear();
            yield return null;          // let the destroys flush before rebuilding
            BuildWorld();
        }

        static void SetupLighting(Transform parent)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.17f, 0.21f, 0.29f);
            RenderSettings.ambientEquatorColor = new Color(0.10f, 0.12f, 0.16f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.09f, 0.05f);   // lava bounce
            RenderSettings.fog = false;
            QualitySettings.shadowDistance = 120f;

            var key = new GameObject("KeyLight");
            key.transform.SetParent(parent, false);
            key.transform.rotation = Quaternion.Euler(52f, -28f, 0f);
            var kl = key.AddComponent<Light>();
            kl.type = LightType.Directional;
            kl.color = new Color(1f, 0.96f, 0.88f);
            kl.intensity = 1.15f;
            kl.shadows = LightShadows.Soft;
            kl.shadowStrength = 0.5f;

            var fill = new GameObject("FillLight");
            fill.transform.SetParent(parent, false);
            fill.transform.rotation = Quaternion.Euler(-18f, 152f, 0f);
            var fl = fill.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.55f, 0.68f, 1f);
            fl.intensity = 0.38f;
            fl.shadows = LightShadows.None;
        }
    }
}
