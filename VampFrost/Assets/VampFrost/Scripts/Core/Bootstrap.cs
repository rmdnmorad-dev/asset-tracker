using UnityEngine;

namespace VampFrost
{
    /// Drop this on any GameObject in an empty scene and press Play.
    /// (It also auto-creates itself if you forget - a totally empty scene works.)
    public class Bootstrap : MonoBehaviour
    {
        static bool booted;

        // supports Enter Play Mode Options (no domain reload)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => booted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoBoot()
        {
            if (Object.FindFirstObjectByType<Bootstrap>() == null)
                new GameObject("Bootstrap").AddComponent<Bootstrap>();
        }

        void Awake()
        {
            if (booted) { Destroy(gameObject); return; }
            booted = true;

            SaveSystem.Load();
            CameraRig.Create();
            UIBuilder.Init();

            new GameObject("AudioManager").AddComponent<AudioManager>();
            new GameObject("GameManager").AddComponent<GameManager>();

            Menus.Init();
            HUD.Create();
            LevelUpPanel.Init();
            Menus.ShowMain();

            Debug.Log("[VampFrost] booted - all content generated at runtime.");
        }

        void OnApplicationQuit() => SaveSystem.Save();
        void OnApplicationPause(bool p) { if (p) SaveSystem.Save(); }
    }
}
