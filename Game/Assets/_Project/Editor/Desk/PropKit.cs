using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using Ward0.Gameplay;
using Ward0.Props;
using static Ward0.EditorTools.DeskBuilder;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds the extra props the map needs (minecart, breaker panel, valve station, elevator car, miners, torches, cave-in, crates,
    /// steam / gas particle prefabs). Menu: Ward0 / Build Map Props
    /// </summary>
    public static class PropKit
    {
        public const string Root = "Assets/_Project/Art/Props";
        public const string TexDir = Root + "/Textures";
        static readonly Color Cream = new Color(0.74f, 0.71f, 0.63f, 0.95f);

        [MenuItem("Ward0/Build Map Props")]
        public static void BuildAll()
        {
            MatDir = Root + "/Materials"; MeshDir = Root + "/Meshes";
            foreach (var d in new[] { Root, TexDir, MatDir, MeshDir, Root + "/Prefabs" }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            EnsureMaterials();
            Minecart(); BreakerPanel(); ValveStation(); ElevatorCarProp(); Miner(false); Miner(true); ToolKit.Build(); Battery(); WallTorch(); CaveIn(); SupplyCluster(); SteamVent(); GasCloud(); Stalactite();
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Ward0] Map props built in " + Root);
        }

        // ============================================================ shared setup
        static Material Load(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);

        public static void EnsureMaterials()
        {
            mats.Clear(); mirrored.Clear(); SavedMeshNames.Clear(); Origin = Vector3.zero;
            string T = "Assets/_Project/Art/Tunnel/Materials/", D = "Assets/_Project/Art/Desk/Materials/", R = "Assets/_Project/Art/Door/Materials/";
            foreach (var n in new[] { "MineRock", "MineFloor", "MineWood", "RustSteel", "RustDark", "Rubber", "Brass", "BlackPlastic", "LampBulb", "Puddle" }) mats[n] = Load(T + n + ".mat");
            foreach (var n in new[] { "LedGreen", "LedAmber", "LedRed" }) mats[n] = Load(D + n + ".mat");
            foreach (var n in new[] { "DGun", "DDark", "DYellow", "DStripes", "BioYellow", "WarnYellow", "Chrome" }) mats[n] = Load(R + n + ".mat");
            mats["DarkIron"] = mats["RustDark"]; mats["Gunmetal"] = mats["RustSteel"];

            var puff = TexKit.SoftPuff(TexDir);
            Particle("SteamParticle", puff, new Color(0.86f, 0.9f, 0.92f, 0.55f));
            Particle("GasParticle", puff, new Color(0.55f, 0.78f, 0.22f, 0.22f));
            Particle("FireParticle", puff, new Color(1f, 0.55f, 0.15f, 0.8f));
            Particle("DustParticle", puff, new Color(0.35f, 0.3f, 0.26f, 0.5f));

            Mk("MinerCloth", new Color(0.30f, 0.17f, 0.07f), 0f, 0.12f, null);
            Mk("MinerCloth2", new Color(0.08f, 0.09f, 0.1f), 0f, 0.12f, null);
            Mk("MinerSkin", new Color(0.46f, 0.38f, 0.32f), 0f, 0.25f, null);
            Mk("InfectedSkin", new Color(0.36f, 0.4f, 0.3f), 0f, 0.3f, null);
            Mk("Helmet", new Color(0.72f, 0.56f, 0.07f), 0.3f, 0.4f, null);
            Mk("Reflective", new Color(0.7f, 0.7f, 0.6f), 0.4f, 0.55f, null);
            Emissive("EyeCalm", new Color(0.2f, 0.18f, 0.15f), new Color(0.15f, 0.12f, 0.1f), null, 0.6f);
            Emissive("EyeRed", new Color(0.2f, 0.02f, 0.02f), new Color(5.5f, 0.12f, 0.06f), null, 0.6f);
            Emissive("BatteryGlow", new Color(0.05f, 0.2f, 0.05f), new Color(0.3f, 2.4f, 0.4f), null, 0.5f);
            Emissive("FlameCore", new Color(1f, 0.6f, 0.2f), new Color(4f, 2f, 0.5f), null, 0.5f);
            Mk("RedPaintLite", new Color(0.5f, 0.06f, 0.04f), 0.5f, 0.4f, null);
            Mk("Canvas", new Color(0.32f, 0.3f, 0.22f), 0f, 0.1f, null);

            var glass = Mk("Glass", new Color(0.55f, 0.75f, 0.85f, 0.12f), 0f, 0.95f, null);
            MakeTransparent(glass);
        }

        static void MakeTransparent(Material m)
        {
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        static Material Particle(string name, Texture tex, Color tint)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = name };
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", tint);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_SrcBlend", 5f); m.SetFloat("_DstBlend", 10f); m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = 3000;
            string path = MatDir + "/" + name + ".mat"; AssetDatabase.DeleteAsset(path); AssetDatabase.CreateAsset(m, path); mats[name] = m; return m;
        }

        static void Start(string name) { cb = new Combiner(); Origin = Vector3.zero; root = new GameObject(name).transform; }

        static GameObject Finish(string prefabName)
        {
            var body = Obj("Body", root, Vector3.zero);
            foreach (var kv in cb.Bake(prefabName))
            {
                string p = SaveMesh(kv.Value, prefabName + "_" + kv.Key.name);
                var g = Obj("Body_" + kv.Key.name, body.transform, Vector3.zero);
                g.AddComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(p);
                g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            var go = root.gameObject;
            PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/" + prefabName + ".prefab");
            Object.DestroyImmediate(go);
            return AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + prefabName + ".prefab");
        }

        public static GameObject LoadPrefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab");

        public static ParticleSystem MakePS(Transform parent, string name, Vector3 pos, string matName, float life, float size, float speed, float rate, Color c0, int max, ParticleSystemShapeType shape, float shapeScale, float gravity = 0f, bool loop = true, float sizeEnd = 1.8f, bool playOnAwake = true)
        {
            var g = Obj(name, parent, pos); g.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var ps = g.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = loop; main.playOnAwake = playOnAwake; main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.7f, life * 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.6f, size * 1.3f); main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startColor = c0; main.maxParticles = max; main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 6.28f);
            var em = ps.emission; em.rateOverTime = rate;
            var sh = ps.shape; sh.shapeType = shape; sh.radius = shapeScale; sh.angle = 18f; sh.scale = Vector3.one * shapeScale;
            var col = ps.colorOverLifetime; col.enabled = true;
            var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, sizeEnd));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
            var rend = g.GetComponent<ParticleSystemRenderer>(); rend.sharedMaterial = M(matName); rend.renderMode = ParticleSystemRenderMode.Billboard;
            return ps;
        }

        static void Collider(Transform parent, Vector3 c, Vector3 s) { var b = parent.gameObject.AddComponent<BoxCollider>(); b.center = Zm(c); b.size = s; }

        // ============================================================ minecart
        static void Minecart()
        {
            Start("Minecart");
            // body: tapered box built from plates
            Box("RustSteel", new Vector3(0, 0.45f, 0), new Vector3(0.86f, 0.04f, 1.5f), 0.01f);
            foreach (float sx in new[] { -1f, 1f }) Box("RustSteel", new Vector3(sx * 0.46f, 0.72f, 0), new Vector3(0.04f, 0.55f, 1.55f), 0.008f, new Vector3(0, 0, -sx * 8f));
            foreach (float sz in new[] { -1f, 1f }) Box("RustSteel", new Vector3(0, 0.72f, sz * 0.76f), new Vector3(0.95f, 0.55f, 0.04f), 0.008f, new Vector3(sz * 6f, 0, 0));
            // rim angle iron + ribs + rivets
            foreach (float sx in new[] { -1f, 1f }) Box("RustDark", new Vector3(sx * 0.505f, 1.0f, 0), new Vector3(0.07f, 0.05f, 1.62f), 0.01f);
            foreach (float sz in new[] { -1f, 1f }) Box("RustDark", new Vector3(0, 1.0f, sz * 0.8f), new Vector3(1.05f, 0.05f, 0.07f), 0.01f);
            for (int i = -2; i <= 2; i++) foreach (float sx in new[] { -1f, 1f }) Box("RustDark", new Vector3(sx * 0.475f, 0.72f, i * 0.31f), new Vector3(0.03f, 0.55f, 0.05f), 0.006f, new Vector3(0, 0, -sx * 8f));
            for (int i = 0; i < 12; i++) { Rivet(new Vector3(-0.47f - 0.0f, 0.55f + (i % 2) * 0.3f, -0.7f + i * 0.13f), Vector3.left, "RustDark"); Rivet(new Vector3(0.47f, 0.55f + (i % 2) * 0.3f, -0.7f + i * 0.13f), Vector3.right, "RustDark"); }
            // chassis, axles, wheels
            Box("RustDark", new Vector3(0, 0.38f, 0), new Vector3(0.12f, 0.08f, 1.5f), 0.01f);
            foreach (float z in new[] { -0.5f, 0.5f })
            {
                Cyl("RustDark", new Vector3(0, 0.2f, z), 0.035f, 1.0f, new Vector3(0, 0, 90), 10, 0.003f);
                foreach (float sx in new[] { -1f, 1f })
                {
                    Cyl("RustSteel", new Vector3(sx * 0.4f, 0.2f, z), 0.17f, 0.05f, new Vector3(0, 0, 90), 24, 0.006f);
                    Cyl("RustDark", new Vector3(sx * 0.37f, 0.2f, z), 0.2f, 0.018f, new Vector3(0, 0, 90), 24, 0.004f);
                    Cyl("Brass", new Vector3(sx * 0.435f, 0.2f, z), 0.04f, 0.03f, new Vector3(0, 0, 90), 10, 0.003f);
                    Box("RustDark", new Vector3(sx * 0.33f, 0.31f, z), new Vector3(0.03f, 0.14f, 0.1f), 0.004f);
                }
            }
            // couplings
            foreach (float sz in new[] { -1f, 1f }) { Box("RustDark", new Vector3(0, 0.4f, sz * 0.86f), new Vector3(0.06f, 0.06f, 0.18f), 0.006f); Cyl("RustSteel", new Vector3(0, 0.4f, sz * 0.97f), 0.04f, 0.02f, new Vector3(90, 0, 0), 12, 0.002f); }
            // cargo: coal heap
            var rng = new System.Random(77);
            for (int i = 0; i < 34; i++)
            {
                float rx = (float)rng.NextDouble() * 0.7f - 0.35f, rz = (float)rng.NextDouble() * 1.25f - 0.62f; float s = 0.1f + (float)rng.NextDouble() * 0.14f; float h = 0.98f - 0.22f * (Mathf.Abs(rx) / 0.35f + Mathf.Abs(rz) / 0.62f) / 2f;
                Box("MineRock", new Vector3(rx, h + s * 0.2f, rz), new Vector3(s, s * 0.8f, s * 0.9f), s * 0.28f, new Vector3((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f));
            }
            var go = root.gameObject; Collider(root, new Vector3(0, 0.6f, 0), new Vector3(1.1f, 1.2f, 1.7f));
            Finish("Minecart");
        }

        // ============================================================ breaker panel (repair station)
        static void BreakerPanel()
        {
            Start("BreakerPanel");
            Box("RustSteel", new Vector3(0, 1.1f, 0), new Vector3(1.4f, 1.9f, 0.34f), 0.025f);                  // cabinet
            Box("RustDark", new Vector3(0, 1.1f, 0.19f), new Vector3(1.28f, 1.78f, 0.04f), 0.015f);             // inner frame
            Box("BlackPlastic", new Vector3(0, 1.1f, 0.215f), new Vector3(1.18f, 1.68f, 0.012f), 0.004f);       // backplane
            RivetRect(new Vector3(0, 1.1f, 0.2f), 1.32f, 1.82f, 6, 9, Vector3.forward, Vector3.right, Vector3.up);
            // fuse modules (3 columns x 5 rows) with LEDs
            for (int cx = -1; cx <= 1; cx++)
                for (int r = 0; r < 5; r++)
                {
                    var p = new Vector3(cx * 0.36f, 1.62f - r * 0.27f, 0.235f);
                    Box("RustDark", p, new Vector3(0.28f, 0.2f, 0.05f), 0.01f);
                    Box("Chrome", p + new Vector3(0, 0.02f, 0.03f), new Vector3(0.18f, 0.05f, 0.02f), 0.004f);
                }
            // big knife-switch base (the lever itself is separate)
            Box("RustDark", new Vector3(0, 0.55f, 0.24f), new Vector3(0.3f, 0.5f, 0.06f), 0.012f);
            Box("Chrome", new Vector3(0, 0.55f, 0.275f), new Vector3(0.05f, 0.4f, 0.02f), 0.004f);
            // hazard band, label plate, warning lamp
            Put(MeshKit.Quad(1.3f, 0.12f), "DStripes", new Vector3(0, 0.22f, 0.176f));
            Box("DDark", new Vector3(0, 2.1f, 0.0f), new Vector3(0.9f, 0.18f, 0.2f), 0.012f);
            Cyl("RustDark", new Vector3(0.45f, 2.12f, 0.06f), 0.09f, 0.12f, null, 16, 0.006f);
            // cable bundle falling from the bottom
            Put(MeshKit.Tube(new[] { new Vector3(-0.4f, 0.15f, 0.15f), new Vector3(-0.45f, 0.0f, 0.3f), new Vector3(-0.2f, -0.05f, 0.6f) }, 0.03f, 10, 8, 60f), "Rubber", Vector3.zero);
            Put(MeshKit.Tube(new[] { new Vector3(0.35f, 0.15f, 0.15f), new Vector3(0.45f, 0.0f, 0.35f), new Vector3(0.3f, -0.05f, 0.7f) }, 0.025f, 10, 8, 60f), "Rubber", Vector3.zero);
            // status LEDs (red until repaired)
            var leds = new List<Renderer>();
            for (int i = 0; i < 3; i++) leds.Add(Part("Led_" + i, root, MeshKit.Sphere(0.022f, 12, 8, 0.7f), "LedRed", new Vector3(-0.36f + i * 0.36f, 0.88f, 0.26f)));
            // the switch lever that moves while you hold E
            var lever = Obj("Lever", root, new Vector3(0, 0.38f, 0.3f), new Vector3(60f, 0, 0));
            Part("LeverArm", lever.transform, MeshKit.Cylinder(0.018f, 0.5f, 10, 0.002f), "Chrome", new Vector3(0, 0.25f, 0));
            Part("LeverKnob", lever.transform, MeshKit.Sphere(0.045f, 14, 10), "RedPaintLite", new Vector3(0, 0.54f, 0));
            Label("BREAKER", new Vector3(0, 2.1f, 0.11f), Vector3.zero, 0.06f, root, 0.9f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.9f, 0.8f, 0.3f), 6f);
            Label("MAIN FEED - HIGH VOLTAGE", new Vector3(0, 0.1f, 0.2f), Vector3.zero, 0.04f, root, 1.2f, TextAlignmentOptions.Center, FontStyles.Bold, Cream, 3f);
            Collider(root, new Vector3(0, 1.1f, 0.15f), new Vector3(1.4f, 2.0f, 0.6f));
            var go = root.gameObject;
            var st = go.AddComponent<RepairStation>();
            var so = new SerializedObject(st);
            so.FindProperty("stationName").stringValue = "breaker";
            so.FindProperty("spinner").objectReferenceValue = lever.transform;
            so.FindProperty("spinAxis").vector3Value = Vector3.right; so.FindProperty("spinDegrees").floatValue = -80f;
            var arr = so.FindProperty("ledsToGreen"); arr.arraySize = leds.Count; for (int i = 0; i < leds.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = leds[i];
            so.FindProperty("greenMat").objectReferenceValue = M("LedGreen");
            so.ApplyModifiedPropertiesWithoutUndo();
            Finish("BreakerPanel");
        }

        // ============================================================ valve station
        static void ValveStation()
        {
            Start("ValveStation");
            // base plate and manifold
            Box("RustDark", new Vector3(0, 0.05f, 0), new Vector3(1.8f, 0.1f, 1.0f), 0.015f);
            Cyl("RustSteel", new Vector3(0, 0.55f, -0.25f), 0.13f, 3.2f, new Vector3(0, 0, 90), 24, 0.005f);        // horizontal header
            Cyl("RustSteel", new Vector3(-0.5f, 0.9f, -0.25f), 0.11f, 1.6f, null, 22, 0.005f);                        // riser
            Cyl("RustSteel", new Vector3(0.5f, 0.9f, -0.25f), 0.11f, 1.6f, null, 22, 0.005f);
            foreach (float x in new[] { -1.2f, -0.5f, 0.5f, 1.2f }) { Cyl("RustDark", new Vector3(x, 0.55f, -0.25f), 0.19f, 0.06f, new Vector3(0, 0, 90), 24, 0.006f); }
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; Rivet(new Vector3(-1.2f + 0.03f, 0.55f + Mathf.Sin(a) * 0.16f, -0.25f + Mathf.Cos(a) * 0.16f), Vector3.right, "RustSteel"); }
            // gate valve body + yoke
            Box("RustDark", new Vector3(0, 0.55f, -0.25f), new Vector3(0.5f, 0.42f, 0.42f), 0.04f);
            Cyl("RustDark", new Vector3(0, 0.88f, -0.25f), 0.12f, 0.3f, null, 20, 0.01f);
            Cyl("RustSteel", new Vector3(0, 1.2f, -0.25f), 0.03f, 0.5f, null, 10, 0.003f);
            foreach (float sx in new[] { -1f, 1f }) Box("RustDark", new Vector3(sx * 0.1f, 1.05f, -0.25f), new Vector3(0.04f, 0.5f, 0.1f), 0.006f);
            Box("RustDark", new Vector3(0, 1.33f, -0.25f), new Vector3(0.3f, 0.05f, 0.12f), 0.006f);
            // gauges on the risers
            foreach (float x in new[] { -0.5f, 0.5f })
            {
                Cyl("RustDark", new Vector3(x, 1.0f, -0.07f), 0.02f, 0.28f, new Vector3(90, 0, 0), 8, 0.002f);
                Cyl("Brass", new Vector3(x, 1.0f, 0.1f), 0.075f, 0.05f, new Vector3(90, 0, 0), 24, 0.006f);
                Put(MeshKit.Quad(0.1f, 0.1f), "WarnYellow", new Vector3(x, 1.0f, 0.127f));
            }
            // hand wheel (spins while repairing)
            var wheel = Obj("Wheel", root, new Vector3(0, 1.5f, -0.25f), new Vector3(90, 0, 0));
            var ring = new List<Vector2>(); for (int i = 0; i <= 12; i++) { float a = i / 12f * Mathf.PI * 2f; ring.Add(new Vector2(0.3f + Mathf.Cos(a) * 0.03f, Mathf.Sin(a) * 0.03f)); }
            Part("WheelRim", wheel.transform, MeshKit.Lathe(ring.ToArray(), 40, true), "RedPaintLite", Vector3.zero);
            for (int k = 0; k < 4; k++) Part("Spoke" + k, wheel.transform, MeshKit.Box(new Vector3(0.6f, 0.04f, 0.04f), 0.008f), "RedPaintLite", Vector3.zero, new Vector3(0, k * 45f, 0));
            Part("Hub", wheel.transform, MeshKit.Cylinder(0.06f, 0.1f, 16, 0.006f), "Brass", Vector3.zero);
            // leak indicator LEDs
            var leds = new List<Renderer> { Part("LedA", root, MeshKit.Sphere(0.024f, 12, 8, 0.7f), "LedRed", new Vector3(-0.8f, 0.3f, 0.3f)), Part("LedB", root, MeshKit.Sphere(0.024f, 12, 8, 0.7f), "LedRed", new Vector3(-0.7f, 0.3f, 0.3f)) };
            Box("DDark", new Vector3(-0.75f, 0.25f, 0.28f), new Vector3(0.28f, 0.12f, 0.1f), 0.01f);
            Put(MeshKit.Quad(1.0f, 0.12f), "DStripes", new Vector3(0.0f, 0.12f, 0.505f));
            Label("COOLANT VALVE", new Vector3(0, 1.82f, -0.25f), Vector3.zero, 0.07f, root, 1.3f, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.9f, 0.8f, 0.3f), 5f);
            // steam leak (before repair)
            Collider(root, new Vector3(0, 0.9f, -0.1f), new Vector3(1.7f, 1.9f, 1.0f));
            var go = root.gameObject; var st = go.AddComponent<RepairStation>();
            var so = new SerializedObject(st);
            so.FindProperty("stationName").stringValue = "valve";
            so.FindProperty("spinner").objectReferenceValue = wheel.transform; so.FindProperty("spinAxis").vector3Value = Vector3.up; so.FindProperty("spinDegrees").floatValue = 720f;
            var arr = so.FindProperty("ledsToGreen"); arr.arraySize = leds.Count; for (int i = 0; i < leds.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = leds[i];
            so.FindProperty("greenMat").objectReferenceValue = M("LedGreen");
            so.ApplyModifiedPropertiesWithoutUndo();
            Finish("ValveStation");
        }

        // ============================================================ elevator car
        static void ElevatorCarProp()
        {
            Start("ElevatorCar");
            const float W = 3.4f, D = 3.2f, H = 3.0f;
            Box("DDark", new Vector3(0, 0.06f, 0), new Vector3(W, 0.12f, D), 0.015f);                               // floor
            Put(MeshKit.Quad(W - 0.4f, 0.14f), "DStripes", new Vector3(0, 0.125f, D / 2 - 0.2f), new Vector3(-90, 0, 0));
            Box("DDark", new Vector3(0, H, 0), new Vector3(W, 0.12f, D), 0.015f);                                    // ceiling
            // cage frame: corner posts, rails, vertical bars on back and sides
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f }) Box("DGun", new Vector3(sx * (W / 2 - 0.06f), H / 2, sz * (D / 2 - 0.06f)), new Vector3(0.12f, H, 0.12f), 0.01f);
            foreach (float y in new[] { 0.45f, 1.1f, 1.8f, 2.6f })
            {
                Box("DGun", new Vector3(0, y, -(D / 2 - 0.05f)), new Vector3(W - 0.1f, 0.06f, 0.06f), 0.005f);
                foreach (float sx in new[] { -1f, 1f }) Box("DGun", new Vector3(sx * (W / 2 - 0.05f), y, 0), new Vector3(0.06f, 0.06f, D - 0.1f), 0.005f);
            }
            for (int i = -6; i <= 6; i++) Cyl("DDark", new Vector3(i * 0.25f, H / 2, -(D / 2 - 0.05f)), 0.012f, H - 0.2f, null, 6, 0.0015f);
            for (int i = -6; i <= 6; i++) foreach (float sx in new[] { -1f, 1f }) Cyl("DDark", new Vector3(sx * (W / 2 - 0.05f), H / 2, i * 0.23f), 0.012f, H - 0.2f, null, 6, 0.0015f);
            // lintel above the door gap + side jambs
            Box("DGun", new Vector3(0, 2.75f, D / 2 - 0.06f), new Vector3(W, 0.5f, 0.12f), 0.012f);
            foreach (float sx in new[] { -1f, 1f }) Box("DGun", new Vector3(sx * 1.22f, H / 2, D / 2 - 0.06f), new Vector3(0.96f, H, 0.12f), 0.012f);
            // ceiling lamp panel
            Box("BlackPlastic", new Vector3(0, H - 0.08f, 0), new Vector3(0.9f, 0.04f, 0.5f), 0.006f);
            Add(MeshKit.Quad(0.8f, 0.4f), M("LampBulb"), new Vector3(0, H - 0.101f, 0), Quaternion.Euler(90, 0, 0));
            // call button panel on the right wall
            Box("DDark", new Vector3(W / 2 - 0.1f, 1.25f, 0.5f), new Vector3(0.1f, 0.34f, 0.22f), 0.012f);
            Box("RustDark", new Vector3(W / 2 - 0.14f, 1.3f, 0.5f), new Vector3(0.04f, 0.1f, 0.1f), 0.01f);
            Part("CallLed", root, MeshKit.Sphere(0.025f, 12, 8, 0.7f), "LedAmber", new Vector3(W / 2 - 0.17f, 1.2f, 0.5f));
            Label("CALL", new Vector3(W / 2 - 0.158f, 1.37f, 0.5f), new Vector3(0, 90, 0), 0.03f, root, 0.2f, TextAlignmentOptions.Center, FontStyles.Bold, Cream, 3f);
            var btn = Obj("CallButton", root, new Vector3(W / 2 - 0.14f, 1.3f, 0.5f));
            var bc = btn.AddComponent<BoxCollider>(); bc.size = new Vector3(0.3f, 0.5f, 0.45f);
            var eb = btn.AddComponent<ElevatorButton>();
            // collider walls so the player cannot leave the cage except through the door
            var cols = Obj("Colliders", root, Vector3.zero);
            void C(Vector3 c, Vector3 s) { var b = cols.AddComponent<BoxCollider>(); b.center = Zm(c); b.size = s; }
            C(new Vector3(0, 0.06f, 0), new Vector3(W, 0.12f, D)); C(new Vector3(0, H, 0), new Vector3(W, 0.2f, D));
            C(new Vector3(0, H / 2, -(D / 2)), new Vector3(W, H, 0.1f));
            foreach (float sx in new[] { -1f, 1f }) C(new Vector3(sx * (W / 2), H / 2, 0), new Vector3(0.1f, H, D));
            var stand = Obj("StandPoint", root, new Vector3(0, 0.12f, 0.2f), new Vector3(0, 180, 0));   // player faces the door
            // the elevator car prefab holds the car; the door is instanced and wired in the map builder
            var go = root.gameObject; var car = go.AddComponent<ElevatorCar>();
            var so = new SerializedObject(car); so.FindProperty("standPoint").objectReferenceValue = stand.transform; so.ApplyModifiedPropertiesWithoutUndo();
            var so2 = new SerializedObject(eb); so2.FindProperty("car").objectReferenceValue = car; so2.ApplyModifiedPropertiesWithoutUndo();
            var lamp = Obj("CarLight", root, new Vector3(0, H - 0.4f, 0)).AddComponent<Light>(); lamp.type = LightType.Point; lamp.color = new Color(1f, 0.85f, 0.65f); lamp.intensity = 16f; lamp.range = 7f; lamp.shadows = LightShadows.None;
            lamp.gameObject.AddComponent<FlickerLight>();
            Finish("ElevatorCar");
        }

        // ============================================================ miners
        static void Miner(bool infected) => CharacterKit.BuildMiner(infected);

        // ============================================================ battery
        static void Battery()
        {
            Start("Battery");
            Cyl("RustDark", new Vector3(0, 0.1f, 0), 0.055f, 0.2f, null, 18, 0.006f);
            Cyl("Chrome", new Vector3(0, 0.215f, 0), 0.025f, 0.03f, null, 12, 0.003f);
            Box("WarnYellow", new Vector3(0, 0.1f, 0.056f), new Vector3(0.07f, 0.12f, 0.004f), 0.001f);
            Part("Glow", root, MeshKit.Sphere(0.02f, 10, 8), "BatteryGlow", new Vector3(0, 0.05f, 0.058f));
            var go = root.gameObject; var bc = go.AddComponent<BoxCollider>(); bc.center = new Vector3(0, 0.11f, 0); bc.size = new Vector3(0.2f, 0.3f, 0.2f);
            go.AddComponent<BatteryPickup>();
            var l = Obj("Glow", root, new Vector3(0, 0.12f, 0)).AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.4f, 1f, 0.5f); l.intensity = 0.6f; l.range = 2.2f; l.shadows = LightShadows.None;
            Finish("Battery");
        }

        // ============================================================ wall torch
        static void WallTorch()
        {
            Start("WallTorch");
            Box("RustDark", new Vector3(0, 0, 0), new Vector3(0.14f, 0.2f, 0.03f), 0.006f);
            Cyl("RustDark", new Vector3(0, 0.0f, 0.08f), 0.045f, 0.14f, new Vector3(75, 0, 0), 12, 0.004f);
            Cyl("MineWood", new Vector3(0, 0.12f, 0.17f), 0.03f, 0.5f, new Vector3(-12, 0, 0), 10, 0.004f);
            Cyl("RustDark", new Vector3(0, 0.34f, 0.22f), 0.05f, 0.05f, null, 12, 0.004f);
            Part("Ember", root, MeshKit.Sphere(0.045f, 10, 8, 1.2f), "FlameCore", new Vector3(0, 0.38f, 0.22f));
            var fire = MakePS(root, "Flame", new Vector3(0, 0.4f, 0.22f), "FireParticle", 0.7f, 0.22f, 0.5f, 22f, new Color(1f, 0.6f, 0.2f, 0.9f), 50, ParticleSystemShapeType.Cone, 0.04f, -0.05f, true, 0.4f);
            var l = Obj("Light", root, new Vector3(0, 0.45f, 0.35f)).AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1f, 0.55f, 0.2f); l.intensity = 3.2f; l.range = 7f; l.shadows = LightShadows.None;
            l.gameObject.AddComponent<FlickerLight>();
            Finish("WallTorch");
        }

        // ============================================================ cave-in gate
        static void CaveIn()
        {
            Start("CaveIn");
            var rng = new System.Random(321);
            for (int i = 0; i < 70; i++)
            {
                float t = (float)rng.NextDouble(); float x = (float)(rng.NextDouble() * 2 - 1) * 1.85f; float y = 0.1f + (float)rng.NextDouble() * 3.0f;
                float arch = Mathf.Sqrt(Mathf.Max(0f, 1.9f * 1.9f - x * x)) + 1.3f; if (y > arch + 0.1f) continue;
                float s = 0.35f + (float)rng.NextDouble() * 0.8f; float z = (float)(rng.NextDouble() - 0.5f) * 1.0f;
                Box("MineRock", new Vector3(x, y, z), new Vector3(s, s * 0.8f, s), s * 0.3f, new Vector3((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f));
            }
            for (int i = 0; i < 4; i++) Box("MineWood", new Vector3(-1.2f + i * 0.8f, 1.1f + (i % 2) * 0.7f, 0.6f), new Vector3(0.18f, 2.2f, 0.18f), 0.02f, new Vector3(0, 0, (i - 1.5f) * 22f));
            Box("RustDark", new Vector3(0, 0.2f, 0.7f), new Vector3(3.2f, 0.12f, 0.2f), 0.01f);
            var ps = MakePS(root, "Dust", new Vector3(0, 0.2f, 0.4f), "DustParticle", 4f, 1.1f, 0.15f, 4f, new Color(0.4f, 0.35f, 0.3f, 0.35f), 40, ParticleSystemShapeType.Box, 1.5f, 0f, true, 2.2f);
            Collider(root, new Vector3(0, 1.6f, 0), new Vector3(3.7f, 3.3f, 1.5f));
            Finish("CaveIn");
        }

        // ============================================================ supply cluster (crates + barrels)
        static void SupplyCluster()
        {
            Start("SupplyCluster");
            Box("MineWood", new Vector3(0, 0.4f, 0), new Vector3(0.9f, 0.8f, 0.9f), 0.02f);
            for (int i = 0; i < 2; i++) { Box("RustDark", new Vector3(0, 0.4f, 0.455f), new Vector3(0.92f, 0.07f, 0.02f), 0.004f, new Vector3(0, 0, i == 0 ? 38f : -38f)); }
            RivetRect(new Vector3(0, 0.4f, 0.46f), 0.82f, 0.72f, 4, 4, Vector3.forward, Vector3.right, Vector3.up);
            Box("MineWood", new Vector3(0.95f, 0.3f, 0.1f), new Vector3(0.7f, 0.6f, 0.7f), 0.02f, new Vector3(0, 14, 0));
            Box("MineWood", new Vector3(0.4f, 1.05f, 0.05f), new Vector3(0.7f, 0.6f, 0.7f), 0.02f, new Vector3(0, -10, 0));
            foreach (var p in new[] { new Vector3(-0.85f, 0, 0.3f), new Vector3(-0.75f, 0, -0.45f) })
            {
                Cyl("RustSteel", p + new Vector3(0, 0.45f, 0), 0.28f, 0.9f, null, 22, 0.01f);
                foreach (float y in new[] { 0.12f, 0.45f, 0.78f }) Cyl("RustDark", p + new Vector3(0, y, 0), 0.292f, 0.04f, null, 22, 0.004f);
                Put(MeshKit.Quad(0.28f, 0.28f), "BioYellow", p + new Vector3(0, 0.5f, 0.288f));
            }
            Collider(root, new Vector3(0.1f, 0.5f, 0), new Vector3(2.4f, 1.0f, 1.3f));
            Finish("SupplyCluster");
        }

        // ============================================================ steam vent + gas cloud + stalactites
        static void SteamVent()
        {
            Start("SteamVent");
            Cyl("RustDark", new Vector3(0, 0.05f, 0), 0.22f, 0.1f, null, 20, 0.006f);
            Cyl("RustSteel", new Vector3(0, 0.13f, 0), 0.12f, 0.12f, null, 18, 0.006f);
            var a = MakePS(root, "SteamA", new Vector3(0, 0.2f, 0), "SteamParticle", 3.2f, 2.2f, 1.6f, 55f, new Color(0.9f, 0.93f, 0.95f, 0.8f), 220, ParticleSystemShapeType.Cone, 0.3f, -0.02f, true, 3.2f, false);
            var b = MakePS(root, "SteamB", new Vector3(0, 0.3f, 0), "SteamParticle", 3.5f, 2.8f, 0.9f, 40f, new Color(0.9f, 0.93f, 0.95f, 0.7f), 160, ParticleSystemShapeType.Sphere, 1.0f, 0f, true, 3.2f, false);
            var go = root.gameObject; var sb = go.AddComponent<SteamBurst>();
            var so = new SerializedObject(sb); var arr = so.FindProperty("puffs"); arr.arraySize = 2; arr.GetArrayElementAtIndex(0).objectReferenceValue = a; arr.GetArrayElementAtIndex(1).objectReferenceValue = b; so.ApplyModifiedPropertiesWithoutUndo();
            Finish("SteamVent");
        }

        static void GasCloud()
        {
            Start("GasCloud");
            MakePS(root, "Gas", new Vector3(0, 0.1f, 0), "GasParticle", 6f, 3.2f, 0.2f, 14f, new Color(0.6f, 0.85f, 0.25f, 0.5f), 120, ParticleSystemShapeType.Box, 4.5f, -0.01f, true, 1.8f);
            var l = Obj("GasGlow", root, new Vector3(0, 1.2f, 0)).AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.5f, 1f, 0.3f); l.intensity = 1.2f; l.range = 7f; l.shadows = LightShadows.None;
            Finish("GasCloud");
        }

        static void Stalactite()
        {
            Start("Stalactites");
            var rng = new System.Random(555);
            for (int i = 0; i < 9; i++)
            {
                float x = ((float)rng.NextDouble() - 0.5f) * 3.6f, z = ((float)rng.NextDouble() - 0.5f) * 3.6f; float h = 0.5f + (float)rng.NextDouble() * 1.1f, r = 0.1f + (float)rng.NextDouble() * 0.16f;
                Add(MeshKit.Lathe(new[] { new Vector2(r, 0f), new Vector2(r * 0.7f, -h * 0.4f), new Vector2(r * 0.35f, -h * 0.75f), new Vector2(0f, -h) }, 10, true), M("MineRock"), new Vector3(x, 0, z));
            }
            Finish("Stalactites");
        }
    }
}
