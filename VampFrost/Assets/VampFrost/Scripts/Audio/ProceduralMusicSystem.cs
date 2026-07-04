using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace VampFrost
{
    // =====================================================================
    //  ADAPTIVE MUSIC - 4 synchronized generated loops:
    //    L1 drone (always) · L2 rhythm (danger) · L3 combat (enemy density)
    //    L4 boss (boss fights). Rhythm layers share one pitch group so
    //    tempo scaling never desyncs them.
    // =====================================================================
    public class ProceduralMusicSystem : MonoBehaviour
    {
        AudioSource srcDrone, srcRhy, srcCom, srcBoss;
        AudioLowPassFilter lpf;
        AudioHighPassFilter hpf;
        AudioDistortionFilter dist;
        AudioReverbFilter rev;

        static AudioClip cDrone, cRhy, cCom, cBoss;

        float vD, vR, vC, vB;
        float pitchGrp = 1f;
        float lastUserVol = 1f;
        bool running, inTransition;

        public static ProceduralMusicSystem Create(Transform parent, AudioMixerGroup grp)
        {
            var go = new GameObject("Music");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<ProceduralMusicSystem>();
            m.srcDrone = Mk(go, grp);
            m.srcRhy = Mk(go, grp);
            m.srcCom = Mk(go, grp);
            m.srcBoss = Mk(go, grp);
            m.lpf = go.AddComponent<AudioLowPassFilter>(); m.lpf.cutoffFrequency = 22000f;
            m.hpf = go.AddComponent<AudioHighPassFilter>(); m.hpf.cutoffFrequency = 10f;
            m.dist = go.AddComponent<AudioDistortionFilter>(); m.dist.distortionLevel = 0f;
            m.rev = go.AddComponent<AudioReverbFilter>(); m.rev.reverbPreset = AudioReverbPreset.Off;
            return m;
        }

        static AudioSource Mk(GameObject go, AudioMixerGroup grp)
        {
            var s = go.AddComponent<AudioSource>();
            s.loop = true; s.playOnAwake = false;
            s.spatialBlend = 0f; s.volume = 0f;
            s.ignoreListenerPause = true; // music keeps breathing (dimmed) while paused
            if (grp != null) s.outputAudioMixerGroup = grp;
            return s;
        }

        static void Generate()
        {
            if (cDrone != null) return;
            cDrone = MusicGen.Drone();
            cRhy = MusicGen.Rhythm();
            cCom = MusicGen.Combat();
            cBoss = MusicGen.BossLayer();
            Debug.Log("[VampFrost] Procedural music layers generated.");
        }

        public void StartRun(MapDef map)
        {
            Generate();
            srcDrone.clip = cDrone;
            srcRhy.clip = cRhy; srcCom.clip = cCom; srcBoss.clip = cBoss;

            vD = .55f; vR = vC = vB = 0f;
            pitchGrp = 1f;
            srcRhy.pitch = srcCom.pitch = srcBoss.pitch = 1f;
            srcDrone.pitch = 1f;

            double t = AudioSettings.dspTime + .08;
            srcDrone.PlayScheduled(t);
            srcRhy.PlayScheduled(t);   // sample-synced trio
            srcCom.PlayScheduled(t);
            srcBoss.PlayScheduled(t);

            rev.reverbPreset = map.reverb;
            running = true; inTransition = false;
        }

        public void StopRun()
        {
            running = false;
            srcDrone.Stop(); srcRhy.Stop(); srcCom.Stop(); srcBoss.Stop();
            vD = vR = vC = vB = 0f;
        }

        /// Called every frame by AudioManager with live gameplay parameters.
        public void Tick(float udt, float intensity, float combat01, float hp01,
                         bool bossActive, int phase, bool paused,
                         AudioReverbPreset mapPreset, float userVol)
        {
            lastUserVol = userVol;
            if (!running) return;

            float tD = .55f;
            float tR = intensity < .10f ? 0f
                     : Mathf.InverseLerp(.10f, .55f, intensity) * .65f;
            float tC = Mathf.Clamp01(combat01) * .70f;
            float tB = 0f;
            if (bossActive)
            {
                tB = (phase >= 3 ? 1f : phase == 2 ? .78f : .55f) * .85f;
                tR = Mathf.Max(tR, .55f);
                tC = Mathf.Max(tC, .55f);
            }
            if (paused) { tD *= .35f; tR *= .22f; tC *= .18f; tB *= .3f; }

            if (!inTransition)
            {
                vD = Move(vD, tD, udt); vR = Move(vR, tR, udt);
                vC = Move(vC, tC, udt); vB = Move(vB, tB, udt);
                Apply(userVol);

                float pT = 1f + intensity * .22f + (bossActive ? .04f : 0f);
                pitchGrp = Mathf.MoveTowards(pitchGrp, pT, .25f * udt);
                srcRhy.pitch = srcCom.pitch = srcBoss.pitch = pitchGrp;
            }

            // dynamic filter rack ------------------------------------------
            float lpTarget = Mathf.Lerp(700f, 22000f, Mathf.Clamp01(hp01 / .85f));
            if (paused) lpTarget = Mathf.Min(lpTarget, 900f);
            lpf.cutoffFrequency = Mathf.MoveTowards(lpf.cutoffFrequency, lpTarget, 40000f * udt);

            hpf.cutoffFrequency = intensity > .85f
                ? Mathf.Lerp(10f, 230f, (intensity - .85f) / .15f)
                : 10f;

            float dTarget = Mathf.Clamp01(
                (bossActive && phase >= 3 ? .35f : 0f) +
                (hp01 < .3f ? (.3f - hp01) * 1.1f : 0f));
            dist.distortionLevel = Mathf.MoveTowards(dist.distortionLevel, dTarget, 1.5f * udt);

            rev.reverbPreset = bossActive ? AudioReverbPreset.Hangar : mapPreset;
        }

        void Apply(float userVol)
        {
            srcDrone.volume = vD * userVol;
            srcRhy.volume = vR * userVol;
            srcCom.volume = vC * userVol;
            srcBoss.volume = vB * userVol;
        }

        static float Move(float v, float t, float dt)
            => Mathf.MoveTowards(v, t, (t > v ? 1.4f : .5f) * dt);

        /// Boss phase change: swell (volume + pitch rise) -> sudden silence -> slam back in.
        public IEnumerator PhaseTransition()
        {
            if (!running) yield break;
            inTransition = true;
            float p0 = pitchGrp;
            float d0 = vD, r0 = vR, c0 = vC, b0 = vB;
            float t = 0f;
            const float swell = .7f;
            while (t < swell)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / swell);
                float pg = Mathf.Lerp(p0, p0 * 1.16f, k);
                srcRhy.pitch = srcCom.pitch = srcBoss.pitch = pg;
                float g = 1f + .3f * k;
                srcDrone.volume = Mathf.Clamp01(d0 * g) * lastUserVol;
                srcRhy.volume = Mathf.Clamp01(r0 * g) * lastUserVol;
                srcCom.volume = Mathf.Clamp01(c0 * g) * lastUserVol;
                srcBoss.volume = Mathf.Clamp01(b0 * g) * lastUserVol;
                yield return null;
            }
            // the drop
            srcDrone.volume = srcRhy.volume = srcCom.volume = srcBoss.volume = 0f;
            yield return new WaitForSecondsRealtime(.32f);
            pitchGrp = p0;
            srcRhy.pitch = srcCom.pitch = srcBoss.pitch = p0;
            inTransition = false; // Tick ramps the new phase mix back in
        }
    }

    // =====================================================================
    //  MUSIC GENERATION - 100 BPM, 8-bar loops (19.2 s), sample-exact
    // =====================================================================
    static class MusicGen
    {
        const int BPM = 100;
        const int SPB = Synth.RATE * 60 / BPM;   // 26460 samples per beat
        const int BAR = SPB * 4;
        const int BARS = 8;
        const int LEN = BAR * BARS;              // 846720 samples = 19.2 s

        // ---- one-shot drums ----
        static float[] Kick()
        {
            var b = Synth.Buf(.16f);
            Synth.AddSine(b, 118f, 42f, .95f, .5f);
            Synth.ExpDecay(b, 20f);
            var click = Synth.Buf(.004f);
            Synth.AddNoise(click, new System.Random(3), .5f, .1f);
            Synth.AddInto(b, click, 0);
            Synth.Fade(b, .001f, .02f);
            return b;
        }

        static float[] Snare()
        {
            var b = Synth.Buf(.16f);
            Synth.AddNoise(b, new System.Random(11), .8f, .1f);
            Synth.HighPass(b, 500f);
            Synth.LowPass(b, 4500f);
            var body = Synth.Buf(.09f);
            Synth.AddSine(body, 195f, 170f, .3f, .05f);
            Synth.AddInto(b, body, 0);
            Synth.ExpDecay(b, 22f);
            Synth.Fade(b, .001f, .03f);
            return b;
        }

        static float[] Hat()
        {
            var b = Synth.Buf(.05f);
            Synth.AddNoise(b, new System.Random(23), .6f, .1f);
            Synth.HighPass(b, 5500f);
            Synth.ExpDecay(b, 55f);
            return b;
        }

        static float[] Tom()
        {
            var b = Synth.Buf(.22f);
            Synth.AddSine(b, 160f, 92f, .6f, .1f);
            Synth.ExpDecay(b, 11f);
            Synth.Fade(b, .001f, .04f);
            return b;
        }

        static void Stamp(float[] dst, float[] hit, float beat, float gain)
            => Synth.AddInto(dst, hit, Mathf.RoundToInt(beat * SPB), gain);

        // ---- Layer 1 : ambient drone (20 s, loop-exact partials) ----
        public static AudioClip Drone()
        {
            const float SEC = 20f;
            var b = Synth.Buf(SEC);

            // low airy noise bed first (needs a crossfade)
            var rng = new System.Random(77);
            Synth.AddNoise(b, rng, .06f, .06f);
            Synth.LowPass(b, 240f);
            Synth.Crossfade(b, 1f);

            // dark A-minor stack; every freq * 20 s is an integer -> seamless
            AddPartial(b, 27.5f, .20f, .05f, 0f);
            AddPartial(b, 55f, .38f, .10f, 1.1f);
            AddPartial(b, 65.4f, .22f, .05f, 2.3f);   // minor third
            AddPartial(b, 82.4f, .19f, .15f, 3.7f);   // fifth
            AddPartial(b, 110f, .12f, .10f, 4.9f);
            AddPartial(b, 163.8f, .06f, .20f, .6f);

            Synth.Normalize(b, .8f);
            return Synth.Clip("mus_drone", b);
        }

        static void AddPartial(float[] b, float f, float amp, float lfoHz, float lfoPhase)
        {
            double ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                ph += 2.0 * System.Math.PI * f / Synth.RATE;
                float lfo = .72f + .28f * Mathf.Sin(2f * Mathf.PI * lfoHz * i / Synth.RATE + lfoPhase);
                b[i] += (float)System.Math.Sin(ph) * amp * lfo;
            }
        }

        // ---- Layer 2 : rhythm pulse ----
        public static AudioClip Rhythm()
        {
            var b = new float[LEN];
            var kick = Kick(); var hat = Hat();
            for (int bar = 0; bar < BARS; bar++)
                for (int beat = 0; beat < 4; beat++)
                {
                    Stamp(b, kick, bar * 4 + beat, .85f);
                    Stamp(b, hat, bar * 4 + beat + .5f, .10f); // ghost offbeat
                }
            Synth.Normalize(b, .85f);
            return Synth.Clip("mus_rhythm", b);
        }

        // ---- Layer 3 : combat percussion ----
        public static AudioClip Combat()
        {
            var b = new float[LEN];
            var snare = Snare(); var hat = Hat(); var tom = Tom();
            float[] hatAcc = { .5f, .18f, .34f, .18f };

            for (int bar = 0; bar < BARS; bar++)
            {
                Stamp(b, snare, bar * 4 + 1, .7f);
                Stamp(b, snare, bar * 4 + 3, .7f);
                if (bar % 2 == 1) Stamp(b, snare, bar * 4 + 3.75f, .28f); // ghost

                for (int s16 = 0; s16 < 16; s16++)
                    Stamp(b, hat, bar * 4 + s16 * .25f, hatAcc[s16 % 4]);

                if (bar % 2 == 0)
                {
                    Stamp(b, tom, bar * 4 + 2.75f, .5f);
                    Stamp(b, tom, bar * 4 + 3.5f, .42f);
                }
                else Stamp(b, tom, bar * 4 + 1.5f, .45f);
            }
            Synth.Normalize(b, .85f);
            return Synth.Clip("mus_combat", b);
        }

        // ---- Layer 4 : boss distortion / tension ----
        public static AudioClip BossLayer()
        {
            var b = new float[LEN];

            // gritty power stack (110/165/220 -> all loop-exact over 19.2 s)
            Synth.AddSaw(b, 110f, 110f, .17f, .17f);
            Synth.AddSaw(b, 165f, 165f, .13f, .13f);
            Synth.AddSaw(b, 220f, 220f, .10f, .10f);
            Synth.AddSine(b, 55f, 55f, .22f, .22f);

            // 16th-note gate for aggression (smoothed to avoid clicks)
            int s16 = SPB / 4;
            float[] gate = { 1, 0, 1, 1, 0, 1, 0, 1, 1, 1, 0, 1, 0, 1, 1, 0 };
            float g = 0;
            for (int i = 0; i < LEN; i++)
            {
                float target = gate[(i / s16) % 16];
                g += (target - g) * .0025f;
                b[i] *= .25f + .75f * g;
            }

            // rising-tension lead: vibrato sine (loop-exact: 440*19.2 and 5*19.2 are integers)
            Synth.AddFM(b, 440f, 5f, 7f, .09f);

            Synth.SoftClip(b, 2.1f);
            Synth.LowPass(b, 5200f);
            Synth.Normalize(b, .85f);
            return Synth.Clip("mus_boss", b);
        }
    }
}
