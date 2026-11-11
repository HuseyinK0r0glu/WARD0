using System;
using System.Collections.Generic;
using UnityEngine;
using Ring = Ward0.EditorTools.SkinMesh.Ring;

namespace Ward0.EditorTools
{
    /// <summary>Shared description of one character build (miner or patrol): detail level, pose warp and which gear is modelled.</summary>
    internal sealed class CharSpec
    {
        public string name;
        public float q = 1f;              // detail multiplier (1 = LOD0)
        public float hunchDeg;            // spine bend baked into the bind pose
        public float headUpDeg;           // counter-rotation of the head against the hunch
        public float armLen = 1f;
        public bool rolledSleeves;        // forearms bare (veined skin) under rolled cuffs
        public bool leatherHarness;       // brown leather suspenders + chest strap
        public bool hiVisBraces;          // reflective vertical braces over the shoulders
        public bool bands = true;         // horizontal reflective bands (chest, upper arm, forearm, thigh, ankle)
        public bool fine = true;          // pockets, fingers, pouches, straps (off for the far LODs)
        public bool Mid => q >= 0.15f;
        public bool Lowest => q < 0.1f;
        public CharSpec Clone() => (CharSpec)MemberwiseClone();
    }

    /// <summary>Humanoid skeleton layout (Mixamo naming, world-aligned bind pose) and all body / head / hat geometry.</summary>
    internal static class CharacterGeo
    {
        // ------------------------------------------------------------ skeleton
        public enum B { Hips, Spine, Spine1, Spine2, Neck, Head, HeadTop, LShoulder, LArm, LFore, LHand, RShoulder, RArm, RFore, RHand, LUpLeg, LLeg, LFoot, LToe, LToeEnd, RUpLeg, RLeg, RFoot, RToe, RToeEnd, Count }
        public static readonly string[] BoneName =
        {
            "Hips", "Spine", "Spine1", "Spine2", "Neck", "Head", "HeadTop_End", "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand",
            "RightShoulder", "RightArm", "RightForeArm", "RightHand", "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "LeftToe_End",
            "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase", "RightToe_End"
        };
        public static readonly int[] BoneParent = { -1, 0, 1, 2, 3, 4, 5, 3, 7, 8, 9, 3, 11, 12, 13, 0, 15, 16, 17, 18, 0, 20, 21, 22, 23 };

        public static Vector3 HeadCenter => new Vector3(0f, 1.685f, 0.008f);
        const float Hip = 0.98f;

        sealed class Arm { public Vector3 shoulder, elbow, wrist, d1, d2; }

        static Arm MakeArm(float sx, CharSpec sp)
        {
            var a = new Arm { shoulder = new Vector3(sx * 0.185f, 1.44f, 0f) };
            a.d1 = new Vector3(sx * Mathf.Sin(27f * Mathf.Deg2Rad), -Mathf.Cos(27f * Mathf.Deg2Rad), 0f);
            a.d2 = new Vector3(sx * 0.32f, -0.92f, 0.13f).normalized;
            a.elbow = a.shoulder + a.d1 * (0.29f * sp.armLen); a.wrist = a.elbow + a.d2 * (0.26f * sp.armLen);
            return a;
        }

        /// <summary>Bind-pose joint positions (before the hunch warp).</summary>
        public static Vector3[] BonePositions(CharSpec sp)
        {
            var p = new Vector3[(int)B.Count];
            p[(int)B.Hips] = new Vector3(0, 0.97f, 0); p[(int)B.Spine] = new Vector3(0, 1.04f, 0); p[(int)B.Spine1] = new Vector3(0, 1.17f, 0); p[(int)B.Spine2] = new Vector3(0, 1.32f, 0);
            p[(int)B.Neck] = new Vector3(0, 1.50f, 0.005f); p[(int)B.Head] = new Vector3(0, 1.59f, 0.01f); p[(int)B.HeadTop] = new Vector3(0, 1.80f, 0.01f);
            foreach (float sx in new[] { -1f, 1f })
            {
                var arm = MakeArm(sx, sp); int o = sx < 0 ? 0 : 4;
                p[(int)B.LShoulder + o] = new Vector3(sx * 0.04f, 1.46f, 0f); p[(int)B.LArm + o] = arm.shoulder; p[(int)B.LFore + o] = arm.elbow; p[(int)B.LHand + o] = arm.wrist;
                int l = sx < 0 ? 0 : 5;
                p[(int)B.LUpLeg + l] = new Vector3(sx * 0.09f, 0.93f, 0); p[(int)B.LLeg + l] = new Vector3(sx * 0.09f, 0.50f, 0.008f); p[(int)B.LFoot + l] = new Vector3(sx * 0.09f, 0.095f, 0f);
                p[(int)B.LToe + l] = new Vector3(sx * 0.09f, 0.03f, 0.14f); p[(int)B.LToeEnd + l] = new Vector3(sx * 0.09f, 0.03f, 0.22f);
            }
            return p;
        }

        // ------------------------------------------------------------ pose warp (hunch)
        static float SS(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        public static float WarpAngle(CharSpec sp, float y) => (sp.hunchDeg * SS(Hip, 1.52f, y) - sp.headUpDeg * SS(1.50f, 1.62f, y)) * Mathf.Deg2Rad;

        public static Vector3 WarpPoint(CharSpec sp, Vector3 p)
        {
            if (Mathf.Approximately(sp.hunchDeg, 0f) && Mathf.Approximately(sp.headUpDeg, 0f)) return p;
            float al = WarpAngle(sp, p.y), c = Mathf.Cos(al), s = Mathf.Sin(al);
            float ry = p.y - Hip;
            return new Vector3(p.x, Hip + ry * c - p.z * s, ry * s + p.z * c);
        }

        public static Vector3 WarpDir(CharSpec sp, Vector3 at, Vector3 d)
        {
            float al = WarpAngle(sp, at.y), c = Mathf.Cos(al), s = Mathf.Sin(al);
            return new Vector3(d.x, d.y * c - d.z * s, d.y * s + d.z * c);
        }

        public static void Warp(SkinMesh m, CharSpec sp) => m.Warp(p => WarpPoint(sp, p), (p, n) => WarpDir(sp, p, n));

        // ------------------------------------------------------------ helpers
        static BoneWeight W(B b) => SkinMesh.BW((int)b);
        static BoneWeight W2(B a, B b, float t) => SkinMesh.BW((int)a, 1f - t, (int)b);

        static readonly float[] TJoint = { 0.97f, 1.04f, 1.17f, 1.32f, 1.50f };
        static readonly B[] TBone = { B.Hips, B.Spine, B.Spine1, B.Spine2, B.Neck };

        /// <summary>Torso skin weights by height: blends along the spine chain.</summary>
        public static BoneWeight TW(float y)
        {
            if (y <= TJoint[0]) return W(B.Hips);
            for (int i = 0; i < 4; i++)
                if (y < TJoint[i + 1]) return W2(TBone[i], TBone[i + 1], SS(TJoint[i], TJoint[i + 1], y));
            return W(B.Neck);
        }

        static BoneWeight Chain(B a, B b, B c, float s, float j1, float j2, float h = 0.05f)
        {
            if (s < j1 + h) return W2(a, b, SS(j1 - h, j1 + h, s));
            return W2(b, c, SS(j2 - h * 0.6f, j2 + h * 0.6f, s));
        }

        static readonly float[] TY = { 0.74f, 0.82f, 0.92f, 1.00f, 1.10f, 1.22f, 1.32f, 1.40f, 1.45f, 1.49f, 1.52f, 1.55f };
        static readonly float[] TA = { 0.115f, 0.168f, 0.185f, 0.172f, 0.160f, 0.172f, 0.188f, 0.195f, 0.168f, 0.108f, 0.060f, 0.050f };
        static readonly float[] TB2 = { 0.085f, 0.105f, 0.112f, 0.108f, 0.104f, 0.116f, 0.122f, 0.116f, 0.098f, 0.080f, 0.056f, 0.048f };
        static readonly float[] TZ = { 0f, -0.005f, -0.010f, 0f, 0.004f, 0.010f, 0.012f, 0.005f, -0.005f, -0.008f, 0f, 0f };

        public static void TorsoAt(float y, out float a, out float b, out float cz)
        {
            y = Mathf.Clamp(y, TY[0], TY[TY.Length - 1]); int i = 0;
            while (i < TY.Length - 2 && y > TY[i + 1]) i++;
            float t = SS(TY[i], TY[i + 1], y);
            a = Mathf.Lerp(TA[i], TA[i + 1], t); b = Mathf.Lerp(TB2[i], TB2[i + 1], t); cz = Mathf.Lerp(TZ[i], TZ[i + 1], t);
        }

        /// <summary>Point on the torso surface (th = 0 front, +pi/2 = +X) pushed outward by off.</summary>
        public static Vector3 TorsoSurf(float y, float th, float off)
        {
            TorsoAt(y, out float a, out float b, out float cz);
            return new Vector3((a + off) * Mathf.Sin(th), y, cz + (b + off) * Mathf.Cos(th));
        }

        static Vector3 TorsoNormal(float y, float th)
        {
            var p = TorsoSurf(y, th, 0f); var dy = TorsoSurf(y + 0.01f, th, 0f) - TorsoSurf(y - 0.01f, th, 0f); var dt = TorsoSurf(y, th + 0.02f, 0f) - TorsoSurf(y, th - 0.02f, 0f);
            var n = Vector3.Cross(dt, dy).normalized; var outward = new Vector3(p.x, 0f, p.z - 0.005f);
            if (Vector3.Dot(n, outward) < 0f) n = -n; return n;
        }

        static float Hash01(int n) { unchecked { n = (n ^ 61) ^ (n >> 16); n *= 9; n ^= n >> 4; n *= 0x27d4eb2d; n ^= n >> 15; return (n & 0xFFFF) / 65535f; } }

        // ------------------------------------------------------------ limb sweeps
        sealed class Path
        {
            readonly Vector3[] _p; readonly float[] _l;
            public Path(params Vector3[] pts) { _p = pts; _l = new float[pts.Length]; for (int i = 1; i < pts.Length; i++) _l[i] = _l[i - 1] + (pts[i] - pts[i - 1]).magnitude; }
            public float Length => _l[_l.Length - 1];
            public Vector3 At(float s)
            {
                if (s <= 0f) return _p[0] + (_p[1] - _p[0]).normalized * s;
                for (int i = 1; i < _p.Length; i++) if (s <= _l[i]) return Vector3.Lerp(_p[i - 1], _p[i], (s - _l[i - 1]) / (_l[i] - _l[i - 1]));
                return _p[_p.Length - 1] + (_p[_p.Length - 1] - _p[_p.Length - 2]).normalized * (s - Length);
            }
            public Vector3 Tan(float s) => (At(s + 0.03f) - At(s - 0.03f)).normalized;
        }

        static Ring MakeRing(Path path, float s, float r, float ratio, float uvS, BoneWeight w, float n = 2f)
        {
            var c = path.At(s); var t = path.Tan(s); var v = (Vector3.forward - t * Vector3.Dot(Vector3.forward, t)).normalized; var u = Vector3.Cross(v, t).normalized;
            return new Ring { c = c, u = u, v = v, a = r, b = r * ratio, n = n, s = uvS, w = w };
        }

        static List<Ring> Rings(Path path, float[] ss, Func<float, float> rad, float ratio, Func<float, float> uvS, Func<float, BoneWeight> wf, float off = 0f)
        {
            var l = new List<Ring>(); foreach (float s in ss) l.Add(MakeRing(path, s, rad(s) + off, ratio, uvS(s), wf(s)));
            return l;
        }

        static float[] Range(float a, float b, int n) { var r = new float[n]; for (int i = 0; i < n; i++) r[i] = Mathf.Lerp(a, b, n == 1 ? 0f : (float)i / (n - 1)); return r; }
        static float[] Cat(params float[][] arrs) { var l = new List<float>(); foreach (var a in arrs) l.AddRange(a); return l.ToArray(); }

        static int Seg(CharSpec sp, int n) => Mathf.Max(6, Mathf.RoundToInt(n * Mathf.Sqrt(sp.q)));
        static int Cnt(CharSpec sp, int n) => Mathf.Max(3, Mathf.RoundToInt(n * Mathf.Sqrt(sp.q)));

        // slots of the body mesh
        public const int Skin = 0, Suit = 1, Band = 2, Gear = 3, Hardware = 4, SlotCount = 5;

        // ------------------------------------------------------------ body
        public static SkinMesh BuildBody(CharSpec sp)
        {
            var m = new SkinMesh(SlotCount); var bp = BonePositions(sp);
            Torso(m, sp); Neck(m, sp); Head(m, sp);
            foreach (float sx in new[] { -1f, 1f }) { Sleeve(m, sp, sx); Leg(m, sp, sx); Hand(m, sp, sx); Boot(m, sp, sx); }
            if (sp.fine) Webbing(m, sp);
            Warp(m, sp);
            return m;
        }

        static void Torso(SkinMesh m, CharSpec sp)
        {
            int rad = Seg(sp, 40); int n = Cnt(sp, 26);
            var ys = Range(0.74f, 1.55f, n);
            var rings = new List<Ring>();
            foreach (float y in ys) { TorsoAt(y, out float a, out float b, out float cz); rings.Add(new Ring { c = new Vector3(0, y, cz), u = Vector3.right, v = Vector3.forward, a = a, b = b, n = 2.3f, s = y - 0.74f, w = TW(y) }); }
            m.Loft(Suit, rings, rad, (t, s) => CharacterTex.SuitUV(CharacterTex.Part.Torso, t, s), 0f, Mathf.PI * 2f, true, true);
            // collar (thick lip, open at the top)
            var col = new List<Ring>();
            float[,] cl = { { 1.46f, 0.082f, 0.078f }, { 1.50f, 0.074f, 0.071f }, { 1.54f, 0.068f, 0.066f }, { 1.552f, 0.064f, 0.062f }, { 1.548f, 0.057f, 0.055f }, { 1.52f, 0.055f, 0.053f }, { 1.49f, 0.055f, 0.053f } };
            for (int i = 0; i < cl.GetLength(0); i++) col.Add(new Ring { c = new Vector3(0, cl[i, 0], 0.008f), u = Vector3.right, v = Vector3.forward, a = cl[i, 1], b = cl[i, 2], n = 2f, s = i * 0.02f, w = TW(cl[i, 0]) });
            if (sp.Mid) m.Loft(Suit, col, Seg(sp, 24), (t, s) => CharacterTex.MiscUV(0.05f + t * 0.5f, 0.55f + s * 0.4f));
            // reflective chest band, belt
            if (sp.bands) BandAround(m, sp, 1.215f, 1.275f, 0.004f);
            if (sp.fine)
            {
                var belt = new List<Ring>(); float[] by = { 0.980f, 0.986f, 1.03f, 1.040f }; float[] bo = { 0.002f, 0.010f, 0.010f, 0.002f };
                for (int i = 0; i < 4; i++) { TorsoAt(by[i], out float a, out float b, out float cz); belt.Add(new Ring { c = new Vector3(0, by[i], cz), u = Vector3.right, v = Vector3.forward, a = a + bo[i], b = b + bo[i], n = 2.3f, s = i * 0.02f, w = W(B.Spine) }); }
                m.Loft(Gear, belt, rad, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Leather, t, s * 5f));
            }
        }

        static void BandAround(SkinMesh m, CharSpec sp, float y0, float y1, float off)
        {
            var rings = new List<Ring>(); int k = 5;
            for (int i = 0; i < k; i++)
            {
                float y = Mathf.Lerp(y0, y1, (float)i / (k - 1)); float o = (i == 0 || i == k - 1) ? 0.001f : off;
                TorsoAt(y, out float a, out float b, out float cz);
                rings.Add(new Ring { c = new Vector3(0, y, cz), u = Vector3.right, v = Vector3.forward, a = a + o, b = b + o, n = 2.3f, s = y - y0, w = TW(y) });
            }
            m.Loft(Band, rings, Seg(sp, 40), (t, s) => CharacterTex.BandUV(t * 3.2f, s / (y1 - y0)));
        }

        static void Neck(SkinMesh m, CharSpec sp)
        {
            var rings = new List<Ring>(); int n = Cnt(sp, 8);
            foreach (float y in Range(1.44f, 1.64f, n))
                rings.Add(new Ring { c = new Vector3(0, y, -0.016f + (y - 1.44f) * 0.14f), u = Vector3.right, v = Vector3.forward, a = 0.049f, b = 0.050f, n = 2f, s = y - 1.44f, w = y < 1.53f ? W(B.Neck) : W2(B.Neck, B.Head, SS(1.53f, 1.60f, y)) });
            m.Loft(Skin, rings, Seg(sp, 20), (t, s) => CharacterTex.SkinNeckUV(t, s));
        }

        // ------------------------------------------------------------ sleeves, legs, boots, hands
        static void Sleeve(SkinMesh m, CharSpec sp, float sx)
        {
            var arm = MakeArm(sx, sp); var path = new Path(arm.shoulder, arm.elbow, arm.wrist);
            float el = 0.29f * sp.armLen, wr = path.Length;
            B bA = sx < 0 ? B.LArm : B.RArm, bF = sx < 0 ? B.LFore : B.RFore, bH = sx < 0 ? B.LHand : B.RHand;
            var part = sx < 0 ? CharacterTex.Part.SleeveL : CharacterTex.Part.SleeveR;
            Func<float, float> rf = s => s < 0f ? Mathf.Sqrt(Mathf.Max(0.00001f, 0.066f * 0.066f - s * s)) : SampleKeys(s, new[] { 0f, 0.15f, el, el + 0.1f, wr }, new[] { 0.066f, 0.062f, 0.054f, 0.049f, 0.041f });
            Func<float, BoneWeight> wf = s => Chain(bA, bF, bH, s, el, wr - 0.01f);
            float end = sp.rolledSleeves ? el + 0.075f : wr;
            int rad = Seg(sp, 24);
            var ss = Cat(new[] { -0.063f, -0.055f, -0.04f, -0.02f }, Range(0f, end, Cnt(sp, 12)));
            m.Loft(Suit, Rings(path, ss, rf, 0.96f, s => s + 0.065f, wf), rad, (t, s) => CharacterTex.SuitUV(part, t, s), Mathf.PI, Mathf.PI * 2f, true, false);
            if (sp.rolledSleeves && sp.Mid)
            {   // thick rolled cuff + bare forearm
                m.Loft(Suit, Rings(path, new[] { end - 0.075f, end - 0.07f, end - 0.01f, end }, rf, 0.96f, s => s - (end - 0.075f), wf, 0.011f), rad, (t, s) => CharacterTex.MiscUV(0.3f + t * 0.3f, 0.2f + s * 2f), Mathf.PI, Mathf.PI * 2f);
                var fa = Rings(path, Range(end - 0.06f, wr + 0.02f, Cnt(sp, 8)), s => Mathf.Lerp(0.047f, 0.036f, Mathf.InverseLerp(el, wr, s)), 0.94f, s => s - el, wf);
                m.Loft(Skin, fa, Seg(sp, 16), (t, s) => CharacterTex.SkinForeUV(sx < 0, t, s), Mathf.PI, Mathf.PI * 2f);
            }
            else if (sp.fine)
                m.Loft(Suit, Rings(path, new[] { wr - 0.065f, wr - 0.06f, wr - 0.01f, wr }, rf, 0.96f, s => s - (wr - 0.065f), wf, 0.006f), rad, (t, s) => CharacterTex.MiscUV(0.35f + t * 0.2f, 0.3f + s), Mathf.PI, Mathf.PI * 2f);
            if (sp.bands)
            {
                SleeveBand(m, sp, path, 0.10f, 0.155f, rf, wf, sx); if (!sp.rolledSleeves) SleeveBand(m, sp, path, el + 0.085f, el + 0.14f, rf, wf, sx);
            }
        }

        static void SleeveBand(SkinMesh m, CharSpec sp, Path path, float s0, float s1, Func<float, float> rf, Func<float, BoneWeight> wf, float sx)
        {
            var ss = new[] { s0, s0 + 0.004f, (s0 + s1) * 0.5f, s1 - 0.004f, s1 };
            var rings = new List<Ring>();
            for (int i = 0; i < ss.Length; i++) rings.Add(MakeRing(path, ss[i], rf(ss[i]) + (i == 0 || i == 4 ? 0.001f : 0.0035f), 0.96f, ss[i] - s0, wf(ss[i])));
            m.Loft(Band, rings, Seg(sp, 24), (t, s) => CharacterTex.BandUV(t * 1.6f, s / (s1 - s0)), Mathf.PI);
        }

        static float SampleKeys(float x, float[] k, float[] v)
        {
            if (x <= k[0]) return v[0]; if (x >= k[k.Length - 1]) return v[v.Length - 1];
            int i = 0; while (x > k[i + 1]) i++;
            return Mathf.Lerp(v[i], v[i + 1], SS(k[i], k[i + 1], x));
        }

        static void Leg(SkinMesh m, CharSpec sp, float sx)
        {
            var bp = BonePositions(sp); int o = sx < 0 ? 0 : 5;
            var path = new Path(new Vector3(sx * 0.09f, Hip, 0f), bp[(int)B.LLeg + o], new Vector3(sx * 0.09f, 0.19f, 0f));
            B bU = sx < 0 ? B.LUpLeg : B.RUpLeg, bL = sx < 0 ? B.LLeg : B.RLeg, bF = sx < 0 ? B.LFoot : B.RFoot;
            var part = sx < 0 ? CharacterTex.Part.LegL : CharacterTex.Part.LegR;
            float knee = Hip - 0.50f, hem = path.Length;
            Func<float, float> rf = s => SampleKeys(s, new[] { 0f, 0.15f, 0.30f, knee, 0.60f, 0.75f, hem - 0.03f, hem }, new[] { 0.092f, 0.089f, 0.080f, 0.071f, 0.068f, 0.068f, 0.071f, 0.074f });
            Func<float, BoneWeight> wf = s => s < 0.10f ? W2(B.Hips, bU, SS(0f, 0.10f, s) * 0.6f + 0.4f * (s > 0.04f ? 1f : 0f)) : Chain(bU, bL, bF, s, knee, hem - 0.04f, 0.07f);
            var ss = Cat(Range(0f, knee - 0.12f, Cnt(sp, 6)), Range(knee - 0.08f, knee + 0.08f, Cnt(sp, 6)), Range(knee + 0.14f, hem, Cnt(sp, 8)));
            m.Loft(Suit, Rings(path, ss, rf, 1f, s => s, wf), Seg(sp, 28), (t, s) => CharacterTex.SuitUV(part, t, s), Mathf.PI, Mathf.PI * 2f, true, true);
            if (sp.bands) { LegBand(m, sp, path, knee - 0.17f, knee - 0.115f, rf, wf); LegBand(m, sp, path, hem - 0.135f, hem - 0.08f, rf, wf); }
            if (sp.fine)
            {
                // knee pad
                var rings = new List<Ring>(); foreach (float s in Range(knee - 0.06f, knee + 0.07f, 6)) rings.Add(MakeRing(path, s, rf(s) + 0.0045f - Mathf.Abs(s - knee) * 0.025f, 1f, s - knee + 0.08f, wf(s)));
                m.Loft(Gear, rings, Seg(sp, 14), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Pad, t, s * 5f), -0.95f, 1.9f, true, true);
                // cargo pocket
                var pc = new Vector3(sx * (0.09f + 0.083f + 0.014f), Hip - 0.30f, 0.015f);
                Func<Vector3, BoneWeight> wb = p => W(bU);
                m.AddMesh(MeshKit.Box(new Vector3(0.036f, 0.17f, 0.14f), 0.012f), Matrix4x4.TRS(pc, Quaternion.identity, Vector3.one), Suit, wb, CharacterTex.MiscAt(pc));
                m.AddMesh(MeshKit.Box(new Vector3(0.04f, 0.055f, 0.145f), 0.008f), Matrix4x4.TRS(pc + new Vector3(sx * 0.002f, 0.06f, 0f), Quaternion.Euler(0, 0, -sx * 4f), Vector3.one), Suit, wb, CharacterTex.MiscAt(pc + Vector3.up * 0.06f));
                m.AddMesh(MeshKit.Cylinder(0.009f, 0.008f, 8, 0.002f), Matrix4x4.TRS(pc + new Vector3(sx * 0.024f, 0.045f, 0f), Quaternion.Euler(0, 0, 90f), Vector3.one), Hardware, wb);
            }
        }

        static void LegBand(SkinMesh m, CharSpec sp, Path path, float s0, float s1, Func<float, float> rf, Func<float, BoneWeight> wf)
        {
            var ss = new[] { s0, s0 + 0.004f, (s0 + s1) * 0.5f, s1 - 0.004f, s1 }; var rings = new List<Ring>();
            for (int i = 0; i < ss.Length; i++) rings.Add(MakeRing(path, ss[i], rf(ss[i]) + (i == 0 || i == 4 ? 0.001f : 0.0035f), 1f, ss[i] - s0, wf(ss[i])));
            m.Loft(Band, rings, Seg(sp, 28), (t, s) => CharacterTex.BandUV(t * 2.2f, s / (s1 - s0)), Mathf.PI);
        }

        static void Boot(SkinMesh m, CharSpec sp, float sx)
        {
            int o = sx < 0 ? 0 : 5; B bL = sx < 0 ? B.LLeg : B.RLeg, bF = sx < 0 ? B.LFoot : B.RFoot, bT = sx < 0 ? B.LToe : B.RToe;
            float x = sx * 0.09f;
            if (sp.Lowest) { var f0 = new List<Ring>(); foreach (var z in new[] { -0.06f, 0.07f, 0.2f }) f0.Add(new Ring { c = new Vector3(x, 0.07f, z), u = Vector3.right, v = Vector3.up, a = 0.055f, b = 0.06f - z * 0.1f, n = 2f, s = z, w = W(bF) }); m.Loft(Gear, f0, 6, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Rubber, t, 0.05f), 0f, Mathf.PI * 2f, true, true); return; }
            // shaft
            var shaft = new List<Ring>(); float[,] sh = { { 0.33f, 0.060f, 0.062f }, { 0.22f, 0.058f, 0.060f }, { 0.13f, 0.060f, 0.064f }, { 0.07f, 0.062f, 0.068f } };
            for (int i = 0; i < 4; i++) shaft.Add(new Ring { c = new Vector3(x, sh[i, 0], 0.0f), u = Vector3.right, v = Vector3.forward, a = sh[i, 1], b = sh[i, 2], n = 2f, s = 0.31f - sh[i, 0], w = sh[i, 0] > 0.2f ? W(bL) : W2(bL, bF, SS(0.2f, 0.1f, sh[i, 0])) });
            if (!sp.fine) { m.Loft(Gear, shaft, Seg(sp, 16), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Rubber, t, 0.3f - s)); }
            else m.Loft(Gear, shaft, Seg(sp, 24), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Rubber, t, 0.3f - s), 0f, Mathf.PI * 2f, false, false);
            // foot (loft along Z), rings in the XY plane
            float[,] ft = { { -0.065f, 0.050f, 0.12f, 0.060f }, { 0f, 0.056f, 0.118f, 0.060f }, { 0.07f, 0.058f, 0.098f, 0.057f }, { 0.14f, 0.057f, 0.085f, 0.052f }, { 0.195f, 0.050f, 0.07f, 0.042f }, { 0.228f, 0.032f, 0.044f, 0.034f } };
            var foot = new List<Ring>();
            for (int i = 0; i < ft.GetLength(0); i++)
            {
                float z = ft[i, 0], top = ft[i, 2], bot = 0.03f;
                foot.Add(new Ring { c = new Vector3(x, (top + bot) * 0.5f, z), u = Vector3.right, v = Vector3.up, a = ft[i, 1], b = (top - bot) * 0.5f, n = 2.4f, s = z + 0.07f, w = z < 0.1f ? W(bF) : W2(bF, bT, SS(0.1f, 0.16f, z)) });
            }
            m.Loft(Gear, foot, Seg(sp, 16), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Rubber, t, 0.02f + s * 0.1f), 0f, Mathf.PI * 2f, true, true);
            // sole
            var sole = new List<Ring>(); float[,] so = { { -0.085f, 0.047f }, { -0.06f, 0.056f }, { 0.07f, 0.062f }, { 0.15f, 0.06f }, { 0.215f, 0.046f }, { 0.236f, 0.026f } };
            for (int i = 0; i < 6; i++) sole.Add(new Ring { c = new Vector3(x, 0.017f, so[i, 0]), u = Vector3.right, v = Vector3.up, a = so[i, 1], b = 0.017f, n = 2.8f, s = so[i, 0], w = so[i, 0] < 0.1f ? W(bF) : W2(bF, bT, SS(0.1f, 0.16f, so[i, 0])) });
            m.Loft(Gear, sole, Seg(sp, 14), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Rubber, t, 0.005f), 0f, Mathf.PI * 2f, true, true);
        }

        static void Hand(SkinMesh m, CharSpec sp, float sx)
        {
            var arm = MakeArm(sx, sp); var f = arm.d2; var side = Vector3.Cross(f, new Vector3(-sx, 0f, 0f)).normalized; // finger spread axis (Z-ish)
            if (Vector3.Dot(side, Vector3.forward) < 0f) side = -side;
            var med = Vector3.Cross(side, f).normalized; if (Vector3.Dot(med, new Vector3(-sx, 0, 0)) < 0f) med = -med; // palm normal (towards the body)
            B bH = sx < 0 ? B.LHand : B.RHand; var wH = W(bH); var wrist = arm.wrist;
            if (sp.Lowest) { var mi = new List<Ring>(); foreach (float s in new[] { 0f, 0.1f, 0.185f }) mi.Add(new Ring { c = wrist + f * s, u = side, v = med, a = 0.04f - s * 0.12f, b = 0.02f, n = 2f, s = s, w = wH }); m.Loft(Gear, mi, 6, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s), 0f, Mathf.PI * 2f, false, true); return; }
            Func<float, float> palmW = s => SampleKeys(s, new[] { -0.03f, 0f, 0.05f, 0.095f }, new[] { 0.035f, 0.036f, 0.044f, 0.045f });
            Func<float, float> palmT = s => SampleKeys(s, new[] { -0.03f, 0f, 0.05f, 0.095f }, new[] { 0.027f, 0.027f, 0.026f, 0.021f });
            var palm = new List<Ring>();
            foreach (float s in new[] { -0.035f, 0f, 0.05f, 0.095f }) palm.Add(new Ring { c = wrist + f * s, u = side, v = med, a = palmW(s), b = palmT(s), n = 2.4f, s = s + 0.04f, w = s < 0f ? W2(sx < 0 ? B.LFore : B.RFore, bH, SS(-0.035f, 0f, s)) : wH });
            m.Loft(Gear, palm, Seg(sp, 12), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s * 4f), 0f, Mathf.PI * 2f, true, true);
            // glove cuff
            var cuff = new List<Ring>(); foreach (float s in new[] { -0.075f, -0.07f, -0.02f, -0.015f }) cuff.Add(new Ring { c = wrist + f * s, u = side, v = med, a = 0.048f, b = 0.04f, n = 2.2f, s = s + 0.08f, w = s < -0.05f ? W(sx < 0 ? B.LFore : B.RFore) : wH });
            m.Loft(Gear, cuff, Seg(sp, 14), (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s * 4f), 0f, Mathf.PI * 2f, true, false);
            if (!sp.fine)
            {   // far LOD: a single mitten block
                var mit = new List<Ring>(); foreach (float s in new[] { 0.09f, 0.14f, 0.185f }) mit.Add(new Ring { c = wrist + f * s, u = side, v = med, a = 0.04f - (s - 0.09f) * 0.2f, b = 0.02f, n = 2.2f, s = s, w = wH });
                m.Loft(Gear, mit, 8, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s), 0f, Mathf.PI * 2f, false, true); return;
            }
            float[] zo = { 0.032f, 0.011f, -0.011f, -0.031f }, len = { 0.078f, 0.088f, 0.082f, 0.066f }, rr = { 0.0105f, 0.0108f, 0.0102f, 0.0095f };
            for (int i = 0; i < 4; i++)
            {
                var r = new List<Ring>(); var baseP = wrist + f * 0.085f + side * zo[i];
                for (int k = 0; k <= 3; k++)
                {
                    float t = k / 3f; float curl = t * t * 0.03f;
                    r.Add(new Ring { c = baseP + f * (len[i] * t) + med * (curl + 0.004f) - f * (curl * 0.4f), u = side, v = med, a = rr[i] * (1f - t * 0.25f), b = rr[i] * 0.92f * (1f - t * 0.25f), n = 2f, s = t * len[i], w = wH });
                }
                m.Loft(Gear, r, 7, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s * 4f), 0f, Mathf.PI * 2f, false, true);
            }
            var td = (f * 0.55f + side * 0.75f + med * 0.15f).normalized; var tb = wrist + f * 0.035f + side * 0.033f + med * 0.004f;
            var th = new List<Ring>();
            for (int k = 0; k <= 3; k++) { float t = k / 3f; th.Add(new Ring { c = tb + td * (0.06f * t) + med * (t * t * 0.01f), u = Vector3.Cross(td, med).normalized, v = med, a = 0.0125f * (1f - t * 0.2f), b = 0.0115f * (1f - t * 0.2f), n = 2f, s = t * 0.06f, w = wH }); }
            m.Loft(Gear, th, 7, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Glove, t, s * 4f), 0f, Mathf.PI * 2f, false, true);
        }

        // ------------------------------------------------------------ harness, pouches
        static void Webbing(SkinMesh m, CharSpec sp)
        {
            // belt hardware + pouches + radio
            TorsoAt(1.01f, out float a0, out float b0, out float cz0);
            Func<Vector3, BoneWeight> wS = p => W(B.Spine);
            m.AddMesh(MeshKit.Box(new Vector3(0.05f, 0.042f, 0.012f), 0.004f), Matrix4x4.TRS(new Vector3(0, 1.01f, cz0 + b0 + 0.014f), Quaternion.identity, Vector3.one), Hardware, wS);
            foreach (float th in new[] { 0.95f, -0.95f })
            {
                var p = TorsoSurf(1.0f, th, 0.024f); float yaw = Mathf.Atan2(Mathf.Sin(th) / a0, Mathf.Cos(th) / b0) * Mathf.Rad2Deg;
                var q = Quaternion.Euler(0, yaw, 0);
                m.AddMesh(MeshKit.Box(new Vector3(0.055f, 0.095f, 0.04f), 0.01f), Matrix4x4.TRS(p - Vector3.up * 0.045f, q, Vector3.one), Gear, wS, CharacterTex.GearAt(CharacterTex.GearQ.Leather, p));
                m.AddMesh(MeshKit.Box(new Vector3(0.058f, 0.035f, 0.044f), 0.008f), Matrix4x4.TRS(p + Vector3.up * 0.0f, q, Vector3.one), Gear, wS, CharacterTex.GearAt(CharacterTex.GearQ.Leather, p + Vector3.right));
                m.AddMesh(MeshKit.Cylinder(0.006f, 0.008f, 8, 0.001f), Matrix4x4.TRS(p + q * new Vector3(0, -0.01f, 0.024f), Quaternion.Euler(90, 0, 0) * q, Vector3.one), Hardware, wS);
            }
            { var p = TorsoSurf(1.0f, -1.45f, 0.03f); float yaw = Mathf.Atan2(Mathf.Sin(-1.45f) / a0, Mathf.Cos(-1.45f) / b0) * Mathf.Rad2Deg; var q = Quaternion.Euler(0, yaw, 0);
              m.AddMesh(MeshKit.Box(new Vector3(0.05f, 0.13f, 0.035f), 0.008f), Matrix4x4.TRS(p - Vector3.up * 0.04f, q, Vector3.one), Hardware, wS);
              m.AddMesh(MeshKit.Cylinder(0.004f, 0.09f, 6, 0.001f), Matrix4x4.TRS(p + Vector3.up * 0.075f + q * new Vector3(0, 0, -0.01f), q, Vector3.one), Hardware, wS); }
            if (!sp.hiVisBraces && !sp.leatherHarness) return;
            int slot = sp.leatherHarness ? Gear : Band; float width = sp.leatherHarness ? 0.042f : 0.056f;
            foreach (float sx in new[] { -1f, 1f })
            {
                var ctl = new List<Vector2>(); // (y, theta)
                float[,] c = { { 1.03f, 0.42f }, { 1.14f, 0.40f }, { 1.30f, 0.46f }, { 1.40f, 0.66f }, { 1.475f, 1.00f }, { 1.485f, 1.45f }, { 1.46f, 2.15f }, { 1.38f, 2.6f }, { 1.22f, 2.78f }, { 1.05f, 2.80f } };
                for (int i = 0; i < c.GetLength(0); i++) ctl.Add(new Vector2(c[i, 0], c[i, 1]));
                var pts = new List<Vector3>(); var nr = new List<Vector3>();
                for (int i = 0; i < ctl.Count - 1; i++)
                    for (int k = 0; k < 3; k++)
                    {
                        float t = k / 3f; var p0 = ctl[Mathf.Max(i - 1, 0)]; var p1 = ctl[i]; var p2 = ctl[i + 1]; var p3 = ctl[Mathf.Min(i + 2, ctl.Count - 1)];
                        var q = 0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);
                        float th = q.y * sx; float off = sp.leatherHarness ? 0.009f : 0.0055f;
                        pts.Add(TorsoSurf(q.x, th, off)); nr.Add(TorsoNormal(q.x, th));
                    }
                { var q = ctl[ctl.Count - 1]; float th = q.y * sx; pts.Add(TorsoSurf(q.x, th, sp.leatherHarness ? 0.009f : 0.0055f)); nr.Add(TorsoNormal(q.x, th)); }
                m.Ribbon(slot, pts, nr, width, 0.006f, (t, s) => slot == Gear ? CharacterTex.GearUV(CharacterTex.GearQ.Leather, Mathf.InverseLerp(0.25f, 0.75f, t), s) : CharacterTex.BandUV(s * 3f, Mathf.InverseLerp(0.25f, 0.75f, t)), p => TW(p.y), 8);
            }
            if (sp.leatherHarness)
            {   // chest strap + buckles
                var pts = new List<Vector3>(); var nr = new List<Vector3>();
                for (int k = 0; k <= 14; k++) { float th = Mathf.Lerp(-0.5f, 0.5f, k / 14f); pts.Add(TorsoSurf(1.30f, th, 0.011f)); nr.Add(TorsoNormal(1.30f, th)); }
                m.Ribbon(Gear, pts, nr, 0.03f, 0.005f, (t, s) => CharacterTex.GearUV(CharacterTex.GearQ.Leather, Mathf.InverseLerp(0.25f, 0.75f, t), s), p => TW(p.y), 8);
                foreach (float sx in new[] { -1f, 1f })
                {
                    var bp = TorsoSurf(1.30f, 0.45f * sx, 0.016f); m.AddMesh(MeshKit.Box(new Vector3(0.026f, 0.034f, 0.008f), 0.003f), Matrix4x4.TRS(bp, Quaternion.Euler(0, sx * 25f, 0), Vector3.one), Hardware, p => W(B.Spine2));
                    var lp = TorsoSurf(1.05f, 0.42f * sx, 0.016f); m.AddMesh(MeshKit.Box(new Vector3(0.026f, 0.03f, 0.008f), 0.003f), Matrix4x4.TRS(lp, Quaternion.Euler(0, sx * 25f, 0), Vector3.one), Hardware, p => W(B.Spine));
                }
            }
            else
            {   // chest pockets on the suit
                foreach (float sx in new[] { -1f, 1f })
                {
                    var p = TorsoSurf(1.17f, 0.50f * sx, 0.009f); var q = Quaternion.Euler(0, sx * 28.6f, 0);
                    m.AddMesh(MeshKit.Box(new Vector3(0.085f, 0.09f, 0.014f), 0.005f), Matrix4x4.TRS(p, q, Vector3.one), Suit, p2 => W(B.Spine1), CharacterTex.MiscAt(p));
                    m.AddMesh(MeshKit.Box(new Vector3(0.088f, 0.03f, 0.016f), 0.004f), Matrix4x4.TRS(p + Vector3.up * 0.035f, q, Vector3.one), Suit, p2 => W(B.Spine1), CharacterTex.MiscAt(p + Vector3.right));
                }
            }
        }

        // ------------------------------------------------------------ head
        static float G(float x, float s) => Mathf.Exp(-(x * x) / (2f * s * s));
        static float G2(float a, float e, float ma, float me, float sa, float se) => G(a - ma, sa) * G(e - me, se);

        /// <summary>Radial face relief in metres (positive = outward) at angle a (0 = straight ahead) and elevation e.</summary>
        static float Relief(float a, float e, bool detail)
        {
            float aa = Mathf.Abs(a); float r = 0f;
            r += 0.007f * (G2(aa, e, 0.45f, 0.24f, 0.30f, 0.07f) + 0.7f * G2(aa, e, 0.0f, 0.22f, 0.13f, 0.07f));       // brow ridge + glabella
            r -= 0.0095f * G2(aa, e, 0.42f, 0.085f, 0.16f, 0.095f);                                                    // eye sockets
            r += 0.0035f * G2(aa, e, 0.45f, -0.07f, 0.17f, 0.08f);                                                     // lower orbit
            r += 0.009f * G2(aa, e, 0.80f, -0.12f, 0.22f, 0.13f);                                                      // cheekbones
            r -= 0.003f * G2(aa, e, 0.58f, -0.40f, 0.22f, 0.17f);                                                     // cheek hollows
            r += 0.015f * G2(aa, e, 0.0f, -0.02f, 0.085f, 0.22f);                                                      // nose bridge
            r += 0.024f * G2(aa, e, 0.0f, -0.20f, 0.085f, 0.085f);                                                     // nose tip
            r += 0.009f * G2(aa, e, 0.24f, -0.21f, 0.075f, 0.07f);                                                    // nostril wings
            r -= 0.0055f * G2(aa, e, 0.0f, -0.285f, 0.22f, 0.045f);                                                    // under the nose
            r += 0.0045f * G2(aa, e, 0.0f, -0.33f, 0.20f, 0.04f);                                                      // upper lip
            r += 0.0065f * G2(aa, e, 0.0f, -0.46f, 0.20f, 0.045f);                                                     // lower lip
            r -= 0.0045f * G2(aa, e, 0.0f, -0.395f, 0.30f, 0.0125f);                                                   // mouth slit
            r -= 0.004f * G2(aa, e, 0.0f, -0.55f, 0.22f, 0.035f);                                                      // chin groove
            r += 0.012f * G2(aa, e, 0.0f, -0.82f, 0.19f, 0.12f);                                                       // chin
            r -= 0.004f * G2(aa, e, 1.05f, 0.15f, 0.25f, 0.2f);                                                        // temples
            r += 0.004f * G2(aa, e, 1.30f, -0.55f, 0.25f, 0.2f);                                                       // jaw muscle
            // eyelid ring around the eyeball
            float da = (aa - 0.42f) * 0.09f, de = (e - 0.085f) * 0.115f; float d = Mathf.Sqrt(da * da + de * de);
            r += 0.0032f * Mathf.Exp(-((d - 0.0125f) * (d - 0.0125f)) / (2f * 0.0042f * 0.0042f));
            return r;
        }

        static float Rate(float x) => x;

        public static Vector3 HeadPoint(float a, float e, float offset = 0f, bool relief = true)
        {
            float ce = Mathf.Cos(e), se = Mathf.Sin(e);
            var d = new Vector3(Mathf.Sin(a) * ce, se, Mathf.Cos(a) * ce);
            float rx = 0.075f, ry = 0.118f, rz = 0.098f;
            if (Mathf.Abs(a) > 1.5708f) rz = 0.102f;
            var p = new Vector3(d.x * rx, d.y * ry, d.z * rz);
            float low = SS(0f, -1.15f, e);
            p.x *= 1f - 0.2f * low; p.z *= 1f - 0.06f * low * (1f - Mathf.Abs(a) / Mathf.PI);
            p.z += 0.022f * SS(-0.25f, -0.95f, e) * Mathf.Max(0f, 1f - Mathf.Abs(a) / 1.6f);                    // lower face projects forward (chin / jaw)
            p.y -= 0.006f * SS(-0.5f, -1.2f, e);
            var n = new Vector3(d.x / rx, d.y / ry, d.z / rz).normalized;
            float r = (relief ? Relief(a, e, true) : 0f) + offset;
            return p + n * r;
        }

        public static void HeadUV(float a, float e, out float u, out float v)
        {
            u = a / (2f * Mathf.PI) + 0.5f; v = CharacterTex.HeadV0 + (e - HeadE0) / (Mathf.PI * 0.5f - HeadE0) * (1f - CharacterTex.HeadV0);
        }
        public const float HeadE0 = -1.32f;

        static float WarpA(int i, int nu) { float x = 2f * i / nu - 1f; return Mathf.PI * (0.45f * x + 0.55f * x * Mathf.Abs(x) * Mathf.Sqrt(Mathf.Abs(x))); }

        static void Head(SkinMesh m, CharSpec sp)
        {
            int nu = Mathf.Max(14, Mathf.RoundToInt(72 * Mathf.Sqrt(sp.q))), nv = Mathf.Max(9, Mathf.RoundToInt(40 * Mathf.Sqrt(sp.q)));
            var hc = HeadCenter; var wH = W(B.Head);
            Func<int, int, float> av = (i, j) => WarpA(i, nu); Func<int, int, float> ev = (i, j) => HeadE0 + (Mathf.PI * 0.5f - HeadE0) * j / nv;
            m.Grid(Skin, nu, nv, (i, j) => hc + HeadPoint(av(i, j), ev(i, j), 0f, sp.q > 0.3f),
                (i, j) => { HeadUV(av(i, j), ev(i, j), out float u, out float v); return new Vector2(u, v); }, (i, j) => wH, (i, j) => hc);
            if (sp.q > 0.3f) { Ear(m, sp, 1f); Ear(m, sp, -1f); }
        }

        static void Ear(SkinMesh m, CharSpec sp, float sx)
        {
            var hc = HeadCenter; var basePt = hc + HeadPoint(sx * 1.60f, -0.04f, -0.003f, false) + new Vector3(0f, 0f, -0.004f);
            var rot = Quaternion.Euler(0f, sx * -18f, 0f);
            int nr = 5, na = 12; float h = 0.034f, w = 0.0125f;
            Func<int, int, Vector3> pos = (i, j) =>
            {
                float t = (float)j / nr; float ang = 2f * Mathf.PI * i / na;
                float x = Mathf.Cos(ang) * t * w, y = Mathf.Sin(ang) * t * h * (ang > 0f && ang < Mathf.PI ? 0.55f : 0.5f);
                float rim = Mathf.Exp(-Mathf.Pow((t - 0.85f) / 0.2f, 2f)); float bowl = Mathf.Exp(-Mathf.Pow(t / 0.45f, 2f));
                float zz = 0.011f * rim - 0.006f * bowl + 0.002f;                      // outward (sx) offset along local x? use local z as outward
                return basePt + rot * new Vector3(zz * sx, y * 1.0f, -x * 1.6f * 0.4f - 0.002f);
            };
            m.Grid(Skin, na, nr, pos, (i, j) => { var p = pos(i, j) - hc; float a = Mathf.Atan2(p.x, p.z); HeadUV(a, 0f, out float u, out float v); return new Vector2(u, CharacterTex.HeadV0 + 0.45f * (1f - CharacterTex.HeadV0)); }, (i, j) => W(B.Head), (i, j) => basePt + rot * new Vector3(-sx * 0.02f, 0, 0));
        }

        // ------------------------------------------------------------ eyeballs (separate renderers, centred at the origin, looking +Z)
        public static Vector3 EyeCenter(float sx)
        {
            float a = sx * 0.42f; var hc = HeadCenter;
            var p = HeadPoint(a, 0.085f, 0f, false); float ce = Mathf.Cos(0.085f), se = Mathf.Sin(0.085f);
            var n = new Vector3(Mathf.Sin(a) * ce / 0.075f, se / 0.118f, Mathf.Cos(a) * ce / 0.098f).normalized;
            return hc + p - n * 0.0085f;
        }

        public static SkinMesh Eyeball(float r, int nu, int nv)
        {
            var m = new SkinMesh(1); var w = new BoneWeight();
            Func<int, int, Vector3> pos = (i, j) => { float th = Mathf.PI * j / nv, ph = 2f * Mathf.PI * i / nu; return new Vector3(Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th)) * r; };
            m.Grid(0, nu, nv, pos, (i, j) =>
            {
                float th = Mathf.PI * j / nv, ph = 2f * Mathf.PI * i / nu; float rr = Mathf.Sqrt(th / Mathf.PI) * 0.5f;
                return new Vector2(0.5f + rr * Mathf.Sin(ph), 0.5f + rr * Mathf.Cos(ph));
            }, (i, j) => w, (i, j) => Vector3.zero);
            return m;
        }

        // ------------------------------------------------------------ hair / beard shells on the head surface
        public static SkinMesh HairShell(CharSpec sp, bool beard, float thick)
        {
            var m = new SkinMesh(1); var hc = HeadCenter; var wH = W(B.Head);
            int nu = Mathf.RoundToInt((beard ? 28 : 40) * Mathf.Sqrt(sp.q)), nv = Mathf.RoundToInt(14 * Mathf.Sqrt(sp.q));
            Func<int, float> av = i => beard ? Mathf.Lerp(-1.9f, 1.9f, (float)i / nu) : WarpA(i, nu);
            // lower / upper boundary elevation per azimuth
            Func<float, float> lo = a => { float aa = Mathf.Abs(a); if (beard) return -1.2f; float h = Mathf.Lerp(0.55f, -0.38f, SS(0.3f, 2.6f, aa)); return Mathf.Lerp(h, 0.34f, G(aa - 1.62f, 0.26f)); };
            Func<float, float> hi = a => { float aa = Mathf.Abs(a); if (beard) return Mathf.Lerp(-0.50f, -0.02f, SS(0.3f, 1.35f, aa)); return Mathf.PI * 0.5f; };
            Func<int, int, float> ev = (i, j) => { float a = av(i); return Mathf.Lerp(lo(a), hi(a), (float)j / nv); };
            Func<int, int, float> off = (i, j) =>
            {
                float t = (float)j / nv, a = Mathf.Abs(av(i)); float k = SS(0f, 0.18f, t) * (beard ? SS(1f, 0.82f, t) * SS(1.9f, 1.55f, a) : 1f);
                return 0.0005f + thick * k;
            };
            m.Grid(0, nu, nv, (i, j) => hc + HeadPoint(av(i), ev(i, j), off(i, j), true),
                (i, j) => new Vector2(av(i) / (2f * Mathf.PI) + 0.5f, 0.1f + ev(i, j) * 0.3f), (i, j) => wH, (i, j) => hc);
            return m;
        }

        // ------------------------------------------------------------ hard hat + lamp (slot 0 hat, 1 hardware, 2 lens)
        public static SkinMesh Hat(CharSpec sp)
        {
            var m = new SkinMesh(3); var wH = W(B.Head); float y0 = 1.737f; var rings = new List<Ring>();
            float[,] h = {
                { -0.006f, 0.089f, 0.103f, 0.003f, 2f }, { 0.000f, 0.112f, 0.118f, 0.030f, 2.6f }, { 0.006f, 0.113f, 0.120f, 0.031f, 2.6f }, { 0.013f, 0.100f, 0.110f, 0.012f, 2.2f },
                { 0.034f, 0.098f, 0.110f, 0.006f, 2f }, { 0.062f, 0.092f, 0.106f, 0.004f, 2f }, { 0.088f, 0.081f, 0.097f, 0.003f, 2f }, { 0.108f, 0.064f, 0.080f, 0.002f, 2f }, { 0.124f, 0.038f, 0.050f, 0.001f, 2f }, { 0.131f, 0.012f, 0.016f, 0.001f, 2f } };
            for (int i = 0; i < h.GetLength(0); i++) rings.Add(new Ring { c = new Vector3(0, y0 + h[i, 0], HeadCenter.z + h[i, 3]), u = Vector3.right, v = Vector3.forward, a = h[i, 1], b = h[i, 2], n = h[i, 4], s = i * 0.04f, w = wH });
            m.Loft(0, rings, Seg(sp, 40), (t, s) => CharacterTex.PlasticUV(t * 2f, s * 2f), 0f, Mathf.PI * 2f, true, true);
            if (!sp.fine) return m;
            // centre rib
            var rib = new List<Ring>();
            for (int k = 0; k <= 8; k++)
            {
                float z = Mathf.Lerp(-0.098f, 0.100f, k / 8f); float top = y0 + 0.131f * Mathf.Sqrt(Mathf.Max(0.02f, 1f - Mathf.Pow((z - HeadCenter.z) / 0.108f, 2f)));
                rib.Add(new Ring { c = new Vector3(0, top - 0.002f, z), u = Vector3.right, v = Vector3.up, a = 0.0095f, b = 0.0085f, n = 2f, s = k * 0.03f, w = wH });
            }
            m.Loft(0, rib, 8, (t, s) => CharacterTex.PlasticUV(t, s * 2f), 0f, Mathf.PI * 2f, true, true);
            // lamp housing + bracket + lens
            var lc = new Vector3(0, y0 + 0.075f, HeadCenter.z + 0.108f);
            m.AddMesh(MeshKit.Box(new Vector3(0.05f, 0.02f, 0.035f), 0.004f), Matrix4x4.TRS(lc + new Vector3(0, -0.032f, -0.004f), Quaternion.identity, Vector3.one), 1, p => wH);
            m.AddMesh(MeshKit.Cylinder(0.031f, 0.045f, 18, 0.004f), Matrix4x4.TRS(lc + new Vector3(0, 0, 0.012f), Quaternion.Euler(90, 0, 0), Vector3.one), 1, p => wH);
            m.AddMesh(MeshKit.Cylinder(0.024f, 0.006f, 18, 0.001f), Matrix4x4.TRS(lc + new Vector3(0, 0, 0.0365f), Quaternion.Euler(90, 0, 0), Vector3.one), 2, p => wH);
            return m;
        }

        public static SkinMesh Goggles(CharSpec sp)
        {
            var m = new SkinMesh(2); var wH = W(B.Head); float y = 1.737f + 0.055f; float z = HeadCenter.z + 0.095f;
            foreach (float sx in new[] { -1f, 1f })
            {
                m.AddMesh(MeshKit.Cylinder(0.036f, 0.03f, 16, 0.006f), Matrix4x4.TRS(new Vector3(sx * 0.04f, y, z + 0.006f), Quaternion.Euler(80, 0, 0), new Vector3(1f, 1f, 0.85f)), 0, p => wH);
                m.AddMesh(MeshKit.Cylinder(0.029f, 0.006f, 16, 0.001f), Matrix4x4.TRS(new Vector3(sx * 0.04f, y + 0.0045f, z + 0.0205f), Quaternion.Euler(80, 0, 0), new Vector3(1f, 1f, 0.85f)), 1, p => wH);
            }
            m.AddMesh(MeshKit.Box(new Vector3(0.03f, 0.02f, 0.02f), 0.005f), Matrix4x4.TRS(new Vector3(0, y, z + 0.012f), Quaternion.identity, Vector3.one), 0, p => wH);
            m.AddMesh(MeshKit.Box(new Vector3(0.19f, 0.03f, 0.02f), 0.006f), Matrix4x4.TRS(new Vector3(0, y - 0.004f, z - 0.034f), Quaternion.Euler(-12, 0, 0), Vector3.one), 0, p => wH);
            return m;
        }

        /// <summary>Half-mask respirator with two filter cartridges, facing +Z, origin at the centre of the mask (slot 0 rubber, 1 hardware).</summary>
        public static SkinMesh Respirator(CharSpec sp, Matrix4x4 place, Vector3[] strap)
        {
            var m = new SkinMesh(2); var wH = W(B.Head);
            int nu = 18, nv = 10;
            Func<int, int, Vector3> pos = (i, j) =>
            {
                float s = (float)i / nu * 2f - 1f, t = (float)j / nv * 2f - 1f;
                float x = s * 0.060f, y = t * 0.046f - Mathf.Abs(s) * Mathf.Abs(s) * 0.012f; float taper = 1f - 0.22f * Mathf.Max(0f, -t);
                x *= taper; float depth = 0.045f * (1f - s * s * 0.75f) * (1f - t * t * 0.6f) - 0.012f * (t > 0.3f ? (t - 0.3f) : 0f);
                return new Vector3(x, y, depth);
            };
            m.Grid(0, nu, nv, pos, (i, j) => new Vector2((float)i / nu, (float)j / nv), (i, j) => wH, (i, j) => new Vector3(0, 0, -0.1f));
            foreach (float sx in new[] { -1f, 1f })
            {
                m.AddMesh(MeshKit.Cylinder(0.021f, 0.03f, 14, 0.004f), Matrix4x4.TRS(new Vector3(sx * 0.052f, -0.005f, 0.03f), Quaternion.Euler(90, 0, sx * -20f) , Vector3.one), 1, p => wH);
                m.AddMesh(MeshKit.Cylinder(0.018f, 0.012f, 14, 0.002f), Matrix4x4.TRS(new Vector3(sx * 0.057f, -0.005f, 0.052f), Quaternion.Euler(90, 0, sx * -20f), Vector3.one), 0, p => wH);
            }
            m.AddMesh(MeshKit.Cylinder(0.016f, 0.014f, 12, 0.003f), Matrix4x4.TRS(new Vector3(0, -0.015f, 0.052f), Quaternion.Euler(90, 0, 0), Vector3.one), 1, p => wH);
            m.Warp(p => place.MultiplyPoint3x4(p), (p, n) => place.MultiplyVector(n));
            m.AddMesh(MeshKit.Tube(strap, 0.005f, 6, 6), Matrix4x4.identity, 0, p => wH);
            return m;
        }
    }
}
