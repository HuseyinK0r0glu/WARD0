using System.Collections.Generic;
using UnityEngine;

namespace Ward0.EditorTools
{
    /// <summary>Procedural mesh helpers: chamfered boxes, lathes, tubes and a per-material combiner.</summary>
    public static class MeshKit
    {
        // ---------- low level ----------
        sealed class Buf
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> UV = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3[] p, Vector3[] n, Vector2[] uv)
            {
                var gn = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                var avg = n[0] + n[1] + n[2] + n[3];
                bool flip = Vector3.Dot(gn, avg) < 0f;
                int b = V.Count;
                for (int i = 0; i < 4; i++) { V.Add(p[i]); N.Add(n[i]); UV.Add(uv[i]); }
                if (!flip) { T.Add(b); T.Add(b + 1); T.Add(b + 2); T.Add(b); T.Add(b + 2); T.Add(b + 3); }
                else { T.Add(b); T.Add(b + 2); T.Add(b + 1); T.Add(b); T.Add(b + 3); T.Add(b + 2); }
            }

            public void FlatPoly(Vector3[] p, Vector3 want, float uvs)
            {
                want.Normalize();
                var gn = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
                bool flip = Vector3.Dot(gn, want) < 0f;
                int b = V.Count;
                var a = new Vector3(Mathf.Abs(want.x), Mathf.Abs(want.y), Mathf.Abs(want.z));
                for (int i = 0; i < p.Length; i++)
                {
                    V.Add(p[i]); N.Add(want);
                    Vector2 uv;
                    if (a.y >= a.x && a.y >= a.z) uv = new Vector2(p[i].x, p[i].z);
                    else if (a.z >= a.x) uv = new Vector2(p[i].x, p[i].y);
                    else uv = new Vector2(p[i].z, p[i].y);
                    UV.Add(uv * uvs);
                }
                for (int i = 1; i < p.Length - 1; i++)
                {
                    if (!flip) { T.Add(b); T.Add(b + i); T.Add(b + i + 1); }
                    else { T.Add(b); T.Add(b + i + 1); T.Add(b + i); }
                }
            }

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name };
                if (V.Count > 65000) m.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                m.SetVertices(V); m.SetNormals(N); m.SetUVs(0, UV); m.SetTriangles(T, 0);
                m.RecalculateBounds(); m.RecalculateTangents();
                return m;
            }
        }

        static Vector3 Axis(int i) => i == 0 ? Vector3.right : (i == 1 ? Vector3.up : Vector3.forward);
        static Vector3 Comp(int i, float v) => Axis(i) * v;

        // ---------- chamfered box ----------
        public static Mesh Box(Vector3 size, float bevel, float uvScale = 1f)
        {
            var b = new Buf();
            var h = size * 0.5f;
            float bv = Mathf.Min(bevel, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.95f);
            Vector3 inner = new Vector3(h.x - bv, h.y - bv, h.z - bv);
            float H(int i) => i == 0 ? h.x : (i == 1 ? h.y : h.z);
            float I(int i) => i == 0 ? inner.x : (i == 1 ? inner.y : inner.z);

            // faces
            for (int a = 0; a < 3; a++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int u = (a + 1) % 3, v = (a + 2) % 3;
                    Vector3 c = Comp(a, s * H(a));
                    var p = new[]
                    {
                        c + Comp(u, -I(u)) + Comp(v, -I(v)), c + Comp(u, I(u)) + Comp(v, -I(v)),
                        c + Comp(u, I(u)) + Comp(v, I(v)), c + Comp(u, -I(u)) + Comp(v, I(v))
                    };
                    b.FlatPoly(p, Comp(a, s), uvScale);
                }

            if (bv > 0.0001f)
            {
                // edge chamfers
                for (int c = 0; c < 3; c++)
                {
                    int a = (c + 1) % 3, bb = (c + 2) % 3;
                    for (int sa = -1; sa <= 1; sa += 2)
                        for (int sb = -1; sb <= 1; sb += 2)
                        {
                            Vector3 p1 = Comp(a, sa * H(a)) + Comp(bb, sb * I(bb));
                            Vector3 p2 = Comp(a, sa * I(a)) + Comp(bb, sb * H(bb));
                            var p = new[]
                            {
                                p1 + Comp(c, -I(c)), p1 + Comp(c, I(c)), p2 + Comp(c, I(c)), p2 + Comp(c, -I(c))
                            };
                            b.FlatPoly(p, Comp(a, sa) + Comp(bb, sb), uvScale);
                        }
                }
                // corners
                for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                        for (int sz = -1; sz <= 1; sz += 2)
                        {
                            var p = new[]
                            {
                                new Vector3(sx * h.x, sy * inner.y, sz * inner.z),
                                new Vector3(sx * inner.x, sy * h.y, sz * inner.z),
                                new Vector3(sx * inner.x, sy * inner.y, sz * h.z)
                            };
                            b.FlatPoly(p, new Vector3(sx, sy, sz), uvScale);
                        }
            }
            return b.ToMesh("Box");
        }

        // ---------- lathe (surface of revolution around Y) ----------
        /// <summary>profile = (radius, y) points going bottom->top on the outside. Hard edges between segments unless smooth.</summary>
        public static Mesh Lathe(Vector2[] profile, int seg = 28, bool smooth = false, float uvScale = 1f)
        {
            var b = new Buf();
            int n = profile.Length;
            var nrm = new Vector2[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                var d = profile[i + 1] - profile[i];
                nrm[i] = new Vector2(d.y, -d.x).normalized;
            }
            for (int i = 0; i < n - 1; i++)
            {
                Vector2 n0 = nrm[i], n1 = nrm[i];
                if (smooth)
                {
                    if (i > 0) n0 = (nrm[i] + nrm[i - 1]).normalized;
                    if (i < n - 2) n1 = (nrm[i] + nrm[i + 1]).normalized;
                }
                for (int s = 0; s < seg; s++)
                {
                    float a0 = (float)s / seg * Mathf.PI * 2f, a1 = (float)(s + 1) / seg * Mathf.PI * 2f;
                    Vector3 R(float r, float a, float y) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                    Vector3 N(Vector2 nn, float a) => new Vector3(Mathf.Cos(a) * nn.x, nn.y, Mathf.Sin(a) * nn.x);
                    var p = new[]
                    {
                        R(profile[i].x, a0, profile[i].y), R(profile[i].x, a1, profile[i].y),
                        R(profile[i + 1].x, a1, profile[i + 1].y), R(profile[i + 1].x, a0, profile[i + 1].y)
                    };
                    var nn4 = new[] { N(n0, a0), N(n0, a1), N(n1, a1), N(n1, a0) };
                    var uv = new[]
                    {
                        new Vector2((float)s / seg, profile[i].y) * uvScale, new Vector2((float)(s + 1) / seg, profile[i].y) * uvScale,
                        new Vector2((float)(s + 1) / seg, profile[i + 1].y) * uvScale, new Vector2((float)s / seg, profile[i + 1].y) * uvScale
                    };
                    // degenerate (radius 0) quads are fine; Quad() flips by normal agreement
                    b.Quad(p, nn4, uv);
                }
            }
            return b.ToMesh("Lathe");
        }

        public static Mesh Cylinder(float radius, float height, int seg = 28, float bevel = 0.003f, float uvScale = 1f)
        {
            float h = height * 0.5f, bv = Mathf.Min(bevel, radius * 0.4f);
            Vector2[] prof = bv > 0.0001f
                ? new[] { new Vector2(0, -h), new Vector2(radius - bv, -h), new Vector2(radius, -h + bv), new Vector2(radius, h - bv), new Vector2(radius - bv, h), new Vector2(0, h) }
                : new[] { new Vector2(0, -h), new Vector2(radius, -h), new Vector2(radius, h), new Vector2(0, h) };
            return Lathe(prof, seg, false, uvScale);
        }

        public static Mesh Sphere(float radius, int seg = 20, int rings = 12, float squashY = 1f)
        {
            var prof = new Vector2[rings + 1];
            for (int i = 0; i <= rings; i++)
            {
                float t = Mathf.PI * i / rings - Mathf.PI * 0.5f;
                prof[i] = new Vector2(Mathf.Cos(t) * radius, Mathf.Sin(t) * radius * squashY);
            }
            prof[0].x = 0f; prof[rings].x = 0f;
            return Lathe(prof, seg, true);
        }

        // ---------- tube along a spline ----------
        public static Mesh Tube(Vector3[] ctrl, float radius, int radial = 10, int samplesPerSpan = 10, float ribFreq = 0f, float ribDepth = 0.12f)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < ctrl.Length - 1; i++)
            {
                Vector3 p0 = ctrl[Mathf.Max(i - 1, 0)], p1 = ctrl[i], p2 = ctrl[i + 1], p3 = ctrl[Mathf.Min(i + 2, ctrl.Length - 1)];
                for (int s = 0; s < samplesPerSpan; s++)
                {
                    float t = (float)s / samplesPerSpan, t2 = t * t, t3 = t2 * t;
                    pts.Add(0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3));
                }
            }
            pts.Add(ctrl[ctrl.Length - 1]);

            var b = new Buf();
            Vector3 prevN = Vector3.zero;
            var rings = new Vector3[pts.Count][];
            var rnorm = new Vector3[pts.Count][];
            float len = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 tan = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                Vector3 nrm = i == 0 ? Vector3.Cross(tan, Mathf.Abs(tan.y) < 0.9f ? Vector3.up : Vector3.right).normalized
                                     : (prevN - Vector3.Dot(prevN, tan) * tan).normalized;
                prevN = nrm;
                Vector3 bin = Vector3.Cross(tan, nrm);
                if (i > 0) len += (pts[i] - pts[i - 1]).magnitude;
                float r = radius * (1f + (ribFreq > 0f ? ribDepth * Mathf.Sin(len * ribFreq) : 0f));
                rings[i] = new Vector3[radial + 1]; rnorm[i] = new Vector3[radial + 1];
                for (int k = 0; k <= radial; k++)
                {
                    float a = (float)k / radial * Mathf.PI * 2f;
                    Vector3 dir = Mathf.Cos(a) * nrm + Mathf.Sin(a) * bin;
                    rings[i][k] = pts[i] + dir * r; rnorm[i][k] = dir;
                }
            }
            float acc = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float seg = (pts[i + 1] - pts[i]).magnitude;
                for (int k = 0; k < radial; k++)
                {
                    b.Quad(new[] { rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k] },
                           new[] { rnorm[i][k], rnorm[i][k + 1], rnorm[i + 1][k + 1], rnorm[i + 1][k] },
                           new[] { new Vector2((float)k / radial, acc), new Vector2((float)(k + 1) / radial, acc), new Vector2((float)(k + 1) / radial, acc + seg), new Vector2((float)k / radial, acc + seg) });
                }
                acc += seg;
            }
            return b.ToMesh("Tube");
        }

        // ---------- flat quad (x right, y up, facing +Z) ----------
        public static Mesh Quad(float w, float h)
        {
            var m = new Mesh { name = "Quad" };
            m.vertices = new[] { new Vector3(-w / 2, -h / 2), new Vector3(w / 2, -h / 2), new Vector3(w / 2, h / 2), new Vector3(-w / 2, h / 2) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
            return m;
        }
    }

    /// <summary>Collects meshes per material and bakes them into one mesh per material.</summary>
    public sealed class Combiner
    {
        readonly Dictionary<Material, List<CombineInstance>> _by = new Dictionary<Material, List<CombineInstance>>();

        public void Add(Mesh mesh, Material mat, Vector3 pos, Quaternion rot = default, Vector3? scale = null)
        {
            if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0) rot = Quaternion.identity;
            if (!_by.TryGetValue(mat, out var list)) _by[mat] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale ?? Vector3.one) });
        }

        public IEnumerable<KeyValuePair<Material, Mesh>> Bake(string namePrefix)
        {
            foreach (var kv in _by)
            {
                var m = new Mesh { name = namePrefix + "_" + kv.Key.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.CombineMeshes(kv.Value.ToArray(), true, true);
                m.RecalculateBounds(); m.RecalculateTangents();
                yield return new KeyValuePair<Material, Mesh>(kv.Key, m);
            }
        }
    }
}
