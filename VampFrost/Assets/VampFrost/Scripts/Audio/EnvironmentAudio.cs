using System.Collections.Generic;
using UnityEngine;

namespace VampFrost
{
    /// Seamless 16-second ambience loops, one per map theme. Fully synthesized.
    public static class EnvironmentAudio
    {
        const float LEN = 16f;
        static readonly Dictionary<AmbientType, AudioClip> cache = new();

        public static AudioClip Get(AmbientType a)
        {
            if (cache.TryGetValue(a, out var c)) return c;

            var b = Synth.Buf(LEN);
            var rng = new System.Random((int)a * 991 + 7);

            switch (a)
            {
                case AmbientType.Graveyard:
                    Wind(b, rng, .55f, 300f, 2f / LEN);
                    Ping(b, 2.6f, 659.25f, .05f, 1.1f);
                    Ping(b, 6.9f, 440f, .045f, 1.4f);
                    Ping(b, 10.3f, 554.37f, .04f, 1.2f);
                    Ping(b, 13.0f, 493.88f, .035f, 1.5f);
                    break;

                case AmbientType.Village:
                    Wind(b, rng, .45f, 260f, 3f / LEN);
                    Creak(b, rng, 1.8f); Creak(b, rng, 5.4f);
                    Creak(b, rng, 8.9f); Creak(b, rng, 12.6f);
                    break;

                case AmbientType.Forest:
                    Wind(b, rng, .35f, 240f, 2f / LEN);
                    for (int i = 0; i < 18; i++)
                        Chirp(b, .5f + (float)rng.NextDouble() * 14.5f,
                              3600f + (float)rng.NextDouble() * 1600f, .035f);
                    break;

                case AmbientType.Ruins:
                    Synth.AddSine(b, 55f, 55f, .05f, .05f);
                    Synth.AddSine(b, 82.5f, 82.5f, .03f, .03f);
                    PingVerb(b, 2.2f, 220f, .07f);
                    PingVerb(b, 6.4f, 329.63f, .06f);
                    PingVerb(b, 10.6f, 261.63f, .06f);
                    PingVerb(b, 13.6f, 174.61f, .05f);
                    break;

                case AmbientType.City:
                    Synth.AddSine(b, 55f, 55f, .055f, .055f);
                    Synth.AddSine(b, 110f, 110f, .04f, .04f);
                    Synth.AddSine(b, 165f, 165f, .022f, .022f);
                    Hiss(b, rng, .16f, 900f, 3f / LEN);
                    Ping(b, 3.4f, 70f, .06f, 2.8f);
                    Ping(b, 9.8f, 62f, .05f, 3.0f);
                    break;

                case AmbientType.Desert:
                    Hiss(b, rng, .45f, 700f, 2f / LEN);
                    Hiss(b, rng, .2f, 1600f, 5f / LEN);
                    break;
            }

            Synth.Crossfade(b, 1.2f);
            Synth.Tremolo(b, 1f / LEN, .12f); // very slow breathing, loop-exact
            Synth.Normalize(b, .65f);
            c = Synth.Clip("amb_" + a, b);
            cache[a] = c;
            return c;
        }

        // ---------------- builders ----------------
        /// Brown-ish wind: leaky-integrated noise, low-passed, with gusts.
        static void Wind(float[] b, System.Random rng, float amp, float cutoff, float gustHz)
        {
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Synth.RATE);
            float y = 0, lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                y = y * .996f + (float)(rng.NextDouble() * 2.0 - 1.0) * .25f;
                lp += k * (y - lp);
                float gust = .62f + .38f * Mathf.Sin(2f * Mathf.PI * gustHz * i / Synth.RATE + 1.3f);
                b[i] += lp * amp * gust;
            }
        }

        /// Dry high hiss (desert / traffic bed): high-passed white noise with gusts.
        static void Hiss(float[] b, System.Random rng, float amp, float hpCut, float gustHz)
        {
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * hpCut / Synth.RATE);
            float lp = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float x = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp += k * (x - lp);
                float hp = x - lp;
                float gust = .55f + .45f * Mathf.Sin(2f * Mathf.PI * gustHz * i / Synth.RATE + .4f);
                b[i] += hp * amp * gust * .5f;
            }
        }

        static void Ping(float[] b, float atSec, float freq, float amp, float decay)
        {
            int off = Mathf.RoundToInt(atSec * Synth.RATE);
            int len = Mathf.Min(b.Length - off, Mathf.RoundToInt(2.6f * Synth.RATE));
            double ph = 0;
            for (int j = 0; j < len; j++)
            {
                ph += 2.0 * System.Math.PI * freq / Synth.RATE;
                b[off + j] += (float)System.Math.Sin(ph) * amp * Mathf.Exp(-decay * j / Synth.RATE);
            }
        }

        /// Ping with fake reverb taps.
        static void PingVerb(float[] b, float atSec, float freq, float amp)
        {
            Ping(b, atSec, freq, amp, 2.2f);
            Ping(b, atSec + .14f, freq, amp * .55f, 2.2f);
            Ping(b, atSec + .30f, freq * .999f, amp * .3f, 2.0f);
            Ping(b, atSec + .47f, freq * 1.002f, amp * .17f, 1.8f);
        }

        static void Creak(float[] b, System.Random rng, float atSec)
        {
            int off = Mathf.RoundToInt(atSec * Synth.RATE);
            int len = Mathf.RoundToInt(.2f * Synth.RATE);
            if (off + len >= b.Length) return;
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * 800f / Synth.RATE);
            float lp = 0; double ph = 0;
            for (int j = 0; j < len; j++)
            {
                float t = j / (float)len;
                float f = Mathf.Lerp(260f, 150f, t);
                ph += 2.0 * System.Math.PI * f / Synth.RATE;
                float x = (float)System.Math.Sin(ph) * .5f
                        + (float)(rng.NextDouble() * 2.0 - 1.0) * .4f;
                lp += k * (x - lp);
                b[off + j] += lp * .09f * Mathf.Sin(t * Mathf.PI);
            }
        }

        static void Chirp(float[] b, float atSec, float freq, float amp)
        {
            for (int p = 0; p < 2; p++)
            {
                int off = Mathf.RoundToInt((atSec + p * .05f) * Synth.RATE);
                int len = Mathf.RoundToInt(.025f * Synth.RATE);
                if (off + len >= b.Length) return;
                double ph = 0;
                for (int j = 0; j < len; j++)
                {
                    ph += 2.0 * System.Math.PI * freq / Synth.RATE;
                    float env = Mathf.Sin(j / (float)len * Mathf.PI);
                    b[off + j] += (float)System.Math.Sin(ph) * amp * env;
                }
            }
        }
    }
}
