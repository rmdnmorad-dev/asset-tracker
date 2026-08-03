using System.Collections.Generic;
using UnityEngine;

namespace TPBR
{
    public enum Sfx
    {
        UiHover, UiClick, UiBack, Lock,
        Hop, Incoming, Impact, Death, Crumble,
        Lava, Dogpile, Shield, Decoy, Scout,
        Tick, RoundStart, Win, Lose
    }

    /// Every sound in the game is synthesised into PCM at boot. No audio files,
    /// no licensing questions, no import settings - and it means the whole project
    /// stays asset-free.
    public class Audio : MonoBehaviour
    {
        public static Audio I;
        public static float MasterVolume = 0.8f;
        public static float MusicVolume = 0.45f;
        public static bool Muted;

        const int SR = 44100;

        readonly Dictionary<Sfx, AudioClip> clips = new Dictionary<Sfx, AudioClip>();
        AudioSource[] pool;
        AudioSource musicCalm, musicTense, ambience;
        int next;
        float intensity, intensityTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { I = null; }

        public static void Init(Transform parent = null)
        {
            if (I != null) return;
            var go = new GameObject("Audio");
            if (parent != null) go.transform.SetParent(parent, false);
            I = go.AddComponent<Audio>();
            I.Build();
        }

        // ------------------------------------------------------------------ api

        public static void Play(Sfx id, float pitch = 1f, float vol = 1f)
        {
            if (I == null || Muted) return;
            I.PlayInternal(id, pitch, vol);
        }

        /// 0 = calm prep bed, 1 = tense commit-countdown bed. Crossfaded, and the
        /// two loops are the same length so they stay sample-aligned forever.
        public static void SetIntensity(float t)
        {
            if (I == null) return;
            I.intensityTarget = Mathf.Clamp01(t);
        }

        void PlayInternal(Sfx id, float pitch, float vol)
        {
            AudioClip c;
            if (!clips.TryGetValue(id, out c) || c == null) return;
            var src = pool[next++ % pool.Length];
            src.clip = c;
            src.pitch = Mathf.Clamp(pitch, 0.25f, 3f);
            src.volume = Mathf.Clamp01(vol) * MasterVolume;
            src.Play();
        }

        void Update()
        {
            intensity = Mathf.MoveTowards(intensity, intensityTarget, Time.unscaledDeltaTime * 0.8f);
            float m = Muted ? 0f : MusicVolume * MasterVolume;
            if (musicCalm != null) musicCalm.volume = (1f - intensity) * m;
            if (musicTense != null) musicTense.volume = intensity * m;
            if (ambience != null) ambience.volume = (Muted ? 0f : 0.18f) * MasterVolume;
        }

        // ---------------------------------------------------------------- build

        void Build()
        {
            pool = new AudioSource[7];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("sfx" + i);
                go.transform.SetParent(transform, false);
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.spatialBlend = 0f;
                pool[i] = a;
            }

            clips[Sfx.UiHover]    = Blip(1180f, 0.035f, 0.18f, Wave.Sine);
            clips[Sfx.UiClick]    = Blip(760f, 0.055f, 0.42f, Wave.Square);
            clips[Sfx.UiBack]     = Sweep(620f, 380f, 0.09f, 0.38f, Wave.Square);
            clips[Sfx.Lock]       = Chord(new float[] { 392f, 523f, 784f }, 0.30f, 0.42f);
            clips[Sfx.Hop]        = Sweep(300f, 620f, 0.10f, 0.22f, Wave.Sine);
            clips[Sfx.Incoming]   = Sweep(240f, 900f, 0.34f, 0.26f, Wave.Saw);
            clips[Sfx.Impact]     = Boom();
            clips[Sfx.Death]      = DeathSting();
            clips[Sfx.Crumble]    = Rubble();
            clips[Sfx.Lava]       = Rumble();
            clips[Sfx.Dogpile]    = Alarm();
            clips[Sfx.Shield]     = Sweep(520f, 1180f, 0.28f, 0.4f, Wave.Sine);
            clips[Sfx.Decoy]      = Sweep(880f, 330f, 0.26f, 0.36f, Wave.Square);
            clips[Sfx.Scout]      = Chord(new float[] { 880f, 1320f }, 0.22f, 0.3f);
            clips[Sfx.Tick]       = Blip(1560f, 0.028f, 0.3f, Wave.Square);
            clips[Sfx.RoundStart] = Chord(new float[] { 261f, 329f, 392f }, 0.5f, 0.36f);
            clips[Sfx.Win]        = Fanfare(true);
            clips[Sfx.Lose]       = Fanfare(false);

            musicCalm  = MusicSource("MusicCalm", MusicLoop(false));
            musicTense = MusicSource("MusicTense", MusicLoop(true));
            ambience   = MusicSource("Ambience", AmbienceLoop());

            // started on the same frame so the two music beds stay phase-locked
            musicCalm.Play();
            musicTense.Play();
            ambience.Play();
        }

        AudioSource MusicSource(string name, AudioClip clip)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.clip = clip;
            a.loop = true;
            a.playOnAwake = false;
            a.volume = 0f;
            a.spatialBlend = 0f;
            return a;
        }

        // --------------------------------------------------------------- synth

        enum Wave { Sine, Square, Saw, Noise }

        static float Osc(Wave w, float phase, ref uint seed)
        {
            switch (w)
            {
                case Wave.Square: return Mathf.Sin(phase) >= 0f ? 1f : -1f;
                case Wave.Saw:    return (phase / (2f * Mathf.PI)) % 1f * 2f - 1f;
                case Wave.Noise:  return Rand(ref seed) * 2f - 1f;
                default:          return Mathf.Sin(phase);
            }
        }

        /// Deterministic PRNG so the sound set is identical every run.
        static float Rand(ref uint s)
        {
            s = s * 1664525u + 1013904223u;
            return ((s >> 8) & 0xFFFFFF) / (float)0x1000000;
        }

        static AudioClip Ship(string name, float[] d)
        {
            for (int i = 0; i < d.Length; i++) d[i] = Mathf.Clamp(d[i], -1f, 1f);
            var c = AudioClip.Create(name, d.Length, 1, SR, false);
            c.SetData(d, 0);
            return c;
        }

        static float[] Buf(float seconds) { return new float[Mathf.Max(16, (int)(seconds * SR))]; }

        /// Percussive envelope: quick attack, exponential tail.
        static float Env(int i, int n, float attack)
        {
            float t = i / (float)n;
            float a = attack <= 0f ? 1f : Mathf.Clamp01(t / attack);
            return a * Mathf.Exp(-4.2f * t);
        }

        static AudioClip Blip(float freq, float dur, float amp, Wave w)
        {
            var d = Buf(dur);
            uint s = 7u;
            for (int i = 0; i < d.Length; i++)
                d[i] = Osc(w, 2f * Mathf.PI * freq * i / SR, ref s) * Env(i, d.Length, 0.06f) * amp;
            return Ship("blip" + freq, d);
        }

        static AudioClip Sweep(float f0, float f1, float dur, float amp, Wave w)
        {
            var d = Buf(dur);
            uint s = 11u;
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = Mathf.Lerp(f0, f1, t * t);
                phase += 2f * Mathf.PI * f / SR;
                d[i] = Osc(w, phase, ref s) * Env(i, d.Length, 0.08f) * amp;
            }
            return Ship("sweep", d);
        }

        static AudioClip Chord(float[] freqs, float dur, float amp)
        {
            var d = Buf(dur);
            for (int i = 0; i < d.Length; i++)
            {
                float v = 0f;
                for (int k = 0; k < freqs.Length; k++)
                {
                    // stagger the entries so it reads as a roll, not a stab
                    float t = i / (float)SR - k * 0.05f;
                    if (t <= 0f) continue;
                    v += Mathf.Sin(2f * Mathf.PI * freqs[k] * t) * Mathf.Exp(-3.4f * t);
                }
                d[i] = v / freqs.Length * amp * Mathf.Clamp01(i / (SR * 0.004f));
            }
            return Ship("chord", d);
        }

        static AudioClip Boom()
        {
            var d = Buf(0.55f);
            uint s = 17u;
            float phase = 0f, lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = Mathf.Lerp(190f, 38f, Mathf.Sqrt(t));
                phase += 2f * Mathf.PI * f / SR;
                float body = Mathf.Sin(phase) * Mathf.Exp(-3.6f * t);
                float n = Rand(ref s) * 2f - 1f;
                lp += (n - lp) * 0.09f;                       // low-passed crack
                d[i] = (body * 0.85f + lp * Mathf.Exp(-16f * t) * 0.7f) * 0.85f;
            }
            return Ship("boom", d);
        }

        static AudioClip DeathSting()
        {
            var d = Buf(0.8f);
            uint s = 23u;
            float phase = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float f = Mathf.Lerp(520f, 62f, t * t);
                phase += 2f * Mathf.PI * f / SR;
                float saw = (phase / (2f * Mathf.PI)) % 1f * 2f - 1f;
                float n = (Rand(ref s) * 2f - 1f) * Mathf.Exp(-11f * t) * 0.35f;
                d[i] = (saw * 0.55f + n) * Mathf.Exp(-3.1f * t) * 0.6f;
            }
            return Ship("death", d);
        }

        static AudioClip Rubble()
        {
            var d = Buf(0.5f);
            uint s = 29u;
            float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float n = Rand(ref s) * 2f - 1f;
                lp += (n - lp) * 0.22f;
                // a few discrete knocks on top of the gravel
                float knock = 0f;
                for (int k = 0; k < 4; k++)
                {
                    float kt = t - k * 0.09f;
                    if (kt > 0f) knock += Mathf.Sin(2f * Mathf.PI * (120f - k * 14f) * kt) * Mathf.Exp(-42f * kt);
                }
                d[i] = (lp * 0.5f + knock * 0.35f) * Mathf.Exp(-3.4f * t) * 0.8f;
            }
            return Ship("rubble", d);
        }

        static AudioClip Rumble()
        {
            var d = Buf(1.4f);
            uint s = 31u;
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)d.Length;
                float n = Rand(ref s) * 2f - 1f;
                lp += (n - lp) * 0.035f;
                lp2 += (lp - lp2) * 0.035f;
                float swell = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t));
                d[i] = lp2 * 5.5f * swell;
            }
            return Ship("rumble", d);
        }

        static AudioClip Alarm()
        {
            var d = Buf(0.62f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float f = (((int)(t / 0.09f)) % 2 == 0) ? 740f : 560f;
                float sq = Mathf.Sin(2f * Mathf.PI * f * t) >= 0f ? 1f : -1f;
                d[i] = sq * 0.32f * Mathf.Exp(-2.1f * (i / (float)d.Length)) *
                       Mathf.Clamp01(i / (SR * 0.004f));
            }
            return Ship("alarm", d);
        }

        static AudioClip Fanfare(bool major)
        {
            float[] notes = major
                ? new float[] { 392f, 523f, 659f, 784f }
                : new float[] { 440f, 392f, 330f, 262f };
            var d = Buf(1.5f);
            for (int i = 0; i < d.Length; i++)
            {
                float t = i / (float)SR;
                float v = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float nt = t - k * 0.16f;
                    if (nt <= 0f) continue;
                    v += (Mathf.Sin(2f * Mathf.PI * notes[k] * nt) * 0.6f
                       +  Mathf.Sin(4f * Mathf.PI * notes[k] * nt) * 0.2f) * Mathf.Exp(-2.2f * nt);
                }
                d[i] = v * 0.3f;
            }
            return Ship(major ? "win" : "lose", d);
        }

        // ---------------------------------------------------------------- music

        const float LoopSeconds = 7.2f;   // 4 bars at 100bpm-ish

        /// Both beds share this progression so the crossfade is musical, not a cut.
        static readonly float[] Roots = { 110.00f, 87.31f, 130.81f, 98.00f };  // Am F C G

        static AudioClip MusicLoop(bool tense)
        {
            var d = Buf(LoopSeconds);
            int n = d.Length;
            float bar = LoopSeconds / 4f;
            uint s = tense ? 101u : 97u;
            float lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SR;
                int barIdx = Mathf.Min(3, (int)(t / bar));
                float root = Roots[barIdx];
                float inBar = t - barIdx * bar;

                // sustained pad: root + fifth + octave, gently detuned
                float pad = Mathf.Sin(2f * Mathf.PI * root * t) * 0.5f
                          + Mathf.Sin(2f * Mathf.PI * root * 1.4983f * t) * 0.26f
                          + Mathf.Sin(2f * Mathf.PI * root * 2.0f * t + 0.4f) * 0.18f;
                pad *= 0.5f + 0.5f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(inBar / bar));

                float v = pad * 0.30f;

                if (tense)
                {
                    // pulse on eighths + a nervous arp an octave up
                    float beat = 0.45f;
                    float bt = inBar % beat;
                    float kick = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(120f, 45f, bt / beat) * bt)
                               * Mathf.Exp(-24f * bt) * 0.55f;

                    int step = (int)(inBar / (beat * 0.5f));
                    float arpF = root * 4f * (step % 3 == 0 ? 1f : (step % 3 == 1 ? 1.2f : 1.5f));
                    float at = inBar % (beat * 0.5f);
                    float arp = Mathf.Sin(2f * Mathf.PI * arpF * at) * Mathf.Exp(-13f * at) * 0.16f;

                    v += kick + arp;
                }
                else
                {
                    // soft noise breath under the pad
                    float nz = Rand(ref s) * 2f - 1f;
                    lp += (nz - lp) * 0.012f;
                    v += lp * 0.5f;
                }

                // equal-power fade across the loop seam
                float edge = 0.06f * SR;
                if (i < edge) v *= i / edge;
                else if (i > n - edge) v *= (n - i) / edge;

                d[i] = v * 0.62f;
            }
            return Ship(tense ? "musicTense" : "musicCalm", d);
        }

        static AudioClip AmbienceLoop()
        {
            var d = Buf(5f);
            int n = d.Length;
            uint s = 61u;
            float lp = 0f, lp2 = 0f;
            for (int i = 0; i < n; i++)
            {
                float nz = Rand(ref s) * 2f - 1f;
                lp += (nz - lp) * 0.02f;
                lp2 += (lp - lp2) * 0.02f;
                float v = lp2 * 4.5f;
                float edge = 0.25f * SR;
                if (i < edge) v *= i / edge;
                else if (i > n - edge) v *= (n - i) / edge;
                d[i] = v;
            }
            return Ship("ambience", d);
        }
    }
}
