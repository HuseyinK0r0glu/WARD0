using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ward0.EditorTools
{
    /// <summary>Procedural texture generation for the quarantine desk (worn, rusted industrial metal, CRT screens, decals).</summary>
    public static class TexKit
    {
        /// <summary>GLSL-style smoothstep (edge0 may be greater than edge1).</summary>
        static float SS(float e0, float e1, float x) { float t = Mathf.Clamp01((x - e0) / (e1 - e0)); return t * t * (3f - 2f * t); }

        // ---------- tileable noise ----------
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / (float)0xFFFFFF;
            }
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            int x0 = ((xi % period) + period) % period, x1 = (x0 + 1) % period;
            int y0 = ((yi % period) + period) % period, y1 = (y0 + 1) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        public static float Fbm(float u, float v, int baseFreq, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f;
            int f = baseFreq;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * f, v * f, f, seed + o * 17);
                norm += amp; amp *= 0.5f; f *= 2;
            }
            return sum / norm;
        }

        // ---------- worn metal set ----------
        public sealed class MetalSet
        {
            public Texture2D Gun, Dark, Red, Yellow, Normal, MetalGloss;
        }

        public static MetalSet BuildMetal(string dir, int size = 1024, string prefix = "Metal", float rustBias = 0f)
        {
            int n = size;
            var height = new float[n * n];
            var rust = new float[n * n];
            var scratch = new float[n * n];
            var grime = new float[n * n];
            var cols = new Color[4][];
            for (int i = 0; i < 4; i++) cols[i] = new Color[n * n];
            var mg = new Color[n * n];

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    int i = y * n + x;
                    float big = Fbm(u, v, 3, 5, 11);
                    float mid = Fbm(u, v, 9, 4, 23);
                    float fine = Fbm(u, v, 48, 3, 37);
                    float speck = Hash(x, y, 5);

                    // rust blooms: thresholded low-freq noise, broken up by mid-freq
                    float r = SS(0.50f - rustBias, 0.70f - rustBias, big * 0.7f + mid * 0.5f - 0.14f);
                    r = Mathf.Clamp01(r * (0.65f + 0.7f * fine));
                    // pitting / speckled rust
                    float pits = SS(0.62f, 0.8f, fine) * 0.6f;
                    r = Mathf.Clamp01(Mathf.Max(r, pits * SS(0.3f, 0.6f, big)));
                    rust[i] = r;

                    // vertical grime streaks
                    float streak = Fbm(u * 0.15f, v, 28, 3, 71);
                    grime[i] = SS(0.45f, 0.75f, streak) * (0.4f + 0.6f * (1f - v));

                    // scratches: thin bright lines
                    float sc = Fbm(u * 0.04f + v * 0.5f, v * 0.9f, 60, 2, 91);
                    scratch[i] = SS(0.74f, 0.78f, sc) * 0.6f;

                    // height: pits where rusty, flaky blisters
                    height[i] = fine * 0.5f + mid * 0.25f + r * 0.35f * (0.5f + speck * 0.5f);
                }

            // albedo variants + metal/gloss
            Color gunA = new Color(0.105f, 0.105f, 0.10f), gunB = new Color(0.20f, 0.195f, 0.18f);
            Color darkA = new Color(0.055f, 0.055f, 0.052f), darkB = new Color(0.125f, 0.12f, 0.11f);
            Color redA = new Color(0.36f, 0.035f, 0.03f), redB = new Color(0.55f, 0.07f, 0.05f);
            Color yelA = new Color(0.50f, 0.37f, 0.04f), yelB = new Color(0.72f, 0.56f, 0.09f);
            Color rustA = new Color(0.22f, 0.095f, 0.04f), rustB = new Color(0.40f, 0.19f, 0.065f), rustDeep = new Color(0.10f, 0.045f, 0.022f);

            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    float u = (float)x / n, v = (float)y / n;
                    float mottle = Fbm(u, v, 14, 3, 53) * 0.85f + Fbm(u, v, 60, 3, 29) * 0.3f;
                    float r = rust[i];
                    Color rustCol = Color.Lerp(Color.Lerp(rustDeep, rustA, mottle), rustB, Hash(x / 2, y / 2, 3) * r);
                    float g = grime[i];
                    for (int k = 0; k < 4; k++)
                    {
                        Color baseA = k == 0 ? gunA : (k == 1 ? darkA : (k == 2 ? redA : yelA));
                        Color baseB = k == 0 ? gunB : (k == 1 ? darkB : (k == 2 ? redB : yelB));
                        Color c = Color.Lerp(baseA, baseB, mottle);
                        // red paint chips away to rust/steel more readily
                        float rr = k == 2 ? Mathf.Clamp01(r * 1.3f - 0.1f) : (k == 3 ? Mathf.Clamp01(r * 1.6f - 0.05f) : r);
                        c = Color.Lerp(c, rustCol, rr * 0.8f);
                        c = Color.Lerp(c, c * 0.45f, g * 0.7f);
                        c += Color.white * scratch[i] * 0.07f;
                        c.a = 1f;
                        cols[k][i] = c;
                    }
                    float clean = 1f - Mathf.Clamp01(r * 1.2f + g * 0.3f);
                    float metal = Mathf.Lerp(0.1f, 0.75f, clean);
                    float smooth = Mathf.Lerp(0.08f, 0.34f, clean) + scratch[i] * 0.12f - g * 0.1f;
                    mg[i] = new Color(metal, 0f, 0f, Mathf.Clamp01(smooth));
                }

            // normal map from height (wrap-around Sobel)
            var nm = new Color[n * n];
            float strength = 1.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int xl = (x - 1 + n) % n, xr = (x + 1) % n, yd = (y - 1 + n) % n, yu = (y + 1) % n;
                    float dx = (height[y * n + xr] - height[y * n + xl]) * strength;
                    float dy = (height[yu * n + x] - height[yd * n + x]) * strength;
                    Vector3 nv = new Vector3(-dx, -dy, 1f).normalized;
                    nm[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f, 1f);
                }

            var set = new MetalSet
            {
                Gun = Save(dir, prefix + "_Gun", cols[0], n, n, false, false),
                Dark = Save(dir, prefix + "_Dark", cols[1], n, n, false, false),
                Red = Save(dir, prefix + "_RedPaint", cols[2], n, n, false, false),
                Yellow = Save(dir, prefix + "_YellowPaint", cols[3], n, n, false, false),
                Normal = Save(dir, prefix + "_Normal", nm, n, n, true, false),
                MetalGloss = Save(dir, prefix + "_MetalGloss", mg, n, n, false, true),
            };
            return set;
        }

        // ---------- simple 2D canvas for screens/decals ----------
        public sealed class Canvas
        {
            public readonly int W, H;
            public readonly Color[] Px;
            public Canvas(int w, int h, Color fill) { W = w; H = h; Px = new Color[w * h]; for (int i = 0; i < Px.Length; i++) Px[i] = fill; }

            public void Blend(int x, int y, Color c, float a = 1f)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x; float t = Mathf.Clamp01(c.a * a);
                Px[i] = new Color(Mathf.Lerp(Px[i].r, c.r, t), Mathf.Lerp(Px[i].g, c.g, t), Mathf.Lerp(Px[i].b, c.b, t), Mathf.Max(Px[i].a, t));
            }
            public void Add(int x, int y, Color c, float a = 1f)
            {
                if (x < 0 || y < 0 || x >= W || y >= H) return;
                int i = y * W + x;
                Px[i] = new Color(Px[i].r + c.r * a, Px[i].g + c.g * a, Px[i].b + c.b * a, 1f);
            }
            public void Line(float x0, float y0, float x1, float y1, Color c, float width = 1.5f, bool add = false)
            {
                float dx = x1 - x0, dy = y1 - y0; int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) * 2f) + 1;
                for (int s = 0; s <= steps; s++)
                {
                    float t = (float)s / steps; Dot(x0 + dx * t, y0 + dy * t, c, width, add);
                }
            }
            public void Dot(float cx, float cy, Color c, float width, bool add = false)
            {
                int r = Mathf.CeilToInt(width);
                for (int y = -r; y <= r; y++)
                    for (int x = -r; x <= r; x++)
                    {
                        float d = Mathf.Sqrt(x * x + y * y); float a = Mathf.Clamp01(width * 0.5f + 0.5f - d);
                        if (a <= 0) continue;
                        int px = Mathf.RoundToInt(cx) + x, py = Mathf.RoundToInt(cy) + y;
                        if (add) Add(px, py, c, a); else Blend(px, py, c, a);
                    }
            }
            public void Ring(float cx, float cy, float r, Color c, float width = 1.5f, bool add = false, float a0 = 0f, float a1 = 360f)
            {
                int steps = Mathf.CeilToInt(r * 6f);
                for (int s = 0; s <= steps; s++)
                {
                    float a = Mathf.Lerp(a0, a1, (float)s / steps) * Mathf.Deg2Rad;
                    Dot(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, c, width, add);
                }
            }
            public void FillRect(int x0, int y0, int x1, int y1, Color c)
            {
                for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) Blend(x, y, c);
            }
            public void FillEllipse(float cx, float cy, float rx, float ry, Color c, float soft = 2f, bool add = false)
            {
                int x0 = Mathf.FloorToInt(cx - rx - soft), x1 = Mathf.CeilToInt(cx + rx + soft), y0 = Mathf.FloorToInt(cy - ry - soft), y1 = Mathf.CeilToInt(cy + ry + soft);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float dx = (x - cx) / rx, dy = (y - cy) / ry; float d = (Mathf.Sqrt(dx * dx + dy * dy) - 1f) * Mathf.Min(rx, ry);
                        float a = Mathf.Clamp01(0.5f - d / soft);
                        if (a <= 0) continue;
                        if (add) Add(x, y, c, a); else Blend(x, y, c, a);
                    }
            }
            public Texture2D ToTexture(string name, bool linear = false)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, false, linear) { name = name };
                t.SetPixels(Px); t.Apply(false);
                return t;
            }
        }

        // ---------- CRT screens ----------
        static void CrtFinish(Canvas c, Color tint, int seed)
        {
            for (int y = 0; y < c.H; y++)
                for (int x = 0; x < c.W; x++)
                {
                    int i = y * c.W + x;
                    float u = (x + 0.5f) / c.W * 2f - 1f, v = (y + 0.5f) / c.H * 2f - 1f;
                    float vign = 1f - 0.45f * (u * u * 0.8f + v * v);
                    float scan = 0.88f + 0.12f * Mathf.Sin(y * Mathf.PI);
                    float noise = 0.96f + 0.08f * Hash(x, y, seed);
                    float flick = 1f + 0.04f * Mathf.Sin(y * 0.05f);
                    var p = c.Px[i];
                    p = new Color(p.r * tint.r, p.g * tint.g, p.b * tint.b, 1f) * (vign * scan * noise * flick);
                    p.a = 1f; c.Px[i] = p;
                }
        }

        public static Texture2D XRayScreen(string dir)
        {
            int W = 512, H = 384;
            var c = new Canvas(W, H, new Color(0, 0, 0, 1f));
            float cx = 190f, cy = 190f;
            // base density in the red channel
            for (int y = 14; y < H - 24; y++)
                for (int x = 14; x < 362; x++)
                {
                    float dx = (x - cx) / 160f, dy = (y - (cy - 6f)) / 165f;
                    float body = SS(1.02f, 0.78f, Mathf.Sqrt(dx * dx * 0.9f + dy * dy * 0.62f));
                    float shoulders = SS(40f, 0f, Mathf.Abs(y - (cy + 138f)) * 0.5f + Mathf.Max(0f, Mathf.Abs(x - cx) - 120f)) * 0.6f;
                    float d = 0.07f + 0.30f * body;
                    float ll = SS(1f, 0.55f, Mathf.Sqrt(Mathf.Pow((x - (cx - 58f)) / 54f, 2) + Mathf.Pow((y - (cy + 4f)) / 104f, 2)));
                    float rl = SS(1f, 0.55f, Mathf.Sqrt(Mathf.Pow((x - (cx + 56f)) / 52f, 2) + Mathf.Pow((y - (cy + 4f)) / 100f, 2)));
                    d -= 0.17f * Mathf.Max(ll, rl) * body;
                    d += 0.22f * SS(9f, 1.5f, Mathf.Abs(x - cx)) * body * SS(-130f, 100f, y - cy + 20f);
                    d += 0.20f * SS(1f, 0.45f, Mathf.Sqrt(Mathf.Pow((x - (cx + 26f)) / 44f, 2) + Mathf.Pow((y - (cy - 48f)) / 46f, 2)));
                    d += 0.10f * shoulders;
                    d += 0.03f * (Hash(x, y, 13) - 0.5f) + 0.05f * Fbm(x / 512f, y / 384f, 6, 3, 5);
                    c.Px[y * W + x] = new Color(Mathf.Clamp01(d), 0, 0, 1);
                }
            // ribs (10 per side), clavicles, vertebrae drawn as additive density lines
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 10; k++)
                {
                    float y0 = cy + 112f - k * 15.5f; float prevX = cx + side * 9f, prevY = y0;
                    for (int st = 1; st <= 28; st++)
                    {
                        float t = st / 28f; float ang = t * Mathf.PI * 0.5f;
                        float rx = (30f + k * 1.5f) + (92f - Mathf.Abs(k - 4) * 3.2f) * Mathf.Sin(ang);
                        float xx = cx + side * (9f + rx * 0.92f);
                        float yy = y0 - t * (22f + k * 3.5f) - 7f * Mathf.Sin(ang * 2f);
                        float fade = Mathf.Sin(t * Mathf.PI * 0.95f + 0.1f);
                        c.Line(prevX, prevY, xx, yy, new Color(0.36f * Mathf.Clamp01(fade + 0.25f), 0, 0, 1), 2.6f, true);
                        prevX = xx; prevY = yy;
                    }
                }
                c.Line(cx + side * 10f, cy + 128f, cx + side * 112f, cy + 118f, new Color(0.35f, 0, 0, 1), 3.2f, true);
                c.Line(cx + side * 112f, cy + 118f, cx + side * 126f, cy + 98f, new Color(0.28f, 0, 0, 1), 2.4f, true);
            }
            for (int v = 0; v < 18; v++) c.Dot(cx, cy + 126f - v * 11.5f, new Color(0.18f, 0, 0, 1), 4.2f, true);
            c.Line(cx, cy + 132f, cx, cy + 72f, new Color(-0.12f, 0, 0, 1), 3.5f, true);

            // colour map (density -> cyan/white film)
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float d = Mathf.Clamp01(c.Px[y * W + x].r);
                    var col = Color.Lerp(new Color(0.01f, 0.07f, 0.1f), new Color(0.06f, 0.42f, 0.55f), Mathf.Pow(d * 1.8f, 1.1f));
                    col = Color.Lerp(col, new Color(0.9f, 1f, 1f), Mathf.Pow(Mathf.Clamp01(d * 1.55f - 0.35f), 1.6f));
                    col.a = 1f; c.Px[y * W + x] = col;
                }
            var line = new Color(0.35f, 0.85f, 0.95f, 0.9f);
            c.Line(368, 14, 368, H - 14, line * 0.6f, 1f);
            float bx = 436f, by = 60f; var sil = new Color(0.5f, 0.9f, 1f, 0.95f);
            c.Ring(bx, by + 214, 8, sil, 1.2f); c.Line(bx, by + 204, bx, by + 150, sil, 1.2f);
            c.Line(bx - 14, by + 196, bx + 14, by + 196, sil, 1.2f); c.Line(bx - 14, by + 196, bx - 20, by + 150, sil, 1.2f); c.Line(bx + 14, by + 196, bx + 20, by + 150, sil, 1.2f);
            c.Line(bx, by + 150, bx - 8, by + 70, sil, 1.2f); c.Line(bx, by + 150, bx + 8, by + 70, sil, 1.2f);
            c.Line(bx - 10, by + 150, bx + 10, by + 150, sil, 1.2f);
            for (int k = 0; k < 4; k++)
            {
                float x0 = k % 2 == 0 ? 24 : 346, y0 = k < 2 ? 24 : H - 36; float sx = k % 2 == 0 ? 1 : -1, sy = k < 2 ? 1 : -1;
                c.Line(x0, y0, x0 + 18 * sx, y0, line, 1.2f); c.Line(x0, y0, x0, y0 + 18 * sy, line, 1.2f);
            }
            CrtFinish(c, new Color(0.85f, 1f, 1f), 3);
            return Save(dir, "Screen_XRay", c.Px, W, H, false, false);
        }

        public static Texture2D BioscanScreen(string dir)
        {
            int W = 512, H = 384;
            var c = new Canvas(W, H, new Color(0.01f, 0.05f, 0.07f, 1f));
            var cy1 = new Color(0.35f, 0.9f, 1f, 0.9f); var green = new Color(0.25f, 1f, 0.5f, 1f);
            // left: wire-frame body scan
            c.FillRect(18, 18, 190, H - 28, new Color(0.02f, 0.12f, 0.16f, 0.9f));
            for (int g = 0; g < 14; g++) { c.Line(18 + g * 12.4f, 18, 18 + g * 12.4f, H - 28, cy1 * 0.18f, 1f); }
            for (int g = 0; g < 22; g++) { c.Line(18, 18 + g * 16f, 190, 18 + g * 16f, cy1 * 0.18f, 1f); }
            float bx = 104f; float top = 340f;
            c.Ring(bx, top - 22, 20, cy1, 1.2f);
            c.Line(bx - 24, top - 52, bx + 24, top - 52, cy1, 1.2f);
            c.Line(bx - 24, top - 52, bx - 40, top - 140, cy1, 1.2f); c.Line(bx + 24, top - 52, bx + 40, top - 140, cy1, 1.2f);
            c.Line(bx - 24, top - 52, bx - 20, top - 160, cy1, 1.2f); c.Line(bx + 24, top - 52, bx + 20, top - 160, cy1, 1.2f);
            c.Line(bx - 20, top - 160, bx - 26, top - 290, cy1, 1.2f); c.Line(bx + 20, top - 160, bx + 26, top - 290, cy1, 1.2f);
            c.Line(bx - 4, top - 160, bx - 8, top - 290, cy1, 1.2f); c.Line(bx + 4, top - 160, bx + 8, top - 290, cy1, 1.2f);
            for (int k = 0; k < 8; k++) c.Line(bx - 21 + k * 0.5f, top - 60 - k * 12, bx + 21 - k * 0.5f, top - 60 - k * 12, cy1 * 0.55f, 1f);
            // mid: vitals boxes + traces
            for (int k = 0; k < 3; k++) c.FillRect(204, 270 - k * 80, 352, 340 - k * 80 - 6, new Color(0.02f, 0.1f, 0.13f, 0.9f));
            for (int k = 0; k < 2; k++)
            {
                float y0 = 190f - k * 62f; float prev = y0;
                for (int x = 214; x < 348; x++)
                {
                    float t = (x - 214) / 134f; float ph = (t * 4f + k * 0.37f) % 1f;
                    float wave = ph > 0.45f && ph < 0.5f ? 26f : (ph > 0.5f && ph < 0.54f ? -12f : (ph > 0.62f && ph < 0.74f ? 5f * Mathf.Sin((ph - 0.62f) / 0.12f * Mathf.PI) : 0f));
                    float yy = y0 + wave + (Hash(x, k, 4) - 0.5f) * 1.2f;
                    c.Line(x - 1, prev, x, yy, green, 1.1f); prev = yy;
                }
            }
            // right: brain slice discs
            for (int r = 0; r < 2; r++)
                for (int q = 0; q < 2; q++)
                {
                    float px = 410f + q * 46f - 22f, py = 96f + r * 104f;
                    for (int yy = (int)py - 42; yy <= (int)py + 42; yy++)
                        for (int xx = (int)px - 38; xx <= (int)px + 38; xx++)
                        {
                            float ddx = (xx - px) / 35f, ddy = (yy - py) / 39f; float rr = Mathf.Sqrt(ddx * ddx + ddy * ddy);
                            if (rr > 1f) continue;
                            float gyri = Fbm(xx / 40f + r * 0.3f, yy / 40f + q * 0.7f, 5, 3, 31);
                            float fold = Mathf.Abs(Mathf.Sin(gyri * 22f));
                            float v = Mathf.Lerp(0.55f, 0.15f, SS(0.2f, 0.9f, fold)) * (1f - SS(0.75f, 1f, rr) * 0.3f);
                            float skull = SS(0.86f, 0.94f, rr);
                            float fissure = SS(0.05f, 0f, Mathf.Abs(ddx)) * 0.35f;
                            c.Blend(xx, yy, new Color(0.25f + v * 0.9f, 0.55f + v, 0.65f + v * 0.8f, 1f), 0.95f);
                            c.Blend(xx, yy, new Color(0.9f, 1f, 1f, 1f), skull * 0.6f);
                            c.Blend(xx, yy, new Color(0f, 0.05f, 0.08f, 1f), fissure);
                        }
                    c.Ring(px, py, 34f, cy1, 1.2f);
                }
            // small status bars
            for (int k = 0; k < 5; k++) c.FillRect(212, 36 + k * 11, 212 + 30 + (int)(Hash(k, 1, 2) * 60), 41 + k * 11, new Color(0.2f, 0.9f, 0.5f, 0.55f));
            CrtFinish(c, new Color(0.85f, 1f, 1f), 9);
            return Save(dir, "Screen_Bioscan", c.Px, W, H, false, false);
        }

        // ---------- gauge face ----------
        public static Texture2D GaugeFace(string dir, string name, float redFrom)
        {
            int S = 256; var c = new Canvas(S, S, new Color(0.62f, 0.55f, 0.38f, 1f));
            // aged paper mottle
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
                {
                    float m = Fbm(x / (float)S, y / (float)S, 6, 4, 61);
                    float d = Mathf.Sqrt((x - S / 2f) * (x - S / 2f) + (y - S / 2f) * (y - S / 2f)) / (S / 2f);
                    var col = Color.Lerp(new Color(0.55f, 0.46f, 0.29f), new Color(0.78f, 0.70f, 0.50f), m);
                    col *= 1f - 0.45f * SS(0.55f, 1f, d);
                    col.a = 1f; c.Px[y * S + x] = col;
                }
            var ink = new Color(0.07f, 0.06f, 0.05f, 1f); var red = new Color(0.6f, 0.08f, 0.05f, 1f);
            float cx = S / 2f, cy = S / 2f;
            c.Ring(cx, cy, 98f, red, 5f, false, -45f, 225f - redFrom * 270f);
            for (int t = 0; t <= 20; t++)
            {
                float a = (225f - t * (270f / 20f)) * Mathf.Deg2Rad; float major = (t % 5 == 0) ? 1f : 0f;
                float r0 = 104f - (major > 0 ? 14f : 7f), r1 = 106f; var col = (t / 20f >= redFrom) ? red : ink;
                c.Line(cx + Mathf.Cos(a) * r0, cy + Mathf.Sin(a) * r0, cx + Mathf.Cos(a) * r1, cy + Mathf.Sin(a) * r1, col, major > 0 ? 2.5f : 1.3f);
            }
            c.Ring(cx, cy, 107f, ink, 1.5f);
            c.Ring(cx, cy, 120f, ink * 0.8f, 2f);
            return Save(dir, name, c.Px, S, S, false, false);
        }

        // ---------- biohazard stencil (alpha) ----------
        public static Texture2D Biohazard(string dir, string name, Color paint)
        {
            int S = 256; var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = ((x + 0.5f) / S * 2f - 1f) * 0.74f, v = ((y + 0.5f) / S * 2f - 1f) * 0.74f;
                    float a = 0f; float rr = Mathf.Sqrt(u * u + v * v);
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = (90f + k * 120f) * Mathf.Deg2Rad; var dir2 = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                        float dO = Vector2.Distance(new Vector2(u, v), dir2 * 0.30f);
                        float dI = Vector2.Distance(new Vector2(u, v), dir2 * 0.20f);
                        if (dO < 0.40f && dI > 0.31f) a = 1f;
                        float eye = Vector2.Distance(new Vector2(u, v), dir2 * 0.50f);
                        if (eye < 0.075f) a = 0f;
                        if (eye > 0.075f && eye < 0.115f && dO < 0.40f && dI > 0.31f) a = 1f;
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float ang = (30f + k * 120f) * Mathf.Deg2Rad;
                        float along = u * Mathf.Cos(ang) + v * Mathf.Sin(ang);
                        float perp = Mathf.Abs(-u * Mathf.Sin(ang) + v * Mathf.Cos(ang));
                        if (along > 0.05f && perp < 0.045f) a = 0f;
                    }
                    if (rr < 0.17f) a = 0f;
                    if (rr > 0.10f && rr < 0.15f) a = 1f;
                    if (rr < 0.05f) a = 1f;
                    if (rr > 0.74f) a = 0f;
                    float wear = Fbm((float)x / S, (float)y / S, 10, 4, 77);
                    a *= SS(0.28f, 0.45f, wear + Hash(x, y, 8) * 0.2f);
                    px[y * S + x] = new Color(paint.r, paint.g, paint.b, a);
                }
            return Save(dir, name, px, S, S, false, false, true);
        }

        public static Texture2D HazardStripes(string dir)
        {
            int W = 256, H = 64; var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float t = ((x + y) / 22f) % 2f; bool on = t < 1f;
                    var c = on ? new Color(0.72f, 0.56f, 0.07f) : new Color(0.07f, 0.06f, 0.05f);
                    float wear = Fbm(x / (float)W, y / (float)H, 8, 4, 19);
                    c = Color.Lerp(c, new Color(0.25f, 0.1f, 0.05f), SS(0.55f, 0.8f, wear));
                    c.a = 1f; px[y * W + x] = c;
                }
            return Save(dir, "HazardStripes", px, W, H, false, false);
        }

        public static Texture2D Paper(string dir)
        {
            int W = 256, H = 320; var c = new Canvas(W, H, Color.white);
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
                {
                    float m = Fbm(x / (float)W, y / (float)H, 5, 5, 41);
                    var col = Color.Lerp(new Color(0.55f, 0.5f, 0.38f), new Color(0.86f, 0.82f, 0.68f), m);
                    float edge = Mathf.Min(Mathf.Min(x, W - x), Mathf.Min(y, H - y)) / 24f;
                    col *= Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(edge));
                    col.a = 1f; c.Px[y * W + x] = col;
                }
            var ink = new Color(0.12f, 0.1f, 0.08f, 0.85f);
            c.Line(20, 256, 236, 256, ink, 1.8f);
            for (int k = 0; k < 5; k++)
            {
                float y = 222f - k * 34f;
                c.Line(38, y - 2, 224, y - 2, ink * 0.5f, 1f);
                c.Ring(40, y + 6, 6, ink, 1.3f, false, 0f, 360f);
                c.Line(34, y + 6, 40, y, ink, 1.8f); c.Line(40, y, 52, y + 14, ink, 1.8f);
            }
            return Save(dir, "Paper_Checklist", c.Px, W, H, false, false);
        }


        // ---------- door textures ----------
        public static Texture2D Stripes(string dir, string name, int W, int H, Color yellow, float period = 46f)
        {
            var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float t = ((x + y) / period) % 2f; bool on = t < 1f;
                    var c = on ? yellow : new Color(0.045f, 0.04f, 0.035f);
                    float wear = Fbm(x / (float)W * 3f, y / (float)H, 8, 4, 19);
                    float chips = SS(0.52f, 0.72f, wear + Hash(x / 2, y / 2, 3) * 0.25f);
                    c = Color.Lerp(c, new Color(0.16f, 0.09f, 0.05f), chips * 0.9f);
                    c.a = 1f; px[y * W + x] = c;
                }
            return Save(dir, name, px, W, H, false, false);
        }

        public static Texture2D WireMesh(string dir)
        {
            int S = 256; var px = new Color[S * S];
            float p = 24f;
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float a1 = Mathf.Abs(((x + y) % p) / p - 0.5f), a2 = Mathf.Abs((((x - y) % p) + p) % p / p - 0.5f);
                    float l1 = a1 > 0.43f ? 1f : 0f, l2 = a2 > 0.43f ? 1f : 0f;
                    float a = Mathf.Max(l1, l2);
                    float shade = 0.18f + 0.25f * Fbm(x / (float)S, y / (float)S, 8, 3, 7);
                    px[y * S + x] = new Color(shade, shade * 0.95f, shade * 0.9f, a);
                }
            return Save(dir, "WireMesh", px, S, S, false, false, true);
        }

        public static Texture2D WindowGlow(string dir)
        {
            int W = 256, H = 160; var px = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float dx = (u - 0.5f) / 0.16f, dy = (v - 0.9f) / 0.05f;
                    float lamp = Mathf.Exp(-(dx * dx + dy * dy * 0.6f));
                    float cone = Mathf.Exp(-Mathf.Pow((u - 0.5f) / (0.15f + (1f - v) * 0.35f), 2f)) * Mathf.Pow(v, 1.5f) * 0.5f;
                    float vign = 0.25f + 0.75f * (1f - SS(0.55f, 1f, Mathf.Max(Mathf.Abs(u - 0.5f) * 1.6f, Mathf.Abs(v - 0.5f) * 1.4f)));
                    float b = Mathf.Clamp01((lamp * 1.3f + cone) * vign + 0.02f);
                    px[y * W + x] = new Color(b * 0.85f, b * 0.95f, b, 1f);
                }
            return Save(dir, "WindowGlow", px, W, H, false, false);
        }

        // ---------- IO ----------
        public static Texture2D Save(string dir, string name, Color[] px, int w, int h, bool normal, bool linear, bool alphaTransparency = false)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.SetPixels(px); t.Apply(false);
            var bytes = t.EncodeToPNG(); UnityEngine.Object.DestroyImmediate(t);
            Directory.CreateDirectory(dir);
            string path = dir + "/" + name + ".png";
            File.WriteAllBytes(path, bytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            imp.sRGBTexture = !(normal || linear);
            imp.alphaIsTransparency = alphaTransparency;
            imp.mipmapEnabled = true;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.filterMode = FilterMode.Bilinear;
            imp.anisoLevel = 8;
            imp.maxTextureSize = 2048;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
