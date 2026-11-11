using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Mesh accumulator for the procedural characters: lofted tubes, UV grids, ribbons and transformed primitives, all with per-vertex bone
    /// weights and several material slots. Normals of generated surfaces are smoothed across welded vertices (seams, poles, caps).
    /// </summary>
    internal sealed class SkinMesh
    {
        /// <summary>Elliptical cross-section frame: centre, two axes, half extents, squareness exponent (2 = ellipse), distance along the part and bone weights.</summary>
        public struct Ring { public Vector3 c, u, v; public float a, b, n, s; public BoneWeight w; }

        readonly List<Vector3> _v = new List<Vector3>(), _n = new List<Vector3>();
        readonly List<Vector2> _uv = new List<Vector2>();
        readonly List<BoneWeight> _w = new List<BoneWeight>();
        readonly List<bool> _auto = new List<bool>();
        readonly List<int>[] _t;

        public SkinMesh(int slots) { _t = new List<int>[slots]; for (int i = 0; i < slots; i++) _t[i] = new List<int>(); }

        public int VertexCount => _v.Count;
        public int TriangleCount { get { int n = 0; foreach (var t in _t) n += t.Count / 3; return n; } }

        public static BoneWeight BW(int b0, float w0 = 1f, int b1 = 0)
        {
            w0 = Mathf.Clamp01(w0);
            var w = new BoneWeight { boneIndex0 = b0, weight0 = w0 };
            if (w0 < 0.999f) { w.boneIndex1 = b1; w.weight1 = 1f - w0; }
            return w;
        }

        public int Vert(Vector3 p, Vector2 uv, BoneWeight w, bool auto = true, Vector3 n = default)
        {
            _v.Add(p); _uv.Add(uv); _w.Add(w); _auto.Add(auto); _n.Add(auto ? Vector3.up : n);
            return _v.Count - 1;
        }

        public void Tri(int slot, int a, int b, int c)
        {
            if (Vector3.Cross(_v[b] - _v[a], _v[c] - _v[a]).sqrMagnitude < 1e-12f) return;
            _t[slot].Add(a); _t[slot].Add(b); _t[slot].Add(c);
        }

        static float SP(float x, float n) => n == 2f ? x : Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), 2f / n);

        // ---------- generic grid ----------
        /// <summary>(nu+1) x (nv+1) vertex grid; winding is chosen so the surface faces away from inside(i, j).</summary>
        public void Grid(int slot, int nu, int nv, Func<int, int, Vector3> pos, Func<int, int, Vector2> uv, Func<int, int, BoneWeight> bw,
                         Func<int, int, Vector3> inside, Func<int, int, bool> skip = null)
        {
            int b = _v.Count, w1 = nu + 1;
            for (int j = 0; j <= nv; j++) for (int i = 0; i <= nu; i++) Vert(pos(i, j), uv(i, j), bw(i, j));
            bool flip = false;
            for (int probe = 0; probe < nv * nu; probe++)
            {
                int i0 = (probe * 7 + nu / 3) % nu, j0 = (probe * 3 + nv / 2) % nv;
                var p00 = _v[b + j0 * w1 + i0]; var n0 = Vector3.Cross(_v[b + j0 * w1 + i0 + 1] - p00, _v[b + (j0 + 1) * w1 + i0 + 1] - p00);
                if (n0.sqrMagnitude < 1e-12f) continue;
                flip = Vector3.Dot(n0, p00 - inside(i0, j0)) < 0f; break;
            }
            for (int j = 0; j < nv; j++)
                for (int i = 0; i < nu; i++)
                {
                    if (skip != null && skip(i, j)) continue;
                    int a = b + j * w1 + i, bb = a + 1, c = a + w1 + 1, d = a + w1;
                    if (!flip) { Tri(slot, a, bb, c); Tri(slot, a, c, d); } else { Tri(slot, a, c, bb); Tri(slot, a, d, c); }
                }
        }

        // ---------- loft ----------
        /// <summary>Sweeps elliptical rings. phase/span choose the angular range (full circle by default); caps close the ends with a fan.</summary>
        public void Loft(int slot, IList<Ring> r, int radial, Func<float, float, Vector2> uvf, float phase = 0f, float span = Mathf.PI * 2f, bool capStart = false, bool capEnd = false)
        {
            int nv = r.Count - 1; int b0 = _v.Count;
            Func<int, int, Vector3> pos = (i, j) =>
            {
                var q = r[j]; float f = phase + span * i / radial;
                return q.c + q.u * (q.a * SP(Mathf.Sin(f), q.n)) + q.v * (q.b * SP(Mathf.Cos(f), q.n));
            };
            Grid(slot, radial, nv, pos, (i, j) => uvf((float)i / radial, r[j].s), (i, j) => r[j].w, (i, j) => r[j].c);
            if (capStart) Cap(slot, r[0], b0, radial, r[0].c - r[1].c, uvf);
            if (capEnd) Cap(slot, r[nv], b0 + nv * (radial + 1), radial, r[nv].c - r[nv - 1].c, uvf);
        }

        void Cap(int slot, Ring q, int ringStart, int radial, Vector3 outward, Func<float, float, Vector2> uvf)
        {
            int c = Vert(q.c, uvf(0.5f, q.s), q.w);
            for (int i = 0; i < radial; i++)
            {
                int a = ringStart + i, b = ringStart + i + 1;
                if (Vector3.Dot(Vector3.Cross(_v[a] - _v[c], _v[b] - _v[c]), outward) >= 0f) Tri(slot, c, a, b); else Tri(slot, c, b, a);
            }
        }

        /// <summary>Flat strap following a polyline lying on a surface; nrm are the outward surface normals.</summary>
        public void Ribbon(int slot, IList<Vector3> pts, IList<Vector3> nrm, float width, float thick, Func<float, float, Vector2> uvf, Func<Vector3, BoneWeight> bw, int radial = 8)
        {
            var rings = new List<Ring>(); float s = 0f;
            for (int i = 0; i < pts.Count; i++)
            {
                if (i > 0) s += (pts[i] - pts[i - 1]).magnitude;
                var t = (i < pts.Count - 1 ? pts[i + 1] - pts[i] : pts[i] - pts[i - 1]).normalized;
                var side = Vector3.Cross(t, nrm[i]).normalized;
                rings.Add(new Ring { c = pts[i], u = side, v = nrm[i], a = width * 0.5f, b = thick * 0.5f, n = 6f, s = s, w = bw(pts[i]) });
            }
            Loft(slot, rings, radial, uvf, Mathf.PI, Mathf.PI * 2f, true, true);
        }

        // ---------- primitives ----------
        /// <summary>Appends an existing mesh (MeshKit primitive) with its own normals, rigidly weighted (or by position).</summary>
        public void AddMesh(Mesh m, Matrix4x4 xf, int slot, Func<Vector3, BoneWeight> bw, Func<Vector3, Vector2> uvf = null)
        {
            var vs = m.vertices; var ns = m.normals; var uvs = m.uv; var ts = m.triangles; int b = _v.Count;
            var nm = xf.inverse.transpose;
            for (int i = 0; i < vs.Length; i++)
            {
                var p = xf.MultiplyPoint3x4(vs[i]);
                Vert(p, uvf != null ? uvf(p) : uvs[i], bw(p), false, nm.MultiplyVector(ns[i]).normalized);
            }
            for (int i = 0; i < ts.Length; i += 3) Tri(slot, b + ts[i], b + ts[i + 1], b + ts[i + 2]);
        }

        // ---------- post processing ----------
        public void Warp(Func<Vector3, Vector3> pf, Func<Vector3, Vector3, Vector3> nf)
        {
            for (int i = 0; i < _v.Count; i++) { var p = _v[i]; _v[i] = pf(p); if (!_auto[i]) _n[i] = nf(p, _n[i]); }
        }

        public void Offset(Vector3 d) { for (int i = 0; i < _v.Count; i++) _v[i] += d; }

        static (int, int, int) Key(Vector3 p) => (Mathf.RoundToInt(p.x * 4000f), Mathf.RoundToInt(p.y * 4000f), Mathf.RoundToInt(p.z * 4000f));

        public Mesh ToMesh(string name, bool skinned)
        {
            var acc = new Dictionary<(int, int, int), Vector3>();
            foreach (var t in _t)
                for (int i = 0; i < t.Count; i += 3)
                {
                    var fn = Vector3.Cross(_v[t[i + 1]] - _v[t[i]], _v[t[i + 2]] - _v[t[i]]);
                    for (int k = 0; k < 3; k++)
                    {
                        if (!_auto[t[i + k]]) continue;
                        var key = Key(_v[t[i + k]]); acc.TryGetValue(key, out var s); acc[key] = s + fn;
                    }
                }
            var n = new Vector3[_v.Count];
            for (int i = 0; i < n.Length; i++)
            {
                if (_auto[i]) { acc.TryGetValue(Key(_v[i]), out var s); n[i] = s.sqrMagnitude > 1e-14f ? s.normalized : Vector3.up; } else n[i] = _n[i];
            }
            var m = new Mesh { name = name, indexFormat = _v.Count > 60000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            m.SetVertices(_v); m.SetNormals(n); m.SetUVs(0, _uv);
            m.subMeshCount = _t.Length;
            for (int i = 0; i < _t.Length; i++) m.SetTriangles(_t[i], i, false);
            if (skinned) m.boneWeights = _w.ToArray();
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }
}
