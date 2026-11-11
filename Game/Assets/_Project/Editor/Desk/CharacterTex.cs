using System;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Procedural PBR textures for the characters (URP Lit: albedo, normal, metallic-smoothness, emission) plus the shared UV layouts
    /// that the character meshes and the painters both use. All pixel loops are pure maths on tileable noise, so they run in parallel.
    /// </summary>
    public static class CharacterTex
    {
        /// <summary>Bump when a painter changes so the cached textures are rebuilt.</summary>
        public const string Version = "10";

        public enum Part { Torso, SleeveL, SleeveR, LegL, LegR }
        public enum GearQ { Leather, Rubber, Glove, Pad }

        // ------------------------------------------------------------ UV layouts (metres on the suit atlas, 1.8 m square)
        public const float L = 1.8f;
        public const float HeadV0 = 0.36f;
        static readonly Rect[] SuitR =
        {
            new Rect(0f, 0f, 1.0f, 0.82f), new Rect(1.0f, 0f, 0.4f, 0.70f), new Rect(1.4f, 0f, 0.4f, 0.70f),
            new Rect(0f, 0.85f, 0.58f, 0.90f), new Rect(0.58f, 0.85f, 0.58f, 0.90f)
        };
        static readonly Rect MiscR = new Rect(1.16f, 0.85f, 0.64f, 0.90f);

        public static Vector2 SuitUV(Part p, float t, float s)
        {
            var r = SuitR[(int)p]; return new Vector2((r.x + t * r.width) / L, (r.y + Mathf.Clamp(s, 0f, r.height)) / L);
        }
        public static Vector2 MiscUV(float a, float b) => new Vector2((MiscR.x + Mathf.Clamp(a, 0.01f, MiscR.width - 0.01f)) / L, (MiscR.y + Mathf.Clamp(b, 0.01f, MiscR.height - 0.01f)) / L);
        public static Func<Vector3, Vector2> MiscAt(Vector3 c)
        {
            float h1 = Mathf.Abs(Mathf.Sin(c.x * 91.7f + c.y * 37.1f + c.z * 17.3f)), h2 = Mathf.Abs(Mathf.Cos(c.x * 53.3f + c.y * 71.9f));
            return p => MiscUV(0.15f + h1 * 0.3f + (p.x - c.x + p.z - c.z) * 1.3f, 0.2f + h2 * 0.4f + (p.y - c.y) * 1.3f);
        }

        public static Vector2 GearUV(GearQ q, float u, float v)
        {
            float ox = (q == GearQ.Rubber || q == GearQ.Pad) ? 0.5f : 0f, oy = (q == GearQ.Leather || q == GearQ.Rubber) ? 0.5f : 0f;
            return new Vector2(ox + 0.01f + Mathf.Clamp01(u) * 0.48f, oy + 0.01f + Mathf.Clamp01(v) * 0.48f);
        }
        public static Func<Vector3, Vector2> GearAt(GearQ q, Vector3 c) => p => GearUV(q, 0.3f + (p.x - c.x) * 5f, 0.3f + (p.y - c.y) * 5f);

        public static Vector2 BandUV(float u, float v) => new Vector2(u, v);
        public static Vector2 PlasticUV(float u, float v) => new Vector2(u, v);

        public static Vector2 SkinNeckUV(float t, float s) => new Vector2(t * 0.5f, Mathf.Clamp01(s / 0.2f) * 0.34f);
        public static Vector2 SkinForeUV(bool left, float t, float s) => new Vector2((left ? 0.5f : 0.75f) + t * 0.25f, Mathf.Clamp01(s / 0.34f) * 0.34f);

        // ------------------------------------------------------------ helpers
        static float SS(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }
        static float F(float u, float v, int f, int o, int seed) => TexKit.Fbm(Frac(u), Frac(v), f, o, seed);
        static float Frac(float x) => x - Mathf.Floor(x);
        static float G(float x, float s) => Mathf.Exp(-(x * x) / (2f * s * s));
        static float Hash(int x, int y, int seed)
        {
            unchecked { int h = x * 374761393 + y * 668265263 + seed * 1442695041; h = (h ^ (h >> 13)) * 1274126177; h ^= h >> 16; return (h & 0xFFFFFF) / (float)0xFFFFFF; }
        }
        static Color Mix(Color a, Color b, float t) => Color.Lerp(a, b, Mathf.Clamp01(t));
        static Color Rgb(float r, float g, float b) => new Color(r, g, b, 1f);

        sealed class Img
        {
            public readonly int n; public readonly Color[] alb, mg, em; public readonly float[] h;
            public Img(int n, bool emission = false) { this.n = n; alb = new Color[n * n]; mg = new Color[n * n]; h = new float[n * n]; if (emission) em = new Color[n * n]; }
            public void Paint(Action<int, int> f) { int nn = n; Parallel.For(0, nn, y => { for (int x = 0; x < nn; x++) f(x, y); }); }
            public void Set(int x, int y, Color c, float height, float smooth, float metal = 0f) { int i = y * n + x; alb[i] = new Color(c.r, c.g, c.b, 1f); h[i] = height; mg[i] = new Color(metal, 0f, 0f, smooth); }
            public Color[] Normal(float pixelMeters, bool wrapX = false)
            {
                var nm = new Color[n * n]; int nn = n; var hh = h; float inv = 1f / (2f * pixelMeters);
                Parallel.For(0, nn, y =>
                {
                    for (int x = 0; x < nn; x++)
                    {
                        int xl = wrapX ? (x - 1 + nn) % nn : Mathf.Max(x - 1, 0), xr = wrapX ? (x + 1) % nn : Mathf.Min(x + 1, nn - 1), yd = Mathf.Max(y - 1, 0), yu = Mathf.Min(y + 1, nn - 1);
                        float dx = (hh[y * nn + xr] - hh[y * nn + xl]) * inv, dy = (hh[yu * nn + x] - hh[yd * nn + x]) * inv;
                        var v = new Vector3(-dx, -dy, 1f).normalized; nm[y * nn + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                    }
                });
                return nm;
            }
        }

        public sealed class Set { public Texture2D Albedo, Normal, MG, Emis; }

        static Texture2D Put(string dir, string name, Color[] px, int n, bool normal, bool linear)
        {
            var t = TexKit.Save(dir, name, px, n, n, normal, linear);
            var path = AssetDatabase.GetAssetPath(t); var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.maxTextureSize = n; imp.crunchedCompression = false;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Set Finish(string dir, string name, Img im, float pixelMeters, bool wrapX = false)
        {
            var s = new Set
            {
                Albedo = Put(dir, name + "_Albedo", im.alb, im.n, false, false), Normal = Put(dir, name + "_Normal", im.Normal(pixelMeters, wrapX), im.n, true, false),
                MG = Put(dir, name + "_MG", im.mg, im.n, false, true)
            };
            if (im.em != null) s.Emis = Put(dir, name + "_Emis", im.em, im.n, false, false);
            return s;
        }

        // ------------------------------------------------------------ suit atlas
        /// <summary>Jumpsuit fabric: twill weave, mud / grease / dust, wear at knees and elbows, stitching, zipper. Near-white base so material tints pick the suit colour.</summary>
        public static Set Suit(string dir, string name, int n, bool infected)
        {
            var im = new Img(n); float ppm = n / L, pitch = 4.2f / ppm; int seed = infected ? 91 : 17;
            im.Paint((x, y) =>
            {
                float mx = (x + 0.5f) / ppm, my = (y + 0.5f) / ppm; int part = 5; Rect r = MiscR;
                for (int i = 0; i < 5; i++) if (SuitR[i].Contains(new Vector2(mx, my))) { part = i; r = SuitR[i]; break; }
                float pu = part == 5 ? Frac((mx - MiscR.x) / MiscR.width) : (mx - r.x) / r.width, s = Mathf.Max(0f, my - r.y), hh = r.height, wid = r.width;
                float pl = Mathf.Clamp01(s / hh), pv = s / wid; int sd = seed + part * 13;
                // base cloth
                float tw = Mathf.Sin(2f * Mathf.PI * (mx + my) / pitch) * 0.5f + 0.5f, tw2 = Mathf.Sin(2f * Mathf.PI * (mx - my * 0.6f) / (pitch * 1.3f)) * 0.5f + 0.5f;
                float weave = 0.5f * tw + 0.5f * tw2;
                float tone = 0.9f * (0.93f + 0.14f * F(pu, pv, 24, 3, sd + 1) + 0.05f * (weave - 0.5f));
                float fade = SS(0.55f, 0.8f, F(pu, pv, 5, 4, sd + 2));
                var col = Rgb(tone, tone, tone * 0.97f); col = Mix(col, Rgb(1f, 1f, 0.98f), fade * 0.35f);
                float height = 0.00014f * weave; float smooth = 0.10f + 0.06f * fade;
                // wear (knees, elbows, waist) and dirt level
                float wear = 0f, dirt;
                if (part == (int)Part.LegL || part == (int)Part.LegR) { wear = G(s - 0.48f, 0.07f); dirt = 0.30f + 0.45f * pl; }
                else if (part == (int)Part.SleeveL || part == (int)Part.SleeveR) { wear = G(s - 0.355f, 0.055f); dirt = 0.26f + 0.34f * pl; }
                else if (part == (int)Part.Torso) { wear = 0.5f * G(s - 0.27f, 0.05f); dirt = 0.36f + 0.24f * (1f - pl); }
                else dirt = 0.4f;
                if (infected) dirt += 0.22f;
                float mudN = 0.65f * F(pu, pv, 5, 5, sd + 3) + 0.35f * F(pu, pv, 14, 3, sd + 4);
                float th = Mathf.Lerp(0.70f, 0.40f, Mathf.Clamp01(dirt)) - wear * 0.07f;
                float mud = SS(th, th + 0.09f, mudN);
                TexKit.Worley(pu, pv, 26, sd + 5, out float f1, out _, out float id1);
                TexKit.Worley(pu, pv, 64, sd + 6, out float g1, out _, out float id2);
                float spat = (1f - SS(0.05f, 0.2f, f1)) * (id1 > 0.55f ? 1f : 0f) * Mathf.Clamp01(dirt * 1.5f) + (1f - SS(0.04f, 0.14f, g1)) * (id2 > 0.6f ? 1f : 0f) * 0.8f;
                mud = Mathf.Max(mud, Mathf.Clamp01(spat) * 0.9f);
                var mudC = Mix(Rgb(0.11f, 0.08f, 0.05f), Rgb(0.03f, 0.027f, 0.024f), F(pu, pv, 9, 3, sd + 7));
                mudC = Mix(mudC, Rgb(0.30f, 0.24f, 0.17f), SS(0.45f, 0.75f, F(pu, pv, 17, 3, sd + 8)) * 0.45f);
                col = Mix(col, mudC, mud * 0.92f); height += 0.0005f * mud * (0.6f + 0.4f * F(pu, pv, 30, 2, sd + 9)); smooth = Mathf.Lerp(smooth, 0.06f, mud);
                // worn lighter patches at joints
                col = Mix(col, Rgb(1f, 0.97f, 0.9f), wear * 0.3f * (1f - mud)); smooth += wear * 0.05f;
                // grease
                float gr = SS(0.74f, 0.82f, F(pu, pv, 4, 4, sd + 10)) * (0.4f + 0.6f * pl);
                col = Mix(col, Rgb(0.02f, 0.02f, 0.018f), gr * 0.85f); smooth = Mathf.Lerp(smooth, 0.5f, gr);
                if (infected)
                {
                    float bl = SS(0.7f, 0.78f, F(pu * 1.0f, pv * 0.6f, 3, 4, sd + 11)) * SS(0.3f, 0.9f, F(pu, pv, 12, 3, sd + 12));
                    col = Mix(col, Rgb(0.18f, 0.012f, 0.012f), bl * 0.85f); smooth = Mathf.Lerp(smooth, 0.42f, bl);
                    TexKit.Worley(pu, pv, 11, sd + 13, out float t1, out float t2, out float tid);
                    float tear = (1f - SS(0.02f, 0.11f, t1)) * (tid > 0.9f ? 1f : 0f);
                    float tearEdge = SS(0.1f, 0.15f, t1) * (1f - SS(0.15f, 0.2f, t1)) * (tid > 0.9f ? 1f : 0f);
                    col = Mix(col, Rgb(0.05f, 0.04f, 0.035f), tear); col = Mix(col, Rgb(0.7f, 0.65f, 0.55f), tearEdge * 0.4f); height -= tear * 0.0008f; height += tearEdge * 0.0003f;
                }
                // folds in the cloth around joints
                float fold = 0f;
                if (part == (int)Part.LegL || part == (int)Part.LegR) fold = G(s - 0.48f, 0.09f) * 0.0011f * Mathf.Sin((s - 0.48f) * 2f * Mathf.PI / 0.034f + F(pu, pv, 6, 3, sd + 14) * 6f) + G(s - 0.04f, 0.07f) * 0.0009f * Mathf.Sin(s * 2f * Mathf.PI / 0.04f + pu * 12f);
                else if (part == (int)Part.SleeveL || part == (int)Part.SleeveR) fold = G(s - 0.355f, 0.07f) * 0.001f * Mathf.Sin((s - 0.355f) * 2f * Mathf.PI / 0.03f + F(pu, pv, 6, 3, sd + 14) * 6f);
                else if (part == (int)Part.Torso) fold = G(s - 0.27f, 0.06f) * 0.0009f * Mathf.Sin(s * 2f * Mathf.PI / 0.035f + F(pu, pv, 5, 3, sd + 14) * 7f) + 0.0004f * (F(pu, pv, 7, 3, sd + 15) - 0.5f);
                height += fold; col *= 1f + fold * 80f;
                // seams, stitches, zipper
                float[] seams = part == (int)Part.Torso ? new[] { 0.25f, 0.75f } : (part == 5 ? new float[0] : new[] { 0.25f, 0.75f });
                foreach (float sm in seams)
                {
                    float dx = (pu - sm) * wid; float ad = Mathf.Abs(dx);
                    float dash = Frac(s / 0.0038f) < 0.62f ? 1f : 0f;
                    if (Mathf.Abs(ad - 0.0048f) < 0.0005f && dash > 0f) { col = Mix(col, Rgb(0.72f, 0.62f, 0.4f), 0.75f); height -= 0.00018f; }
                    if (ad < 0.0009f) { col *= 0.72f; height -= 0.00045f; }
                    else if (ad < 0.006f) height += 0.00012f;
                }
                if (part == (int)Part.Torso)
                {
                    float zd = Mathf.Min(pu, 1f - pu) * wid;
                    if (zd < 0.0045f) { bool teeth = Frac(s / 0.0042f) < 0.5f; col = teeth ? Rgb(0.45f, 0.45f, 0.42f) : Rgb(0.12f, 0.12f, 0.12f); height += teeth ? 0.0003f : -0.0002f; smooth = 0.5f; }
                    else if (zd < 0.011f) { col *= 0.78f; height += 0.0002f; }
                }
                if (part != 5 && (hh - s < 0.022f && hh - s > 0.016f) && Frac(pu * wid / 0.0038f) < 0.62f) { col = Mix(col, Rgb(0.72f, 0.62f, 0.4f), 0.65f); height -= 0.00015f; }
                im.Set(x, y, col, height, Mathf.Clamp01(smooth));
            });
            return Finish(dir, name, im, 1f / ppm);
        }

        // ------------------------------------------------------------ gear atlas (leather, rubber boots, gloves, pads) 1K
        public static Set Gear(string dir, string name, int n)
        {
            var im = new Img(n); float ppm = n * 0.5f / 0.5f;
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n; int qx = u < 0.5f ? 0 : 1, qy = v < 0.5f ? 0 : 1; float lu = Frac(u * 2f), lv = Frac(v * 2f); int sd = 31 + qx * 5 + qy * 11;
                Color col; float height, smooth;
                float grain = F(lu, lv, 40, 3, sd), cracks; TexKit.Worley(lu, lv, 28, sd + 1, out float f1, out float f2, out _); cracks = 1f - SS(0f, 0.07f, f2 - f1);
                float dirtN = F(lu, lv, 5, 5, sd + 2), dirt2 = F(lu, lv, 18, 3, sd + 3);
                if (qy == 1 && qx == 0)
                {   // leather
                    col = Mix(Rgb(0.20f, 0.115f, 0.06f), Rgb(0.32f, 0.19f, 0.1f), grain); col = Mix(col, Rgb(0.05f, 0.03f, 0.02f), cracks * 0.55f);
                    height = 0.0005f * (grain + 0.6f * (1f - cracks)); smooth = 0.30f + 0.2f * SS(0.5f, 0.8f, dirt2);
                    float worn = SS(0.55f, 0.8f, dirtN); col = Mix(col, Rgb(0.42f, 0.3f, 0.18f), worn * 0.3f);
                    float mud = SS(0.5f, 0.62f, dirtN * 0.6f + dirt2 * 0.4f); col = Mix(col, Rgb(0.07f, 0.055f, 0.04f), mud * 0.75f);
                }
                else if (qy == 1)
                {   // rubber boot: mud gradient from the sole upwards
                    float mudAmt = SS(0.36f, 0.0f, lv * 0.8f) * 1.15f + 0.1f; float mn = 0.6f * dirtN + 0.4f * dirt2;
                    col = Mix(Rgb(0.07f, 0.07f, 0.065f), Rgb(0.13f, 0.125f, 0.115f), grain);
                    float mud = SS(0.62f - mudAmt * 0.45f, 0.7f - mudAmt * 0.45f, mn + 0.1f * grain); col = Mix(col, Mix(Rgb(0.10f, 0.075f, 0.05f), Rgb(0.04f, 0.033f, 0.026f), dirt2), mud * 0.95f);
                    col = Mix(col, Rgb(0.2f, 0.15f, 0.1f), cracks * mud * 0.55f);
                    float crease = Mathf.Abs(Mathf.Sin(lv * 90f + dirtN * 6f)); height = 0.0004f * grain + 0.0006f * mud + 0.0004f * (1f - crease) * SS(0.02f, 0.3f, lv); smooth = Mathf.Lerp(0.38f, 0.1f, mud);
                    if (lv < 0.012f * 3f) { col = Rgb(0.02f, 0.02f, 0.02f); smooth = 0.2f; }
                }
                else if (qx == 0)
                {   // work glove
                    col = Mix(Rgb(0.34f, 0.30f, 0.24f), Rgb(0.5f, 0.44f, 0.35f), grain); float stain = SS(0.5f, 0.65f, dirtN); col = Mix(col, Rgb(0.04f, 0.035f, 0.03f), stain * 0.7f);
                    col = Mix(col, Rgb(0.1f, 0.07f, 0.04f), SS(0.55f, 0.7f, dirt2) * 0.5f); height = 0.0006f * grain + 0.0003f * Mathf.Sin(lv * 200f); smooth = 0.14f;
                }
                else
                {   // knee pad / rubberised patches
                    col = Mix(Rgb(0.13f, 0.11f, 0.09f), Rgb(0.22f, 0.19f, 0.16f), grain); float mud = SS(0.5f, 0.65f, dirtN); col = Mix(col, Rgb(0.09f, 0.07f, 0.05f), mud * 0.8f);
                    height = 0.0007f * (1f - cracks) + 0.0005f * grain; smooth = 0.25f - mud * 0.12f;
                }
                im.Set(x, y, col, height, smooth);
            });
            return Finish(dir, name, im, 1f / ppm * 0.5f);
        }

        // ------------------------------------------------------------ reflective hi-vis tape (tileable)
        public static Set Reflective(string dir, string name, int n)
        {
            var im = new Img(n); float ppm = n / 0.30f;
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n; int sd = 77;
                float scuff = F(u, v, 12, 4, sd), fine = F(u, v, 48, 3, sd + 1), dirt = F(u, v, 6, 5, sd + 2);
                float edge = 0.18f; bool silver = v > edge && v < 1f - edge; Color col; float smooth, metal = 0f, height;
                if (silver)
                {
                    TexKit.Worley(u, v * 1.0f, 70, sd + 3, out float f1, out float f2, out _);
                    float prism = SS(0f, 0.25f, f2 - f1); col = Mix(Rgb(0.46f, 0.47f, 0.47f), Rgb(0.72f, 0.74f, 0.74f), prism * 0.7f + fine * 0.3f);
                    float sc = SS(0.55f, 0.72f, scuff); col = Mix(col, Rgb(0.34f, 0.34f, 0.33f), sc * 0.5f);
                    float grime = SS(0.5f, 0.7f, dirt); col = Mix(col, Rgb(0.07f, 0.06f, 0.05f), grime * 0.65f);
                    smooth = Mathf.Lerp(0.78f, 0.28f, Mathf.Max(sc * 0.6f, grime)); metal = Mathf.Lerp(0.6f, 0.15f, grime); height = 0.0003f * prism;
                }
                else
                {
                    float tw = Mathf.Sin(u * 2f * Mathf.PI * 70f) * Mathf.Sin(v * 2f * Mathf.PI * 70f) * 0.5f + 0.5f;
                    col = Mix(Rgb(0.86f, 0.33f, 0.02f), Rgb(0.95f, 0.45f, 0.04f), fine) * (0.92f + 0.08f * tw); col = Mix(col, Rgb(0.07f, 0.045f, 0.02f), SS(0.5f, 0.68f, dirt) * 0.7f);
                    smooth = 0.14f; height = 0.00012f * tw;
                }
                float gap = 1f - SS(0.0f, 0.012f, Mathf.Abs(v - edge)); col = Mix(col, Rgb(0.04f, 0.04f, 0.04f), gap * 0.75f); float gap2 = 1f - SS(0f, 0.012f, Mathf.Abs(v - (1f - edge))); col = Mix(col, Rgb(0.04f, 0.04f, 0.04f), gap2 * 0.75f);
                im.Set(x, y, col, height, smooth, metal);
            });
            return Finish(dir, name, im, 0.30f / n, true);
        }

        // ------------------------------------------------------------ hard hat plastic and hair
        public static Set Plastic(string dir, string name, int n)
        {
            var im = new Img(n);
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n; int sd = 55;
                float fine = F(u, v, 32, 3, sd), dirt = F(u, v, 5, 5, sd + 1), dirt2 = F(u, v, 16, 4, sd + 2);
                float scratch = SS(0.985f, 0.998f, Mathf.Abs(Mathf.Sin((u * 60f + v * 23f + F(u, v, 9, 3, sd + 3) * 4f) * 3f))) * SS(0.6f, 0.75f, F(u, v, 8, 2, sd + 4));
                var col = Rgb(0.9f, 0.9f, 0.9f) * (0.93f + 0.07f * fine); col = Mix(col, Rgb(0.5f, 0.5f, 0.5f), scratch * 0.5f);
                float mud = SS(0.58f, 0.72f, dirt * 0.6f + dirt2 * 0.4f); col = Mix(col, Rgb(0.12f, 0.09f, 0.06f), mud * 0.8f);
                im.Set(x, y, col, 0.0003f * fine - scratch * 0.0004f + 0.0004f * mud, Mathf.Lerp(0.55f, 0.18f, mud));
            });
            return Finish(dir, name, im, 0.003f, true);
        }

        public static Set Hair(string dir, string name, int n)
        {
            var im = new Img(n);
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n; int sd = 5;
                float strand = F(u, v, 48, 2, sd) * 0.5f + F(u + F(u, v, 8, 2, sd + 1) * 0.05f, v, 20, 2, sd + 2) * 0.5f; float clump = F(u, v, 12, 3, sd + 3);
                float c = 0.3f + 0.95f * strand * (0.6f + 0.4f * clump);
                im.Set(x, y, Rgb(c, c * 0.95f, c * 0.9f), 0.0006f * strand, 0.35f);
            });
            return Finish(dir, name, im, 0.002f, true);
        }

        // ------------------------------------------------------------ skin
        static float Veins(float u, float v, int seed, float w)
        {
            float wu = u + 0.05f * (F(u, v, 5, 3, seed + 1) - 0.5f), wv = v + 0.05f * (F(u, v, 5, 3, seed + 2) - 0.5f);
            float r1 = 1f - Mathf.Abs(2f * F(wu, wv, 7, 4, seed) - 1f), r2 = 1f - Mathf.Abs(2f * F(wu, wv, 16, 3, seed + 5) - 1f), r3 = 1f - Mathf.Abs(2f * F(wu, wv, 34, 2, seed + 7) - 1f);
            TexKit.Worley(wu, wv, 20, seed + 9, out float f1, out float f2, out _);
            float net = 1f - SS(0f, 0.05f, f2 - f1);
            float t1 = 1f - 0.014f * w, t2 = 1f - 0.01f * w;
            return Mathf.Clamp01(Mathf.Max(SS(t1 - 0.012f, t1, r1), 0.8f * SS(t2 - 0.01f, t2, r2), 0.5f * SS(0.992f, 0.999f, r3), net * 0.35f));
        }

        /// <summary>Face + neck + forearm skin atlas. Healthy: pores, redness, grime, brows, lips. Infected: grey-purple, sunken eyes, branching dark veins with an emission mask.</summary>
        public static Set Skin(string dir, string name, int n, bool infected)
        {
            var im = new Img(n, infected); float ppm = n / 0.55f; int seed = infected ? 211 : 101;
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n, v = (y + 0.5f) / n; int sd = seed;
                float a = 0f, e = 0f; int reg = 0; // 0 head, 1 neck, 2 forearm
                if (v >= HeadV0) { a = (u - 0.5f) * 2f * Mathf.PI; e = CharacterGeo.HeadE0 + (v - HeadV0) / (1f - HeadV0) * (Mathf.PI * 0.5f - CharacterGeo.HeadE0); }
                else reg = u < 0.5f ? 1 : 2;
                float aa = Mathf.Abs(a);
                var baseC = infected ? Rgb(0.60f, 0.52f, 0.62f) : Rgb(0.84f, 0.64f, 0.54f);
                float low = F(u, v, 6, 4, sd), mid = F(u, v, 22, 3, sd + 1), fine = F(u, v, 90, 2, sd + 2);
                var col = baseC * (0.93f + 0.14f * low); float height = 0.00025f * fine; float smooth = infected ? 0.42f : 0.32f;
                TexKit.Worley(u, v, reg == 0 ? 190 : 120, sd + 3, out float p1, out float p2, out _);
                float pore = 1f - SS(0f, 0.2f, p1); height -= 0.00022f * pore; col *= 1f - 0.05f * pore;
                // blotchy redness / pallor
                col = Mix(col, infected ? Rgb(0.55f, 0.62f, 0.46f) : Rgb(0.9f, 0.5f, 0.45f), SS(0.5f, 0.75f, mid) * (infected ? 0.35f : 0.28f));
                float veinMask = 0f; float eyeDark = 0f;
                if (reg == 0)
                {
                    float cheek = G(aa - 0.8f, 0.3f) * G(e + 0.2f, 0.25f), nose = G(aa, 0.1f) * G(e + 0.15f, 0.12f), ear = G(aa - 1.6f, 0.17f) * G(e + 0.03f, 0.2f);
                    col = Mix(col, Rgb(0.92f, 0.42f, 0.38f), (cheek * 0.35f + nose * 0.45f + ear * 0.5f) * (infected ? 0.3f : 1f));
                    // eye sockets / bags
                    float ed = Mathf.Sqrt(Mathf.Pow((aa - 0.42f) * 0.09f, 2f) + Mathf.Pow((e - 0.04f) * 0.115f, 2f));
                    eyeDark = G(ed, 0.026f) * (infected ? 0.85f : 0.45f); col = Mix(col, infected ? Rgb(0.28f, 0.2f, 0.3f) : Rgb(0.52f, 0.34f, 0.38f), eyeDark);
                    float bag = G(aa - 0.45f, 0.18f) * G(e + 0.1f, 0.05f); col = Mix(col, infected ? Rgb(0.3f, 0.22f, 0.32f) : Rgb(0.5f, 0.38f, 0.42f), bag * 0.5f);
                    // lips and mouth slit
                    float lip = SS(0.3f, 0.2f, aa) * G(e + 0.4f, 0.07f); col = Mix(col, infected ? Rgb(0.4f, 0.28f, 0.34f) : Rgb(0.66f, 0.3f, 0.28f), lip * 0.8f);
                    float slit = SS(0.3f, 0.12f, aa) * G(e + 0.395f, 0.0115f); col = Mix(col, Rgb(0.1f, 0.03f, 0.03f), slit * 0.9f); height -= slit * 0.0007f;
                    float nostril = G(aa - 0.13f, 0.045f) * G(e + 0.265f, 0.03f); col = Mix(col, Rgb(0.06f, 0.025f, 0.02f), nostril);
                    // eyebrows
                    float browE = 0.255f + 0.09f * Mathf.Cos((aa - 0.2f) * 3.2f) - aa * 0.1f; float browW = SS(0.1f, 0.2f, aa) * SS(0.85f, 0.62f, aa);
                    float strands = 0.5f + 0.5f * F(u, v, 220, 2, sd + 4); float brow = SS(0.045f, 0.01f, Mathf.Abs(e - browE)) * browW * strands;
                    col = Mix(col, infected ? Rgb(0.2f, 0.17f, 0.18f) : Rgb(0.12f, 0.08f, 0.055f), brow * 0.85f);
                    // stubble
                    float stub = SS(-0.3f, -0.8f, e) * (Hash(x, y, sd + 5) > 0.82f ? 1f : 0f) * SS(1.7f, 1.1f, aa); col = Mix(col, Rgb(0.2f, 0.17f, 0.15f), stub * 0.45f);
                    // forehead / eye wrinkles in the relief
                    height += 0.0003f * Mathf.Max(0f, Mathf.Sin(e * 38f + F(u, v, 6, 2, sd + 6) * 3f)) * SS(0.3f, 0.5f, e) * SS(1.1f, 0.5f, aa);
                    smooth += 0.14f * G(e - 0.45f, 0.12f) + 0.14f * nose + lip * 0.15f;
                    veinMask = infected ? Mathf.Clamp01(0.25f + 0.9f * G(aa - 0.7f, 0.5f) * SS(-0.7f, 0.0f, e) + 0.8f * G(ed, 0.04f) + 0.9f * SS(0.2f, 0.6f, aa) * G(e - 0.3f, 0.4f) + 0.7f * SS(-0.5f, -1.0f, e)) * (1f - 0.9f * G(aa, 0.14f) * G(e + 0.1f, 0.3f)) * (1f - 0.7f * lip) : 0f;
                }
                else if (reg == 1) { col *= 0.92f + 0.08f * mid; veinMask = infected ? 0.95f : 0f; col = Mix(col, Rgb(0.3f, 0.22f, 0.15f), SS(0.5f, 0.62f, F(u, v, 5, 4, sd + 8)) * 0.35f); }
                else { veinMask = infected ? 1.0f : 0f; col *= 0.96f; }
                // grime / soot smudges
                float gr = SS(0.58f, 0.72f, 0.6f * F(u, v, 5, 5, sd + 9) + 0.4f * F(u, v, 14, 3, sd + 10)); col = Mix(col, Rgb(0.15f, 0.11f, 0.08f), gr * (reg == 2 ? 0.6f : 0.38f)); smooth -= gr * 0.12f;
                if (infected)
                {
                    float vv = Veins(u, v, sd + 20, 1f) * veinMask;
                    col = Mix(col, Rgb(0.13f, 0.05f, 0.11f), vv * 0.92f); height += vv * 0.0004f; smooth -= vv * 0.05f;
                    col = Mix(col, Rgb(0.32f, 0.14f, 0.2f), Mathf.Clamp01(Veins(u, v, sd + 30, 0.6f) * veinMask * 0.35f) * (1f - vv)); // soft purple halo
                    im.em[y * n + x] = new Color(0.25f * vv, 1f * vv, 0.45f * vv, 1f);
                }
                im.Set(x, y, col, height, Mathf.Clamp01(smooth));
            });
            return Finish(dir, name, im, 0.52f / n * 0.5f, true);
        }

        // ------------------------------------------------------------ eyes (azimuthal map: r = sqrt(theta / pi) / 2)
        public static Set Eye(string dir, string name, int n, bool infected)
        {
            var im = new Img(n, infected); int sd = infected ? 301 : 303;
            im.Paint((x, y) =>
            {
                float u = (x + 0.5f) / n - 0.5f, v = (y + 0.5f) / n - 0.5f; float rr = Mathf.Sqrt(u * u + v * v); float th = Mathf.PI * (rr / 0.5f) * (rr / 0.5f); float ang = Mathf.Atan2(v, u);
                float aN = (ang / (2f * Mathf.PI) + 0.5f);
                var sclera = infected ? Rgb(0.88f, 0.62f, 0.58f) : Rgb(0.9f, 0.87f, 0.82f);
                // blood vessels: radial streaks from the rim
                float ves = F(aN, Mathf.Clamp01(th / Mathf.PI), 28, 3, sd) * F(aN, th, 90, 2, sd + 1); float vessel = SS(infected ? 0.2f : 0.34f, infected ? 0.45f : 0.5f, ves) * SS(0.5f, 1.4f, th);
                var col = Mix(sclera, infected ? Rgb(0.8f, 0.1f, 0.1f) : Rgb(0.78f, 0.35f, 0.33f), vessel * (infected ? 0.9f : 0.5f));
                col = Mix(col, infected ? Rgb(0.6f, 0.35f, 0.3f) : Rgb(0.7f, 0.6f, 0.52f), SS(1.8f, 3f, th) * 0.5f);
                float iris = 1f - SS(0.5f, 0.56f, th);
                var fib = F(aN, th, 40, 3, sd + 2); var irisC = infected ? Mix(Rgb(0.7f, 0.1f, 0.05f), Rgb(1f, 0.38f, 0.1f), fib) : Mix(Rgb(0.2f, 0.12f, 0.06f), Rgb(0.45f, 0.3f, 0.14f), fib);
                irisC = Mix(irisC, Rgb(0.03f, 0.02f, 0.015f), SS(0.38f, 0.52f, th) * 0.8f);
                col = Mix(col, irisC, iris); float pupil = 1f - SS(0.2f, 0.25f, th); col = Mix(col, Rgb(0.005f, 0.005f, 0.005f), pupil);
                im.Set(x, y, col, 0f, 0.92f);
                if (infected) { float g = iris * (0.35f + 0.65f * fib) * (1f - pupil); im.em[y * n + x] = new Color(g * 1f, g * 0.2f, g * 0.1f, 1f); }
            });
            var t = new Set { Albedo = Put(dir, name + "_Albedo", im.alb, n, false, false) };
            if (infected) t.Emis = Put(dir, name + "_Emis", im.em, n, false, false);
            return t;
        }

        // ------------------------------------------------------------ cache marker
        public static bool UpToDate(string dir)
        {
            string p = dir + "/version.txt"; return File.Exists(p) && File.ReadAllText(p) == Version;
        }
        public static void MarkDone(string dir) { Directory.CreateDirectory(dir); File.WriteAllText(dir + "/version.txt", Version); }
    }
}
