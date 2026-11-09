using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Ward0.Props;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds the "Quarantine Control Desk" prop procedurally (meshes, textures, materials, prefab),
    /// laid out from the reference concept art. Front of the desk faces +Z. Origin is floor centre.
    /// Menu: Ward0 / Build Quarantine Desk
    /// </summary>
    public static class DeskBuilder
    {
        const string Root = "Assets/_Project/Art/Desk";
        const string TexDir = Root + "/Textures";
        internal static string MatDir = Root + "/Materials";
        internal static string MeshDir = Root + "/Meshes";
        const string PrefabPath = Root + "/QuarantineDesk.prefab";

        // reference-art pixel -> metres
        const float S = 0.00163f;
        internal static float X(float px) => (px - 765f) * S;

        // The design is authored with the front toward +Z (right-handed thinking: viewer at +Z sees +X on the right).
        // Unity is left-handed, so everything is mirrored through the XY plane at build time: the desk front ends up
        // facing -Z, and a viewer standing in front sees the layout exactly like the reference art.
        internal static Vector3 Zm(Vector3 v) => new Vector3(v.x, v.y, -v.z);
        internal static Quaternion ZQ(Quaternion q) => new Quaternion(-q.x, -q.y, q.z, q.w);
        internal static readonly Dictionary<Mesh, Mesh> mirrored = new Dictionary<Mesh, Mesh>();

        internal static Mesh Mirror(Mesh src)
        {
            if (mirrored.TryGetValue(src, out var done)) return done;
            var m = new Mesh { name = src.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var v = src.vertices; var n = src.normals; var uv = src.uv; var t = src.triangles;
            for (int i = 0; i < v.Length; i++) { v[i].z = -v[i].z; n[i].z = -n[i].z; }
            for (int i = 0; i < t.Length; i += 3)
            {
                var gn = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                var an = n[t[i]] + n[t[i + 1]] + n[t[i + 2]];
                if (Vector3.Dot(gn, an) < 0f) { int tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
            }
            m.vertices = v; m.normals = n; m.uv = uv; m.triangles = t;
            m.RecalculateBounds(); m.RecalculateTangents();
            mirrored[src] = m;
            return m;
        }

        internal static Vector3 Origin = Vector3.zero;
        internal static readonly HashSet<string> SavedMeshNames = new HashSet<string>();
        internal static void Add(Mesh mesh, Material mat, Vector3 pos, Quaternion rot = default)
        {
            if (rot.x == 0 && rot.y == 0 && rot.z == 0 && rot.w == 0) rot = Quaternion.identity;
            cb.Add(Mirror(mesh), mat, Zm(pos - Origin), ZQ(rot));
        }

        internal static Combiner cb;
        internal static Transform root;
        internal static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        internal static readonly Color Paint = new Color(0.66f, 0.66f, 0.6f, 0.92f);

        [MenuItem("Ward0/Build Quarantine Desk")]
        public static void Build()
        {
            MatDir = Root + "/Materials"; MeshDir = Root + "/Meshes";
            foreach (var d in new[] { Root, TexDir, MatDir, MeshDir })
                Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            mats.Clear(); SavedMeshNames.Clear();
            cb = new Combiner();

            BuildMaterials();

            var go = new GameObject("QuarantineDesk");
            root = go.transform;
            var desk = go.AddComponent<QuarantineDesk>();

            var shell = BuildStructure();
            BuildMonitors(out var xray, out var bio);
            BuildGaugesAndPanels(out var leds);
            var lever = BuildIncinerator();
            BuildLeftSide();
            BuildUnderDesk();
            BuildToggles();
            var lamp = BuildLamp();

            BakeCombined();
            AddColliders();

            // wire runtime script
            var so = new SerializedObject(desk);
            so.FindProperty("xrayScreen").objectReferenceValue = xray;
            so.FindProperty("bioscanScreen").objectReferenceValue = bio;
            so.FindProperty("incinerateLever").objectReferenceValue = lever;
            so.FindProperty("cageLamp").objectReferenceValue = lamp;
            var arr = so.FindProperty("statusLeds");
            arr.arraySize = leds.Count;
            for (int i = 0; i < leds.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = leds[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Ward0] Quarantine desk built: " + PrefabPath + (prefab != null ? "" : " (SAVE FAILED)"));
        }

        // ============================================================ materials
        internal static Material Mk(string name, Color col, float metal, float smooth, Texture albedo = null, Texture normal = null, Texture metalGloss = null, float normalScale = 1f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", col);
            if (albedo != null) m.SetTexture("_BaseMap", albedo);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_Smoothness", smooth);
            if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
            if (metalGloss != null) { m.SetTexture("_MetallicGlossMap", metalGloss); m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); }
            string path = MatDir + "/" + name + ".mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(m, path);
            mats[name] = m;
            return m;
        }

        internal static Material Emissive(string name, Color baseCol, Color emission, Texture map = null, float smooth = 0.6f)
        {
            var m = Mk(name, baseCol, 0f, smooth, map);
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            if (map != null) m.SetTexture("_EmissionMap", map);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            return m;
        }

        internal static Material Cutout(string name, Texture tex, Color tint)
        {
            var m = Mk(name, tint, 0.3f, 0.25f, tex);
            m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
            m.SetOverrideTag("RenderType", "TransparentCutout");
            return m;
        }

        internal static void BuildMaterials()
        {
            var set = TexKit.BuildMetal(TexDir, 1024);
            Mk("Gunmetal", Color.white, 1f, 1f, set.Gun, set.Normal, set.MetalGloss, 0.8f);
            Mk("DarkIron", Color.white, 1f, 1f, set.Dark, set.Normal, set.MetalGloss, 0.7f);
            Mk("RedPaint", Color.white, 1f, 1f, set.Red, set.Normal, set.MetalGloss, 0.6f);
            Mk("Brass", new Color(0.55f, 0.42f, 0.17f), 0.9f, 0.55f);
            Mk("Chrome", new Color(0.62f, 0.62f, 0.6f), 1f, 0.7f);
            Mk("Rubber", new Color(0.035f, 0.035f, 0.035f), 0f, 0.35f);
            Mk("BlackPlastic", new Color(0.05f, 0.05f, 0.055f), 0.1f, 0.5f);
            Mk("Ceramic", new Color(0.7f, 0.68f, 0.6f), 0f, 0.55f);
            Mk("Wood", new Color(0.30f, 0.20f, 0.11f), 0f, 0.3f);
            Mk("Yellow", new Color(0.65f, 0.5f, 0.07f), 0.2f, 0.4f);

            Emissive("ScreenXRay", Color.white * 0.2f, Color.white * 1.5f, TexKit.XRayScreen(TexDir), 0.85f);
            Emissive("ScreenBioscan", Color.white * 0.2f, Color.white * 1.5f, TexKit.BioscanScreen(TexDir), 0.85f);
            TexKit.XRayScreen(TexDir, "Screen_XRay_Anomaly", true); TexKit.BioscanScreen(TexDir, "Screen_Bioscan_Anomaly", true);
            Emissive("LedGreen", new Color(0.02f, 0.1f, 0.02f), new Color(0.2f, 3f, 0.4f));
            Emissive("LedAmber", new Color(0.12f, 0.06f, 0.0f), new Color(3.2f, 1.4f, 0.1f));
            Emissive("LedRed", new Color(0.12f, 0.0f, 0.0f), new Color(3.2f, 0.15f, 0.1f));
            Emissive("LampBulb", new Color(1f, 0.8f, 0.45f), new Color(5f, 3.1f, 1.1f), null, 0.9f);

            Mk("GaugeRadiation", Color.white, 0f, 0.8f, TexKit.GaugeFace(TexDir, "Gauge_Radiation", 0.7f));
            Mk("GaugeAir", Color.white, 0f, 0.8f, TexKit.GaugeFace(TexDir, "Gauge_Air", 0.85f));
            Mk("GaugeContainment", Color.white, 0f, 0.8f, TexKit.GaugeFace(TexDir, "Gauge_Containment", 0.5f));
            Mk("Paper", Color.white, 0f, 0.15f, TexKit.Paper(TexDir));
            Mk("HazardStripes", Color.white, 0.1f, 0.35f, TexKit.HazardStripes(TexDir));
            Cutout("BiohazardPaint", TexKit.Biohazard(TexDir, "Biohazard_Stencil", new Color(0.55f, 0.55f, 0.5f)), Color.white);
            Cutout("BiohazardBlack", TexKit.Biohazard(TexDir, "Biohazard_Dark", new Color(0.05f, 0.05f, 0.05f)), Color.white);
            Mk("WarnYellow", new Color(0.7f, 0.55f, 0.08f), 0.2f, 0.35f);
        }

        internal static Material M(string n) => mats[n];

        // ============================================================ helpers
        internal static void Put(Mesh m, string mat, Vector3 pos, Vector3? euler = null)
            => Add(m, M(mat), pos, Quaternion.Euler(euler ?? Vector3.zero));

        internal static void Box(string mat, Vector3 c, Vector3 size, float bevel = 0.01f, Vector3? euler = null)
            => Put(MeshKit.Box(size, bevel, 1f), mat, c, euler);

        internal static void Cyl(string mat, Vector3 c, float r, float h, Vector3? euler = null, int seg = 24, float bevel = 0.003f)
            => Put(MeshKit.Cylinder(r, h, seg, bevel), mat, c, euler);

        internal static Mesh _rivet;
        internal static Mesh RivetMesh => _rivet != null ? _rivet : (_rivet = MeshKit.Cylinder(0.0075f, 0.007f, 10, 0.002f));

        internal static void Rivet(Vector3 pos, Vector3 normal, string mat = "DarkIron")
            => Add(RivetMesh, M(mat), pos, Quaternion.FromToRotation(Vector3.up, normal));

        /// <summary>Row of rivets along a line on a face.</summary>
        internal static void RivetLine(Vector3 a, Vector3 b, int count, Vector3 normal, string mat = "DarkIron")
        {
            for (int i = 0; i < count; i++) Rivet(Vector3.Lerp(a, b, count == 1 ? 0.5f : (float)i / (count - 1)) + normal * 0.0035f, normal, mat);
        }

        internal static void RivetRect(Vector3 center, float w, float h, int nx, int ny, Vector3 normal, Vector3 right, Vector3 up, string mat = "DarkIron")
        {
            Vector3 bl = center - right * w / 2 - up * h / 2, br = center + right * w / 2 - up * h / 2, tl = center - right * w / 2 + up * h / 2, tr = center + right * w / 2 + up * h / 2;
            RivetLine(bl, br, nx, normal, mat); RivetLine(tl, tr, nx, normal, mat);
            if (ny > 2) { RivetLine(Vector3.Lerp(bl, tl, 1f / (ny - 1)), Vector3.Lerp(bl, tl, (ny - 2f) / (ny - 1)), ny - 2, normal, mat); RivetLine(Vector3.Lerp(br, tr, 1f / (ny - 1)), Vector3.Lerp(br, tr, (ny - 2f) / (ny - 1)), ny - 2, normal, mat); }
        }

        internal static Mesh ScreenMesh(float w, float h, float bulge, int nx = 14, int ny = 10)
        {
            var m = new Mesh { name = "ScreenBulge" };
            var v = new Vector3[(nx + 1) * (ny + 1)]; var uv = new Vector2[v.Length]; var n = new Vector3[v.Length];
            for (int y = 0; y <= ny; y++)
                for (int x = 0; x <= nx; x++)
                {
                    float u = (float)x / nx, t = (float)y / ny; float cx = u * 2 - 1, cy = t * 2 - 1;
                    float z = bulge * (1f - (cx * cx + cy * cy) * 0.5f);
                    int i = y * (nx + 1) + x;
                    v[i] = new Vector3((u - 0.5f) * w, (t - 0.5f) * h, z); uv[i] = new Vector2(u, t);
                    n[i] = new Vector3(cx * bulge * 1.5f, cy * bulge * 1.5f, 1f).normalized;
                }
            var tri = new List<int>();
            for (int y = 0; y < ny; y++)
                for (int x = 0; x < nx; x++)
                {
                    int a = y * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                    tri.AddRange(new[] { a, d, b, a, c, d });
                }
            m.vertices = v; m.uv = uv; m.normals = n; m.triangles = tri.ToArray();
            var tg = new Vector4[v.Length]; for (int i = 0; i < tg.Length; i++) tg[i] = new Vector4(1, 0, 0, -1); m.tangents = tg;
            m.RecalculateBounds(); return m;
        }

        internal static Mesh DiscMesh(float r, int seg = 40)
        {
            var m = new Mesh { name = "Disc" };
            var v = new List<Vector3> { Vector3.zero }; var uv = new List<Vector2> { new Vector2(0.5f, 0.5f) }; var n = new List<Vector3> { Vector3.forward };
            var tri = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f; v.Add(new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0)); uv.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f)); n.Add(Vector3.forward);
                if (i > 0) { tri.Add(0); tri.Add(i + 1); tri.Add(i); }
            }
            m.SetVertices(v); m.SetUVs(0, uv); m.SetNormals(n); m.SetTriangles(tri, 0);
            var tg = new Vector4[v.Count]; for (int i = 0; i < tg.Length; i++) tg[i] = new Vector4(1, 0, 0, -1); m.tangents = tg;
            m.RecalculateBounds(); return m;
        }

        // separate (non-combined) objects --------------------------------------------------------
        internal static string SaveMesh(Mesh m, string name)
        {
            string unique = name; int n = 2;
            while (!SavedMeshNames.Add(MeshDir + "/" + unique)) unique = name + "_" + n++;
            m.name = unique; name = unique;
            string p = MeshDir + "/" + name + ".asset";
            AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
            return p;
        }

        internal static GameObject Obj(string name, Transform parent, Vector3 pos, Vector3? euler = null)
        {
            var g = new GameObject(name);
            g.transform.SetParent(parent != null ? parent : root, false);
            g.transform.localPosition = Zm(pos); g.transform.localRotation = ZQ(Quaternion.Euler(euler ?? Vector3.zero));
            return g;
        }

        internal static MeshRenderer Part(string name, Transform parent, Mesh mesh, string mat, Vector3 pos, Vector3? euler = null)
        {
            var g = Obj(name, parent, pos, euler);
            mesh = Mirror(mesh); SaveMesh(mesh, "Part_" + name);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = M(mat);
            return r;
        }

        internal static TextMeshPro Label(string text, Vector3 pos, Vector3 euler, float size, Transform parent = null, float width = 0.5f, TextAlignmentOptions align = TextAlignmentOptions.Center,
                                 FontStyles style = FontStyles.Bold, Color? col = null, float spacing = 6f)
        {
            var g = Obj("Label_" + (text.Length > 14 ? text.Substring(0, 14) : text).Replace("\n", " "), parent, pos, euler);
            var t = g.AddComponent<TextMeshPro>();
            t.text = text; t.fontSize = size * 10f; t.fontStyle = style; t.alignment = align;
            t.color = col ?? Paint; t.characterSpacing = spacing; t.textWrappingMode = TextWrappingModes.NoWrap;
            t.rectTransform.sizeDelta = new Vector2(width, size * 4f);
            bool left = align == TextAlignmentOptions.Left, right = align == TextAlignmentOptions.Right;
            t.rectTransform.pivot = new Vector2(left ? 0f : (right ? 1f : 0.5f), 0.5f);
            t.raycastTarget = false;
            return t;
        }

        // ============================================================ structure
        const float TopY = 0.80f;

        internal static Transform BuildStructure()
        {
            // tabletop slab + underside apron
            Box("Gunmetal", new Vector3(0, 0.76f, 0), new Vector3(2.42f, 0.08f, 0.92f), 0.014f);
            Box("DarkIron", new Vector3(0, 0.695f, 0), new Vector3(2.28f, 0.05f, 0.80f), 0.01f);
            // raised lip trims along the tabletop edge
            Box("DarkIron", new Vector3(0, 0.806f, 0.445f), new Vector3(2.38f, 0.012f, 0.03f), 0.004f);
            Box("DarkIron", new Vector3(-1.195f, 0.806f, 0), new Vector3(0.03f, 0.012f, 0.9f), 0.004f);
            Box("DarkIron", new Vector3(1.195f, 0.806f, 0), new Vector3(0.03f, 0.012f, 0.9f), 0.004f);
            RivetLine(new Vector3(-1.1f, TopY + 0.015f, 0.43f), new Vector3(1.1f, TopY + 0.015f, 0.43f), 26, Vector3.up);
            // front edge rivets + corner plates
            RivetLine(new Vector3(-1.15f, 0.77f, 0.4635f), new Vector3(1.15f, 0.77f, 0.4635f), 28, Vector3.forward);
            Box("DarkIron", new Vector3(-1.19f, 0.76f, 0.44f), new Vector3(0.05f, 0.085f, 0.06f), 0.008f);
            Box("DarkIron", new Vector3(1.19f, 0.76f, 0.44f), new Vector3(0.05f, 0.085f, 0.06f), 0.008f);

            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 0.90f;
                Box("Gunmetal", new Vector3(x, 0.39f, 0.02f), new Vector3(0.58f, 0.66f, 0.80f), 0.014f);          // body
                Box("DarkIron", new Vector3(x, 0.03f, 0.02f), new Vector3(0.64f, 0.06f, 0.86f), 0.012f);        // plinth
                Box("DarkIron", new Vector3(x, 0.695f, 0.02f), new Vector3(0.62f, 0.05f, 0.84f), 0.012f);       // top cap
                // reinforcement ribs on the outer side
                for (int i = 0; i < 3; i++)
                    Box("DarkIron", new Vector3(sx * (0.90f + 0.298f), 0.2f + i * 0.2f, 0.02f), new Vector3(0.016f, 0.015f, 0.74f), 0.004f);
                Box("DarkIron", new Vector3(sx * (0.90f + 0.298f), 0.39f, -0.36f), new Vector3(0.018f, 0.62f, 0.03f), 0.004f);
                Box("DarkIron", new Vector3(sx * (0.90f + 0.298f), 0.39f, 0.40f), new Vector3(0.018f, 0.62f, 0.03f), 0.004f);
                // inner side bolts
                RivetLine(new Vector3(sx * (0.90f - 0.29f), 0.1f, 0.4f), new Vector3(sx * (0.90f - 0.29f), 0.62f, 0.4f), 6, new Vector3(-sx, 0, 0));
                // front corner posts
                Box("DarkIron", new Vector3(x - 0.28f, 0.39f, 0.425f), new Vector3(0.025f, 0.64f, 0.025f), 0.005f);
                Box("DarkIron", new Vector3(x + 0.28f, 0.39f, 0.425f), new Vector3(0.025f, 0.64f, 0.025f), 0.005f);
            }

            // --- left pedestal: three drawers
            for (int i = 0; i < 3; i++)
            {
                float y = 0.595f - i * 0.2f;
                Box("Gunmetal", new Vector3(-0.90f, y, 0.425f), new Vector3(0.50f, 0.18f, 0.026f), 0.008f);
                Box("DarkIron", new Vector3(-0.90f, y, 0.4375f), new Vector3(0.21f, 0.058f, 0.012f), 0.004f);          // handle pocket
                Box("Gunmetal", new Vector3(-0.90f, y - 0.018f, 0.447f), new Vector3(0.18f, 0.012f, 0.022f), 0.003f);  // finger lip
                RivetRect(new Vector3(-0.90f, y, 0.4385f), 0.46f, 0.15f, 5, 2, Vector3.forward, Vector3.right, Vector3.up);
            }
            Label("QC-7", new Vector3(-1.045f, 0.095f, 0.4395f), Vector3.zero, 0.045f);

            // --- right pedestal: containment locker
            Box("Gunmetal", new Vector3(0.90f, 0.39f, 0.425f), new Vector3(0.50f, 0.60f, 0.024f), 0.008f);
            Box("DarkIron", new Vector3(0.90f, 0.39f, 0.4375f), new Vector3(0.43f, 0.52f, 0.012f), 0.006f);
            Box("Gunmetal", new Vector3(0.90f, 0.39f, 0.4445f), new Vector3(0.39f, 0.47f, 0.008f), 0.004f);
            for (int i = 0; i < 7; i++)
                Box("BlackPlastic", new Vector3(0.90f, 0.575f - i * 0.0175f, 0.4505f), new Vector3(0.30f, 0.009f, 0.006f), 0.002f);
            Box("DarkIron", new Vector3(0.90f, 0.575f - 3 * 0.0175f, 0.4465f), new Vector3(0.325f, 0.14f, 0.004f), 0.002f);
            Cyl("Chrome", new Vector3(1.065f, 0.37f, 0.4535f), 0.014f, 0.016f, new Vector3(90, 0, 0), 14);
            Box("Chrome", new Vector3(1.065f, 0.37f, 0.4675f), new Vector3(0.028f, 0.09f, 0.016f), 0.004f);
            foreach (float yy in new[] { 0.55f, 0.23f }) Box("Chrome", new Vector3(0.715f, yy, 0.4535f), new Vector3(0.024f, 0.07f, 0.018f), 0.004f);
            RivetRect(new Vector3(0.90f, 0.39f, 0.449f), 0.37f, 0.45f, 6, 5, Vector3.forward, Vector3.right, Vector3.up);
            Put(MeshKit.Quad(0.2f, 0.2f), "BiohazardPaint", new Vector3(0.875f, 0.225f, 0.4562f));
            Label("BIOHAZARD\nCONTAINMENT\nUNIT", new Vector3(0.875f, 0.415f, 0.4562f), Vector3.zero, 0.026f, null, 0.34f, TextAlignmentOptions.Center, FontStyles.Bold, null, 3f);

            // --- back panel between pedestals
            Box("DarkIron", new Vector3(0, 0.38f, -0.385f), new Vector3(1.22f, 0.64f, 0.03f), 0.008f);
            return root;
        }

        // ============================================================ monitors
        internal static void BuildMonitors(out Renderer xray, out Renderer bio)
        {
            // riser deck the monitors sit on
            Box("Gunmetal", new Vector3(0, 0.89f, -0.2f), new Vector3(1.48f, 0.18f, 0.5f), 0.016f);
            Box("DarkIron", new Vector3(0, 0.805f, 0.06f), new Vector3(1.40f, 0.012f, 0.05f), 0.004f);
            RivetLine(new Vector3(-0.68f, 0.95f, 0.054f), new Vector3(0.68f, 0.95f, 0.054f), 18, Vector3.forward);

            float[] xs = { -0.36f, 0.33f };
            string[] sm = { "ScreenXRay", "ScreenBioscan" };
            var rend = new Renderer[2];
            for (int k = 0; k < 2; k++)
            {
                float x = xs[k];
                Box("DarkIron", new Vector3(x, 1.285f, -0.22f), new Vector3(0.69f, 0.61f, 0.5f), 0.022f);              // housing
                Box("Gunmetal", new Vector3(x, 1.285f, 0.0185f), new Vector3(0.65f, 0.57f, 0.03f), 0.012f);          // front plate
                // bezel: raised frame around the opening
                float sw = 0.50f, sh = 0.375f, bz = 0.045f, zf = 0.045f;
                Box("DarkIron", new Vector3(x, 1.285f + sh / 2 + bz / 2, zf), new Vector3(sw + bz * 2, bz, 0.036f), 0.008f);
                Box("DarkIron", new Vector3(x, 1.285f - sh / 2 - bz / 2, zf), new Vector3(sw + bz * 2, bz, 0.036f), 0.008f);
                Box("DarkIron", new Vector3(x - sw / 2 - bz / 2, 1.285f, zf), new Vector3(bz, sh, 0.036f), 0.008f);
                Box("DarkIron", new Vector3(x + sw / 2 + bz / 2, 1.285f, zf), new Vector3(bz, sh, 0.036f), 0.008f);
                // recessed black backing so the curved screen sits in a hole
                Box("BlackPlastic", new Vector3(x, 1.285f, 0.028f), new Vector3(sw, sh, 0.01f), 0.002f);
                RivetRect(new Vector3(x, 1.285f, 0.0335f), 0.62f, 0.54f, 5, 4, Vector3.forward, Vector3.right, Vector3.up);
                // top vents
                for (int v = 0; v < 6; v++) Box("BlackPlastic", new Vector3(x - 0.2f + v * 0.08f, 1.595f, -0.22f), new Vector3(0.04f, 0.008f, 0.26f), 0.002f);
                // side handle brackets
                Box("DarkIron", new Vector3(x + (k == 0 ? -0.355f : 0.355f), 1.35f, -0.08f), new Vector3(0.02f, 0.2f, 0.16f), 0.006f);
                // curved screen (separate renderer so a Render Texture can be swapped in)
                var sr = Part(k == 0 ? "Screen_XRay" : "Screen_Bioscan", root, ScreenMesh(sw - 0.006f, sh - 0.006f, 0.014f), sm[k], new Vector3(x, 1.285f, 0.0335f));
                sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rend[k] = sr;
            }
            xray = rend[0]; bio = rend[1];
            // ---- rear of the monitor block: service panels, vents, cable glands (visible from behind / the booth side)
            Vector3 back = Vector3.back;
            foreach (float rx in new[] { -0.36f, 0.33f })
            {
                Box("Gunmetal", new Vector3(rx, 1.30f, -0.4775f), new Vector3(0.50f, 0.34f, 0.012f), 0.004f);
                RivetRect(new Vector3(rx, 1.30f, -0.4845f), 0.47f, 0.31f, 5, 4, back, Vector3.right, Vector3.up);
                for (int v = 0; v < 9; v++) Box("BlackPlastic", new Vector3(rx, 1.455f - v * 0.02f, -0.4875f), new Vector3(0.36f, 0.008f, 0.008f), 0.002f);
                Box("DarkIron", new Vector3(rx, 1.08f, -0.4745f), new Vector3(0.58f, 0.09f, 0.012f), 0.004f);
                Cyl("Chrome", new Vector3(rx - 0.16f, 1.08f, -0.485f), 0.021f, 0.02f, new Vector3(90, 0, 0), 16);
                Cyl("Chrome", new Vector3(rx + 0.16f, 1.08f, -0.485f), 0.021f, 0.02f, new Vector3(90, 0, 0), 16);
                Box("WarnYellow", new Vector3(rx, 1.08f, -0.4815f), new Vector3(0.05f, 0.05f, 0.003f), 0.001f, new Vector3(0, 0, 45));
            }
            // power cables dropping from the rear glands to the desk back
            Put(MeshKit.Tube(new[] { new Vector3(-0.52f, 1.08f, -0.50f), new Vector3(-0.54f, 1.0f, -0.60f), new Vector3(-0.50f, 0.88f, -0.62f), new Vector3(-0.42f, 0.78f, -0.55f), new Vector3(-0.40f, 0.70f, -0.42f) }, 0.018f, 10, 9, 70f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(0.17f, 1.08f, -0.50f), new Vector3(0.12f, 0.98f, -0.60f), new Vector3(0.18f, 0.86f, -0.63f), new Vector3(0.26f, 0.77f, -0.55f), new Vector3(0.28f, 0.70f, -0.42f) }, 0.018f, 10, 9, 70f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(-0.17f, 1.08f, -0.50f), new Vector3(-0.15f, 0.95f, -0.58f), new Vector3(-0.02f, 0.88f, -0.58f), new Vector3(0.05f, 0.95f, -0.50f), new Vector3(0.17f, 1.08f, -0.50f) }, 0.012f, 8, 9), "Rubber", Vector3.zero);
            // back plate of the desk top (cable cut-out frame)
            Box("DarkIron", new Vector3(0, 0.806f, -0.445f), new Vector3(2.38f, 0.012f, 0.03f), 0.004f);

            // seam between the two housings
            Box("Gunmetal", new Vector3(-0.015f, 1.285f, 0.03f), new Vector3(0.03f, 0.6f, 0.05f), 0.006f);

            // screen captions (tiny HUD text on glass)
            var teal = new Color(0.55f, 0.95f, 1f, 1f);
            Label("SCAN 0427-A", new Vector3(-0.575f, 1.466f, 0.0485f), Vector3.zero, 0.016f, null, 0.2f, TextAlignmentOptions.Left, FontStyles.Normal, teal, 2f);
            Label("THORAX X-RAY", new Vector3(-0.165f, 1.466f, 0.0485f), Vector3.zero, 0.016f, null, 0.2f, TextAlignmentOptions.Right, FontStyles.Normal, teal, 2f);
            Label("ID: 7M-3381\nDEP: 12.4.7\nTEMP: 36.8°C\nHR: 72 bpm\nO2: 98%", new Vector3(-0.165f, 1.33f, 0.0485f), Vector3.zero, 0.0145f, null, 0.2f, TextAlignmentOptions.Right, FontStyles.Normal, teal, 2f);
            Label("NO OBVIOUS ANOMALIES", new Vector3(-0.47f, 1.118f, 0.0485f), Vector3.zero, 0.0125f, null, 0.3f, TextAlignmentOptions.Center, FontStyles.Normal, teal, 2f);
            Label("BIOSCAN ANALYSIS", new Vector3(0.14f, 1.466f, 0.0485f), Vector3.zero, 0.016f, null, 0.25f, TextAlignmentOptions.Left, FontStyles.Normal, teal, 2f);
            Label("SUBJECT 7M-3381", new Vector3(0.49f, 1.466f, 0.0485f), Vector3.zero, 0.016f, null, 0.22f, TextAlignmentOptions.Right, FontStyles.Normal, teal, 2f);
            Label("HR   72\nSpO2 98%\nTEMP 36.8°C", new Vector3(0.405f, 1.372f, 0.0485f), Vector3.zero, 0.015f, null, 0.2f, TextAlignmentOptions.Left, FontStyles.Bold, new Color(0.7f, 1f, 1f), 2f);
            Label("VIRUS:     NEG\nBACTERIA: NEG\nTOXINS:    NEG\nRADIATION: NEG\nANOMALY:  NEG", new Vector3(0.16f, 1.19f, 0.0485f), Vector3.zero, 0.0115f, null, 0.28f, TextAlignmentOptions.Left, FontStyles.Normal, new Color(0.4f, 1f, 0.55f), 2f);

            // sign above the monitors
            Box("DarkIron", new Vector3(0, 1.68f, -0.12f), new Vector3(0.86f, 0.17f, 0.06f), 0.012f);
            Box("Gunmetal", new Vector3(0, 1.68f, -0.0875f), new Vector3(0.82f, 0.13f, 0.006f), 0.003f);
            RivetRect(new Vector3(0, 1.68f, -0.0845f), 0.80f, 0.115f, 8, 2, Vector3.forward, Vector3.right, Vector3.up);
            foreach (float bx in new[] { -0.3f, 0.3f }) Box("DarkIron", new Vector3(bx, 1.595f, -0.12f), new Vector3(0.05f, 0.04f, 0.1f), 0.006f);
            Label("MINING SECTOR 7", new Vector3(0, 1.735f, -0.0835f), Vector3.zero, 0.022f, null, 0.8f, TextAlignmentOptions.Center, FontStyles.Bold, null, 12f);
            Label("QUARANTINE INSPECTION", new Vector3(0, 1.682f, -0.0835f), Vector3.zero, 0.046f, null, 0.84f, TextAlignmentOptions.Center, FontStyles.Bold, null, 4f);
            Label("HEALTH  •  CONTAINMENT  •  SAFETY", new Vector3(0, 1.633f, -0.0835f), Vector3.zero, 0.016f, null, 0.8f, TextAlignmentOptions.Center, FontStyles.Normal, null, 10f);
        }

        // ============================================================ gauges, lights, speaker, status panel
        internal static void BuildGaugesAndPanels(out List<Renderer> leds)
        {
            leds = new List<Renderer>();
            // tilted gauge console in front of monitors
            var panelPos = new Vector3(-0.11f, 0.865f, 0.115f); var panelRot = Quaternion.Euler(-24f, 0, 0);
            Add(MeshKit.Box(new Vector3(0.74f, 0.2f, 0.075f), 0.012f), M("DarkIron"), panelPos, panelRot);
            Add(MeshKit.Box(new Vector3(0.70f, 0.17f, 0.012f), 0.004f), M("Gunmetal"), panelPos + panelRot * new Vector3(0, 0, 0.04f), panelRot);
            Vector3 PL(float x, float y, float z) => panelPos + panelRot * new Vector3(x, y, z);

            string[] gm = { "GaugeRadiation", "GaugeAir", "GaugeContainment" };
            string[] gl = { "RADIATION", "AIR QUALITY", "CONTAINMENT" };
            float[] gx = { -0.215f, -0.005f, 0.205f }; float[] needle = { 28f, -8f, -52f };
            for (int i = 0; i < 3; i++)
            {
                var rot = panelRot * Quaternion.Euler(90, 0, 0);
                // brass bezel
                Add(MeshKit.Lathe(new[] { new Vector2(0.044f, 0), new Vector2(0.066f, 0), new Vector2(0.066f, 0.022f), new Vector2(0.06f, 0.03f), new Vector2(0.046f, 0.026f), new Vector2(0.044f, 0.0f) }, 32), M("Brass"), PL(gx[i], 0.012f, 0.047f), rot);
                // face
                Add(DiscMesh(0.052f), M(gm[i]), PL(gx[i], 0.012f, 0.0548f), panelRot);
                // needle pivot (kept separate)
                var piv = Obj("Needle_" + gl[i].Replace(' ', '_'), root, PL(gx[i], 0.012f, 0.0575f), panelRot.eulerAngles);
                var nm = MeshKit.Box(new Vector3(0.004f, 0.046f, 0.002f), 0.0005f);
                var nr = Part("NeedleMesh_" + i, piv.transform, nm, "BlackPlastic", new Vector3(0, 0.017f, 0), null);
                piv.transform.localRotation = panelRot * Quaternion.Euler(0, 0, needle[i]);
                Add(MeshKit.Cylinder(0.007f, 0.006f, 12, 0.001f), M("Brass"), PL(gx[i], 0.012f, 0.0592f), rot);
                Label(gl[i], PL(gx[i], -0.066f, 0.0495f), panelRot.eulerAngles, 0.0105f, null, 0.2f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.65f, 0.62f, 0.5f, 0.95f), 3f);
                RivetLine(PL(gx[i] - 0.085f, 0.07f, 0.0475f), PL(gx[i] + 0.085f, 0.07f, 0.0475f), 2, panelRot * Vector3.forward);
            }
            Label("mSv/h", PL(gx[0], -0.028f, 0.0555f), panelRot.eulerAngles, 0.0065f, null, 0.1f, TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.1f, 0.09f, 0.07f, 0.9f), 1f);

            // indicator column (amber, amber, green, green)
            Box("DarkIron", new Vector3(0.255f, 0.925f, 0.085f), new Vector3(0.055f, 0.20f, 0.05f), 0.008f, new Vector3(-12, 0, 0));
            string[] ledMats = { "LedAmber", "LedAmber", "LedGreen", "LedGreen" };
            for (int i = 0; i < 4; i++)
            {
                var rr = Part("StatusLight_" + i, root, MeshKit.Sphere(0.0105f, 14, 8, 0.7f), ledMats[i], new Vector3(0.255f, 0.985f - i * 0.045f, 0.117f - (i * 0.0045f)), new Vector3(-12, 0, 0));
                leds.Add(rr);
                Cyl("Chrome", new Vector3(0.255f, 0.985f - i * 0.045f, 0.1145f - (i * 0.0045f)), 0.0135f, 0.004f, new Vector3(78, 0, 0), 14, 0.001f);
            }

            // speaker grille
            Box("DarkIron", new Vector3(0.375f, 0.93f, 0.075f), new Vector3(0.185f, 0.19f, 0.06f), 0.01f, new Vector3(-6, 0, 0));
            for (int ry = 0; ry < 7; ry++)
                for (int rx = 0; rx < 7; rx++)
                    Cyl("BlackPlastic", new Vector3(0.375f - 0.066f + rx * 0.022f, 0.93f - 0.066f + ry * 0.022f, 0.1065f), 0.0062f, 0.006f, new Vector3(84, 0, 0), 8, 0.0005f);

            // system status panel
            var spPos = new Vector3(0.565f, 0.93f, 0.09f); var spRot = Quaternion.Euler(-12, 0, 0);
            Add(MeshKit.Box(new Vector3(0.21f, 0.26f, 0.06f), 0.01f), M("DarkIron"), spPos, spRot);
            Add(MeshKit.Box(new Vector3(0.185f, 0.235f, 0.01f), 0.003f), M("Gunmetal"), spPos + spRot * new Vector3(0, 0, 0.032f), spRot);
            Label("SYSTEM STATUS", spPos + spRot * new Vector3(0, 0.1f, 0.0375f), spRot.eulerAngles, 0.0135f, null, 0.2f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.75f, 0.75f, 0.7f, 0.95f), 3f);
            string[] sl = { "ONLINE", "SCANNING", "ANALYZING", "READY" }; string[] sm = { "LedGreen", "LedAmber", "LedGreen", "LedGreen" };
            for (int i = 0; i < 4; i++)
            {
                Vector3 lp = spPos + spRot * new Vector3(-0.058f, 0.05f - i * 0.045f, 0.039f);
                var rr = Part("StatusLED_" + sl[i], root, MeshKit.Sphere(0.008f, 12, 8, 0.7f), sm[i], lp, spRot.eulerAngles);
                leds.Add(rr);
                Label(sl[i], spPos + spRot * new Vector3(-0.03f, 0.05f - i * 0.045f, 0.0375f), spRot.eulerAngles, 0.0115f, null, 0.15f, TextAlignmentOptions.Left, FontStyles.Normal, new Color(0.75f, 0.75f, 0.7f, 0.95f), 3f);
            }
            RivetRect(spPos + spRot * new Vector3(0, 0, 0.0375f), 0.19f, 0.24f, 3, 4, spRot * Vector3.forward, spRot * Vector3.right, spRot * Vector3.up);
        }

        // ============================================================ toggles (interactive pivots)
        internal static void BuildToggles()
        {
            var pos = new Vector3(-0.01f, 0.825f, 0.30f); var rot = Quaternion.Euler(14f, 0, 0);
            Add(MeshKit.Box(new Vector3(0.96f, 0.04f, 0.20f), 0.01f), M("DarkIron"), pos, rot);
            Add(MeshKit.Box(new Vector3(0.92f, 0.008f, 0.17f), 0.003f), M("Gunmetal"), pos + rot * new Vector3(0, 0.021f, 0), rot);
            string[] names = { "X-RAY", "BIOSCAN", "ANALYSIS", "LOG", "POWER", "CLEAR" };
            float[] tx = { -0.375f, -0.25f, -0.12f, 0.01f, 0.14f, 0.27f };
            for (int i = 0; i < 6; i++)
            {
                Vector3 b = pos + rot * new Vector3(tx[i] - (-0.01f) + (-0.01f) - (-0.01f), 0.027f, 0.0f);
                b = pos + rot * new Vector3(tx[i] + 0.01f - 0.0f, 0.027f, 0f);
                Add(MeshKit.Cylinder(0.0225f, 0.01f, 18, 0.002f), M("Chrome"), b, rot);
                Add(MeshKit.Cylinder(0.014f, 0.016f, 16, 0.003f), M("BlackPlastic"), b + rot * new Vector3(0, 0.006f, 0), rot);
                var piv = Obj("Toggle_" + (names[i] == "" ? "Aux" : names[i]), root, b + rot * new Vector3(0, 0.012f, 0), rot.eulerAngles);
                piv.transform.localRotation = rot * Quaternion.Euler(i < 5 ? -18f : 0f, 0, 0);
                var arm = MeshKit.Cylinder(0.0045f, 0.055f, 10, 0.001f);
                Part("ToggleArm_" + i, piv.transform, arm, "Chrome", new Vector3(0, 0.0275f, 0));
                Part("ToggleKnob_" + i, piv.transform, MeshKit.Sphere(0.0085f, 12, 8), "BlackPlastic", new Vector3(0, 0.058f, 0));
                var col = piv.AddComponent<CapsuleCollider>(); col.center = new Vector3(0, 0.035f, 0); col.radius = 0.034f; col.height = 0.12f;
                if (names[i] != "") Label(names[i], pos + rot * new Vector3(tx[i] + 0.01f, 0.026f, 0.07f), rot.eulerAngles + new Vector3(-90, 0, 0), 0.0105f, null, 0.15f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.78f, 0.78f, 0.72f, 0.95f), 3f);
            }
            RivetLine(pos + rot * new Vector3(-0.45f, 0.022f, 0.085f), pos + rot * new Vector3(0.45f, 0.022f, 0.085f), 12, rot * Vector3.up);
        }

        // ============================================================ incinerate lever
        internal static Transform BuildIncinerator()
        {
            // heavy machinery behind the red unit
            Box("Gunmetal", new Vector3(1.06f, 0.95f, -0.12f), new Vector3(0.30f, 0.30f, 0.44f), 0.014f);
            Box("DarkIron", new Vector3(1.09f, 1.13f, -0.14f), new Vector3(0.20f, 0.07f, 0.3f), 0.01f);
            Put(MeshKit.Tube(new[] { new Vector3(1.19f, 1.0f, -0.12f), new Vector3(1.24f, 0.98f, -0.12f), new Vector3(1.26f, 0.9f, -0.15f), new Vector3(1.22f, 0.83f, -0.2f) }, 0.022f, 12, 8, 90f), "Rubber", Vector3.zero);
            // red unit
            Box("RedPaint", new Vector3(0.86f, 0.945f, 0.115f), new Vector3(0.36f, 0.29f, 0.34f), 0.02f);
            Box("DarkIron", new Vector3(0.86f, 0.805f, 0.115f), new Vector3(0.40f, 0.02f, 0.38f), 0.008f);
            Box("BlackPlastic", new Vector3(0.86f, 1.092f, 0.085f), new Vector3(0.14f, 0.012f, 0.1f), 0.003f);                      // lever slot
            Box("RedPaint", new Vector3(0.86f, 1.075f, 0.105f), new Vector3(0.26f, 0.05f, 0.18f), 0.014f);
            RivetRect(new Vector3(0.86f, 0.935f, 0.2865f), 0.33f, 0.26f, 5, 4, Vector3.forward, Vector3.right, Vector3.up, "DarkIron");
            // front label plate + hazard stripes
            Box("RedPaint", new Vector3(0.86f, 0.925f, 0.288f), new Vector3(0.30f, 0.105f, 0.008f), 0.003f);
            Put(MeshKit.Quad(0.28f, 0.042f), "HazardStripes", new Vector3(0.86f, 0.85f, 0.2935f));
            Label("INCINERATE", new Vector3(0.86f, 0.945f, 0.2955f), Vector3.zero, 0.03f, null, 0.3f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.88f, 0.84f, 0.78f, 0.95f), 4f);
            Label("EMERGENCY ONLY", new Vector3(0.86f, 0.9f, 0.2955f), Vector3.zero, 0.0125f, null, 0.3f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.88f, 0.84f, 0.78f, 0.9f), 5f);
            // lever guard side plates
            foreach (float sx in new[] { -1f, 1f }) Box("DarkIron", new Vector3(0.86f + sx * 0.11f, 1.115f, 0.085f), new Vector3(0.016f, 0.07f, 0.11f), 0.004f);

            // pivoting lever (two arms + crossbar), rest tilted toward player
            var piv = Obj("IncinerateLever", root, new Vector3(0.86f, 1.095f, 0.085f), new Vector3(-14f, 0, 0));
            foreach (float sx in new[] { -0.05f, 0.05f })
                Part("LeverArm_" + sx, piv.transform, MeshKit.Cylinder(0.0145f, 0.28f, 14, 0.003f), "RedPaint", new Vector3(sx, 0.14f, 0));
            Part("LeverBar", piv.transform, MeshKit.Cylinder(0.021f, 0.19f, 18, 0.004f), "RedPaint", new Vector3(0, 0.29f, 0), new Vector3(0, 0, 90));
            Part("LeverCapL", piv.transform, MeshKit.Cylinder(0.026f, 0.014f, 18, 0.003f), "DarkIron", new Vector3(-0.1f, 0.29f, 0), new Vector3(0, 0, 90));
            Part("LeverCapR", piv.transform, MeshKit.Cylinder(0.026f, 0.014f, 18, 0.003f), "DarkIron", new Vector3(0.1f, 0.29f, 0), new Vector3(0, 0, 90));
            Part("LeverPivotPin", piv.transform, MeshKit.Cylinder(0.018f, 0.2f, 14, 0.003f), "Chrome", new Vector3(0, 0.0f, 0), new Vector3(0, 0, 90));
            var c = piv.AddComponent<BoxCollider>(); c.center = new Vector3(0, 0.16f, 0); c.size = new Vector3(0.22f, 0.36f, 0.08f);
            return piv.transform;
        }

        // ============================================================ left side: crate, clipboard, mug, cables
        internal static void BuildLeftSide()
        {
            // quarantine crate
            Box("Gunmetal", new Vector3(-1.045f, 0.975f, -0.10f), new Vector3(0.38f, 0.34f, 0.38f), 0.018f);
            Box("DarkIron", new Vector3(-1.045f, 1.155f, -0.10f), new Vector3(0.40f, 0.02f, 0.40f), 0.006f);
            Box("DarkIron", new Vector3(-1.045f, 0.815f, -0.10f), new Vector3(0.40f, 0.02f, 0.40f), 0.006f);
            RivetRect(new Vector3(-1.045f, 0.975f, 0.0915f), 0.34f, 0.30f, 5, 4, Vector3.forward, Vector3.right, Vector3.up);
            Label("QUARANTINE", new Vector3(-1.045f, 1.06f, 0.095f), Vector3.zero, 0.033f, null, 0.4f, TextAlignmentOptions.Center, FontStyles.Bold, null, 4f);
            Put(MeshKit.Quad(0.15f, 0.15f), "BiohazardPaint", new Vector3(-1.045f, 0.925f, 0.0955f));
            Box("Chrome", new Vector3(-0.855f, 0.975f, -0.10f), new Vector3(0.012f, 0.09f, 0.05f), 0.003f);
            // two thick cables off the crate side and down the desk side
            Put(MeshKit.Tube(new[] { new Vector3(-1.22f, 1.0f, -0.05f), new Vector3(-1.29f, 0.95f, 0.02f), new Vector3(-1.34f, 0.8f, 0.08f), new Vector3(-1.32f, 0.62f, 0.16f), new Vector3(-1.2f, 0.52f, 0.22f) }, 0.024f, 12, 8, 80f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(-1.22f, 0.93f, -0.1f), new Vector3(-1.3f, 0.88f, -0.02f), new Vector3(-1.37f, 0.72f, 0.04f), new Vector3(-1.35f, 0.5f, 0.1f), new Vector3(-1.22f, 0.38f, 0.18f) }, 0.018f, 10, 8, 70f), "Rubber", Vector3.zero);

            // clipboard (board + paper + clip)
            var cpos = new Vector3(-0.90f, 0.8125f, 0.2f); float yaw = 7f;
            Box("Wood", cpos, new Vector3(0.31f, 0.012f, 0.40f), 0.003f, new Vector3(0, yaw, 0));
            Put(MeshKit.Quad(0.27f, 0.345f), "Paper", cpos + new Vector3(0, 0.0072f, 0.0f), new Vector3(-90, yaw, 0));
            Box("Chrome", cpos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0.012f, -0.185f), new Vector3(0.09f, 0.016f, 0.03f), 0.004f, new Vector3(0, yaw, 0));
            var ink = new Color(0.1f, 0.08f, 0.06f, 0.95f);
            Label("INSPECTION CHECKLIST", cpos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0.009f, -0.12f), new Vector3(-90, yaw, 0), 0.016f, null, 0.26f, TextAlignmentOptions.Center, FontStyles.Bold, ink, 1f);
            string[] items = { "VITALS", "X-RAY", "BIOSCAN", "CONTAMINATION", "CLEARANCE" };
            for (int i = 0; i < 5; i++)
                Label(items[i], cpos + Quaternion.Euler(0, yaw, 0) * new Vector3(0.04f, 0.009f, -0.066f + i * 0.043f), new Vector3(-90, yaw, 0), 0.0125f, null, 0.2f, TextAlignmentOptions.Left, FontStyles.Normal, ink, 1f);

            // mug
            var mugProf = new[] { new Vector2(0, 0), new Vector2(0.036f, 0), new Vector2(0.038f, 0.004f), new Vector2(0.04f, 0.09f), new Vector2(0.036f, 0.09f), new Vector2(0.034f, 0.008f), new Vector2(0, 0.008f) };
            var mugPos = new Vector3(-0.56f, TopY + 0.006f, 0.02f);
            Add(MeshKit.Lathe(mugProf, 24), M("DarkIron"), mugPos);
            Add(MeshKit.Tube(new[] { new Vector3(0.039f, 0.075f, 0), new Vector3(0.065f, 0.07f, 0), new Vector3(0.068f, 0.04f, 0), new Vector3(0.04f, 0.018f, 0) }, 0.005f, 8, 6), M("DarkIron"), mugPos);
            Label("KEEP\nTHE MINE\nCLEAN", mugPos + new Vector3(0, 0.048f, 0.04f), Vector3.zero, 0.0125f, null, 0.08f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.75f, 0.72f, 0.62f, 0.95f), 1f);
        }

        // ============================================================ under desk
        internal static void BuildUnderDesk()
        {
            // junction boxes under the desk, with warning triangles
            Box("DarkIron", new Vector3(-0.38f, 0.60f, -0.28f), new Vector3(0.22f, 0.14f, 0.12f), 0.012f);
            Box("DarkIron", new Vector3(-0.08f, 0.62f, -0.28f), new Vector3(0.16f, 0.17f, 0.12f), 0.012f);
            Box("DarkIron", new Vector3(0.32f, 0.60f, -0.28f), new Vector3(0.26f, 0.15f, 0.12f), 0.012f);
            Box("DarkIron", new Vector3(0.00f, 0.50f, -0.30f), new Vector3(0.18f, 0.1f, 0.1f), 0.01f);
            foreach (float bx in new[] { -0.38f, 0.32f }) Box("WarnYellow", new Vector3(bx, 0.602f, -0.216f), new Vector3(0.055f, 0.055f, 0.004f), 0.001f, new Vector3(0, 0, 45));
            RivetLine(new Vector3(-0.48f, 0.60f, -0.222f), new Vector3(-0.28f, 0.60f, -0.222f), 4, Vector3.forward);
            RivetLine(new Vector3(0.20f, 0.60f, -0.222f), new Vector3(0.44f, 0.60f, -0.222f), 5, Vector3.forward);
            // conduit rail + brackets
            Cyl("DarkIron", new Vector3(0, 0.675f, -0.32f), 0.018f, 1.0f, new Vector3(0, 0, 90), 14);
            foreach (float bx in new[] { -0.4f, 0f, 0.4f }) Box("DarkIron", new Vector3(bx, 0.665f, -0.32f), new Vector3(0.03f, 0.05f, 0.05f), 0.005f);

            // hanging cables (match the ref: loops between pedestals)
            Put(MeshKit.Tube(new[] { new Vector3(-0.42f, 0.54f, -0.26f), new Vector3(-0.46f, 0.4f, -0.2f), new Vector3(-0.50f, 0.26f, -0.05f), new Vector3(-0.56f, 0.2f, 0.12f), new Vector3(-0.60f, 0.3f, 0.26f) }, 0.026f, 12, 9, 70f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(-0.34f, 0.53f, -0.26f), new Vector3(-0.38f, 0.38f, -0.2f), new Vector3(-0.42f, 0.28f, -0.02f), new Vector3(-0.47f, 0.24f, 0.18f) }, 0.02f, 10, 9, 60f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(-0.04f, 0.53f, -0.25f), new Vector3(-0.05f, 0.42f, -0.18f), new Vector3(-0.02f, 0.36f, -0.05f), new Vector3(0.1f, 0.4f, 0.0f), new Vector3(0.22f, 0.48f, -0.1f) }, 0.014f, 8, 9), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(0.28f, 0.53f, -0.26f), new Vector3(0.3f, 0.36f, -0.18f), new Vector3(0.36f, 0.22f, 0.0f), new Vector3(0.5f, 0.16f, 0.12f), new Vector3(0.58f, 0.26f, 0.26f) }, 0.022f, 12, 9, 70f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(0.40f, 0.54f, -0.26f), new Vector3(0.46f, 0.42f, -0.2f), new Vector3(0.5f, 0.3f, -0.1f), new Vector3(0.56f, 0.34f, 0.04f) }, 0.016f, 8, 9), "Rubber", Vector3.zero);
            // junction small boxes on the pedestal inner faces
            Box("DarkIron", new Vector3(-0.60f, 0.45f, 0.0f), new Vector3(0.03f, 0.12f, 0.1f), 0.006f);
            Box("DarkIron", new Vector3(0.60f, 0.45f, 0.0f), new Vector3(0.03f, 0.12f, 0.1f), 0.006f);
            Box("WarnYellow", new Vector3(0.583f, 0.46f, 0.0f), new Vector3(0.004f, 0.045f, 0.045f), 0.001f, new Vector3(45, 0, 0));
        }

        // ============================================================ caged lamp
        internal static Light BuildLamp()
        {
            var p = new Vector3(-0.60f, 1.585f, -0.05f);
            Box("DarkIron", p + new Vector3(0, 0.012f, 0), new Vector3(0.14f, 0.024f, 0.14f), 0.008f);
            Cyl("Brass", p + new Vector3(0, 0.04f, 0), 0.05f, 0.03f, null, 20, 0.005f);
            var bulb = Part("LampBulb", root, MeshKit.Sphere(0.04f, 20, 12, 1.15f), "LampBulb", p + new Vector3(0, 0.1f, 0));
            bulb.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // cage: vertical bars + rings
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f; var o = new Vector3(Mathf.Cos(a) * 0.052f, 0.1f, Mathf.Sin(a) * 0.052f);
                Add(MeshKit.Cylinder(0.0028f, 0.105f, 6, 0.0005f), M("DarkIron"), p + o);
            }
            foreach (float hy in new[] { 0.056f, 0.1f, 0.146f })
                Add(MeshKit.Lathe(new[] { new Vector2(0.0575f, -0.0025f), new Vector2(0.0575f, 0.0025f) }, 20, true, 1f), M("DarkIron"), p + new Vector3(0, hy, 0));
            Cyl("DarkIron", p + new Vector3(0, 0.16f, 0), 0.036f, 0.016f, null, 16, 0.004f);

            var lg = Obj("LampLight", root, p + new Vector3(0, 0.1f, 0));
            var l = lg.AddComponent<Light>();
            l.type = LightType.Point; l.color = new Color(1f, 0.74f, 0.42f); l.intensity = 2.4f; l.range = 3.5f; l.shadows = LightShadows.Soft;
            return l;
        }

        // ============================================================ finalize
        internal static void BakeCombined()
        {
            var body = Obj("Body", root, Vector3.zero);
            foreach (var kv in cb.Bake("Desk"))
            {
                string p = SaveMesh(kv.Value, "Desk_" + kv.Key.name);
                var g = Obj("Body_" + kv.Key.name, body.transform, Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
        }

        internal static void AddColliders()
        {
            var cols = Obj("Colliders", root, Vector3.zero);
            void C(Vector3 c, Vector3 s) { var b = cols.AddComponent<BoxCollider>(); b.center = Zm(c); b.size = s; }
            C(new Vector3(0, 0.76f, 0), new Vector3(2.42f, 0.08f, 0.92f));
            C(new Vector3(-0.90f, 0.36f, 0.02f), new Vector3(0.64f, 0.72f, 0.86f));
            C(new Vector3(0.90f, 0.36f, 0.02f), new Vector3(0.64f, 0.72f, 0.86f));
            C(new Vector3(0, 0.0f + 0.38f, -0.385f), new Vector3(1.22f, 0.64f, 0.03f));
            C(new Vector3(0, 1.2f, -0.2f), new Vector3(1.48f, 1.0f, 0.52f));
            C(new Vector3(-1.045f, 0.98f, -0.1f), new Vector3(0.4f, 0.36f, 0.4f));
            C(new Vector3(0.95f, 0.98f, 0.0f), new Vector3(0.5f, 0.3f, 0.7f));
        }
    }
}
