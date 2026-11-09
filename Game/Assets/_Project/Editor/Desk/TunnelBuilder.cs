using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Ward0.Props;
using static Ward0.EditorTools.DeskBuilder;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds a modular 6 m mine tunnel segment: carved rock shell, steel arch ribs, timber lagging, narrow-gauge rails,
    /// ballast, pipes, cables and a hanging lamp. The geometry tiles seamlessly along Z. Menu: Ward0 / Build Mine Tunnel
    /// </summary>
    public static class TunnelBuilder
    {
        const string TRoot = "Assets/_Project/Art/Tunnel";
        const string TexDir = TRoot + "/Textures";
        const string PrefabPath = TRoot + "/MineTunnel.prefab";

        const float Len = 6f;            // segment length (z from -3 to +3)
        const float HalfW = 1.78f;       // base rock radius
        const float CY = 1.3f;           // height of arch centre
        const float RibR = 1.62f;        // arch rib radius
        static readonly float[] RibZ = { -2f, 0f, 2f };
        static System.Random rng;

        static float R() => (float)rng.NextDouble();
        static float R(float a, float b) => a + (b - a) * R();

        [MenuItem("Ward0/Build Mine Tunnel")]
        public static void Build()
        {
            MatDir = TRoot + "/Materials"; MeshDir = TRoot + "/Meshes";
            foreach (var d in new[] { TRoot, TexDir, MatDir, MeshDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            mats.Clear(); mirrored.Clear(); Origin = Vector3.zero; rng = new System.Random(20261109);

            BuildTunnelMaterials();

            var go = new GameObject("MineTunnel");
            root = go.transform;
            var seg = go.AddComponent<MineTunnelSegment>();

            // rock shell + floor (separate meshes with colliders)
            var shell = Part("RockShell", root, BuildShell(), "MineRock", Vector3.zero);
            shell.gameObject.AddComponent<MeshCollider>().sharedMesh = shell.GetComponent<MeshFilter>().sharedMesh;
            var floor = Part("Floor", root, BuildFloor(), "MineFloor", Vector3.zero);
            floor.gameObject.AddComponent<MeshCollider>().sharedMesh = floor.GetComponent<MeshFilter>().sharedMesh;

            // everything else is merged per material
            cb = new Combiner();
            BuildRibs();
            BuildLagging();
            BuildRails();
            BuildRubble();
            BuildPipes();
            BuildCables();
            var lamp = BuildLamp(out var bulb);

            var body = Obj("Body", root, Vector3.zero);
            foreach (var kv in cb.Bake("Tunnel"))
            {
                string p = SaveMesh(kv.Value, "Tunnel_" + kv.Key.name);
                var g = Obj("Body_" + kv.Key.name, body.transform, Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }

            var a0 = Obj("StartAnchor", root, new Vector3(0, 0, -Len / 2));
            var a1 = Obj("EndAnchor", root, new Vector3(0, 0, Len / 2));

            var so = new SerializedObject(seg);
            so.FindProperty("startAnchor").objectReferenceValue = a0.transform;
            so.FindProperty("endAnchor").objectReferenceValue = a1.transform;
            so.FindProperty("lamp").objectReferenceValue = lamp;
            so.FindProperty("bulb").objectReferenceValue = bulb;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Ward0] Mine tunnel built: " + PrefabPath);
        }

        // ============================================================ materials
        static void BuildTunnelMaterials()
        {
            var rock = TexKit.RockSurface(TexDir, "Rock");
            var rockMat = Mk("MineRock", Color.white, 0f, 1f, rock.Albedo, rock.Normal, rock.MetalGloss, 1.4f);
            rockMat.SetFloat("_Cull", 0f);                                  // two-sided so the shell also reads from outside
            var grav = TexKit.GravelSurface(TexDir, "Gravel");
            Mk("MineFloor", Color.white, 0f, 1f, grav.Albedo, grav.Normal, grav.MetalGloss, 1.2f);
            var wood = TexKit.WoodSurface(TexDir, "Wood");
            Mk("MineWood", Color.white, 0f, 1f, wood.Albedo, wood.Normal, wood.MetalGloss, 1.0f);

            var set = TexKit.BuildMetal(TexDir, 1024, "MineMetal", 0.08f);
            Mk("RustSteel", new Color(0.95f, 0.72f, 0.58f), 1f, 1f, set.Gun, set.Normal, set.MetalGloss, 1.0f);
            Mk("RustDark", new Color(0.9f, 0.7f, 0.58f), 1f, 1f, set.Dark, set.Normal, set.MetalGloss, 0.9f);
            mats["DarkIron"] = mats["RustDark"]; mats["Gunmetal"] = mats["RustSteel"];      // aliases for shared rivet helpers

            Mk("Rubber", new Color(0.03f, 0.03f, 0.03f), 0f, 0.4f);
            Mk("Brass", new Color(0.45f, 0.34f, 0.12f), 0.9f, 0.4f);
            Mk("BlackPlastic", new Color(0.04f, 0.04f, 0.045f), 0.1f, 0.5f);
            Emissive("LampBulb", new Color(1f, 0.75f, 0.4f), new Color(3.4f, 1.9f, 0.6f), null, 0.9f);
            Mk("Puddle", new Color(0.015f, 0.018f, 0.02f), 0f, 0.96f);
        }

        // ============================================================ rock shell
        // Profile of the tunnel cross-section, parametrised by arc length s (0..Total). Returns point + inward normal.
        static readonly Vector2 LeftFoot = new Vector2(-2.25f, -0.3f), RightFoot = new Vector2(2.25f, -0.3f);
        static float _wedge, _wall, _arc, _total;

        static void ProfileInit()
        {
            var a = new Vector2(-HalfW, 0.12f);
            _wedge = Vector2.Distance(LeftFoot, a); _wall = CY - 0.12f; _arc = Mathf.PI * HalfW;
            _total = 2f * (_wedge + _wall) + _arc;
        }

        static void Profile(float s, out Vector2 pos, out Vector2 inward)
        {
            if (s < _wedge)
            {
                var a = new Vector2(-HalfW, 0.12f); float t = s / _wedge;
                pos = Vector2.Lerp(LeftFoot, a, t); inward = new Vector2(0.45f, 0.9f).normalized; return;
            }
            s -= _wedge;
            if (s < _wall) { pos = new Vector2(-HalfW, 0.12f + s); inward = Vector2.right; return; }
            s -= _wall;
            if (s < _arc)
            {
                float th = Mathf.PI - s / HalfW;
                pos = new Vector2(Mathf.Cos(th) * HalfW, CY + Mathf.Sin(th) * HalfW); inward = -new Vector2(Mathf.Cos(th), Mathf.Sin(th)); return;
            }
            s -= _arc;
            if (s < _wall) { pos = new Vector2(HalfW, CY - s); inward = Vector2.left; return; }
            s -= _wall;
            { var a = new Vector2(HalfW, 0.12f); float t = Mathf.Clamp01(s / _wedge); pos = Vector2.Lerp(a, RightFoot, t); inward = new Vector2(-0.45f, 0.9f).normalized; }
        }

        static Mesh BuildShell()
        {
            ProfileInit();
            int N = 96, M = 60;
            var v = new Vector3[(N + 1) * (M + 1)]; var uv = new Vector2[v.Length];
            for (int k = 0; k <= M; k++)
            {
                float z = -Len / 2 + Len * k / M; float vv = (float)(k % M) / M;     // k==M wraps to k==0 -> tileable
                for (int i = 0; i <= N; i++)
                {
                    float s = _total * i / N; Profile(s, out var pos, out var inw);
                    float uu = Mathf.Repeat(s / _total, 1f);
                    float big = TexKit.Fbm(uu * 0.999f, vv, 5, 5, 71);
                    float mid = TexKit.Fbm(uu * 0.999f, vv, 14, 4, 33);
                    float disp = Mathf.Pow(big, 1.2f) * 0.75f + mid * 0.16f;           // carve OUTWARD only (0..~0.6)
                    // chunky facets: quantise a little so the surface reads as broken rock
                    disp = Mathf.Round(disp * 9f) / 9f * 0.65f + disp * 0.35f;
                    float edge = Mathf.Clamp01(Mathf.Min(i, N - i) / 6f);                // pin the feet so the shell meets the floor
                    pos -= inw * disp * edge;
                    int idx = k * (N + 1) + i;
                    v[idx] = new Vector3(pos.x, pos.y, z); uv[idx] = new Vector2(s * 0.5f, z * 0.5f);
                }
            }
            var tri = new List<int>();
            for (int k = 0; k < M; k++)
                for (int i = 0; i < N; i++)
                {
                    int a = k * (N + 1) + i, b = a + 1, c = a + N + 1, d = c + 1;
                    tri.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "RockShell", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.vertices = v; m.uv = uv; m.triangles = tri.ToArray();
            m.RecalculateNormals();
            // make sure the visible (front) side faces into the tunnel
            var n0 = m.normals[(M / 2) * (N + 1) + N / 2];
            if (Vector3.Dot(n0, new Vector3(0, CY + 1f, 0) - v[(M / 2) * (N + 1) + N / 2]) < 0f) { tri.Reverse(); m.triangles = tri.ToArray(); m.RecalculateNormals(); }
            // tileable normals along z: average the seam rings
            var nrm = m.normals;
            for (int i = 0; i <= N; i++) { var avg = (nrm[i] + nrm[M * (N + 1) + i]).normalized; nrm[i] = avg; nrm[M * (N + 1) + i] = avg; }
            m.normals = nrm;
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        static Mesh BuildFloor()
        {
            int NX = 56, NZ = 60; float W = 4.6f;
            var v = new Vector3[(NX + 1) * (NZ + 1)]; var uv = new Vector2[v.Length];
            for (int k = 0; k <= NZ; k++)
                for (int i = 0; i <= NX; i++)
                {
                    float x = -W / 2 + W * i / NX, z = -Len / 2 + Len * k / NZ;
                    float u = (float)i / NX, vv = (float)(k % NZ) / NZ;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 1.9f, Mathf.Abs(x)));
                    float lumps = (TexKit.Fbm(u * 0.999f, vv, 6, 4, 9) - 0.35f) * 0.28f + (TexKit.Fbm(u * 0.999f, vv, 22, 3, 4) - 0.5f) * 0.05f;
                    float y = lumps * edge + 0.22f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.55f, 2.3f, Mathf.Abs(x))) - 0.02f * (1f - edge);
                    v[k * (NX + 1) + i] = new Vector3(x, y, z); uv[k * (NX + 1) + i] = new Vector2(x * 0.5f, z * 0.5f);
                }
            var tri = new List<int>();
            for (int k = 0; k < NZ; k++)
                for (int i = 0; i < NX; i++)
                {
                    int a = k * (NX + 1) + i, b = a + 1, c = a + NX + 1, d = c + 1;
                    tri.AddRange(new[] { a, c, b, b, c, d });
                }
            var m = new Mesh { name = "Floor", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            m.vertices = v; m.uv = uv; m.triangles = tri.ToArray(); m.RecalculateNormals();
            if (m.normals[(NZ / 2) * (NX + 1) + NX / 2].y < 0f) { tri.Reverse(); m.triangles = tri.ToArray(); m.RecalculateNormals(); }
            var nrm = m.normals;
            for (int i = 0; i <= NX; i++) { var avg = (nrm[i] + nrm[NZ * (NX + 1) + i]).normalized; nrm[i] = avg; nrm[NZ * (NX + 1) + i] = avg; }
            m.normals = nrm; m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }

        // ============================================================ steel arch ribs
        static void BuildRibs()
        {
            foreach (float z in RibZ) Rib(z);
            // longitudinal angle-iron stringers tying the ribs together at shoulders and crown
            foreach (float ang in new[] { 40f, 90f, 140f })
            {
                float a = ang * Mathf.Deg2Rad; var p = new Vector3(Mathf.Cos(a) * (RibR - 0.1f), CY + Mathf.Sin(a) * (RibR - 0.1f), 0f);
                Box("RustDark", p, new Vector3(0.06f, 0.06f, Len), 0.008f, new Vector3(0, 0, ang - 90f));
            }
        }

        static void Rib(float z)
        {
            int seg = 16; float chord = 2f * RibR * Mathf.Sin(Mathf.PI / seg / 2f);
            for (int j = 0; j < seg; j++)
            {
                float th = Mathf.PI * (j + 0.5f) / seg; float deg = th * Mathf.Rad2Deg + 90f;
                Vector3 dir = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f); Vector3 c = new Vector3(0, CY, z);
                Box("RustSteel", c + dir * RibR, new Vector3(chord * 1.07f, 0.13f, 0.035f), 0.004f, new Vector3(0, 0, deg));          // web (radial)
                Box("RustSteel", c + dir * (RibR - 0.075f), new Vector3(chord * 1.07f, 0.026f, 0.2f), 0.004f, new Vector3(0, 0, deg)); // inner flange
                Box("RustSteel", c + dir * (RibR + 0.075f), new Vector3(chord * 1.07f, 0.026f, 0.2f), 0.004f, new Vector3(0, 0, deg)); // outer flange
            }
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * RibR;
                Box("RustSteel", new Vector3(x, CY / 2, z), new Vector3(0.13f, CY, 0.035f), 0.004f);
                Box("RustSteel", new Vector3(x - sx * 0.075f, CY / 2, z), new Vector3(0.026f, CY, 0.2f), 0.004f);
                Box("RustSteel", new Vector3(x + sx * 0.075f, CY / 2, z), new Vector3(0.026f, CY, 0.2f), 0.004f);
                Box("RustDark", new Vector3(x, 0.02f, z), new Vector3(0.36f, 0.04f, 0.36f), 0.008f);                                // foot plate
                RivetRect(new Vector3(x, 0.04f, z), 0.28f, 0.28f, 3, 3, Vector3.up, Vector3.right, Vector3.forward);
                Box("RustDark", new Vector3(x - sx * 0.087f, CY - 0.05f, z), new Vector3(0.014f, 0.46f, 0.22f), 0.004f);               // fish plate at the knee
                RivetRect(new Vector3(x - sx * 0.096f, CY - 0.05f, z), 0.14f, 0.38f, 2, 4, new Vector3(-sx, 0, 0), Vector3.forward, Vector3.up);
                Box("RustDark", new Vector3(x, 0.5f, z - 0.115f), new Vector3(0.2f, 0.14f, 0.03f), 0.005f);                            // gusset
            }
            Box("RustDark", new Vector3(0, CY + RibR - 0.09f, z), new Vector3(0.5f, 0.016f, 0.22f), 0.004f);                           // crown plate
            RivetRect(new Vector3(0, CY + RibR - 0.1f, z), 0.44f, 0.16f, 4, 2, Vector3.down, Vector3.right, Vector3.forward);
        }

        // ============================================================ timber lagging
        static void BuildLagging()
        {
            // bays between the ribs (half bays at the ends continue into the neighbouring segment)
            var bays = new List<Vector2> { new Vector2(-3f + 0.0f, -2.1f), new Vector2(-1.9f, -0.1f), new Vector2(0.1f, 1.9f), new Vector2(2.1f, 3f) };
            foreach (var b in bays)
            {
                float zc = (b.x + b.y) / 2f, len = b.y - b.x;
                foreach (float sx in new[] { -1f, 1f })
                    for (int k = 0; k < 6; k++)
                    {
                        if (R() < 0.5f) continue;                                            // missing / rotted plank
                        float y = 0.16f + k * 0.2f;
                        Box("MineWood", new Vector3(sx * (1.745f + R(0f, 0.012f)), y + R(-0.01f, 0.01f), zc + R(-0.03f, 0.03f)), new Vector3(0.05f, 0.19f, len * R(0.9f, 1.0f)), 0.008f, new Vector3(0, R(-0.8f, 0.8f), R(-1f, 1f)));
                    }
                int segs = 16;
                for (int j = 1; j < segs - 1; j++)
                {
                    if (R() < 0.62f) continue;
                    float th = Mathf.PI * (j + 0.5f) / segs; var dir = new Vector3(Mathf.Cos(th), Mathf.Sin(th), 0f);
                    float chord = 2f * 1.75f * Mathf.Sin(Mathf.PI / segs / 2f);
                    Box("MineWood", new Vector3(0, CY, zc) + dir * 1.752f, new Vector3(chord * 0.96f, 0.05f, len * R(0.88f, 1f)), 0.008f, new Vector3(0, 0, th * Mathf.Rad2Deg + 90f));
                }
            }
        }

        // ============================================================ rails, sleepers, ballast
        static void BuildRails()
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 0.40f;
                Box("RustDark", new Vector3(x, 0.215f, 0f), new Vector3(0.068f, 0.04f, Len), 0.006f);
                Box("RustDark", new Vector3(x, 0.155f, 0f), new Vector3(0.026f, 0.085f, Len), 0.003f);
                Box("RustDark", new Vector3(x, 0.112f, 0f), new Vector3(0.115f, 0.022f, Len), 0.004f);
            }
            for (int i = 0; i < 10; i++)
            {
                float z = -2.7f + i * 0.6f;
                Box("MineWood", new Vector3(R(-0.02f, 0.02f), 0.05f + R(-0.008f, 0.008f), z), new Vector3(1.25f, 0.1f, 0.17f), 0.012f, new Vector3(R(-1f, 1f), R(-2.5f, 2.5f), R(-1f, 1f)));
                foreach (float sx in new[] { -1f, 1f })
                    foreach (float side in new[] { -1f, 1f })
                        Box("RustDark", new Vector3(sx * 0.40f + side * 0.062f, 0.13f, z + R(-0.01f, 0.01f)), new Vector3(0.022f, 0.045f, 0.022f), 0.003f);   // spikes
            }
        }

        static void BuildRubble()
        {
            // loose ballast stones around the track
            for (int i = 0; i < 110; i++)
            {
                float s = R(0.04f, 0.13f); float x = R(-1.35f, 1.35f); if (Mathf.Abs(Mathf.Abs(x) - 0.4f) < 0.06f) continue;
                float z = R(-2.95f, 2.95f);
                cb.Add(Mirror(MeshKit.Box(new Vector3(s, s * 0.7f, s * 0.9f), s * 0.3f, 1f)), M("MineFloor"), Zm(new Vector3(x, 0.02f + s * 0.25f, z)), ZQ(Quaternion.Euler(R(0, 360), R(0, 360), R(0, 360))));
            }
            // boulders slumped against the walls
            for (int i = 0; i < 9; i++)
            {
                float s = R(0.3f, 0.7f); float sx = R() < 0.5f ? -1f : 1f; float z = R(-2.6f, 2.6f);
                Box("MineFloor", new Vector3(sx * R(1.45f, 1.8f), s * 0.3f, z), new Vector3(s, s * 0.7f, s * 0.9f), s * 0.28f, new Vector3(R(-15, 15), R(0, 360), R(-15, 15)));
            }
            // standing water between the rails
            Add(MeshKit.Quad(0.9f, 1.8f), M("Puddle"), new Vector3(0.0f, 0.012f, -0.9f), Quaternion.Euler(-90, 0, 0));
        }

        // ============================================================ pipes
        static void BuildPipes()
        {
            const float px = 1.42f, py = 1.12f;
            Cyl("RustSteel", new Vector3(px, py, 0), 0.095f, Len, new Vector3(90, 0, 0), 22, 0.004f);               // main pipe
            Cyl("RustDark", new Vector3(px + 0.04f, 0.72f, 0), 0.05f, Len, new Vector3(90, 0, 0), 16, 0.003f);      // small pipe
            foreach (float z in new[] { -2.95f, -1.5f, 1.5f, 2.95f })
            {
                Cyl("RustDark", new Vector3(px, py, z), 0.145f, 0.045f, new Vector3(90, 0, 0), 22, 0.005f);
                for (int b = 0; b < 8; b++)
                {
                    float a = b / 8f * Mathf.PI * 2f; var p = new Vector3(px + Mathf.Cos(a) * 0.12f, py + Mathf.Sin(a) * 0.12f, z + 0.0225f);
                    Rivet(p, Vector3.forward, "RustSteel");
                }
                Cyl("RustSteel", new Vector3(px, py, z), 0.11f, 0.22f, new Vector3(90, 0, 0), 22, 0.01f);
                Cyl("RustDark", new Vector3(px + 0.04f, 0.72f, z), 0.075f, 0.05f, new Vector3(90, 0, 0), 16, 0.004f);
            }
            foreach (float z in RibZ)
            {
                Cyl("RustDark", new Vector3(px, py, z), 0.108f, 0.06f, new Vector3(90, 0, 0), 20, 0.004f);           // clamp collar
                Box("RustDark", new Vector3(px + 0.1f, py + 0.0f, z), new Vector3(0.16f, 0.05f, 0.07f), 0.006f);
                Cyl("RustDark", new Vector3(px + 0.04f, 0.72f, z), 0.058f, 0.05f, new Vector3(90, 0, 0), 16, 0.003f);
                Box("RustDark", new Vector3(px + 0.1f, 0.72f, z), new Vector3(0.12f, 0.04f, 0.06f), 0.005f);
            }
            // gate valve with hand wheel on the main pipe
            const float vz = -1.0f;
            Cyl("RustDark", new Vector3(px, py + 0.16f, vz), 0.078f, 0.22f, null, 20, 0.008f);
            Box("RustDark", new Vector3(px, py + 0.3f, vz), new Vector3(0.14f, 0.05f, 0.14f), 0.01f);
            Cyl("RustSteel", new Vector3(px, py + 0.4f, vz), 0.016f, 0.18f, null, 10, 0.002f);
            var ring = new List<Vector2>();
            for (int i = 0; i <= 10; i++) { float a = i / 10f * Mathf.PI * 2f; ring.Add(new Vector2(0.09f + Mathf.Cos(a) * 0.012f, Mathf.Sin(a) * 0.012f)); }
            Add(MeshKit.Lathe(ring.ToArray(), 28, true), M("RustSteel"), new Vector3(px, py + 0.5f, vz));
            for (int k = 0; k < 4; k++)
                Box("RustSteel", new Vector3(px, py + 0.5f, vz) + Quaternion.Euler(0, k * 45f, 0) * Vector3.right * 0f, new Vector3(0.18f, 0.016f, 0.016f), 0.002f, new Vector3(0, k * 45f, 0));
            Cyl("Brass", new Vector3(px, py + 0.51f, vz), 0.02f, 0.03f, null, 10, 0.003f);
            // pressure gauge on the small pipe
            Cyl("RustDark", new Vector3(px + 0.04f, 0.8f, 1.0f), 0.018f, 0.1f, null, 10, 0.002f);
            Cyl("Brass", new Vector3(px + 0.04f, 0.87f, 1.0f), 0.05f, 0.035f, new Vector3(90, 0, 0), 20, 0.004f);
        }

        // ============================================================ cables
        static void BuildCables()
        {
            float[] angs = { 17f, 25f, 33f };
            for (int c = 0; c < angs.Length; c++)
            {
                float a = angs[c] * Mathf.Deg2Rad; float r = 1.50f - c * 0.02f;
                var pts = new List<Vector3>();
                for (int k = -3; k <= 3; k++)
                {
                    bool clip = (k % 2 == 0);
                    float sag = clip ? 0f : 0.09f + 0.025f * c;
                    pts.Add(new Vector3(Mathf.Cos(a) * r, CY + Mathf.Sin(a) * r - sag, k));
                }
                Put(MeshKit.Tube(pts.ToArray(), 0.016f - c * 0.002f, 8, 10), "Rubber", Vector3.zero);
            }
            foreach (float z in RibZ)
                for (int c = 0; c < angs.Length; c++)
                {
                    float a = angs[c] * Mathf.Deg2Rad; float r = 1.50f - c * 0.02f;
                    Box("RustDark", new Vector3(Mathf.Cos(a) * r, CY + Mathf.Sin(a) * r, z), new Vector3(0.045f, 0.045f, 0.045f), 0.006f, new Vector3(0, 0, angs[c]));
                }
            // thin cable on the left, lamp feed along the crown
            var left = new List<Vector3>(); var crown = new List<Vector3>();
            for (int k = -3; k <= 3; k++)
            {
                float sag = (k % 2 == 0) ? 0f : 0.08f;
                float a = 148f * Mathf.Deg2Rad; left.Add(new Vector3(Mathf.Cos(a) * 1.5f, CY + Mathf.Sin(a) * 1.5f - sag, k));
                crown.Add(new Vector3(0.12f, CY + 1.5f - sag * 0.8f, k));
            }
            Put(MeshKit.Tube(left.ToArray(), 0.012f, 8, 10), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(crown.ToArray(), 0.011f, 8, 10), "Rubber", Vector3.zero);
        }

        // ============================================================ lamp
        static Light BuildLamp(out Renderer bulb)
        {
            var p = new Vector3(0.12f, CY + 1.5f - 0.02f, 1.0f);              // hangs on the crown feed cable, between ribs
            Put(MeshKit.Tube(new[] { p + new Vector3(0, 0.04f, 0), p + new Vector3(0, -0.12f, 0), p + new Vector3(0.0f, -0.3f, 0) }, 0.012f, 8, 6), "Rubber", Vector3.zero);
            var bp = p + new Vector3(0, -0.52f, 0);
            Cyl("Brass", bp + new Vector3(0, 0.2f, 0), 0.032f, 0.07f, null, 14, 0.004f);
            Cyl("RustDark", bp + new Vector3(0, 0.15f, 0), 0.045f, 0.03f, null, 14, 0.004f);
            bulb = Part("LampBulb", root, MeshKit.Sphere(0.04f, 20, 12, 1.15f), "LampBulb", bp);
            bulb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f; Cyl("RustDark", bp + new Vector3(Mathf.Cos(a) * 0.055f, 0f, Mathf.Sin(a) * 0.055f), 0.003f, 0.11f, null, 6, 0.0005f);
            }
            foreach (float hy in new[] { -0.05f, 0f, 0.05f })
                Add(MeshKit.Lathe(new[] { new Vector2(0.0585f, -0.0025f), new Vector2(0.0585f, 0.0025f) }, 20, true, 1f), M("RustDark"), bp + new Vector3(0, hy, 0));
            Cyl("RustDark", bp + new Vector3(0, -0.065f, 0), 0.034f, 0.016f, null, 14, 0.004f);

            var lg = Obj("LampLight", root, bp);
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.72f, 0.4f); l.intensity = 4.5f; l.range = 9f; l.shadows = LightShadows.Soft;
            return l;
        }
    }
}
