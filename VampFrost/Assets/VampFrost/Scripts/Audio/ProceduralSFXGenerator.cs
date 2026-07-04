using System;
using System.Collections.Generic;
using UnityEngine;
using URandom = UnityEngine.Random;

namespace VampFrost
{
    // =====================================================================
    //  SYNTH - shared waveform toolkit (used by SFX, music and ambience)
    // =====================================================================
    public static class Synth
    {
        public const int RATE = 44100;

        public static float[] Buf(float sec) => new float[Mathf.CeilToInt(sec * RATE)];

        public static void AddSine(float[] b, float f0, float f1, float a0, float a1)
        {
            double ph = 0; int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                double f = f0 + (f1 - f0) * t;
                ph += 2.0 * Math.PI * f / RATE;
                b[i] += (float)Math.Sin(ph) * (a0 + (a1 - a0) * t);
            }
        }

        public static void AddSquare(float[] b, float f0, float f1, float a0, float a1)
        {
            double ph = 0; int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                double f = f0 + (f1 - f0) * t;
                ph += f / RATE; if (ph >= 1) ph -= 1;
                b[i] += (ph < .5 ? 1f : -1f) * (a0 + (a1 - a0) * t) * .55f;
            }
        }

        public static void AddTriangle(float[] b, float f0, float f1, float a0, float a1)
        {
            double ph = 0; int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                double f = f0 + (f1 - f0) * t;
                ph += f / RATE; if (ph >= 1) ph -= 1;
                b[i] += (float)(4.0 * Math.Abs(ph - .5) - 1.0) * (a0 + (a1 - a0) * t) * .8f;
            }
        }

        public static void AddSaw(float[] b, float f0, float f1, float a0, float a1)
        {
            double ph = 0; int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                double f = f0 + (f1 - f0) * t;
                ph += f / RATE; if (ph >= 1) ph -= 1;
                b[i] += (float)(2.0 * ph - 1.0) * (a0 + (a1 - a0) * t) * .5f;
            }
        }

        public static void AddNoise(float[] b, System.Random rng, float a0, float a1)
        {
            int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                float t = n <= 1 ? 0f : (float)i / (n - 1);
                b[i] += (float)(rng.NextDouble() * 2.0 - 1.0) * (a0 + (a1 - a0) * t);
            }
        }

        /// Frequency-modulated sine (vibrato / crystal shimmer).
        public static void AddFM(float[] b, float carrier, float modHz, float depthHz, float amp)
        {
            double ph = 0; int n = b.Length;
            for (int i = 0; i < n; i++)
            {
                double f = carrier + depthHz * Math.Sin(2.0 * Math.PI * modHz * i / RATE);
                ph += 2.0 * Math.PI * f / RATE;
                b[i] += (float)Math.Sin(ph) * amp;
            }
        }

        public static void LowPass(float[] b, float cutoff)
        {
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / RATE);
            float y = 0;
            for (int i = 0; i < b.Length; i++) { y += k * (b[i] - y); b[i] = y; }
        }

        public static void HighPass(float[] b, float cutoff)
        {
            float k = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / RATE);
            float y = 0;
            for (int i = 0; i < b.Length; i++) { y += k * (b[i] - y); b[i] -= y; }
        }

        public static void ExpDecay(float[] b, float rate)
        {
            for (int i = 0; i < b.Length; i++) b[i] *= Mathf.Exp(-rate * i / RATE);
        }

        public static void Attack(float[] b, float sec)
        {
            int n = Mathf.Min(b.Length, Mathf.CeilToInt(sec * RATE));
            for (int i = 0; i < n; i++) b[i] *= i / (float)n;
        }

        public static void FadeOut(float[] b, float sec)
        {
            int n = Mathf.Min(b.Length, Mathf.CeilToInt(sec * RATE));
            for (int i = 0; i < n; i++) b[b.Length - 1 - i] *= i / (float)n;
        }

        public static void Fade(float[] b, float inS, float outS) { Attack(b, inS); FadeOut(b, outS); }

        public static void Gain(float[] b, float g) { for (int i = 0; i < b.Length; i++) b[i] *= g; }

        public static void SoftClip(float[] b, float drive)
        {
            for (int i = 0; i < b.Length; i++) b[i] = (float)Math.Tanh(b[i] * drive) * .92f;
        }

        public static void Tremolo(float[] b, float hz, float depth)
        {
            for (int i = 0; i < b.Length; i++)
                b[i] *= (1f - depth) + depth * (.5f + .5f * Mathf.Sin(2f * Mathf.PI * hz * i / RATE));
        }

        /// Blend the tail into the head so looping is seamless.
        public static void Crossfade(float[] b, float sec)
        {
            int n = Mathf.Min(b.Length / 2, Mathf.CeilToInt(sec * RATE));
            for (int i = 0; i < n; i++)
            {
                float w = i / (float)n;
                int tail = b.Length - n + i;
                b[tail] = b[tail] * (1f - w) + b[i] * w;
            }
        }

        public static void AddInto(float[] dst, float[] src, int offset, float gain = 1f)
        {
            int n = Mathf.Min(src.Length, dst.Length - offset);
            for (int i = 0; i < n; i++) if (offset + i >= 0) dst[offset + i] += src[i] * gain;
        }

        public static void Normalize(float[] b, float peak = .9f)
        {
            float max = 0;
            for (int i = 0; i < b.Length; i++) { float a = Mathf.Abs(b[i]); if (a > max) max = a; }
            if (max > .0001f && max > peak) Gain(b, peak / max);
        }

        public static AudioClip Clip(string name, float[] data)
        {
            var c = AudioClip.Create(name, data.Length, 1, RATE, false);
            c.SetData(data, 0);
            return c;
        }
    }

    // =====================================================================
    //  SFX LIBRARY - every sound in the game, generated once at startup
    // =====================================================================
    public enum SfxId
    {
        Footstep, Dash, Invis, PlayerHurt, PlayerDeath,
        WeaponFire, Impact, Crit, Freeze,
        EnemyTick, Telegraph, EnemyDeath,
        BossRoar, BossPhase, BossHeavy,
        UIHover, UIClick, UIConfirm, UICancel, UIError, UIOpen, UIClose, UINotify,
        LevelUp, Chest, Coin, XP, Heal,
        WaveSting, GameOver, Victory
    }

    public static class Sfx
    {
        static readonly Dictionary<SfxId, AudioClip[]> map = new();
        public static bool Ready { get; private set; }

        public static AudioClip Get(SfxId id)
        {
            if (!Ready) Init();
            var arr = map[id];
            return arr[arr.Length == 1 ? 0 : URandom.Range(0, arr.Length)];
        }

        public static void Init()
        {
            if (Ready) return;
            var rng = new System.Random(1337);

            map[SfxId.Footstep] = new[] { Footstep(rng, 620f), Footstep(rng, 540f) };
            map[SfxId.Dash] = new[] { Dash(rng) };
            map[SfxId.Invis] = new[] { Invis() };
            map[SfxId.PlayerHurt] = new[] { Hurt(rng) };
            map[SfxId.PlayerDeath] = new[] { PlayerDeath(rng) };
            map[SfxId.WeaponFire] = new[] { Fire(rng, 520f), Fire(rng, 470f) };
            map[SfxId.Impact] = new[] { Impact(rng, 1900f), Impact(rng, 1500f) };
            map[SfxId.Crit] = new[] { Crit() };
            map[SfxId.Freeze] = new[] { Freeze() };
            map[SfxId.EnemyTick] = new[] { Tick(rng) };
            map[SfxId.Telegraph] = new[] { Telegraph() };
            map[SfxId.EnemyDeath] = new[] { EDeath(rng, 1200f), EDeath(rng, 900f), EDeath(rng, 1500f) };
            map[SfxId.BossRoar] = new[] { Roar(rng) };
            map[SfxId.BossPhase] = new[] { PhaseSweep() };
            map[SfxId.BossHeavy] = new[] { Heavy(rng) };
            map[SfxId.UIHover] = new[] { Blip(850f, .035f, .22f) };
            map[SfxId.UIClick] = new[] { ClickTone() };
            map[SfxId.UIConfirm] = new[] { TwoTone(700f, 1050f) };
            map[SfxId.UICancel] = new[] { Slide(520f, 330f, .11f, .3f) };
            map[SfxId.UIError] = new[] { ErrorBuzz() };
            map[SfxId.UIOpen] = new[] { Slide(420f, 860f, .15f, .3f) };
            map[SfxId.UIClose] = new[] { Slide(860f, 420f, .15f, .3f) };
            map[SfxId.UINotify] = new[] { Notify() };
            map[SfxId.LevelUp] = new[] { LevelUpChord() };
            map[SfxId.Chest] = new[] { ChestChime() };
            map[SfxId.Coin] = new[] { CoinBlip() };
            map[SfxId.XP] = new[] { Blip(1250f, .045f, .14f) };
            map[SfxId.Heal] = new[] { HealTone() };
            map[SfxId.WaveSting] = new[] { WaveSting(rng) };
            map[SfxId.GameOver] = new[] { GameOverSting(rng) };
            map[SfxId.Victory] = new[] { VictoryChord() };

            Ready = true;
            Debug.Log("[VampFrost] Procedural SFX generated: " + map.Count + " types.");
        }

        // ---------------- generators ----------------
        static AudioClip Footstep(System.Random rng, float cut)
        {
            var b = Synth.Buf(.07f);
            Synth.AddNoise(b, rng, .5f, .1f);
            Synth.LowPass(b, cut);
            Synth.ExpDecay(b, 55f);
            Synth.Fade(b, .004f, .02f);
            return Synth.Clip("sfx_step", b);
        }

        static AudioClip Dash(System.Random rng)
        {
            var b = Synth.Buf(.28f);
            Synth.AddSine(b, 180f, 950f, .45f, .12f);
            Synth.AddNoise(b, rng, .18f, .0f);
            Synth.HighPass(b, 300f);
            Synth.Fade(b, .01f, .08f);
            return Synth.Clip("sfx_dash", b);
        }

        static AudioClip Invis()
        {
            var b = Synth.Buf(.6f);
            Synth.AddSine(b, 72f, 72f, .0f, .5f);
            Synth.AddSine(b, 144f, 144f, .0f, .18f);
            Synth.Tremolo(b, 8f, .55f);
            Synth.FadeOut(b, .25f);
            return Synth.Clip("sfx_invis", b);
        }

        static AudioClip Hurt(System.Random rng)
        {
            var b = Synth.Buf(.16f);
            Synth.AddNoise(b, rng, .7f, .1f);
            Synth.LowPass(b, 2600f);
            Synth.AddSquare(b, 420f, 150f, .35f, .05f);
            Synth.ExpDecay(b, 22f);
            Synth.Fade(b, .002f, .04f);
            return Synth.Clip("sfx_hurt", b);
        }

        static AudioClip PlayerDeath(System.Random rng)
        {
            var b = Synth.Buf(1.1f);
            Synth.AddSine(b, 210f, 38f, .6f, .3f);
            Synth.AddNoise(b, rng, .3f, .0f);
            Synth.LowPass(b, 700f);
            Synth.ExpDecay(b, 2.6f);
            Synth.Fade(b, .005f, .3f);
            Synth.SoftClip(b, 1.4f);
            return Synth.Clip("sfx_pdeath", b);
        }

        static AudioClip Fire(System.Random rng, float f)
        {
            var b = Synth.Buf(.09f);
            Synth.AddSquare(b, f, f * .7f, .38f, .05f);
            Synth.AddNoise(b, rng, .14f, 0f);
            Synth.ExpDecay(b, 42f);
            Synth.Fade(b, .002f, .02f);
            return Synth.Clip("sfx_fire", b);
        }

        static AudioClip Impact(System.Random rng, float cut)
        {
            var b = Synth.Buf(.09f);
            Synth.AddNoise(b, rng, .6f, .05f);
            Synth.LowPass(b, cut);
            Synth.AddSine(b, 230f, 90f, .4f, .05f);
            Synth.ExpDecay(b, 38f);
            Synth.Fade(b, .001f, .02f);
            return Synth.Clip("sfx_impact", b);
        }

        static AudioClip Crit()
        {
            var b = Synth.Buf(.13f);
            Synth.AddSine(b, 1900f, 2350f, .45f, .05f);
            Synth.AddSine(b, 2850f, 2850f, .18f, 0f);
            Synth.ExpDecay(b, 26f);
            Synth.Fade(b, .001f, .03f);
            return Synth.Clip("sfx_crit", b);
        }

        static AudioClip Freeze()
        {
            var b = Synth.Buf(.38f);
            Synth.AddFM(b, 1250f, 28f, 320f, .32f);
            Synth.AddSine(b, 2400f, 3100f, .12f, 0f);
            Synth.ExpDecay(b, 8f);
            Synth.Fade(b, .004f, .1f);
            return Synth.Clip("sfx_freeze", b);
        }

        static AudioClip Tick(System.Random rng)
        {
            var b = Synth.Buf(.05f);
            Synth.AddNoise(b, rng, .28f, .02f);
            Synth.LowPass(b, 480f);
            Synth.ExpDecay(b, 60f);
            return Synth.Clip("sfx_tick", b);
        }

        static AudioClip Telegraph()
        {
            var b = Synth.Buf(.42f);
            Synth.AddSine(b, 290f, 720f, .05f, .4f);
            Synth.AddSquare(b, 290f, 720f, .02f, .12f);
            Synth.Fade(b, .05f, .03f);
            return Synth.Clip("sfx_telegraph", b);
        }

        static AudioClip EDeath(System.Random rng, float cut)
        {
            var b = Synth.Buf(.24f);
            Synth.AddNoise(b, rng, .5f, .02f);
            Synth.LowPass(b, cut);
            Synth.AddSine(b, 300f, 70f, .3f, .02f);
            Synth.ExpDecay(b, 14f);
            Synth.Fade(b, .002f, .08f);
            return Synth.Clip("sfx_edeath", b);
        }

        static AudioClip Roar(System.Random rng)
        {
            var b = Synth.Buf(1.4f);
            Synth.AddSaw(b, 55f, 48f, .45f, .3f);
            Synth.AddSaw(b, 82f, 74f, .35f, .2f);
            Synth.AddSine(b, 41f, 38f, .35f, .2f);
            Synth.AddNoise(b, rng, .12f, .04f);
            Synth.Tremolo(b, 5.2f, .4f);
            Synth.LowPass(b, 900f);
            Synth.SoftClip(b, 2.2f);
            Synth.Fade(b, .05f, .35f);
            return Synth.Clip("sfx_roar", b);
        }

        static AudioClip PhaseSweep()
        {
            var b = Synth.Buf(1.0f);
            Synth.AddSine(b, 190f, 1450f, .4f, .5f);
            Synth.AddSaw(b, 95f, 720f, .22f, .3f);
            Synth.HighPass(b, 140f);
            Synth.SoftClip(b, 3f);
            Synth.Fade(b, .04f, .08f);
            return Synth.Clip("sfx_phase", b);
        }

        static AudioClip Heavy(System.Random rng)
        {
            var b = Synth.Buf(.5f);
            Synth.AddSine(b, 62f, 36f, .95f, .3f);
            Synth.AddSine(b, 124f, 70f, .3f, .05f);
            var click = Synth.Buf(.02f);
            Synth.AddNoise(click, rng, .6f, .1f);
            Synth.AddInto(b, click, 0);
            Synth.ExpDecay(b, 6f);
            Synth.SoftClip(b, 1.9f);
            Synth.Fade(b, .001f, .1f);
            return Synth.Clip("sfx_heavy", b);
        }

        static AudioClip Blip(float f, float len, float amp)
        {
            var b = Synth.Buf(len);
            Synth.AddSine(b, f, f, amp, amp * .3f);
            Synth.ExpDecay(b, 34f);
            Synth.Fade(b, .002f, .01f);
            return Synth.Clip("sfx_blip", b);
        }

        static AudioClip ClickTone()
        {
            var b = Synth.Buf(.06f);
            Synth.AddSquare(b, 1100f, 900f, .32f, .05f);
            Synth.ExpDecay(b, 46f);
            Synth.Fade(b, .001f, .015f);
            return Synth.Clip("sfx_click", b);
        }

        static AudioClip TwoTone(float f1, float f2)
        {
            var b = Synth.Buf(.20f);
            var a = Synth.Buf(.08f); Synth.AddSine(a, f1, f1, .3f, .1f); Synth.ExpDecay(a, 20f);
            var c = Synth.Buf(.12f); Synth.AddSine(c, f2, f2, .32f, .05f); Synth.ExpDecay(c, 16f);
            Synth.AddInto(b, a, 0);
            Synth.AddInto(b, c, Mathf.RoundToInt(.07f * Synth.RATE));
            Synth.Fade(b, .002f, .03f);
            return Synth.Clip("sfx_confirm", b);
        }

        static AudioClip Slide(float f0, float f1, float len, float amp)
        {
            var b = Synth.Buf(len);
            Synth.AddSine(b, f0, f1, amp, amp * .5f);
            Synth.Fade(b, .01f, .04f);
            return Synth.Clip("sfx_slide", b);
        }

        static AudioClip ErrorBuzz()
        {
            var b = Synth.Buf(.22f);
            var p = Synth.Buf(.07f); Synth.AddSquare(p, 190f, 185f, .4f, .3f);
            Synth.AddInto(b, p, 0);
            Synth.AddInto(b, p, Mathf.RoundToInt(.11f * Synth.RATE));
            Synth.Fade(b, .002f, .03f);
            return Synth.Clip("sfx_error", b);
        }

        static AudioClip Notify()
        {
            var b = Synth.Buf(.22f);
            Synth.AddSine(b, 980f, 980f, .28f, .05f);
            Synth.AddSine(b, 1470f, 1470f, .12f, .02f);
            Synth.ExpDecay(b, 12f);
            Synth.Fade(b, .003f, .05f);
            return Synth.Clip("sfx_notify", b);
        }

        static AudioClip LevelUpChord()
        {
            float[] notes = { 440f, 523.25f, 659.25f, 880f }; // A minor rising
            var b = Synth.Buf(.55f);
            for (int i = 0; i < notes.Length; i++)
            {
                var n = Synth.Buf(.22f);
                Synth.AddSine(n, notes[i], notes[i], .3f, .05f);
                Synth.AddSquare(n, notes[i], notes[i], .06f, .01f);
                Synth.ExpDecay(n, 9f);
                Synth.AddInto(b, n, Mathf.RoundToInt(i * .1f * Synth.RATE));
            }
            Synth.Fade(b, .003f, .12f);
            return Synth.Clip("sfx_levelup", b);
        }

        static AudioClip ChestChime()
        {
            float[] notes = { 660f, 880f, 1320f, 1760f };
            var b = Synth.Buf(.7f);
            for (int i = 0; i < notes.Length; i++)
            {
                var n = Synth.Buf(.4f);
                Synth.AddSine(n, notes[i], notes[i] * 1.003f, .26f, .02f);
                Synth.ExpDecay(n, 6f);
                Synth.AddInto(b, n, Mathf.RoundToInt(i * .07f * Synth.RATE));
            }
            Synth.Fade(b, .003f, .18f);
            return Synth.Clip("sfx_chest", b);
        }

        static AudioClip CoinBlip()
        {
            var b = Synth.Buf(.09f);
            Synth.AddSine(b, 1350f, 1900f, .28f, .05f);
            Synth.AddSine(b, 2100f, 2100f, .1f, 0f);
            Synth.ExpDecay(b, 22f);
            Synth.Fade(b, .001f, .02f);
            return Synth.Clip("sfx_coin", b);
        }

        static AudioClip HealTone()
        {
            var b = Synth.Buf(.32f);
            Synth.AddSine(b, 520f, 790f, .28f, .05f);
            Synth.AddTriangle(b, 660f, 660f, .14f, .02f);
            Synth.Fade(b, .02f, .1f);
            return Synth.Clip("sfx_heal", b);
        }

        static AudioClip WaveSting(System.Random rng)
        {
            var b = Synth.Buf(.55f);
            var hit = Synth.Buf(.3f);
            Synth.AddSaw(hit, 110f, 100f, .4f, .1f);
            Synth.AddSine(hit, 55f, 52f, .35f, .1f);
            Synth.ExpDecay(hit, 9f);
            Synth.AddInto(b, hit, 0);
            Synth.AddInto(b, hit, Mathf.RoundToInt(.18f * Synth.RATE), .8f);
            Synth.LowPass(b, 1400f);
            Synth.SoftClip(b, 1.6f);
            Synth.Fade(b, .003f, .12f);
            return Synth.Clip("sfx_wave", b);
        }

        static AudioClip GameOverSting(System.Random rng)
        {
            var b = Synth.Buf(1.7f);
            Synth.AddSine(b, 220f, 52f, .5f, .2f);
            Synth.AddSaw(b, 110f, 48f, .25f, .1f);
            Synth.AddNoise(b, rng, .08f, 0f);
            Synth.LowPass(b, 800f);
            Synth.SoftClip(b, 1.8f);
            Synth.ExpDecay(b, 1.6f);
            Synth.Fade(b, .01f, .5f);
            return Synth.Clip("sfx_gameover", b);
        }

        static AudioClip VictoryChord()
        {
            float[] notes = { 440f, 554.37f, 659.25f, 880f }; // A major bloom
            var b = Synth.Buf(1.3f);
            for (int i = 0; i < notes.Length; i++)
            {
                var n = Synth.Buf(1.0f);
                Synth.AddSine(n, notes[i], notes[i], .22f, .04f);
                Synth.AddTriangle(n, notes[i] * 2f, notes[i] * 2f, .05f, .01f);
                Synth.ExpDecay(n, 2.6f);
                Synth.AddInto(b, n, Mathf.RoundToInt(i * .09f * Synth.RATE));
            }
            Synth.Fade(b, .01f, .4f);
            return Synth.Clip("sfx_victory", b);
        }
    }
}
