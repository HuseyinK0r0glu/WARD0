using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Procedural, deterministic multi-layer footsteps (heel then toe). Metal = modal plate ring + sharp heel click + body thump;
    /// gravel = thump + granular crunch; puddle = splash + bubbles. Every clip is seeded by (surface, variant) so output is reproducible.
    /// Generation is allocation-free per sample (one float[] per clip); use <see cref="PrewarmRoutine"/> to spread it over loading frames.
    /// </summary>
    public static class FootstepSynth
    {
        /// <summary>Sample rate of the generated clips.</summary>
        public const int Rate = 32000;
        /// <summary>Variants generated per surface and stance.</summary>
        public const int VariantCount = 5;
        const int SurfaceCount = 3, StanceCount = 3;
        const float TwoPi = 6.2831853f;

        static readonly AudioClip[] Clips = new AudioClip[SurfaceCount * VariantCount * StanceCount];

        struct Rng
        {
            uint _s;
            public Rng(uint seed) { _s = seed * 2654435761u + 0x9E3779B9u; if (_s == 0) _s = 1; }
            public float Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / 16777216f; }
            public float Signed() => Next() * 2f - 1f;
            public float Range(float a, float b) => a + (b - a) * Next();
        }

        struct StanceParams { public float heel, toe, gap, bright, ring, peak, lpMetal, lpGravel, lpPuddle; }

        static StanceParams Params(FootstepStance s)
        {
            switch (s)
            {
                case FootstepStance.Sprint: return new StanceParams { heel = 1.3f, toe = 0.78f, gap = 0.062f, bright = 1f, ring = 1.25f, peak = 1f, lpMetal = 0f, lpGravel = 0f, lpPuddle = 0f };
                case FootstepStance.Crouch: return new StanceParams { heel = 0.8f, toe = 0.6f, gap = 0.115f, bright = 0.05f, ring = 0.6f, peak = 0.6f, lpMetal = 1100f, lpGravel = 2200f, lpPuddle = 1600f };
                default: return new StanceParams { heel = 1f, toe = 0.65f, gap = 0.09f, bright = 0.5f, ring = 1f, peak = 0.8f, lpMetal = 0f, lpGravel = 0f, lpPuddle = 0f };
            }
        }

        // Inharmonic plate modes (Hz), relative amplitudes and decay time constants (s).
        static readonly float[] ModeHz = { 310f, 520f, 870f, 1340f, 1910f, 2650f, 3400f };
        static readonly float[] ModeAmp = { 1f, 0.8f, 0.7f, 0.55f, 0.4f, 0.3f, 0.22f };
        static readonly float[] ModeTau = { 0.10f, 0.085f, 0.07f, 0.055f, 0.04f, 0.03f, 0.022f };

        /// <summary>Returns (building and caching on first use) the clip for a surface, variant (wrapped) and stance.</summary>
        public static AudioClip Get(Surface surface, int variant, FootstepStance stance)
        {
            variant = ((variant % VariantCount) + VariantCount) % VariantCount;
            int idx = ((int)surface * VariantCount + variant) * StanceCount + (int)stance;
            var c = Clips[idx];
            if (c != null) return c;
            var data = Render(surface, variant, stance);
            c = AudioClip.Create("fs_" + surface + "_" + stance + "_" + variant, data.Length, 1, Rate, false);
            c.SetData(data, 0);
            Clips[idx] = c;
            return c;
        }

        /// <summary>Builds every clip, one per frame, so no hitch happens on the first step. Start with StartCoroutine.</summary>
        public static IEnumerator PrewarmRoutine()
        {
            for (int s = 0; s < SurfaceCount; s++)
                for (int st = 0; st < StanceCount; st++)
                    for (int v = 0; v < VariantCount; v++) { Get((Surface)s, v, (FootstepStance)st); yield return null; }
        }

        /// <summary>Synthesises the raw mono samples (no AudioClip). Deterministic for the same arguments.</summary>
        public static float[] Render(Surface surface, int variant, FootstepStance stance)
        {
            var p = Params(stance);
            float seconds = surface == Surface.Metal ? 0.38f : (surface == Surface.Gravel ? 0.32f : 0.40f);
            var buf = new float[(int)(seconds * Rate)];
            var rng = new Rng((uint)((int)surface * 7919 + variant * 104729 + 17));
            float gap = p.gap * rng.Range(0.92f, 1.08f), heel = p.heel * rng.Range(0.9f, 1.1f), toe = p.toe * rng.Range(0.88f, 1.12f);
            int toeAt = (int)(gap * Rate);
            float lp = 0f;
            if (surface == Surface.Metal)
            {
                MetalHit(buf, 0, heel, true, p, ref rng);
                MetalHit(buf, toeAt, toe, false, p, ref rng);
                lp = p.lpMetal;
            }
            else if (surface == Surface.Gravel)
            {
                GravelHit(buf, 0, heel, true, p, ref rng);
                GravelHit(buf, toeAt, toe, false, p, ref rng);
                lp = p.lpGravel;
            }
            else
            {
                PuddleHit(buf, 0, heel, true, p, ref rng);
                PuddleHit(buf, toeAt, toe, false, p, ref rng);
                lp = p.lpPuddle;
            }
            Finish(buf, lp, p.peak);
            return buf;
        }

        // ---- layers -------------------------------------------------------------------------------------------------

        static void MetalHit(float[] buf, int at, float gain, bool isHeel, StanceParams p, ref Rng rng)
        {
            float detune = isHeel ? 0.05f : 0.09f;
            float tilt = (p.bright - 0.5f) * 1.4f;
            for (int k = 0; k < ModeHz.Length; k++)
            {
                float f = ModeHz[k] * (1f + detune * rng.Signed()) * (isHeel ? 1f : 1.06f);
                float a = ModeAmp[k] * Mathf.Pow(f / 1000f, tilt) * gain * 0.5f * rng.Range(0.8f, 1.2f);
                Resonate(buf, at, a, f, ModeTau[k] * p.ring);
            }
            // faint long ring of the plate (boot-on-steel)
            Resonate(buf, at, 0.07f * gain, 620f * (1f + 0.04f * rng.Signed()), 0.09f * p.ring);
            Resonate(buf, at, 0.04f * gain, 1480f * (1f + 0.04f * rng.Signed()), 0.07f * p.ring);
            float hp = Mathf.Lerp(0.5f, 0.12f, p.bright);
            Click(buf, at, gain * (0.5f + 0.7f * p.bright), 0.0012f, hp, ref rng);
            // grating rattle: a few loose bar ticks just after the hit
            int ticks = 2 + (int)(rng.Next() * 2.99f);
            for (int i = 0; i < ticks; i++)
                Grain(buf, at + (int)(rng.Range(0.004f, 0.03f) * Rate), 90, gain * rng.Range(0.08f, 0.2f) * (0.5f + p.bright), rng.Range(2200f, 4200f), 0.0018f, ref rng);
            Thump(buf, at, gain * (isHeel ? 0.9f : 0.35f), rng.Range(80f, 105f), 0.045f, 0.5f);
        }

        static void GravelHit(float[] buf, int at, float gain, bool isHeel, StanceParams p, ref Rng rng)
        {
            Thump(buf, at, gain * (isHeel ? 0.5f : 0.2f), rng.Range(58f, 78f), 0.035f, 0.4f);
            LowNoise(buf, at, gain * (isHeel ? 0.3f : 0.18f), 0.03f, 0.12f, ref rng);
            float dur = isHeel ? rng.Range(0.12f, 0.18f) : rng.Range(0.06f, 0.1f);
            int grains = isHeel ? 24 + (int)(rng.Next() * 12f) : 12 + (int)(rng.Next() * 6f);
            float fScale = Mathf.Lerp(0.7f, 1.5f, p.bright);
            for (int i = 0; i < grains; i++)
            {
                float u = rng.Next(); float t = dur * u * Mathf.Sqrt(u);
                float amp = gain * rng.Range(0.1f, 0.5f) * (1f - 0.7f * (t / dur));
                float fc = Mathf.Exp(Mathf.Lerp(Mathf.Log(600f), Mathf.Log(4500f), rng.Next())) * fScale;
                int len = (int)(rng.Range(0.002f, 0.009f) * Rate);
                Grain(buf, at + (int)(t * Rate), len, amp, fc, len / (float)Rate * 0.4f, ref rng);
            }
            if (isHeel) for (int i = 0; i < 3; i++) Grain(buf, at + (int)(rng.Range(0f, 0.03f) * Rate), 160, gain * rng.Range(0.25f, 0.45f) * (0.5f + p.bright * 0.7f), rng.Range(1500f, 3200f) * fScale, 0.0018f, ref rng);
        }

        static void PuddleHit(float[] buf, int at, float gain, bool isHeel, StanceParams p, ref Rng rng)
        {
            Thump(buf, at, gain * (isHeel ? 0.4f : 0.2f), rng.Range(65f, 85f), 0.04f, 0.3f);
            // water slap: resonant noise sweeping upward
            int n = (int)(0.09f * Rate); float y1 = 0f, y2 = 0f, env = gain * 0.9f, dec = Mathf.Exp(-1f / (0.045f * Rate)); float r = 0.85f, f0 = rng.Range(420f, 600f), f1 = rng.Range(1700f, 2300f) * Mathf.Lerp(0.8f, 1.25f, p.bright);
            for (int i = 0; i < n && at + i < buf.Length; i++)
            {
                float f = Mathf.Lerp(f0, f1, i / (float)n);
                float w = TwoPi * f / Rate;
                float y = 0.25f * rng.Signed() + 2f * r * Mathf.Cos(w) * y1 - r * r * y2;
                y2 = y1; y1 = y; buf[at + i] += y * env * 0.5f; env *= dec;
            }
            int bubbles = (isHeel ? 4 : 2) + (int)(rng.Next() * 2.99f);
            for (int i = 0; i < bubbles; i++) Bubble(buf, at + (int)(rng.Range(0.01f, 0.17f) * Rate), gain * rng.Range(0.1f, 0.3f), rng.Range(700f, 2200f), rng.Range(0.012f, 0.03f));
            int sprays = 8 + (int)(rng.Next() * 5f);
            for (int i = 0; i < sprays; i++) Grain(buf, at + (int)(rng.Range(0f, 0.12f) * Rate), 100, gain * rng.Range(0.04f, 0.12f), rng.Range(3000f, 6500f), 0.0015f, ref rng);
        }

        // ---- primitives (no allocations) ----------------------------------------------------------------------------

        /// <summary>Adds a damped sine (impulse response of a resonator) with amplitude gain and decay time constant tau.</summary>
        static void Resonate(float[] buf, int at, float gain, float freq, float tau)
        {
            float r = Mathf.Exp(-1f / (tau * Rate)), w = TwoPi * freq / Rate;
            float a1 = 2f * r * Mathf.Cos(w), a2 = -r * r, y2 = 0f, y1 = gain * r * Mathf.Sin(w);
            int n = Mathf.Min(buf.Length - at, (int)(tau * Rate * 6.9f));
            for (int i = 1; i < n; i++) { buf[at + i] += y1; float y = a1 * y1 + a2 * y2; y2 = y1; y1 = y; }
        }

        /// <summary>Adds a low sine whose pitch drops quickly (the body of the step).</summary>
        static void Thump(float[] buf, int at, float gain, float f0, float tau, float drop)
        {
            float ph = 0f, env = gain, dec = Mathf.Exp(-1f / (tau * Rate)), dd = Mathf.Exp(-70f / Rate), dr = drop;
            int n = Mathf.Min(buf.Length - at, (int)(tau * Rate * 6.9f));
            for (int i = 0; i < n; i++) { ph += TwoPi * f0 * (1f + dr) / Rate; buf[at + i] += Mathf.Sin(ph) * env; env *= dec; dr *= dd; }
        }

        /// <summary>Adds a very short high-passed noise click (sharp transient).</summary>
        static void Click(float[] buf, int at, float gain, float tau, float lpCoef, ref Rng rng)
        {
            float lp = 0f, env = gain, dec = Mathf.Exp(-1f / (tau * Rate));
            int n = Mathf.Min(buf.Length - at, (int)(tau * Rate * 7f));
            for (int i = 0; i < n; i++) { float x = rng.Signed(); lp += (x - lp) * lpCoef; buf[at + i] += (x - lp) * env; env *= dec; }
        }

        /// <summary>Adds low-passed noise (soft scuff / body).</summary>
        static void LowNoise(float[] buf, int at, float gain, float tau, float lpCoef, ref Rng rng)
        {
            float lp = 0f, env = gain, dec = Mathf.Exp(-1f / (tau * Rate));
            int n = Mathf.Min(buf.Length - at, (int)(tau * Rate * 6.9f));
            for (int i = 0; i < n; i++) { lp += (rng.Signed() - lp) * lpCoef; buf[at + i] += lp * env * 4f; env *= dec; }
        }

        /// <summary>Adds a short band-passed noise grain (pebble click). tau is the envelope time constant in seconds.</summary>
        static void Grain(float[] buf, int at, int len, float gain, float freq, float tau, ref Rng rng)
        {
            if (at < 0 || len <= 0) return;
            float r = 0.92f, w = TwoPi * freq / Rate, a1 = 2f * r * Mathf.Cos(w), a2 = -r * r, y1 = 0f, y2 = 0f, env = gain, dec = Mathf.Exp(-1f / (Mathf.Max(tau, 0.0004f) * Rate));
            int n = Mathf.Min(buf.Length - at, len);
            for (int i = 0; i < n; i++) { float y = 0.3f * rng.Signed() + a1 * y1 + a2 * y2; y2 = y1; y1 = y; buf[at + i] += y * env; env *= dec; }
        }

        /// <summary>Adds a water bubble: a sine chirping upward with a fast decay.</summary>
        static void Bubble(float[] buf, int at, float gain, float f0, float tau)
        {
            float ph = 0f, env = gain, dec = Mathf.Exp(-1f / (tau * Rate));
            int n = Mathf.Min(buf.Length - at, (int)(tau * Rate * 6f));
            for (int i = 0; i < n; i++) { float t = i / (float)Rate; ph += TwoPi * f0 * (1f + 3f * t / tau * 0.5f) / Rate; buf[at + i] += Mathf.Sin(ph) * env; env *= dec; }
        }

        /// <summary>Optional two-pole low-pass, tail fade and peak normalisation.</summary>
        static void Finish(float[] buf, float lpHz, float peak)
        {
            if (lpHz > 0f)
            {
                float a = 1f - Mathf.Exp(-TwoPi * lpHz / Rate), s1 = 0f, s2 = 0f;
                for (int i = 0; i < buf.Length; i++) { s1 += (buf[i] - s1) * a; s2 += (s1 - s2) * a; buf[i] = s2; }
            }
            int fade = (int)(0.01f * Rate), n = buf.Length; float mx = 1e-6f;
            for (int i = 0; i < fade; i++) buf[n - 1 - i] *= i / (float)fade;
            for (int i = 0; i < n; i++) { float v = Mathf.Abs(buf[i]); if (v > mx) mx = v; }
            float g = peak / mx;
            for (int i = 0; i < n; i++) buf[i] *= g;
        }
    }
}
