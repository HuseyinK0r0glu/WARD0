using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Ward0.Props;
using static Ward0.EditorTools.DeskBuilder;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds the "Quarantine Blast Door" prop procedurally from the concept art: armoured frame with clamp blocks and
    /// hydraulic rams, a hinged leaf with a viewing window, hazard band, lock bar and spinning lock wheel.
    /// Designed front-toward +Z and mirrored at build time (see DeskBuilder). Menu: Ward0 / Build Quarantine Door
    /// </summary>
    public static class DoorBuilder
    {
        const string DoorRoot = "Assets/_Project/Art/Door";
        const string TexDir = DoorRoot + "/Textures";
        const string PrefabPath = DoorRoot + "/QuarantineDoor.prefab";

        // reference-art pixel -> metres (reference is 1536 wide, door centre at x=770, floor at y=1110)
        const float K = 0.00227f;
        static float DX(float px) => (px - 770f) * K;
        static float DY(float py) => (1110f - py) * K;

        static readonly Vector3 Hinge = new Vector3(-0.75f, 0f, 0.12f);
        static readonly Vector3 WheelC = new Vector3(0f, 1.158f, 0.33f);
        static readonly Color Cream = new Color(0.74f, 0.71f, 0.63f, 0.95f);
        static readonly Color Ochre = new Color(0.78f, 0.62f, 0.12f, 0.95f);

        [MenuItem("Ward0/Build Quarantine Door")]
        public static void Build()
        {
            MatDir = DoorRoot + "/Materials"; MeshDir = DoorRoot + "/Meshes";
            foreach (var d in new[] { DoorRoot, TexDir, MatDir, MeshDir }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            mats.Clear(); mirrored.Clear(); SavedMeshNames.Clear();
            Origin = Vector3.zero;

            BuildDoorMaterials();

            var go = new GameObject("QuarantineDoor");
            root = go.transform;
            var door = go.AddComponent<QuarantineDoor>();

            // ---- frame (static)
            var cbFrame = new Combiner(); cb = cbFrame;
            BuildFrame();
            BuildHydraulics();

            // ---- leaf (hinged)
            var leaf = Obj("DoorLeaf", root, Hinge);
            var cbLeaf = new Combiner(); cb = cbLeaf; Origin = Hinge;
            BuildLeaf(leaf.transform);

            // ---- wheel (spins, child of leaf)
            var wheel = Obj("LockWheel", leaf.transform, WheelC - Hinge);
            var cbWheel = new Combiner(); cb = cbWheel; Origin = WheelC;
            BuildWheel();
            Origin = Vector3.zero;

            var frameObj = Obj("Frame", root, Vector3.zero);
            BakeInto(cbFrame, frameObj.transform, "DoorFrame");
            BakeInto(cbLeaf, leaf.transform, "DoorLeaf");
            BakeInto(cbWheel, wheel.transform, "DoorWheel");

            AddColliders(leaf.transform);

            var so = new SerializedObject(door);
            so.FindProperty("leaf").objectReferenceValue = leaf.transform;
            so.FindProperty("wheel").objectReferenceValue = wheel.transform;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Ward0] Quarantine door built: " + PrefabPath);
        }

        // ============================================================ materials
        static void BuildDoorMaterials()
        {
            var set = TexKit.BuildMetal(TexDir, 1024, "DoorMetal", 0.0f);
            Mk("DGun", new Color(0.78f, 0.72f, 0.66f), 1f, 1f, set.Gun, set.Normal, set.MetalGloss, 0.9f);
            Mk("DDark", new Color(0.86f, 0.80f, 0.74f), 1f, 1f, set.Dark, set.Normal, set.MetalGloss, 0.75f);
            Mk("DYellow", new Color(0.62f, 0.58f, 0.5f), 1f, 1f, set.Yellow, set.Normal, set.MetalGloss, 0.8f);
            Mk("Chrome", new Color(0.35f, 0.34f, 0.32f), 1f, 0.45f);
            Mk("Brass", new Color(0.42f, 0.32f, 0.12f), 0.9f, 0.4f);
            Mk("Rubber", new Color(0.03f, 0.03f, 0.03f), 0f, 0.35f);
            Mk("BlackPlastic", new Color(0.04f, 0.04f, 0.045f), 0.1f, 0.5f);
            Mk("WarnYellow", new Color(0.7f, 0.55f, 0.08f), 0.2f, 0.35f);
            Mk("DStripes", Color.white, 0.1f, 0.3f, TexKit.Stripes(TexDir, "Stripes_Band", 576, 64, new Color(0.62f, 0.5f, 0.08f)));
            Mk("DStripesSmall", Color.white, 0.1f, 0.3f, TexKit.Stripes(TexDir, "Stripes_Small", 256, 32, new Color(0.7f, 0.68f, 0.6f), 22f));
            Emissive("DWindowGlow", new Color(0.05f, 0.05f, 0.05f), Color.white * 0.6f, TexKit.WindowGlow(TexDir), 0.2f);
            Cutout("DWireMesh", TexKit.WireMesh(TexDir), Color.white);
            mats["DarkIron"] = mats["DDark"]; mats["Gunmetal"] = mats["DGun"];   // aliases used by the shared rivet helpers
            Cutout("BioYellow", TexKit.Biohazard(TexDir, "Biohazard_Yellow", new Color(0.74f, 0.58f, 0.09f)), Color.white);
        }

        static void BakeInto(Combiner c, Transform parent, string tag)
        {
            foreach (var kv in c.Bake(tag))
            {
                string p = SaveMesh(kv.Value, tag + "_" + kv.Key.name);
                var g = Obj("Mesh_" + kv.Key.name, parent, Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
        }

        // leaf-space label (position given in prop space, converted to leaf-local)
        static void T(Transform leaf, string text, float x, float y, float z, float size, float width, TextAlignmentOptions al = TextAlignmentOptions.Center, Color? col = null, float spacing = 4f)
            => Label(text, new Vector3(x, y, z) - Hinge, Vector3.zero, size, leaf, width, al, FontStyles.Bold, col ?? Cream, spacing);

        // ============================================================ frame
        static void BuildFrame()
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 0.975f;
                Box("DGun", new Vector3(x, 1.24f, 0f), new Vector3(0.46f, 2.46f, 0.42f), 0.022f);                    // pillar
                Box("DDark", new Vector3(x, 2.50f, 0f), new Vector3(0.52f, 0.07f, 0.46f), 0.012f);                    // cap
                Box("DDark", new Vector3(sx * 0.768f, 1.27f, 0.205f), new Vector3(0.03f, 2.14f, 0.04f), 0.006f);      // inner rail
                // big clamp blocks (top / middle / bottom)
                float[] by = { 2.17f, 1.17f, 0.41f }; float[] bh = { 0.40f, 0.46f, 0.38f };
                for (int i = 0; i < 3; i++)
                {
                    Box("DDark", new Vector3(x, by[i], 0.225f), new Vector3(0.52f, bh[i], 0.13f), 0.022f);
                    Box("DGun", new Vector3(x, by[i], 0.30f), new Vector3(0.40f, bh[i] - 0.09f, 0.06f), 0.014f);
                    RivetRect(new Vector3(x, by[i], 0.33f), 0.34f, bh[i] - 0.15f, 3, 3, Vector3.forward, Vector3.right, Vector3.up);
                    Cyl("Chrome", new Vector3(x - sx * 0.14f, by[i] + bh[i] * 0.22f, 0.345f), 0.026f, 0.028f, new Vector3(90, 0, 0), 6, 0.003f);
                }
                // stacked hinge / lock knuckles along the outer edge
                for (int k = 0; k < 6; k++)
                    Box("DDark", new Vector3(sx * (1.225f + (k % 2) * 0.02f), 0.30f + k * 0.40f, 0f), new Vector3(0.09f, 0.17f, 0.47f - (k % 3) * 0.03f), 0.016f);
                // vertical outer plate and side bolts
                Box("DGun", new Vector3(sx * 1.185f, 1.24f, -0.20f), new Vector3(0.06f, 2.3f, 0.06f), 0.01f);
                RivetLine(new Vector3(x - 0.18f, 0.75f, 0.2125f), new Vector3(x - 0.18f, 2.15f, 0.2125f), 10, Vector3.forward);
                // lock bar clamps (frame side)
                Box("DDark", new Vector3(sx * 0.87f, 1.19f, 0.27f), new Vector3(0.24f, 0.30f, 0.20f), 0.02f);
                Cyl("DDark", new Vector3(sx * 0.87f, 1.19f, 0.27f), 0.062f, 0.26f, new Vector3(0, 0, 90), 16, 0.004f);
                RivetRect(new Vector3(sx * 0.87f, 1.19f, 0.372f), 0.18f, 0.22f, 2, 2, Vector3.forward, Vector3.right, Vector3.up);
                // bottom block
                Box("DDark", new Vector3(x, 0.05f, 0.22f), new Vector3(0.52f, 0.12f, 0.12f), 0.012f);
            }
            // top beam
            Box("DGun", new Vector3(0, 2.395f, 0f), new Vector3(1.6f, 0.21f, 0.42f), 0.02f);
            Box("DDark", new Vector3(0, 2.275f, 0.18f), new Vector3(1.5f, 0.05f, 0.1f), 0.01f);
            RivetLine(new Vector3(-0.7f, 2.42f, 0.2125f), new Vector3(0.7f, 2.42f, 0.2125f), 9, Vector3.forward);
            // threshold, sill and floor grate
            Box("DDark", new Vector3(0, 0.04f, 0.02f), new Vector3(2.46f, 0.08f, 0.50f), 0.012f);
            Box("DGun", new Vector3(0, 0.10f, 0.0f), new Vector3(1.52f, 0.05f, 0.40f), 0.01f);
            Box("DDark", new Vector3(0, 0.055f, 0.31f), new Vector3(1.55f, 0.035f, 0.18f), 0.006f);
            for (int i = 0; i < 18; i++) Box("BlackPlastic", new Vector3(-0.72f + i * 0.085f, 0.0745f, 0.31f), new Vector3(0.05f, 0.004f, 0.14f), 0.001f);
            // jambs
            Box("DDark", new Vector3(0, 2.255f, 0.0f), new Vector3(1.5f, 0.04f, 0.38f), 0.008f);
        }

        // ============================================================ hydraulic rams + hoses
        static void BuildHydraulics()
        {
            foreach (float sx in new[] { -1f, 1f })
            {
                float x = sx * 1.03f, z = 0.31f;
                // brackets to the pillar
                foreach (float by in new[] { 1.86f, 1.40f, 0.86f })
                    Box("DDark", new Vector3(x, by, 0.265f), new Vector3(0.22f, 0.065f, 0.09f), 0.012f);
                // main upper cylinder
                Cyl("DYellow", new Vector3(x, 1.63f, z), 0.083f, 0.54f, null, 32, 0.008f);
                Cyl("DDark", new Vector3(x, 1.935f, z), 0.1f, 0.07f, null, 28, 0.008f);
                Cyl("DDark", new Vector3(x, 1.335f, z), 0.1f, 0.07f, null, 28, 0.008f);
                Cyl("DDark", new Vector3(x, 2.01f, z), 0.056f, 0.09f, null, 6, 0.004f);                  // hex fitting
                Cyl("DDark", new Vector3(x, 2.08f, z), 0.032f, 0.06f, null, 14, 0.004f);
                Cyl("DDark", new Vector3(x + sx * 0.1f, 1.935f, z), 0.024f, 0.07f, new Vector3(0, 0, 90), 12, 0.003f); // side port
                // piston rod + lock block
                Cyl("Chrome", new Vector3(x, 1.18f, z), 0.036f, 0.34f, null, 20, 0.003f);
                Box("DDark", new Vector3(x, 1.12f, z), new Vector3(0.17f, 0.22f, 0.15f), 0.02f);
                RivetRect(new Vector3(x, 1.12f, z + 0.0775f), 0.13f, 0.17f, 2, 2, Vector3.forward, Vector3.right, Vector3.up);
                // lower stub cylinder
                Cyl("DDark", new Vector3(x, 0.93f, z), 0.07f, 0.1f, null, 24, 0.006f);
                Cyl("DYellow", new Vector3(x, 0.79f, z), 0.062f, 0.17f, null, 24, 0.006f);
                Cyl("DDark", new Vector3(x, 0.69f, z), 0.075f, 0.06f, null, 24, 0.006f);
                Cyl("DDark", new Vector3(x, 0.63f, z), 0.04f, 0.07f, null, 6, 0.003f);
                // hex nuts on the clamps
                foreach (float ny in new[] { 1.86f, 1.40f, 0.86f }) Cyl("Chrome", new Vector3(x - sx * 0.08f, ny, 0.318f), 0.022f, 0.02f, new Vector3(90, 0, 0), 6, 0.002f);
                // hoses (ribbed rubber)
                Put(MeshKit.Tube(new[]
                {
                    new Vector3(x + sx * 0.03f, 2.07f, z + 0.02f), new Vector3(x + sx * 0.15f, 2.05f, z + 0.05f), new Vector3(x + sx * 0.2f, 1.7f, z + 0.07f),
                    new Vector3(x + sx * 0.18f, 1.2f, z + 0.07f), new Vector3(x + sx * 0.14f, 0.85f, z + 0.05f), new Vector3(x + sx * 0.05f, 0.68f, z + 0.02f)
                }, 0.022f, 12, 10, 85f), "Rubber", Vector3.zero);
                Put(MeshKit.Tube(new[]
                {
                    new Vector3(x + sx * 0.1f, 1.93f, z + 0.03f), new Vector3(x + sx * 0.11f, 1.6f, z + 0.09f),
                    new Vector3(x + sx * 0.1f, 1.1f, z + 0.1f), new Vector3(x + sx * 0.07f, 0.84f, z + 0.06f)
                }, 0.014f, 10, 10), "Rubber", Vector3.zero);
            }
        }

        // ============================================================ leaf
        static void BuildLeaf(Transform leaf)
        {
            Box("DGun", new Vector3(0, 1.27f, 0.12f), new Vector3(1.46f, 2.10f, 0.16f), 0.022f);                       // slab
            Box("DDark", new Vector3(0, 1.27f, 0.205f), new Vector3(1.40f, 2.02f, 0.03f), 0.014f);                    // raised border
            Box("DGun", new Vector3(0, 1.27f, 0.2235f), new Vector3(1.30f, 1.92f, 0.012f), 0.006f);                   // inner panel
            float zf = 0.2295f;
            foreach (float sx in new[] { -1f, 1f })
                foreach (float cy in new[] { 0.37f, 2.17f })
                    Box("DDark", new Vector3(sx * 0.62f, cy, 0.2275f), new Vector3(0.075f, 0.075f, 0.016f), 0.004f, new Vector3(0, 0, 45));
            Box("BlackPlastic", new Vector3(0, 0.975f, zf), new Vector3(1.28f, 0.010f, 0.005f), 0.001f);               // panel seam
            RivetRect(new Vector3(0, 1.27f, zf), 1.26f, 1.88f, 12, 17, Vector3.forward, Vector3.right, Vector3.up);

            // viewing window with wire mesh
            Vector3 wc = new Vector3(0, 1.83f, zf);
            float ow = 0.70f, oh = 0.41f, bt = 0.07f;
            Box("DDark", wc + new Vector3(0, oh / 2 + bt / 2, 0.03f), new Vector3(ow + bt * 2, bt, 0.075f), 0.012f);
            Box("DDark", wc + new Vector3(0, -oh / 2 - bt / 2, 0.03f), new Vector3(ow + bt * 2, bt, 0.075f), 0.012f);
            Box("DDark", wc + new Vector3(-ow / 2 - bt / 2, 0, 0.03f), new Vector3(bt, oh, 0.075f), 0.012f);
            Box("DDark", wc + new Vector3(ow / 2 + bt / 2, 0, 0.03f), new Vector3(bt, oh, 0.075f), 0.012f);
            RivetRect(wc + new Vector3(0, 0, 0.0675f), ow + 0.07f, oh + 0.07f, 8, 5, Vector3.forward, Vector3.right, Vector3.up);
            Box("DGun", wc + new Vector3(0, 0, 0.004f), new Vector3(ow + 0.02f, oh + 0.02f, 0.006f), 0.002f);
            Put(MeshKit.Quad(ow, oh), "DWindowGlow", wc + new Vector3(0, 0, 0.0095f));
            Put(MeshKit.Quad(ow, oh), "DWireMesh", wc + new Vector3(0, 0, 0.028f));
            Box("DDark", wc + new Vector3(0, 0, 0.034f), new Vector3(ow, 0.012f, 0.012f), 0.002f);

            // hazard band + edge lines
            Put(MeshKit.Quad(1.44f, 0.16f), "DStripes", new Vector3(0, 1.49f, zf + 0.001f));
            Box("DDark", new Vector3(0, 1.573f, zf + 0.002f), new Vector3(1.44f, 0.008f, 0.004f), 0.001f);
            Box("DDark", new Vector3(0, 1.407f, zf + 0.002f), new Vector3(1.44f, 0.008f, 0.004f), 0.001f);

            // lock bar across the leaf, with collars and lugs
            Cyl("DDark", new Vector3(0, 1.19f, 0.275f), 0.06f, 1.5f, new Vector3(0, 0, 90), 20, 0.005f);
            foreach (float bx in new[] { -0.32f, 0.32f })
                Cyl("DDark", new Vector3(bx, 1.19f, 0.275f), 0.078f, 0.13f, new Vector3(0, 0, 90), 20, 0.008f);
            foreach (float sx in new[] { -1f, 1f })
            {
                Box("DDark", new Vector3(sx * 0.66f, 1.19f, 0.26f), new Vector3(0.16f, 0.24f, 0.12f), 0.016f);
                RivetRect(new Vector3(sx * 0.66f, 1.19f, 0.321f), 0.12f, 0.18f, 2, 2, Vector3.forward, Vector3.right, Vector3.up);
                Cyl("DDark", new Vector3(sx * 0.66f, 1.19f, 0.275f), 0.07f, 0.09f, new Vector3(0, 0, 90), 16, 0.006f);
            }
            // lock gear plate behind the wheel
            Add(MeshKit.Lathe(new[] { new Vector2(0, 0), new Vector2(0.215f, 0), new Vector2(0.222f, 0.008f), new Vector2(0.222f, 0.022f), new Vector2(0.2f, 0.03f), new Vector2(0, 0.03f) }, 40), M("DDark"), WheelC + new Vector3(0, 0, -0.1f), Quaternion.Euler(90, 0, 0));
            for (int i = 0; i < 12; i++)
            {
                var q = Quaternion.Euler(0, 0, i * 30f);
                Box("DGun", WheelC + q * new Vector3(0, 0.228f, -0.083f), new Vector3(0.04f, 0.04f, 0.026f), 0.004f, q.eulerAngles);
            }
            Cyl("DDark", WheelC + new Vector3(0, 0, -0.04f), 0.04f, 0.12f, new Vector3(90, 0, 0), 16, 0.004f);

            // hinge knuckles on the leaf's hinge edge
            foreach (float hy in new[] { 0.5f, 1.27f, 2.05f })
            {
                Cyl("DDark", new Vector3(-0.77f, hy, 0.215f), 0.05f, 0.24f, null, 16, 0.006f);
                Box("DDark", new Vector3(-0.72f, hy, 0.215f), new Vector3(0.08f, 0.22f, 0.07f), 0.01f);
            }

            // ---- lettering and decals
            T(leaf, "QUARANTINE", 0, 2.225f, zf + 0.002f, 0.135f, 1.3f, TextAlignmentOptions.Center, Cream, 6f);
            T(leaf, "AUTHORIZED PERSONNEL ONLY", 0, 2.13f, zf + 0.002f, 0.058f, 1.35f, TextAlignmentOptions.Center, Cream, 5f);
            T(leaf, "<color=#B8962A>BIOHAZARD\nQUARANTINE</color>\n<size=78%>CONTAMINATION\nRISK\nDO NOT ENTER</size>", -0.715f, 1.78f, zf + 0.002f, 0.032f, 0.4f, TextAlignmentOptions.Left, Cream, 2f);
            T(leaf, "MINING\nSECTOR 7\n<size=40%> </size>\nBLAST DOOR\nQ-14", 0.435f, 1.82f, zf + 0.002f, 0.042f, 0.4f, TextAlignmentOptions.Left, Cream, 2f);
            Put(MeshKit.Quad(0.34f, 0.34f), "BioYellow", new Vector3(0, 0.81f, zf + 0.002f));
            T(leaf, "BIOHAZARD", 0, 0.655f, zf + 0.002f, 0.098f, 0.9f, TextAlignmentOptions.Center, Ochre, 4f);
            T(leaf, "QUARANTINE", 0, 0.575f, zf + 0.002f, 0.04f, 0.6f, TextAlignmentOptions.Center, Ochre, 5f);
            T(leaf, "HYDRAULIC LOCK\nMAX PRESSURE\n4500 PSI", -0.66f, 0.43f, zf + 0.002f, 0.026f, 0.36f, TextAlignmentOptions.Left, Cream, 2f);
            Box("WarnYellow", new Vector3(-0.27f, 0.53f, zf + 0.002f), new Vector3(0.036f, 0.036f, 0.004f), 0.001f, new Vector3(0, 0, 45));
            T(leaf, "CLASS 4\nBLAST RESISTANT\nSEALED", 0.40f, 0.42f, zf + 0.002f, 0.026f, 0.36f, TextAlignmentOptions.Left, Cream, 2f);
            Put(MeshKit.Quad(0.19f, 0.03f), "DStripesSmall", new Vector3(0.50f, 0.44f, zf + 0.002f));
        }

        // ============================================================ wheel
        static void BuildWheel()
        {
            var torus = new System.Collections.Generic.List<Vector2>();
            const float R = 0.205f, r = 0.024f;
            for (int i = 0; i <= 14; i++) { float a = i / 14f * Mathf.PI * 2f; torus.Add(new Vector2(R + Mathf.Cos(a) * r, Mathf.Sin(a) * r)); }
            Add(MeshKit.Lathe(torus.ToArray(), 44, true), M("DDark"), WheelC, Quaternion.Euler(90, 0, 0));
            for (int k = 0; k < 6; k++)
            {
                var q = Quaternion.Euler(0, 0, k * 60f);
                Box("DDark", WheelC + q * new Vector3(0, 0.115f, 0), new Vector3(0.032f, 0.2f, 0.03f), 0.006f, q.eulerAngles);
                Cyl("DGun", WheelC + q * new Vector3(0, R, 0), 0.03f, 0.05f, new Vector3(90, 0, 0), 12, 0.004f);
            }
            Cyl("DDark", WheelC, 0.065f, 0.07f, new Vector3(90, 0, 0), 20, 0.006f);
            Cyl("Brass", WheelC + new Vector3(0, 0, 0.045f), 0.032f, 0.03f, new Vector3(90, 0, 0), 16, 0.004f);
            Cyl("DDark", WheelC + new Vector3(0, 0, -0.06f), 0.03f, 0.1f, new Vector3(90, 0, 0), 12, 0.003f);
        }

        // ============================================================ colliders
        static void AddColliders(Transform leaf)
        {
            var cols = Obj("Colliders", root, Vector3.zero);
            void C(GameObject o, Vector3 c, Vector3 s, Vector3 relativeTo) { var b = o.AddComponent<BoxCollider>(); b.center = Zm(c - relativeTo); b.size = s; }
            foreach (float sx in new[] { -1f, 1f }) C(cols, new Vector3(sx * 0.975f, 1.25f, 0.02f), new Vector3(0.56f, 2.54f, 0.56f), Vector3.zero);
            C(cols, new Vector3(0, 2.395f, 0), new Vector3(1.6f, 0.21f, 0.42f), Vector3.zero);
            C(cols, new Vector3(0, 0.045f, 0.1f), new Vector3(2.46f, 0.09f, 0.7f), Vector3.zero);
            var lc = leaf.gameObject.AddComponent<BoxCollider>(); lc.center = Zm(new Vector3(0.73f, 1.27f, 0.0f)); lc.size = new Vector3(1.46f, 2.1f, 0.34f);
        }
    }
}
