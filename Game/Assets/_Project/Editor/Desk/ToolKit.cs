using System.IO;
using UnityEditor;
using UnityEngine;
using static Ward0.EditorTools.DeskBuilder;

namespace Ward0.EditorTools
{
    /// <summary>
    /// The technician's two hand tools, built from code after the concept art in Assets/characters: the handheld UV/Thermal Bio-Scanner (model 7A)
    /// and the Pneumatic Blood Extractor (model BX-7). Each stands on a small dock with its hose lying on the desk. Authored like every prop
    /// (front toward +Z, mirrored at build time). Menu: Ward0 / Build Hand Tools; also run by Build Map Props.
    /// </summary>
    public static class ToolKit
    {
        const string DeskTex = "Assets/_Project/Art/Desk/Textures/", DeskMat = "Assets/_Project/Art/Desk/Materials/";

        [MenuItem("Ward0/Build Hand Tools")]
        public static void BuildMenu()
        {
            PropKit.EnsureMaterials(); Build(); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }

        public static void Build()
        {
            MatDir = PropKit.Root + "/Materials"; MeshDir = PropKit.Root + "/Meshes"; Directory.CreateDirectory(MatDir); Directory.CreateDirectory(MeshDir); Directory.CreateDirectory(PropKit.Root + "/Prefabs");
            Materials();
            BioScanner(); BloodExtractor();
            Debug.Log("[Ward0] Hand tools built: BioScanner, BloodExtractor");
        }

        static void Materials()
        {
            foreach (var n in new[] { "GaugeAir", "GaugeContainment", "GaugeRadiation" }) mats[n] = AssetDatabase.LoadAssetAtPath<Material>(DeskMat + n + ".mat");
            var screen = AssetDatabase.LoadAssetAtPath<Texture2D>(DeskTex + "Screen_Bioscan.png");
            Emissive("ToolScreen", Color.white * 0.2f, Color.white * 1.4f, screen, 0.85f);
            Mk("ToolShell", new Color(0.30f, 0.31f, 0.26f), 0.55f, 0.32f);            // chipped olive-grey paint
            Mk("ToolShellDark", new Color(0.09f, 0.09f, 0.095f), 0.7f, 0.4f);          // bumpers, bezels, dock
            Mk("ToolBlack", new Color(0.03f, 0.03f, 0.032f), 0.1f, 0.3f);              // grip rubber, hoses, knobs
            Mk("ToolSteel", new Color(0.45f, 0.45f, 0.43f), 1f, 0.55f);
            Mk("ToolBrass", new Color(0.5f, 0.37f, 0.15f), 0.9f, 0.5f);
            Mk("ToolRed", new Color(0.5f, 0.05f, 0.04f), 0.4f, 0.4f);
            Emissive("ToolUV", new Color(0.12f, 0.05f, 0.3f), new Color(0.55f, 0.12f, 2.4f), null, 0.7f);
            Emissive("ToolLens", new Color(0.05f, 0.05f, 0.2f), new Color(0.5f, 0.35f, 2.6f), null, 0.95f);
            Mk("ToolBlood", new Color(0.32f, 0.01f, 0.02f), 0f, 0.85f);
            var glass = Mk("ToolGlass", new Color(0.65f, 0.82f, 0.88f, 0.16f), 0f, 0.95f);
            glass.SetFloat("_Surface", 1f); glass.SetFloat("_Blend", 0f); glass.SetFloat("_SrcBlend", 5f); glass.SetFloat("_DstBlend", 10f); glass.SetFloat("_ZWrite", 0f);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); glass.SetOverrideTag("RenderType", "Transparent"); glass.renderQueue = 3000; glass.SetShaderPassEnabled("ShadowCaster", false);
            EditorUtility.SetDirty(glass);
        }

        static void Begin(string name) { cb = new Combiner(); Origin = Vector3.zero; root = new GameObject(name).transform; }

        static void End(string name)
        {
            var body = Obj("Body", root, Vector3.zero);
            foreach (var kv in cb.Bake(name))
            {
                string p = SaveMesh(kv.Value, name + "_" + kv.Key.name);
                var g = Obj("Body_" + kv.Key.name, body.transform, Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            string path = PropKit.Root + "/Prefabs/" + name + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, path); Object.DestroyImmediate(root.gameObject);
        }

        static void Dock(float x, float baseY, float w, float d)
        {
            Box("ToolShellDark", new Vector3(x, 0.006f, 0f), new Vector3(w, 0.012f, d), 0.004f);
            Cyl("ToolShellDark", new Vector3(x, (0.012f + baseY) * 0.5f, 0f), 0.02f, baseY - 0.012f, null, 16, 0.002f);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Rivet(new Vector3(x + sx * (w * 0.5f - 0.01f), 0.0125f, sz * (d * 0.5f - 0.01f)), Vector3.up, "ToolSteel");
        }

        // ============================================================ bio-scanner
        static void BioScanner()
        {
            Begin("BioScanner");
            const float s = 0.00036f; const float D = 0.085f, F = D * 0.5f;
            Vector3 P(float ix, float iy, float z = 0f) => new Vector3((ix - 512f) * s, (900f - iy) * s + 0.045f, z);
            // shell + corner bumpers
            Box("ToolShell", P(512, 230), new Vector3(935 * s, 400 * s, D), 0.012f);
            foreach (var c in new[] { new Vector2(85, 85), new Vector2(85, 330), new Vector2(325, 60), new Vector2(325, 380), new Vector2(925, 100), new Vector2(925, 380), new Vector2(775, 45) })
                Box("ToolShellDark", P(c.x, c.y), new Vector3(105 * s, 85 * s, D + 0.012f), 0.01f);
            // front face: UV array + thermal lens
            Box("ToolShellDark", P(120, 190, F + 0.003f), new Vector3(150 * s, 200 * s, 0.012f), 0.004f);
            Box("ToolUV", P(120, 190, F + 0.0105f), new Vector3(118 * s, 168 * s, 0.004f), 0.001f);
            for (int i = 0; i <= 5; i++) Box("ToolShellDark", P(120 - 59 + i * 23.6f, 190, F + 0.0135f), new Vector3(0.0016f, 168 * s, 0.002f), 0f);
            for (int i = 0; i <= 7; i++) Box("ToolShellDark", P(120, 190 - 84 + i * 24f, F + 0.0135f), new Vector3(118 * s, 0.0016f, 0.002f), 0f);
            Cyl("ToolShellDark", P(245, 190, F + 0.012f), 62 * s, 0.026f, new Vector3(90, 0, 0), 28, 0.004f);
            Cyl("ToolSteel", P(245, 190, F + 0.026f), 50 * s, 0.006f, new Vector3(90, 0, 0), 28, 0.002f);
            Cyl("ToolLens", P(245, 190, F + 0.031f), 40 * s, 0.004f, new Vector3(90, 0, 0), 24, 0.001f);
            // CRT screen in a deep bezel
            Box("ToolShellDark", P(520, 215, F + 0.004f), new Vector3(280 * s, 250 * s, 0.014f), 0.008f);
            Box("ToolBlack", P(520, 215, F + 0.0105f), new Vector3(240 * s, 215 * s, 0.004f), 0.003f);
            Part("Tool_Screen", root, ScreenMesh(0.082f, 0.068f, 0.006f), "ToolScreen", P(520, 215, F + 0.0125f));
            // right panel: gauge, two knobs, toggles, label plate
            Box("ToolShellDark", P(800, 115, F + 0.003f), new Vector3(250 * s, 160 * s, 0.01f), 0.004f);
            Cyl("ToolBrass", P(850, 135, F + 0.012f), 62 * s, 0.014f, new Vector3(90, 0, 0), 28, 0.003f);
            Part("Tool_Gauge", root, DiscMesh(0.0205f), "GaugeAir", P(850, 135, F + 0.02f));
            foreach (float y in new[] { 150f, 265f })
            {
                Cyl("ToolBlack", P(735, y, F + 0.013f), 34 * s, 0.022f, new Vector3(90, 0, 0), 20, 0.003f);
                Box("ToolSteel", P(735 + 12, y - 12, F + 0.026f), new Vector3(0.004f, 0.013f, 0.002f), 0f, new Vector3(0, 0, 40));
            }
            Box("ToolShellDark", P(860, 285, F + 0.003f), new Vector3(150 * s, 80 * s, 0.01f), 0.004f);
            foreach (float x in new[] { 830f, 905f })
            {
                Cyl("ToolSteel", P(x, 285, F + 0.012f), 15 * s, 0.012f, new Vector3(90, 0, 0), 12, 0.002f);
                Cyl("ToolRed", P(x + 6, 300, F + 0.022f), 9 * s, 0.014f, new Vector3(110, 0, 0), 10, 0.001f);
            }
            Box("ToolShellDark", P(750, 365, F + 0.003f), new Vector3(130 * s, 55 * s, 0.008f), 0.003f);
            Box("DStripes", P(905, 395, F + 0.004f), new Vector3(70 * s, 28 * s, 0.003f), 0.001f);
            RivetRect(P(512, 230, F), 0.31f, 0.125f, 10, 4, Vector3.forward, Vector3.right, Vector3.up, "ToolSteel");
            // grip (tilted), finger grooves, trigger, collar
            Box("ToolBlack", P(690, 650), new Vector3(150 * s, 470 * s, 0.062f), 0.012f, new Vector3(0, 0, 8.8f));
            for (int i = 0; i < 5; i++) Box("ToolShellDark", P(655 + i * 10f, 520 + i * 70f, F - 0.012f), new Vector3(0.05f, 0.005f, 0.004f), 0.001f, new Vector3(0, 0, 8.8f));
            Box("ToolShellDark", P(520, 480, 0f), new Vector3(34 * s, 100 * s, 0.018f), 0.004f, new Vector3(0, 0, 18f));
            Box("ToolShellDark", P(545, 440, 0f), new Vector3(120 * s, 24 * s, 0.03f), 0.005f);
            Box("ToolShell", P(715, 855), new Vector3(210 * s, 70 * s, 0.072f), 0.008f);
            Box("WarnYellow", P(690, 855, F - 0.008f), new Vector3(70 * s, 40 * s, 0.003f), 0.001f);
            // hose lying on the desk
            Put(MeshKit.Tube(new[] { P(740, 895), new Vector3(0.05f, 0.03f, 0f), new Vector3(0.06f, 0.014f, 0.005f), new Vector3(0.1f, 0.0115f, 0.04f), new Vector3(0.17f, 0.0115f, 0.07f), new Vector3(0.23f, 0.0115f, 0.03f), new Vector3(0.2f, 0.0115f, -0.04f), new Vector3(0.13f, 0.0115f, -0.06f) }, 0.0115f, 10, 8, 360f, 0.1f), "ToolBlack", Vector3.zero);
            Dock(0.075f, 0.045f, 0.13f, 0.11f);
            End("BioScanner");
        }

        // ============================================================ blood extractor
        static void BloodExtractor()
        {
            Begin("BloodExtractor");
            const float e = 0.00033f; const float D = 0.07f, F = D * 0.5f;
            Vector3 P(float ix, float iy, float z = 0f) => new Vector3((ix - 500f) * e, (700f - iy) * e + 0.06f, z);
            // glass barrel with blood, guide rods, flanges
            Cyl("ToolGlass", P(502, 205), 62 * e, 205 * e, new Vector3(0, 0, 90), 28, 0.002f);
            Cyl("ToolBlood", P(470, 215), 54 * e, 135 * e, new Vector3(0, 0, 90), 24, 0.002f);
            foreach (float y in new[] { 135f, 285f }) Cyl("ToolSteel", P(500, y), 6 * e, 340 * e, new Vector3(0, 0, 90), 8, 0.001f);
            foreach (float x in new[] { 345f, 395f, 610f }) Cyl("ToolSteel", P(x, 205), 80 * e, 24 * e, new Vector3(0, 0, 90), 28, 0.003f);
            // nose: tapered nuts and the needle
            Cyl("ToolShellDark", P(300, 205), 62 * e, 90 * e, new Vector3(0, 0, 90), 24, 0.004f);
            Cyl("ToolSteel", P(235, 205), 38 * e, 30 * e, new Vector3(0, 0, 90), 6, 0.002f);
            Cyl("ToolSteel", P(190, 207), 22 * e, 60 * e, new Vector3(0, 0, 90), 12, 0.001f);
            Cyl("ToolSteel", P(95, 208), 5.5f * e, 150 * e, new Vector3(0, 0, 90), 8, 0.0004f);
            Cyl("ToolBlood", P(188, 207), 24 * e, 8 * e, new Vector3(0, 0, 90), 12, 0.0005f);
            // valve body, label plate, gauge
            Box("ToolShellDark", P(735, 212), new Vector3(255 * e, 215 * e, D), 0.008f);
            Box("ToolSteel", P(900, 250, F * 0.2f), new Vector3(150 * e, 140 * e, D * 0.9f), 0.004f);
            Box("ToolShellDark", P(900, 250, F * 0.2f + 0.033f), new Vector3(118 * e, 108 * e, 0.003f), 0.001f);
            Cyl("ToolBrass", P(740, 200, F + 0.004f), 90 * e, 0.016f, new Vector3(90, 0, 0), 32, 0.004f);
            Cyl("ToolSteel", P(740, 200, F + 0.012f), 80 * e, 0.004f, new Vector3(90, 0, 0), 32, 0.001f);
            Part("Tool_VacuumGauge", root, DiscMesh(0.0255f), "GaugeContainment", P(740, 200, F + 0.0145f));
            Cyl("ToolBrass", P(918, 85), 28 * e, 40 * e, null, 20, 0.003f);
            Cyl("ToolSteel", P(918, 125), 16 * e, 50 * e, null, 10, 0.001f);
            Box("ToolRed", P(725, 352, F + 0.002f), new Vector3(118 * e, 40 * e, 0.008f), 0.003f);
            RivetRect(P(735, 212, F), 0.075f, 0.065f, 4, 3, Vector3.forward, Vector3.right, Vector3.up, "ToolSteel");
            // grip (tilted), ribs, trigger and valve lever, collar nuts
            Box("ToolBlack", P(775, 505), new Vector3(145 * e, 385 * e, 0.062f), 0.014f, new Vector3(0, 0, 6.9f));
            for (int i = 0; i < 6; i++) Box("ToolShellDark", P(765 + i * 4f, 470 + i * 38f, F - 0.006f), new Vector3(0.042f, 0.004f, 0.004f), 0.001f, new Vector3(0, 0, 6.9f));
            Box("ToolSteel", P(598, 415), new Vector3(20 * e, 110 * e, 0.012f), 0.003f, new Vector3(0, 0, -12f));
            Box("ToolRed", P(645, 405), new Vector3(16 * e, 70 * e, 0.012f), 0.003f, new Vector3(0, 0, 14f));
            Cyl("ToolSteel", P(805, 725), 42 * e, 40 * e, null, 6, 0.002f);
            Cyl("ToolBrass", P(808, 765), 32 * e, 26 * e, null, 6, 0.002f);
            // vacuum hose from the valve down the right side onto the desk
            Put(MeshKit.Tube(new[] { P(910, 320, 0f), P(985, 430, 0.01f), P(990, 600, 0.015f), new Vector3(0.17f, 0.04f, 0.03f), new Vector3(0.16f, 0.0125f, 0.05f), new Vector3(0.12f, 0.0125f, 0.085f), new Vector3(0.05f, 0.0125f, 0.075f), new Vector3(0.02f, 0.0125f, 0.04f) }, 0.0125f, 10, 8, 300f, 0.1f), "ToolBlack", Vector3.zero);
            Dock(0.095f, 0.06f, 0.14f, 0.1f);
            End("BloodExtractor");
        }
    }
}
