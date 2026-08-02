using UnityEngine;

namespace TPBR
{
    /// Fixed high 3/4 overhead party-game view - the whole arena stays on screen at
    /// all times so every player's movement is readable. Scroll to zoom, that's it.
    public class ArenaCamera : MonoBehaviour
    {
        public static ArenaCamera I;

        public float tilt = 56f;
        public float distance = 47f;
        public float minDistance = 28f;
        public float maxDistance = 66f;

        Camera cam;
        Vector3 focusTarget;
        Vector3 focus;
        float yaw;
        float shakeSeed;

        public static ArenaCamera Create(Transform parent = null)
        {
            var go = new GameObject("ArenaCamera");
            if (parent != null) go.transform.SetParent(parent, false);
            var c = go.AddComponent<Camera>();
            c.fieldOfView = 46f;
            c.nearClipPlane = 0.5f;
            c.farClipPlane = 400f;
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(0.045f, 0.055f, 0.075f);
            c.tag = "MainCamera";
            go.AddComponent<AudioListener>();

            var rig = go.AddComponent<ArenaCamera>();
            rig.cam = c;
            I = rig;
            rig.Apply(0f);
            return rig;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        void Awake() { shakeSeed = Random.value * 100f; }

        /// Leans the view a little toward a zone without ever losing the full arena.
        public void Focus(int zoneIndex)
        {
            focusTarget = zoneIndex < 0
                ? Vector3.zero
                : MeshFactory.Polar(4.2f, zoneIndex * Cfg.ZoneStepDeg);
        }

        void LateUpdate()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
                distance = Mathf.Clamp(distance - scroll * 34f, minDistance, maxDistance);

            focus = Vector3.Lerp(focus, focusTarget, 1f - Mathf.Exp(-4f * Time.deltaTime));
            Apply(Time.time);
        }

        void Apply(float t)
        {
            // a hair of sway keeps it feeling alive without hurting readability
            float sway = Mathf.Sin(t * 0.28f) * 0.7f;
            yaw = sway;

            Quaternion rot = Quaternion.Euler(tilt, yaw, 0f);
            Vector3 pos = focus - rot * Vector3.forward * distance;

            if (Fx.Shake > 0.001f)
            {
                float s = Fx.Shake;
                float n1 = (Mathf.PerlinNoise(shakeSeed + t * 26f, 0f) - 0.5f) * 2f;
                float n2 = (Mathf.PerlinNoise(0f, shakeSeed + t * 26f) - 0.5f) * 2f;
                pos += new Vector3(n1, n2 * 0.6f, 0f) * s * 1.1f;
                rot = Quaternion.Euler(tilt + n2 * s * 0.9f, yaw + n1 * s * 0.9f, n1 * s * 0.6f);
            }

            transform.position = pos;
            transform.rotation = rot;
        }

        public Camera Cam { get { return cam; } }
    }
}
