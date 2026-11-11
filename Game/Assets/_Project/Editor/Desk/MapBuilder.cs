using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Ward0.Gameplay;
using Ward0.Props;
using static Ward0.EditorTools.DeskBuilder;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Assembles the playable level: the armoured checkpoint (booth, quarantine cell, airlock, elevator) high above a
    /// six-chamber mine network joined by modular tunnels, with repair stations, hiding cover, gas, patrols and corridor swaps.
    /// Menu: Ward0 / Build Game Map (run Build Map Props first).
    /// </summary>
    public static class MapBuilder
    {
        const string MapRoot = "Assets/_Project/Art/Map";
        const string ScenePath = "Assets/_Project/Scenes/Ward0_Map.unity";
        const float Y0 = 30f;            // floor height of the upper checkpoint
        const float Half = 4f;           // chamber half-size
        const float OpenR = 2.05f;       // radius of the arch opening in chamber walls
        const float CY = 1.3f;
        const string ZoneDir = "Assets/_Project/Scenes/Zones";
        static Combiner map;
        static Transform worldT, upperT, mineT, sysT, coreMineT;   // mineT = the mine zone currently being built; coreMineT = the always-loaded graph
        // The mine is split into one additive scene per chamber (plus the tunnel segments nearest to it) so it can be streamed in as the player nears it.
        static readonly string[] ZoneIds = { "N0", "N1", "N2", "N3", "N4", "N5" };
        static readonly Dictionary<string, Transform> zoneT = new Dictionary<string, Transform>();
        static readonly Dictionary<string, Combiner> zoneMap = new Dictionary<string, Combiner>();
        static Combiner upperMap;
        static System.Random rng;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Color Cream = new Color(0.74f, 0.71f, 0.63f, 0.95f);

        // chamber centres
        static readonly Vector3 P0 = new Vector3(0, 0, 0), P1 = new Vector3(-26, 0, 0), P4 = new Vector3(26, 0, 0), P2 = new Vector3(-26, 0, -20), P3 = new Vector3(0, 0, -20), P5 = new Vector3(26, 0, -20);

        static Vector3 NodePos(string id) { switch (id) { case "N0": return P0; case "N1": return P1; case "N2": return P2; case "N3": return P3; case "N4": return P4; default: return P5; } }

        /// <summary>The chamber whose zone owns a world position (nearest centre, ignoring height).</summary>
        static string NodeOf(Vector3 p)
        {
            string best = ZoneIds[0]; float bd = float.MaxValue;
            foreach (var id in ZoneIds) { var c = NodePos(id); float d = (c.x - p.x) * (c.x - p.x) + (c.z - p.z) * (c.z - p.z); if (d < bd) { bd = d; best = id; } }
            return best;
        }

        /// <summary>Points the mine builders (combined geometry and the parent for props) at one zone.</summary>
        static void UseZone(string id) { mineT = zoneT[id]; map = zoneMap[id]; }

        // ============================================================ helpers (final-space, no mirroring)
        static void AB(string mat, Vector3 min, Vector3 max, float uv = 0.5f, float bevel = 0.02f) => map.Add(MeshKit.Box(max - min, bevel, uv), M(mat), (min + max) * 0.5f);
        static void BR(string mat, Vector3 c, Vector3 size, Vector3 euler, float uv = 0.5f, float bevel = 0.02f) => map.Add(MeshKit.Box(size, bevel, uv), M(mat), c, Quaternion.Euler(euler));

        static GameObject G(string name, Transform parent, Vector3 pos, Vector3? euler = null)
        {
            var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero); return g;
        }

        static GameObject Place(string prefab, Vector3 pos, float yaw, Transform parent, string name = null)
        {
            if (parent == mineT && zoneT.Count > 0) { UseZone(NodeOf(pos)); parent = mineT; }
            if (!prefabs.TryGetValue(prefab, out var pf)) prefabs[prefab] = pf = AssetDatabase.LoadAssetAtPath<GameObject>(prefab);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(pf, parent); go.transform.position = pos; go.transform.rotation = Quaternion.Euler(0, yaw, 0); if (name != null) go.name = name; return go;
        }
        static string P(string n) => PropKit.Root + "/Prefabs/" + n + ".prefab";

        static TextMeshPro Text(string text, Vector3 pos, Vector3 euler, float size, Transform parent, float width = 3f, TextAlignmentOptions al = TextAlignmentOptions.Center, Color? col = null, float spacing = 4f)
        {
            var g = G("Text_" + (text.Length > 12 ? text.Substring(0, 12) : text).Replace("\n", " "), parent, pos, euler);
            var t = g.AddComponent<TextMeshPro>(); t.text = text; t.fontSize = size * 10f; t.fontStyle = FontStyles.Bold; t.alignment = al; t.color = col ?? Cream; t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap; t.rectTransform.sizeDelta = new Vector2(width, size * 4f); t.raycastTarget = false; return t;
        }

        static Light PointLight(string name, Transform parent, Vector3 pos, Color c, float intensity, float range, bool shadows)
        {
            var g = G(name, parent, pos); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = shadows ? LightShadows.Soft : LightShadows.None; return l;
        }

        static void SetRef(Object o, string field, Object value) { var so = new SerializedObject(o); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetArr(Object o, string field, IList<Object> values) { var so = new SerializedObject(o); var a = so.FindProperty(field); a.arraySize = values.Count; for (int i = 0; i < values.Count; i++) a.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetBool(Object o, string f, bool v) { var so = new SerializedObject(o); so.FindProperty(f).boolValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }
        static void SetFloat(Object o, string f, float v) { var so = new SerializedObject(o); so.FindProperty(f).floatValue = v; so.ApplyModifiedPropertiesWithoutUndo(); }

        // direction tables: N=+z wall, S=-z wall, E=+x wall, W=-x wall
        static Vector3 Dir(char d) => d == 'N' ? Vector3.forward : (d == 'S' ? Vector3.back : (d == 'E' ? Vector3.right : Vector3.left));
        static Vector3 Perp(char d) => (d == 'N' || d == 'S') ? Vector3.right : Vector3.forward;
        static float InwardYaw(char d) => d == 'N' ? 0f : (d == 'S' ? 180f : (d == 'E' ? 90f : -90f));   // yaw that makes a "front = -z" prop face into the chamber

        // ============================================================ entry point
        [MenuItem("Ward0/Build Game Map")]
        public static void Build()
        {
            MatDir = MapRoot + "/Materials"; MeshDir = MapRoot + "/Meshes";
            foreach (var d in new[] { MapRoot, MatDir, MeshDir, MapRoot + "/Textures" }) Directory.CreateDirectory(d);
            AssetDatabase.Refresh();
            PropKit.EnsureMaterials();
            foreach (var g in AssetDatabase.FindAssets("Map_ t:Mesh", new[] { MeshDir })) AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));   // re-baked below (old single-mesh bake or an earlier zone bake)
            SavedMeshNames.Clear(); prefabs.Clear(); rng = new System.Random(20261109); Origin = Vector3.zero;
            MapMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.075f, 0.08f, 0.092f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = 0.022f; RenderSettings.fogColor = new Color(0.02f, 0.024f, 0.03f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.reflectionIntensity = 0.6f;

            worldT = new GameObject("World").transform; upperT = G("Upper Checkpoint", worldT, Vector3.zero).transform; coreMineT = G("Mine", worldT, Vector3.zero).transform; sysT = new GameObject("Systems").transform;
            zoneT.Clear(); zoneMap.Clear();
            foreach (var id in ZoneIds) { zoneT[id] = new GameObject("Zone_" + id).transform; zoneMap[id] = new Combiner(); }
            upperMap = new Combiner(); UseZone("N0");

            var mineMapObj = BuildMine(out var nodes, out var corridors, out var minePlace);
            map = upperMap; var upper = BuildUpper(out var upperRefs);
            BakeMap();
            BuildSystems(nodes, corridors, mineMapObj, upperRefs);

            EditorSceneManager.SaveScene(scene, ScenePath);   // an untitled scene cannot have scenes added next to it
            var zoneScenes = SplitZones(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            list.RemoveAll(bs => bs.path == ScenePath || bs.path.StartsWith(ZoneDir));
            list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            for (int i = 0; i < zoneScenes.Count; i++) list.Insert(1 + i, new EditorBuildSettingsScene(zoneScenes[i], true));
            EditorBuildSettings.scenes = list.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("[Ward0] Game map built: " + ScenePath + " + " + zoneScenes.Count + " mine zone scenes");
        }

        static void MapMaterials()
        {
            string R = "Assets/_Project/Art/Door/Textures/";
            var gun = AssetDatabase.LoadAssetAtPath<Texture2D>(R + "DoorMetal_Dark.png"); var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(R + "DoorMetal_Normal.png"); var mg = AssetDatabase.LoadAssetAtPath<Texture2D>(R + "DoorMetal_MetalGloss.png");
            var gl = M("Glass"); gl.SetColor("_BaseColor", new Color(0.8f, 0.9f, 0.95f, 0.05f)); gl.SetFloat("_Smoothness", 0.5f); EditorUtility.SetDirty(gl);
            Mk("BunkerWall", new Color(0.55f, 0.57f, 0.58f), 1f, 1f, gun, nrm, mg, 0.7f);
            Mk("BunkerFloor", new Color(0.38f, 0.4f, 0.42f), 1f, 1f, gun, nrm, mg, 0.7f);
            Mk("BunkerCeiling", new Color(0.3f, 0.31f, 0.33f), 1f, 1f, gun, nrm, mg, 0.6f);
            Mk("PanelLight", new Color(0.8f, 0.85f, 0.9f), 0f, 0.4f);
            Emissive("PanelGlow", new Color(0.7f, 0.8f, 0.85f), new Color(2.4f, 2.7f, 3.0f), null, 0.5f);
            Emissive("RedGlow", new Color(0.3f, 0.02f, 0.02f), new Color(3f, 0.1f, 0.05f), null, 0.5f);
            Emissive("IncGlow", new Color(0.3f, 0.1f, 0.02f), new Color(1.5f, 0.5f, 0.08f), null, 0.5f);
            // chalk marks
            for (int k = 0; k < 6; k++)
            {
                var t = TexKit.Chalk(MapRoot + "/Textures", "Chalk_" + k, k);
                var m = Mk("Chalk" + k, Color.white, 0f, 0.1f, t); m.SetFloat("_AlphaClip", 1f); m.SetFloat("_Cutoff", 0.35f); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; m.SetOverrideTag("RenderType", "TransparentCutout");
                m.SetColor("_EmissionColor", Color.black);
            }
        }

        // ============================================================ MINE
        static GameObject BuildMine(out Dictionary<string, MineNode> nodes, out List<MineCorridor> corridors, out Dictionary<string, Transform> places)
        {
            var nodesL = new Dictionary<string, MineNode>(); var corridorsL = new List<MineCorridor>(); var placesL = new Dictionary<string, Transform>();
            nodes = nodesL; corridors = corridorsL; places = placesL;
            var graph = G("MineGraph", coreMineT, Vector3.zero);
            void Node(string id, string label, Vector3 pos, bool landing = false)
            {
                var g = G("Node_" + id, graph.transform, pos); var n = g.AddComponent<MineNode>(); n.label = label; n.isLanding = landing; nodesL[id] = n;
            }
            Node("N0", "Landing", P0, true); Node("N1", "Breaker Hall A", P1); Node("N2", "Pump House", P2); Node("N3", "Central Hub", P3); Node("N4", "East Hall", P4); Node("N5", "Gas Chamber", P5);

            // chambers: flags N,S,E,W = has tunnel opening
            Chamber("N0", P0, 'N', "SEW"); Chamber("N1", P1, '-', "ENS"); Chamber("N2", P2, '-', "NEW"); Chamber("N3", P3, '-', "NWE"); Chamber("N4", P4, '-', "WSN"); Chamber("N5", P5, '-', "NWE");

            // corridors (centre-to-centre graph edges + visual segments)
            Corr("N1", "N0", Vector3.right, new Vector3(-22, 0, 0), 3, nodesL, corridorsL, graph.transform);
            Corr("N0", "N4", Vector3.right, new Vector3(4, 0, 0), 3, nodesL, corridorsL, graph.transform);
            Corr("N0", "N3", Vector3.back, new Vector3(0, 0, -4), 2, nodesL, corridorsL, graph.transform);
            Corr("N1", "N2", Vector3.back, new Vector3(-26, 0, -4), 2, nodesL, corridorsL, graph.transform);
            Corr("N4", "N5", Vector3.back, new Vector3(26, 0, -4), 2, nodesL, corridorsL, graph.transform);
            Corr("N2", "N3", Vector3.right, new Vector3(-22, 0, -20), 3, nodesL, corridorsL, graph.transform);
            Corr("N3", "N5", Vector3.right, new Vector3(4, 0, -20), 3, nodesL, corridorsL, graph.transform);

            // dead-end spurs (one segment each, sealed with a rock cap): A west of N2, B north of N1, C east of N5 (gas), D north of N4
            Spur(new Vector3(-30, 0, -20), Vector3.left, graph.transform, false);
            Spur(new Vector3(-26, 0, 4), Vector3.forward, graph.transform, false);
            Spur(new Vector3(30, 0, -20), Vector3.right, graph.transform, true);
            Spur(new Vector3(26, 0, 4), Vector3.forward, graph.transform, false);

            // rails through chambers
            foreach (var kv in new Dictionary<string, string> { { "N0", "SEW" }, { "N1", "ENS" }, { "N2", "NEW" }, { "N3", "NWE" }, { "N4", "WSN" }, { "N5", "NWE" } })
                foreach (char d in kv.Value) TrackStub(nodesL[kv.Key].transform.position, d);   // TrackStub picks the zone itself

            // ---------------- chamber contents
            var c0 = P0; var c1 = P1; var c2 = P2; var c3 = P3; var c4 = P4; var c5 = P5;
            // N0 landing: lower elevator, light, supplies
            UseZone("N0");
            var lowerCar = Place(P("ElevatorCar"), c0 + new Vector3(0, 0, 5.4f), 0f, mineT, "ElevatorCar_Lower");
            var lowerDoor = PlaceDoor(c0 + new Vector3(0, 0, 3.8f), 0f, mineT, "ElevatorDoor_Lower");
            SetRef(lowerCar.GetComponent<ElevatorCar>(), "door", lowerDoor.GetComponent<QuarantineDoor>());
            placesL["lowerCar"] = lowerCar.transform;
            AB("MineRock", c0 + new Vector3(-2.05f, 0, 4.0f), c0 + new Vector3(-1.75f, 4.7f, 7.6f)); AB("MineRock", c0 + new Vector3(1.75f, 0, 4.0f), c0 + new Vector3(2.05f, 4.7f, 7.6f));
            AB("MineRock", c0 + new Vector3(-2.05f, 0, 7.6f), c0 + new Vector3(2.05f, 4.7f, 7.9f)); AB("MineRock", c0 + new Vector3(-2.05f, 3.2f, 4f), c0 + new Vector3(2.05f, 4.7f, 7.6f));
            AB("MineFloor", c0 + new Vector3(-2.05f, -0.6f, 4f), c0 + new Vector3(2.05f, 0f, 7.6f));
            PendantLamp("LandingLamp", c0 + new Vector3(0, 3.55f, -0.5f), 4.5f, 11f, new Color(1f, 0.82f, 0.6f));
            Place(P("SupplyCluster"), c0 + new Vector3(-3.0f, 0, -2.9f), 25f, mineT);
            Torch(c0, 'N', -3.1f, 1.9f); Torch(c0, 'N', 3.1f, 1.9f);
            Chalk(c0, 'E', -3.1f, 1.7f, 3, 0.8f); Chalk(c0, 'W', 3.1f, 1.7f, 0, 0.9f);

            // N1 breaker hall A (repair #1)
            UseZone("N1");
            var vent1 = Place(P("SteamVent"), c1 + new Vector3(-1.4f, 0, -1.3f), 0f, mineT, "SteamVent_N1");
            var br1 = Place(P("BreakerPanel"), c1 + new Vector3(-3.3f, 0, 0), -90f, mineT, "BreakerPanel_A");
            Station(br1, nodesL["N1"], vent1);
            CartCover(c1, 'S', 2.6f, mineT, "N1_Cover"); Torch(c1, 'S', -3.2f, 1.9f); Torch(c1, 'N', 3.2f, 1.9f); Torch(c1, 'W', 3.0f, 2.0f); Torch(c1, 'W', -3.0f, 2.0f);
            Chalk(c1, 'N', -3.2f, 1.6f, 0, 1.0f); Chalk(c1, 'E', 3.0f, 1.7f, 1, 0.9f);
            Place(P("Stalactites"), c1 + new Vector3(0, 4.0f, 0), 0f, mineT);

            // N2 pump house (repair #2)
            UseZone("N2");
            var vent2 = Place(P("SteamVent"), c2 + new Vector3(1.6f, 0, -1.4f), 0f, mineT, "SteamVent_N2");
            var vs = Place(P("ValveStation"), c2 + new Vector3(0, 0, -3.0f), 180f, mineT, "ValveStation_Coolant");
            Station(vs, nodesL["N2"], vent2);
            CartCover(c2, 'S', -2.7f, mineT, "N2_Cover"); Torch(c2, 'S', -3.3f, 2.1f); Torch(c2, 'S', 3.3f, 2.1f);
            Place(P("Stalactites"), c2 + new Vector3(0, 4.0f, 0), 90f, mineT);
            Chalk(c2, 'N', 3.2f, 1.6f, 1, 1.1f); Chalk(c2, 'E', -3.2f, 1.7f, 4, 0.9f);

            // N3 central hub: pillars for cover, carts, signage
            UseZone("N3");
            AB("MineRock", c3 + new Vector3(1.6f, 0, 1.6f), c3 + new Vector3(2.6f, 4.0f, 2.6f), 0.5f, 0.12f); AB("MineRock", c3 + new Vector3(-2.6f, 0, -2.6f), c3 + new Vector3(-1.6f, 4.0f, -1.6f), 0.5f, 0.12f);
            CartCover(c3, 'S', 0f, mineT, "N3_Cover"); CartCover(c3, 'N', 2.9f, mineT, "N3_CoverB");
            Torch(c3, 'S', -3.3f, 1.9f); Torch(c3, 'S', 3.3f, 1.9f); Torch(c3, 'N', -3.3f, 1.9f);
            Chalk(c3, 'W', -3.2f, 1.6f, 2, 1.0f); Chalk(c3, 'N', 3.3f, 1.6f, 5, 1.0f); Chalk(c3, 'E', 3.2f, 1.7f, 1, 1.0f); Chalk(c3, 'S', 3.0f, 1.2f, 0, 0.8f);
            PendantLamp("HubLamp", c3 + new Vector3(0, 3.5f, 0), 2.6f, 9f, new Color(1f, 0.7f, 0.4f));

            // N4 east hall
            UseZone("N4");
            Place(P("SupplyCluster"), c4 + new Vector3(3.0f, 0, 0), -90f, mineT, "N4_Supplies");
            CartCover(c4, 'W', -3.1f, mineT, "N4_Cover"); Torch(c4, 'E', -3.2f, 1.9f); Torch(c4, 'E', 3.2f, 1.9f); Torch(c4, 'S', 3.3f, 1.9f);
            Place(P("Stalactites"), c4 + new Vector3(0, 4.0f, 0), 40f, mineT);
            Chalk(c4, 'S', -3.3f, 1.6f, 3, 1.0f); Chalk(c4, 'N', 3.2f, 1.7f, 0, 0.9f);

            // N5 gas chamber (repair #3) - gas + dark
            UseZone("N5");
            var vent5 = Place(P("SteamVent"), c5 + new Vector3(-1.3f, 0, -1.4f), 0f, mineT, "SteamVent_N5");
            var br5 = Place(P("BreakerPanel"), c5 + new Vector3(0f, 0, -3.3f), 180f, mineT, "BreakerPanel_B");
            Station(br5, nodesL["N5"], vent5);
            Place(P("Stalactites"), c5 + new Vector3(0, 4.0f, 0), 130f, mineT);
            Place(P("Minecart"), c5 + new Vector3(2.6f, 0, 2.2f), 70f, mineT, "N5_WreckedCart");
            Torch(c5, 'N', -3.3f, 1.9f); Chalk(c5, 'N', 3.2f, 1.7f, 2, 1.1f); Chalk(c5, 'W', -3.2f, 1.7f, 2, 1.0f); Chalk(c5, 'E', 3.2f, 1.7f, 2, 1.0f);
            var gas = G("GasZone_N5", mineT, c5 + new Vector3(3.5f, 1.6f, 0)); var gb = gas.AddComponent<BoxCollider>(); gb.isTrigger = true; gb.size = new Vector3(16f, 3.4f, 9f); gas.AddComponent<GasZone>();
            foreach (var o in new[] { new Vector3(-1.8f, 0, 1.2f), new Vector3(1.8f, 0, -1f), new Vector3(5.5f, 0, 0.5f), new Vector3(9f, 0, 0f), new Vector3(2.5f, 0, 3f) }) Place(P("GasCloud"), c5 + o, 0f, mineT);

            // spur rewards (batteries) and hiding cover in the spurs
            Place(P("Battery"), new Vector3(-26.8f, 0, 9.0f), 0f, mineT, "Battery_B"); Place(P("SupplyCluster"), new Vector3(-24.9f, 0, 8.8f), 160f, mineT);
            Place(P("Battery"), new Vector3(-34.6f, 0, -19.4f), 0f, mineT, "Battery_A"); Place(P("Minecart"), new Vector3(-33f, 0, -20f), 90f, mineT, "SpurA_Cart");
            Place(P("Battery"), new Vector3(34.6f, 0, -19.4f), 0f, mineT, "Battery_C");
            Place(P("Battery"), new Vector3(26.9f, 0, 9.1f), 0f, mineT, "Battery_D"); Place(P("SupplyCluster"), new Vector3(24.9f, 0, 8.6f), 20f, mineT);
            Chalk(new Vector3(-26, 0, 7), 'N', 0f, 1.6f, 2, 1.4f); Chalk(new Vector3(-33, 0, -20), 'W', 0f, 1.6f, 2, 1.4f);

            foreach (var zid in ZoneIds)
                foreach (var l in zoneT[zid].GetComponentsInChildren<Light>(true))
                {
                    if (l.name == "Light") { l.intensity *= 2.6f; l.range *= 1.3f; }            // wall torches
                    else if (l.name == "LampLight") { l.intensity *= 2.2f; l.range *= 1.15f; }  // tunnel lamps
                    else if (l.name == "GasGlow") l.intensity *= 1.8f;
                    else if (l.name == "Glow") l.intensity *= 2f;                                // batteries
                }
            // corridor markers for the swap system
            var mapObj = graph;
            return mapObj;
        }

        static void StationLight(GameObject station, Color col)
        {
            UseZone(NodeOf(station.transform.position));
            var g = G("StationLight", mineT, station.transform.position + new Vector3(0, 2.6f, 0) + (station.transform.rotation * new Vector3(0, 0, -1.6f)));
            var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.intensity = 7f; l.range = 6.5f; l.shadows = LightShadows.None; g.AddComponent<FlickerLight>();
        }

        static void Station(GameObject station, MineNode node, GameObject vent)
        {
            StationLight(station, node.label.Contains("Gas") ? new Color(0.75f, 1f, 0.6f) : new Color(1f, 0.85f, 0.6f));
            var rs = station.GetComponent<RepairStation>(); SetRef(rs, "steam", vent.GetComponent<SteamBurst>());   // its node lives in the core scene and is resolved at runtime (a zone scene cannot reference another scene)
        }

        static GameObject PlaceDoor(Vector3 pos, float yaw, Transform parent, string name)
        {
            var d = Place("Assets/_Project/Art/Door/QuarantineDoor.prefab", pos, yaw, parent, name); d.AddComponent<DoorInteractable>(); return d;
        }

        static void PendantLamp(string name, Vector3 pos, float intensity, float range, Color col)
        {
            UseZone(NodeOf(pos));
            var g = G(name, mineT, pos); var l = g.AddComponent<Light>(); l.type = LightType.Point; l.color = col; l.intensity = intensity; l.range = range; l.shadows = LightShadows.Soft; g.AddComponent<FlickerLight>();
            map.Add(MeshKit.Cylinder(0.03f, 0.6f, 8, 0.002f), M("Rubber"), pos + new Vector3(0, 0.55f, 0));
            map.Add(MeshKit.Sphere(0.07f, 14, 10, 1.1f), M("LampBulb"), pos);
            for (int i = 0; i < 8; i++) { float a = i / 8f * Mathf.PI * 2f; map.Add(MeshKit.Cylinder(0.004f, 0.2f, 6, 0.0005f), M("RustDark"), pos + new Vector3(Mathf.Cos(a) * 0.1f, 0, Mathf.Sin(a) * 0.1f)); }
        }

        static void Torch(Vector3 c, char wall, float u, float y)
        {
            var pos = c + Dir(wall) * (Half - 0.5f) + Perp(wall) * u + Vector3.up * y; Place(P("WallTorch"), pos, InwardYaw(wall), mineT);
        }

        static void Chalk(Vector3 c, char wall, float u, float y, int kind, float size)
        {
            UseZone(NodeOf(c));
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(q.GetComponent<Collider>());
            q.name = "Chalk_" + kind; q.transform.SetParent(mineT, false);
            q.transform.position = c + Dir(wall) * (Half - 0.5f - 0.01f) + Perp(wall) * u + Vector3.up * y; q.transform.rotation = Quaternion.Euler(0, InwardYaw(wall), Random.Range(-8f, 8f)); q.transform.localScale = Vector3.one * size;
            q.GetComponent<MeshRenderer>().sharedMaterial = M("Chalk" + kind); q.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        /// <summary>Minecart parked a little away from a wall with a hide zone in the gap behind it.</summary>
        static void CartCover(Vector3 c, char wall, float u, Transform parent, string name)
        {
            UseZone(NodeOf(c)); parent = mineT;
            var d = Dir(wall); var p = Perp(wall);
            float yaw = (wall == 'N' || wall == 'S') ? 90f : 0f;
            Place(P("Minecart"), c + d * 2.15f + p * u, yaw, parent, name);
            var z = G(name + "_Hide", parent, c + d * 3.0f + p * u + Vector3.up * 0.7f); var bc = z.AddComponent<BoxCollider>(); bc.isTrigger = true;
            bc.size = (wall == 'N' || wall == 'S') ? new Vector3(1.9f, 1.4f, 0.8f) : new Vector3(0.8f, 1.4f, 1.9f); z.AddComponent<HideZone>();
        }

        static void TrackStub(Vector3 c, char dir)
        {
            UseZone(NodeOf(c));
            var d = Dir(dir); var p = Perp(dir); float yaw = (dir == 'N' || dir == 'S') ? 0f : 90f;
            for (int i = 0; i < 7; i++)
            {
                var pos = c + d * (0.3f + i * 0.6f); BR("MineWood", pos + Vector3.up * 0.05f, new Vector3(1.25f, 0.1f, 0.17f), new Vector3(0, yaw + (float)(rng.NextDouble() * 4 - 2), 0), 1f, 0.012f);
            }
            foreach (float s in new[] { -1f, 1f })
            {
                var mid = c + d * 2.0f + p * (s * 0.40f);
                var size = (dir == 'N' || dir == 'S') ? new Vector3(0.068f, 0.04f, 4f) : new Vector3(4f, 0.04f, 0.068f);
                map.Add(MeshKit.Box(size, 0.006f, 1f), M("RustDark"), mid + Vector3.up * 0.215f);
                var sizeW = (dir == 'N' || dir == 'S') ? new Vector3(0.026f, 0.085f, 4f) : new Vector3(4f, 0.085f, 0.026f); map.Add(MeshKit.Box(sizeW, 0.003f, 1f), M("RustDark"), mid + Vector3.up * 0.155f);
                var sizeB = (dir == 'N' || dir == 'S') ? new Vector3(0.115f, 0.022f, 4f) : new Vector3(4f, 0.022f, 0.115f); map.Add(MeshKit.Box(sizeB, 0.004f, 1f), M("RustDark"), mid + Vector3.up * 0.112f);
            }
        }

        // ---------------------------------------------------------- chambers
        static void Chamber(string id, Vector3 c, char special, string openings)
        {
            UseZone(id);
            float e = Half + 0.5f;
            AB("MineFloor", c + new Vector3(-e, -0.6f, -e), c + new Vector3(e, 0f, e), 0.5f, 0.03f);
            AB("MineRock", c + new Vector3(-e, 4.0f, -e), c + new Vector3(e, 4.8f, e), 0.5f, 0.05f);
            foreach (char d in "NSEW")
            {
                bool open = openings.IndexOf(d) >= 0; bool door = (id == "N0" && d == 'N');
                Wall(c, d, door ? 2 : (open ? 1 : 0));
            }
            // rubble along the floor edges
            for (int i = 0; i < 16; i++)
            {
                float s = 0.25f + (float)rng.NextDouble() * 0.6f; var d = Dir("NSEW"[rng.Next(4)]); var p = Perp('N');
                var pos = c + d * (Half - 1.0f + (float)rng.NextDouble() * 0.6f) + Perp("NSEW"[rng.Next(4)]) * ((float)rng.NextDouble() * 6f - 3f);
                if (openings.Length > 0 && Mathf.Abs(pos.x - c.x) < 2.4f && Mathf.Abs(pos.z - c.z) < 2.4f) continue;
                BR("MineFloor", pos + Vector3.up * s * 0.25f, new Vector3(s, s * 0.7f, s * 0.9f), new Vector3((float)rng.NextDouble() * 20f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 20f), 0.5f, s * 0.28f);
            }
        }

        /// <summary>kind: 0 solid, 1 arch opening, 2 rectangular door opening</summary>
        static void Wall(Vector3 c, char dir, int kind)
        {
            // build in a local frame where the wall runs along +x and faces +z, then rotate: use explicit axes
            var d = Dir(dir); var p = Perp(dir);
            Vector3 P3(float u, float y, float t) => c + d * t + p * u + Vector3.up * y;   // t = distance from chamber centre toward the wall
            void Slab(float u0, float u1, float y0, float y1, float t0 = 3.5f, float t1 = 4.0f)
            {
                var a = P3(u0, y0, t0); var b = P3(u1, y1, t1); AB("MineRock", Vector3.Min(a, b), Vector3.Max(a, b), 0.5f, 0.03f);
            }
            float E = Half + 0.5f;
            if (kind == 0) { Slab(-E, E, -0.6f, 4.8f, 3.5f, 4.5f); return; }
            float r = kind == 1 ? OpenR : 1.25f;
            Slab(-E, -r, -0.6f, 4.8f, 3.5f, 4.5f); Slab(r, E, -0.6f, 4.8f, 3.5f, 4.5f);
            if (kind == 2) { Slab(-r, r, 2.58f, 4.8f, 3.5f, 4.5f); return; }
            // arch: vertical slices above the opening curve
            float step = 0.2f;
            for (float u = -r; u < r - 0.001f; u += step)
            {
                float uc = u + step / 2f; float top = CY + Mathf.Sqrt(Mathf.Max(0.0f, r * r - uc * uc)) + 0.06f + (float)rng.NextDouble() * 0.1f;
                float jitter = (float)rng.NextDouble() * 0.06f; Slab(u, u + step + 0.01f, top, 4.8f, 3.5f + jitter, 4.0f);
            }
        }

        // ---------------------------------------------------------- corridors
        static void Corr(string a, string b, Vector3 dir, Vector3 startEdge, int segs, Dictionary<string, MineNode> nodes, List<MineCorridor> list, Transform graph)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            for (int i = 0; i < segs; i++)
            {
                var seg = Place("Assets/_Project/Art/Tunnel/MineTunnel.prefab", startEdge + dir * (3f + 6f * i), yaw, mineT, "Tunnel_" + a + "_" + b + "_" + i);
                var ms = seg.GetComponent<MineTunnelSegment>();
                bool off = rng.NextDouble() < 0.28; SetBool(ms, "lampStartsOn", !off);
            }
            var mid = (nodes[a].transform.position + nodes[b].transform.position) * 0.5f;
            var cg = G("Corridor_" + a + "_" + b, graph, mid); var cor = cg.AddComponent<MineCorridor>(); cor.a = nodes[a]; cor.b = nodes[b];
            var gate = Place(P("CaveIn"), mid, Mathf.Abs(dir.x) > 0.5f ? 90f : 0f, cg.transform, "CaveInGate"); gate.SetActive(false); cor.gate = gate; list.Add(cor);
        }

        static void Spur(Vector3 edge, Vector3 dir, Transform graph, bool gas)
        {
            float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            var seg = Place("Assets/_Project/Art/Tunnel/MineTunnel.prefab", edge + dir * 3f, yaw, mineT, "Spur_" + edge.x + "_" + edge.z);
            SetBool(seg.GetComponent<MineTunnelSegment>(), "lampStartsOn", false);
            // rock cap sealing the dead end
            var end = edge + dir * 6.05f; var p = Perp(dir.z != 0 ? 'N' : 'E');
            UseZone(NodeOf(end));
            Vector3 a = end - p * 3.2f, b = end + p * 3.2f + dir * 0.7f + Vector3.up * 4.8f; a.y = -0.6f;
            AB("MineRock", Vector3.Min(a, b), Vector3.Max(a, b), 0.5f, 0.05f);
        }

        // ============================================================ UPPER CHECKPOINT
        class UpperRefs { public GameObject desk, upperCar, upperDoor, minerSpawn, glass, fire, playerStart; public Light fireLight; public List<Light> alarm = new List<Light>(); public Light[] booth; }

        static void WallBox(string mat, float x0, float z0, float x1, float z1, float y0 = 0f, float y1 = 3.6f) => AB(mat, new Vector3(x0, Y0 + y0, z0), new Vector3(x1, Y0 + y1, z1), 0.5f, 0.02f);

        static GameObject BuildUpper(out UpperRefs R)
        {
            R = new UpperRefs();
            float H = 3.6f;
            // floors and ceilings
            WallBox("BunkerFloor", -8.9f, -5.5f, 12.9f, 3.5f, -0.4f, 0f); WallBox("BunkerFloor", 7.9f, 5.0f, 10.1f, 10.2f, -0.4f, 0f);
            WallBox("BunkerCeiling", -8.9f, -5.5f, 12.9f, 3.5f, H, H + 0.4f); WallBox("BunkerCeiling", 7.9f, 5.0f, 10.1f, 10.2f, H, H + 0.4f);
            // ---- booth walls
            WallBox("BunkerWall", -4.4f, 3.0f, 4.4f, 3.5f);                                          // north
            WallBox("BunkerWall", -4.4f, -5.5f, 4.4f, -5.0f);                                        // south
            WallBox("BunkerWall", -4.4f, -5.5f, -4.0f, -3.25f); WallBox("BunkerWall", -4.4f, -0.75f, -4.0f, 3.5f); WallBox("BunkerWall", -4.4f, -3.25f, -4.0f, -0.75f, 2.58f, H);   // west with elevator doorway
            WallBox("BunkerWall", 4.0f, -5.5f, 4.4f, -4.85f); WallBox("BunkerWall", 4.0f, -4.85f, 4.4f, -2.35f, 2.58f, H); WallBox("BunkerWall", 4.0f, -2.35f, 4.4f, -1.6f);   // east: airlock door
            WallBox("BunkerWall", 4.0f, -1.6f, 4.4f, 2.4f, 0f, 0.95f); WallBox("BunkerWall", 4.0f, -1.6f, 4.4f, 2.4f, 2.6f, H); WallBox("BunkerWall", 4.0f, 2.4f, 4.4f, 3.5f);              // east: window
            // window frame
            AB("RustDark", new Vector3(3.9f, Y0 + 0.9f, -1.65f), new Vector3(4.5f, Y0 + 0.98f, 2.45f)); AB("RustDark", new Vector3(3.9f, Y0 + 2.58f, -1.65f), new Vector3(4.5f, Y0 + 2.66f, 2.45f));
            for (int i = 0; i < 5; i++) AB("RustDark", new Vector3(3.9f, Y0 + 0.95f, -1.65f + i * 1.025f), new Vector3(4.5f, Y0 + 2.6f, -1.58f + i * 1.025f));
            // ---- airlock (x 4.4..7.4, z -5.5..-2.4)
            WallBox("BunkerWall", 4.4f, -5.5f, 7.9f, -5.0f); WallBox("BunkerWall", 7.4f, -5.5f, 7.9f, -2.4f);
            WallBox("BunkerWall", 4.4f, -2.8f, 4.65f, -2.4f); WallBox("BunkerWall", 7.15f, -2.8f, 7.9f, -2.4f); WallBox("BunkerWall", 4.4f, -2.8f, 7.9f, -2.4f, 2.58f, H);
            // ---- cell (x 4.4..12.9, z -2.4..5.0)
            WallBox("BunkerWall", 12.5f, -2.4f, 12.9f, 5.5f);
            WallBox("BunkerWall", 7.9f, 5.0f, 8.0f, 5.5f); WallBox("BunkerWall", 4.4f, 5.0f, 8.0f, 5.5f); WallBox("BunkerWall", 10.0f, 5.0f, 12.9f, 5.5f); WallBox("BunkerWall", 8.0f, 5.0f, 10.0f, 5.5f, 2.9f, H);
            // exit hall to the rear (miner leaves through here)
            WallBox("BunkerWall", 7.9f, 5.5f, 8.1f, 10.4f); WallBox("BunkerWall", 9.9f, 5.5f, 10.1f, 10.4f); WallBox("BunkerWall", 7.9f, 10.2f, 10.1f, 10.4f);
            // ---- elevator shaft (car at x -7.4..-4.2)
            WallBox("BunkerWall", -8.9f, -4.3f, -4.4f, -3.9f); WallBox("BunkerWall", -8.9f, -0.1f, -4.4f, 0.3f); WallBox("BunkerWall", -8.9f, -4.3f, -8.5f, 0.3f);
            WallBox("BunkerCeiling", -8.9f, -4.3f, -4.4f, 0.3f, H, H + 0.4f);

            // lights: ceiling panels in the booth, airlock, cell
            var lights = new List<Light>();
            void Panel(Vector3 p, float intensity, float range, Color col, bool shadow)
            {
                map.Add(MeshKit.Box(new Vector3(1.4f, 0.06f, 0.5f), 0.01f, 1f), M("PanelLight"), new Vector3(p.x, Y0 + H - 0.03f, p.z));
                map.Add(MeshKit.Quad(1.3f, 0.4f), M("PanelGlow"), new Vector3(p.x, Y0 + H - 0.065f, p.z), Quaternion.Euler(90, 0, 0));
                var l = PointLight("PanelLight", upperT, new Vector3(p.x, Y0 + H - 0.5f, p.z), col, intensity, range, shadow); l.gameObject.AddComponent<FlickerLight>(); lights.Add(l);
            }
            Panel(new Vector3(-1.5f, 0, -1.0f), 30f, 10f, new Color(0.82f, 0.92f, 1f), true); Panel(new Vector3(1.8f, 0, 1.2f), 26f, 10f, new Color(0.82f, 0.92f, 1f), true);
            Panel(new Vector3(-1.2f, 0, -3.8f), 20f, 9f, new Color(0.9f, 0.92f, 1f), false);
            Panel(new Vector3(5.9f, 0, -3.9f), 18f, 7f, new Color(0.85f, 0.95f, 1f), false);
            Panel(new Vector3(8.5f, 0, 0.5f), 30f, 12f, new Color(0.9f, 1f, 0.95f), true); Panel(new Vector3(6.2f, 0, 3.2f), 16f, 9f, new Color(0.9f, 1f, 0.95f), false);
            R.booth = lights.ToArray();
            for (int i = 0; i < 3; i++) { var al = PointLight("Alarm" + i, upperT, new Vector3(-3f + i * 3f, Y0 + 3.2f, i == 1 ? 1.5f : -3f), new Color(1f, 0.08f, 0.04f), 0f, 9f, false); al.enabled = false; R.alarm.Add(al); }

            // ceiling pipes + conduits, hazard stripes, signage
            for (int i = 0; i < 3; i++) map.Add(MeshKit.Cylinder(0.09f, 8.4f, 14, 0.004f), M("RustSteel"), new Vector3(0, Y0 + H - 0.25f - i * 0.12f, -4.3f + i * 0.35f), Quaternion.Euler(0, 0, 90));
            map.Add(MeshKit.Cylinder(0.12f, 8.0f, 14, 0.004f), M("RustDark"), new Vector3(3.6f, Y0 + H - 0.3f, -1f), Quaternion.Euler(90, 0, 0));
            foreach (float z in new[] { -4.9f, 2.9f }) for (int i = 0; i < 8; i++) AB("RustDark", new Vector3(-3.9f + i * 1.1f, Y0 + 0.0f, z), new Vector3(-3.7f + i * 1.1f, Y0 + 0.5f, z + (z < 0 ? 0.15f : -0.15f)));
            var hz = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(hz.GetComponent<Collider>()); hz.transform.SetParent(upperT, false); hz.name = "HazardBand";
            hz.transform.position = new Vector3(-4.0f + 0.01f, Y0 + 1.1f, 1.65f); hz.transform.rotation = Quaternion.Euler(0, -90, 0); hz.transform.localScale = new Vector3(2.4f, 0.16f, 1f); hz.GetComponent<MeshRenderer>().sharedMaterial = M("DStripes");
            Text("QUARANTINE CHECKPOINT 7", new Vector3(0, Y0 + 3.15f, 2.97f), new Vector3(0, 0, 0), 0.16f, upperT, 7f, TextAlignmentOptions.Center, Cream, 6f);
            Text("TECHNICIAN STATION  //  SHIFT 07", new Vector3(0, Y0 + 2.9f, 2.97f), new Vector3(0, 0, 0), 0.07f, upperT, 7f, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.3f), 5f);
            Text("AIRLOCK", new Vector3(5.9f, Y0 + 3.05f, -4.99f), new Vector3(0, 180, 0), 0.14f, upperT, 3f, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.3f), 6f);
            Text("CELL A  //  CONTAINMENT", new Vector3(3.96f, Y0 + 3.0f, 0.4f), new Vector3(0, 90, 0), 0.09f, upperT, 4f, TextAlignmentOptions.Center, Cream, 4f);
            Text("ELEVATOR  >>  MINE LEVEL 7", new Vector3(-3.95f, Y0 + 3.1f, -2f), new Vector3(0, -90, 0), 0.08f, upperT, 3.2f, TextAlignmentOptions.Center, new Color(0.9f, 0.75f, 0.3f), 4f);
            Text("SCAN  >  ANALYSE\nCLEAR OR INCINERATE", new Vector3(0.0f, Y0 + 2.2f, 2.97f), new Vector3(0, 0, 0), 0.075f, upperT, 4f, TextAlignmentOptions.Center, Cream, 3f);

            // glass between booth and cell
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(glass.GetComponent<Collider>()); glass.name = "ObservationGlass"; glass.transform.SetParent(upperT, false);
            glass.transform.position = new Vector3(4.2f, Y0 + 1.78f, 0.4f); glass.transform.rotation = Quaternion.Euler(0, 90, 0); glass.transform.localScale = new Vector3(4.1f, 1.65f, 1f);
            glass.GetComponent<MeshRenderer>().sharedMaterial = M("Glass"); glass.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            var gcol = glass.AddComponent<BoxCollider>(); gcol.size = new Vector3(1f, 1f, 0.05f);
            R.glass = glass;

            // incinerator pit + red grate in the cell
            map.Add(MeshKit.Cylinder(0.9f, 0.05f, 28, 0.004f), M("RustDark"), new Vector3(8.5f, Y0 + 0.02f, 1.2f));
            map.Add(MeshKit.Cylinder(0.78f, 0.02f, 28, 0.002f), M("IncGlow"), new Vector3(8.5f, Y0 + 0.055f, 1.2f));
            for (int i = -3; i <= 3; i++) map.Add(MeshKit.Box(new Vector3(1.6f, 0.03f, 0.04f), 0.004f, 1f), M("RustDark"), new Vector3(8.5f, Y0 + 0.08f, 1.2f + i * 0.2f));
            var fireObj = G("IncineratorFire", upperT, new Vector3(8.5f, Y0 + 0.1f, 1.2f));
            var fire = PropKit.MakePS(fireObj.transform, "Flames", Vector3.zero, "FireParticle", 1.1f, 0.9f, 1.4f, 35f, new Color(1f, 0.55f, 0.15f, 0.9f), 120, ParticleSystemShapeType.Cone, 0.5f, -0.1f, true, 0.3f, false);
            var fireL = PointLight("IncineratorLight", fireObj.transform, new Vector3(0, 1f, 0), new Color(1f, 0.5f, 0.15f), 10f, 9f, false); fireL.enabled = false;
            R.fire = fireObj; R.fireLight = fireL;
            // cell dressing: drain, benches, restraints, biohazard decals
            AB("RustDark", new Vector3(11.7f, Y0, -2.0f), new Vector3(12.4f, Y0 + 0.5f, 4.6f)); AB("RustSteel", new Vector3(11.65f, Y0 + 0.5f, -2.0f), new Vector3(12.45f, Y0 + 0.56f, 4.6f));
            var bio = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.DestroyImmediate(bio.GetComponent<Collider>()); bio.transform.SetParent(upperT, false); bio.name = "BiohazardWall";
            bio.transform.position = new Vector3(12.48f, Y0 + 2.2f, 3.9f);   // off the sightline from the observation glass to the miner
            bio.transform.rotation = Quaternion.Euler(0, 90, 0); bio.transform.localScale = Vector3.one * 1.2f; bio.GetComponent<MeshRenderer>().sharedMaterial = M("BioYellow");

            // miner spawn (faces the glass)
            R.minerSpawn = G("MinerSpawn", upperT, new Vector3(8.5f, Y0 + 0.02f, 1.2f), new Vector3(0, -90, 0));

            // ---- desk
            var desk = Place("Assets/_Project/Art/Desk/QuarantineDesk.prefab", new Vector3(0.0f, Y0, 1.7f), 0f, upperT, "QuarantineDesk"); R.desk = desk;

            // ---- instrument table beside the desk: the technician's two hand tools (concept art: UV/Thermal Bio-Scanner, Pneumatic Blood Extractor)
            const float tx = -2.35f, tz = 1.5f, th = 0.82f;
            AB("DDark", new Vector3(tx - 0.62f, Y0 + th - 0.04f, tz - 0.32f), new Vector3(tx + 0.62f, Y0 + th, tz + 0.32f), 0.5f, 0.012f);
            AB("RustSteel", new Vector3(tx - 0.62f, Y0 + th, tz - 0.32f), new Vector3(tx + 0.62f, Y0 + th + 0.006f, tz + 0.32f), 0.5f, 0.002f);
            AB("RustDark", new Vector3(tx - 0.58f, Y0 + 0.3f, tz - 0.28f), new Vector3(tx + 0.58f, Y0 + 0.33f, tz + 0.28f), 0.5f, 0.01f);
            foreach (float sx in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                AB("RustDark", new Vector3(tx + sx * 0.57f - 0.025f, Y0, tz + sz * 0.27f - 0.025f), new Vector3(tx + sx * 0.57f + 0.025f, Y0 + th - 0.04f, tz + sz * 0.27f + 0.025f), 0.5f, 0.006f);
            Place(P("BioScanner"), new Vector3(tx - 0.3f, Y0 + th + 0.006f, tz - 0.02f), 14f, upperT, "BioScanner");
            Place(P("BloodExtractor"), new Vector3(tx + 0.28f, Y0 + th + 0.006f, tz - 0.02f), -10f, upperT, "BloodExtractor");
            PointLight("ToolLamp", upperT, new Vector3(tx, Y0 + 1.55f, tz - 0.35f), new Color(1f, 0.86f, 0.62f), 1.4f, 3.2f, false);

            // ---- doors: airlock (booth side), airlock inner (to cell), elevator
            var d1 = PlaceDoor(new Vector3(4.2f, Y0, -3.6f), 90f, upperT, "AirlockDoor_Outer");
            var d2 = PlaceDoor(new Vector3(5.9f, Y0, -2.6f), 0f, upperT, "AirlockDoor_Inner");
            var upperDoor = PlaceDoor(new Vector3(-4.2f, Y0, -2.0f), -90f, upperT, "ElevatorDoor_Upper");
            var car = Place(P("ElevatorCar"), new Vector3(-5.8f, Y0, -2.0f), -90f, upperT, "ElevatorCar_Upper"); R.upperCar = car; R.upperDoor = upperDoor;
            SetBool(car.GetComponent<ElevatorCar>(), "isUpper", true); SetRef(car.GetComponent<ElevatorCar>(), "door", upperDoor.GetComponent<QuarantineDoor>());
            // doors start closed; elevator door closed until it is time

            R.playerStart = G("PlayerStart", upperT, new Vector3(0f, Y0 + 0.05f, -0.4f));
            return upperT.gameObject;
        }

        // ============================================================ combine static geometry
        static void BakeMap()
        {
            BakeInto(upperMap, upperT, "Upper");
            foreach (var id in ZoneIds) BakeInto(zoneMap[id], zoneT[id], id);
        }

        /// <summary>Bakes one area into one mesh per material under its own root, so each area culls (and streams) on its own.</summary>
        static void BakeInto(Combiner area, Transform parent, string tag)
        {
            foreach (var kv in area.Bake("Map_" + tag))
            {
                string path = SaveMesh(kv.Value, "Map_" + tag + "_" + kv.Key.name);
                var g = G("Geo_" + kv.Key.name, parent, Vector3.zero);
                var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                g.AddComponent<MeshFilter>().sharedMesh = mesh; g.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
                if (kv.Key.name != "PanelGlow" && kv.Key.name != "LampBulb" && kv.Key.name != "RedGlow" && kv.Key.name != "IncGlow" && kv.Key.name != "Chalk0") g.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }

        /// <summary>Moves each zone root into its own scene file and returns the saved scene paths (in zone order).</summary>
        static List<string> SplitZones(Scene core)
        {
            Directory.CreateDirectory(ZoneDir);
            foreach (var f in Directory.GetFiles(ZoneDir, "Ward0_Mine_*.unity")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            var paths = new List<string>();
            foreach (var id in ZoneIds)
            {
                string path = ZoneDir + "/Ward0_Mine_" + id + ".unity";
                var zs = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.MoveGameObjectToScene(zoneT[id].gameObject, zs);
                EditorSceneManager.SaveScene(zs, path);
                EditorSceneManager.CloseScene(zs, true);
                paths.Add(path);
            }
            SceneManager.SetActiveScene(core);
            return paths;
        }

        // ============================================================ systems: player, managers, post-processing
        static void BuildSystems(Dictionary<string, MineNode> nodes, List<MineCorridor> corridors, GameObject mineGraph, UpperRefs R)
        {
            // ---- player
            var player = new GameObject("Player"); player.transform.position = R.playerStart.transform.position; player.transform.rotation = Quaternion.identity;
            player.layer = 2;   // Ignore Raycast: interaction rays pass through the player, enemy line-of-sight checks (all layers) still see it
            var cc = player.AddComponent<CharacterController>(); cc.height = 1.75f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.875f, 0); cc.stepOffset = 0.35f; cc.skinWidth = 0.04f;
            var camRoot = G("CameraRoot", player.transform, new Vector3(0, 1.6f, 0));
            var cam = camRoot.AddComponent<Camera>(); cam.fieldOfView = 72f; cam.nearClipPlane = 0.04f; cam.farClipPlane = 120f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black; camRoot.tag = "MainCamera";
            camRoot.AddComponent<AudioListener>(); var ac = camRoot.AddComponent<UniversalAdditionalCameraData>(); ac.renderPostProcessing = true; ac.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; ac.antialiasingQuality = AntialiasingQuality.Medium;
            var pc = player.AddComponent<PlayerController>(); SetRef(pc, "cameraRoot", camRoot.transform);
            var inter = camRoot.AddComponent<Interactor>(); SetRef(inter, "player", pc);
            var torch = G("Torch", camRoot.transform, new Vector3(0.12f, -0.1f, 0.15f)); var sl = torch.AddComponent<Light>(); sl.type = LightType.Spot; sl.spotAngle = 62f; sl.innerSpotAngle = 28f; sl.range = 22f; sl.intensity = 26f; sl.color = new Color(1f, 0.93f, 0.8f); sl.shadows = LightShadows.Soft; sl.enabled = false;
            var fl = camRoot.AddComponent<FlashlightController>(); SetRef(fl, "beam", sl);
            var lm = player.AddComponent<LightMeter>();
            var drone = G("Ambience", player.transform, Vector3.zero).AddComponent<AudioSource>(); drone.clip = ProceduralAudio.Drone(); drone.loop = true; drone.volume = 0.3f; drone.spatialBlend = 0f; drone.playOnAwake = true;

            // ---- post processing
            var vp = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = vp.Add<Bloom>(true); bloom.intensity.Override(0.6f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.65f); bloom.highQualityFiltering.Override(false);
            var tone = vp.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var vig = vp.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.55f);
            var ca = vp.Add<ChromaticAberration>(true); ca.intensity.Override(0f);
            var grain = vp.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.2f); grain.response.Override(0.8f);
            var col = vp.Add<ColorAdjustments>(true); col.postExposure.Override(0.7f); col.contrast.Override(12f); col.saturation.Override(0f);
            var lens = vp.Add<LensDistortion>(true); lens.intensity.Override(0f); lens.active = false;
            string profPath = MapRoot + "/GameVolume.asset"; AssetDatabase.DeleteAsset(profPath); AssetDatabase.CreateAsset(vp, profPath);
            foreach (var comp in vp.components) AssetDatabase.AddObjectToAsset(comp, vp);
            EditorUtility.SetDirty(vp); AssetDatabase.SaveAssets();
            var vg = G("PostFX", sysT, Vector3.zero); var vol = vg.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = vp;

            // ---- managers
            var sanity = new GameObject("Sanity").AddComponent<SanitySystem>(); sanity.transform.SetParent(sysT); SetRef(sanity, "volume", vol);
            var mapComp = mineGraph.AddComponent<MineMap>();
            var nodeArr = new List<Object>(); foreach (var n in nodes.Values) nodeArr.Add(n); SetArr(mapComp, "nodes", nodeArr);
            var corrArr = new List<Object>(); foreach (var c in corridors) corrArr.Add(c); SetArr(mapComp, "corridors", corrArr); SetRef(mapComp, "landing", nodes["N0"]);

            var elevator = new GameObject("Elevator System").AddComponent<ElevatorSystem>(); elevator.transform.SetParent(sysT);
            SetRef(elevator, "upperCar", R.upperCar.GetComponent<ElevatorCar>());
            // the lower car lives in the N0 zone scene and registers itself with the ElevatorSystem when that zone loads

            var hudObj = new GameObject("HUD").AddComponent<HudOverlay>(); hudObj.transform.SetParent(sysT);
            SetRef(hudObj, "interactor", inter); SetRef(hudObj, "player", pc); SetRef(hudObj, "flashlight", fl);

            var dir = new GameObject("Enemy Director").AddComponent<EnemyDirector>(); dir.transform.SetParent(sysT);
            var enemyPf = AssetDatabase.LoadAssetAtPath<GameObject>(P("InfectedPatrol")).GetComponent<PatrolEnemy>();
            SetRef(dir, "enemyPrefab", enemyPf); SetRef(dir, "map", mapComp); SetRef(dir, "player", pc);

            // desk controller
            var deskObj = R.desk; var dc = deskObj.AddComponent<DeskController>(); var qd = deskObj.GetComponent<QuarantineDesk>();
            SetRef(dc, "desk", qd);
            string TD = "Assets/_Project/Art/Desk/Textures/";
            SetRef(dc, "xrayHealthy", AssetDatabase.LoadAssetAtPath<Texture2D>(TD + "Screen_XRay.png")); SetRef(dc, "xrayInfected", AssetDatabase.LoadAssetAtPath<Texture2D>(TD + "Screen_XRay_Anomaly.png"));
            SetRef(dc, "bioHealthy", AssetDatabase.LoadAssetAtPath<Texture2D>(TD + "Screen_Bioscan.png")); SetRef(dc, "bioInfected", AssetDatabase.LoadAssetAtPath<Texture2D>(TD + "Screen_Bioscan_Anomaly.png"));
            SetRef(dc, "needleRadiation", Find(deskObj.transform, "Needle_RADIATION")); SetRef(dc, "needleAir", Find(deskObj.transform, "Needle_AIR_QUALITY")); SetRef(dc, "needleContainment", Find(deskObj.transform, "Needle_CONTAINMENT"));
            var roles = new Dictionary<string, DeskRole> { { "Toggle_X-RAY", DeskRole.XRay }, { "Toggle_BIOSCAN", DeskRole.Bioscan }, { "Toggle_ANALYSIS", DeskRole.Analysis }, { "Toggle_LOG", DeskRole.Log }, { "Toggle_POWER", DeskRole.Power }, { "Toggle_CLEAR", DeskRole.Clear } };
            foreach (var kv in roles)
            {
                var t = Find(deskObj.transform, kv.Key); if (t == null) { Debug.LogWarning("[Ward0] missing " + kv.Key); continue; }
                var tg = t.gameObject.AddComponent<DeskToggle>(); var so = new SerializedObject(tg); so.FindProperty("role").enumValueIndex = (int)kv.Value; so.FindProperty("controller").objectReferenceValue = dc; so.ApplyModifiedPropertiesWithoutUndo();
            }
            var lever = Find(deskObj.transform, "IncinerateLever"); var lv = lever.gameObject.AddComponent<DeskLever>(); SetRef(lv, "controller", dc);

            // elevator call buttons already carry ElevatorButton (set in prefab) - nothing else to wire
            var shift = new GameObject("Shift Manager").AddComponent<ShiftManager>(); shift.transform.SetParent(sysT);
            SetRef(shift, "player", pc); SetRef(shift, "desk", dc);
            SetRef(shift, "minerPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(P("MinerFigure")).GetComponent<MinerSubject>());
            SetRef(shift, "minerSpawn", R.minerSpawn.transform); SetRef(shift, "cellGlass", R.glass.transform);
            SetRef(shift, "elevator", elevator); SetRef(shift, "director", dir); SetRef(shift, "hud", hudObj);
            SetRef(shift, "incineratorFire", R.fire.GetComponentInChildren<ParticleSystem>()); SetRef(shift, "incineratorLight", R.fireLight);
            var alarm = new List<Object>(); foreach (var l in R.alarm) alarm.Add(l); SetArr(shift, "alarmLights", alarm);

            var zs = new GameObject("Zone Streamer").AddComponent<ZoneStreamer>(); zs.transform.SetParent(sysT);
            SetRef(zs, "player", pc); SetRef(zs, "map", mapComp);
            var zso = new SerializedObject(zs); var za = zso.FindProperty("zones"); za.arraySize = ZoneIds.Length;
            for (int i = 0; i < ZoneIds.Length; i++) { var e = za.GetArrayElementAtIndex(i); e.FindPropertyRelative("sceneName").stringValue = "Ward0_Mine_" + ZoneIds[i]; e.FindPropertyRelative("node").objectReferenceValue = nodes[ZoneIds[i]]; }
            zso.ApplyModifiedPropertiesWithoutUndo();

            var lb = new GameObject("Lamp Budget").AddComponent<LampBudget>(); lb.transform.SetParent(sysT); SetRef(lb, "player", player.transform);
        }

        static Transform Find(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null;
        }
    }
}
