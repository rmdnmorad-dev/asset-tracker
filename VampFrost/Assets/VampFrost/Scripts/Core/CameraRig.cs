using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig I;
        public Camera Cam { get; private set; }
        Transform target;
        float shakeAmp, shakeT;

        public static CameraRig Create()
        {
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var rig = go.AddComponent<CameraRig>();
            rig.Cam = go.AddComponent<Camera>();
            rig.Cam.orthographic = true;
            rig.Cam.orthographicSize = 5.5f;
            rig.Cam.clearFlags = CameraClearFlags.SolidColor;
            rig.Cam.backgroundColor = new Color(.05f, .06f, .09f);
            go.AddComponent<AudioListener>();
            go.transform.position = new Vector3(0, 0, -10);
            return rig;
        }

        void Awake() { I = this; }
        void OnDestroy() { if (I == this) I = null; }

        public void SetTarget(Transform t) => target = t;
        public void SetBackground(Color c) => Cam.backgroundColor = c;

        public static void Shake(float amp, float dur)
        {
            if (I == null) return;
            I.shakeAmp = Mathf.Max(I.shakeAmp, amp);
            I.shakeT = Mathf.Max(I.shakeT, dur);
        }

        void LateUpdate()
        {
            Vector3 basePos = target != null
                ? Vector3.Lerp(transform.position, target.position, 8f * Time.unscaledDeltaTime)
                : transform.position;
            basePos.z = -10;
            Vector3 off = Vector3.zero;
            if (shakeT > 0)
            {
                shakeT -= Time.unscaledDeltaTime;
                off = (Vector3)(Random.insideUnitCircle * shakeAmp * (shakeT > 0 ? 1 : 0));
                if (shakeT <= 0) shakeAmp = 0;
            }
            transform.position = basePos + off;
        }
    }

    /// Pooled floating damage numbers.
    public static class FX
    {
        class Num : MonoBehaviour
        {
            public TextMesh tm; public float t;
            void Update()
            {
                t -= Time.deltaTime;
                transform.position += Vector3.up * 1.4f * Time.deltaTime;
                var c = tm.color; c.a = Mathf.Clamp01(t / .3f); tm.color = c;
                if (t <= 0) { gameObject.SetActive(false); pool.Push(this); }
            }
        }

        static readonly Stack<Num> pool = new();
        static Transform root;

        public static void Init(Transform worldRoot) { root = worldRoot; pool.Clear(); }

        public static void Damage(Vector2 pos, int val, bool crit)
        {
            if (root == null) return;
            Num n;
            if (pool.Count > 0) { n = pool.Pop(); n.gameObject.SetActive(true); }
            else
            {
                var go = new GameObject("dmg");
                go.transform.SetParent(root, false);
                n = go.AddComponent<Num>();
                n.tm = go.AddComponent<TextMesh>();
                n.tm.font = UIBuilder.Font;
                go.GetComponent<MeshRenderer>().material = UIBuilder.Font.material;
                go.GetComponent<MeshRenderer>().sortingOrder = 6000;
                n.tm.anchor = TextAnchor.MiddleCenter;
                n.tm.characterSize = 0.09f;
                n.tm.fontStyle = FontStyle.Bold;
            }
            n.transform.position = pos + Random.insideUnitCircle * .2f;
            n.tm.text = val.ToString();
            n.tm.fontSize = crit ? 46 : 32;
            n.tm.color = crit ? new Color(1f, .85f, .2f) : new Color(1f, .95f, .9f);
            n.t = .55f;
        }
    }
}
