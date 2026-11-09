using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Tiny synthesised sound effects so the prototype has audio feedback without any imported assets.</summary>
    public static class ProceduralAudio
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        static AudioClip Make(string name, float seconds, System.Func<float, int, float> gen, bool loop = false)
        {
            if (cache.TryGetValue(name, out var c) && c != null) return c;
            int n = Mathf.CeilToInt(seconds * Rate); var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(gen(i / (float)Rate, i), -1f, 1f);
            c = AudioClip.Create(name, n, 1, Rate, false); c.SetData(d, 0); cache[name] = c; return c;
        }

        static float Noise(int i, int seed) { unchecked { int h = i * 374761393 + seed * 668265263; h = (h ^ (h >> 13)) * 1274126177; return ((h ^ (h >> 16)) & 0xFFFF) / 32768f - 1f; } }

        public static AudioClip Step(int variant)
        {
            float lp = 0f;
            return Make("step" + (variant % 3), 0.16f, (t, i) => { lp += (Noise(i, 3 + variant % 3) - lp) * 0.18f; return lp * Mathf.Exp(-t * 28f) * 1.6f + Mathf.Sin(t * 95f * 6.28f) * Mathf.Exp(-t * 40f) * 0.35f; });
        }

        public static AudioClip Drone()
        {
            float lp = 0f;
            return Make("drone", 12f, (t, i) =>
            {
                lp += (Noise(i, 9) - lp) * 0.012f;
                float s = Mathf.Sin(t * 41f * 6.2832f) * 0.28f + Mathf.Sin(t * 61.5f * 6.2832f) * 0.18f + Mathf.Sin(t * 83f * 6.2832f) * 0.08f;
                float swell = 0.65f + 0.35f * Mathf.Sin(t / 12f * 6.2832f * 2f);
                return (s + lp * 6f) * swell * 0.55f;
            }, true);
        }

        public static AudioClip Whisper()
        {
            float b = 0f, lp = 0f;
            return Make("whisper", 1.8f, (t, i) =>
            {
                float n = Noise(i, 21); b += (n - b) * 0.45f; float hp = n - b; lp += (hp - lp) * 0.5f;
                float syl = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 5.1f * 3.14159f)), 1.5f) * Mathf.Sin(t / 1.8f * 3.14159f);
                return lp * syl * 1.4f;
            });
        }

        public static AudioClip Hiss()
        {
            float b = 0f;
            return Make("hiss", 3f, (t, i) => { float n = Noise(i, 5); b += (n - b) * 0.3f; return (n - b) * 0.7f * Mathf.Clamp01(t * 6f) * Mathf.Clamp01((3f - t) * 3f); });
        }

        public static AudioClip Heartbeat()
        {
            return Make("heart", 0.9f, (t, i) =>
            {
                float a = Mathf.Sin(t * 58f * 6.2832f) * Mathf.Exp(-Mathf.Pow((t - 0.05f) * 22f, 2f));
                float b = Mathf.Sin(t * 50f * 6.2832f) * Mathf.Exp(-Mathf.Pow((t - 0.28f) * 22f, 2f)) * 0.7f;
                return (a + b) * 0.9f;
            });
        }

        public static AudioClip Clank()
        {
            return Make("clank", 1.2f, (t, i) => (Mathf.Sin(t * 180f * 6.2832f) + Mathf.Sin(t * 311f * 6.2832f) * 0.7f + Mathf.Sin(t * 571f * 6.2832f) * 0.4f) * Mathf.Exp(-t * 5f) * 0.45f + Noise(i, 4) * Mathf.Exp(-t * 60f) * 0.5f);
        }

        public static AudioClip Beep()
        {
            return Make("beep", 0.35f, (t, i) => Mathf.Sign(Mathf.Sin(t * 880f * 6.2832f)) * 0.3f * Mathf.Clamp01((0.35f - t) * 10f) * Mathf.Clamp01(t * 200f));
        }

        public static AudioClip Alarm()
        {
            return Make("alarm", 2f, (t, i) => { float f = 520f + 260f * Mathf.Sin(t * 3.14159f); return Mathf.Sign(Mathf.Sin(t * f * 6.2832f)) * 0.22f * Mathf.Clamp01((2f - t) * 4f); });
        }

        public static AudioClip Growl()
        {
            float lp = 0f;
            return Make("growl", 1.6f, (t, i) =>
            {
                float f = 62f + 18f * Mathf.Sin(t * 9f); float saw = (t * f % 1f) * 2f - 1f; lp += (Noise(i, 17) - lp) * 0.08f;
                return (saw * 0.5f + lp * 2f) * (0.5f + 0.5f * Mathf.Sin(t * 28f)) * Mathf.Sin(t / 1.6f * 3.14159f) * 0.8f;
            });
        }

        public static AudioClip Rumble()
        {
            float lp = 0f;
            return Make("rumble", 4f, (t, i) => { lp += (Noise(i, 31) - lp) * 0.02f; return (lp * 7f + Mathf.Sin(t * 34f * 6.2832f) * 0.3f) * Mathf.Clamp01(t * 2f) * Mathf.Clamp01((4f - t) * 2f); });
        }

        public static AudioClip Whoosh()
        {
            float b = 0f;
            return Make("whoosh", 1.4f, (t, i) => { float n = Noise(i, 8); b += (n - b) * (0.1f + 0.3f * t / 1.4f); return b * 2.2f * Mathf.Sin(t / 1.4f * 3.14159f); });
        }

        /// <summary>Plays a one-shot 3D sound at a position.</summary>
        public static AudioSource PlayAt(AudioClip clip, Vector3 pos, float volume = 1f, float maxDistance = 20f, float pitch = 1f)
        {
            var go = new GameObject("SFX_" + clip.name); go.transform.position = pos;
            var a = go.AddComponent<AudioSource>(); a.clip = clip; a.volume = volume; a.pitch = pitch; a.spatialBlend = 1f; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1.5f; a.maxDistance = maxDistance;
            a.Play(); Object.Destroy(go, clip.length / Mathf.Max(0.2f, pitch) + 0.2f);
            return a;
        }

        /// <summary>Plays a non-positional sound.</summary>
        public static void Play2D(AudioClip clip, float volume = 1f)
        {
            var go = new GameObject("SFX2D_" + clip.name); var a = go.AddComponent<AudioSource>(); a.clip = clip; a.volume = volume; a.spatialBlend = 0f; a.Play(); Object.Destroy(go, clip.length + 0.2f);
        }
    }
}
