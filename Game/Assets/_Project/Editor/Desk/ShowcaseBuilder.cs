using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ward0.EditorTools
{
    /// <summary>Builds a small lit showcase scene to review props (Ward0 / Build Desk Showcase).</summary>
    public static class ShowcaseBuilder
    {
        const string ScenePath = "Assets/_Project/Scenes/DeskShowcase.unity";
        const string ProfilePath = "Assets/_Project/Art/Desk/ShowcaseVolume.asset";

        [MenuItem("Ward0/Build Desk Showcase")]
        public static void Build() => Make("Assets/_Project/Art/Desk/QuarantineDesk.prefab", ScenePath, new Vector3(0.05f, 1.25f, -3.6f), new Vector3(0, 1.0f, 0), 34f, 1.0f);

        [MenuItem("Ward0/Build Door Showcase")]
        public static void BuildDoor() => Make("Assets/_Project/Art/Door/QuarantineDoor.prefab", "Assets/_Project/Scenes/DoorShowcase.unity", new Vector3(0f, 1.35f, -5.4f), new Vector3(0, 1.25f, 0), 34f, 1.5f);

        static void Make(string prefabPath, string scenePath, Vector3 camPos, Vector3 lookAt, float fov, float lightScale)
        {
            Directory.CreateDirectory("Assets/_Project/Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.18f, 0.2f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Exponential; RenderSettings.fogDensity = 0.03f; RenderSettings.fogColor = new Color(0.03f, 0.04f, 0.045f);

            // floor + back wall
            var floorMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.07f, 0.07f, 0.07f) };
            floorMat.SetFloat("_Smoothness", 0.35f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Floor";
            floor.transform.position = new Vector3(0, -0.05f, 0); floor.transform.localScale = new Vector3(14, 0.1f, 14);
            floor.GetComponent<Renderer>().sharedMaterial = floorMat;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "BackWall";
            wall.transform.position = new Vector3(0, 2f, 2.9f); wall.transform.localScale = new Vector3(14, 4, 0.1f);
            wall.GetComponent<Renderer>().sharedMaterial = floorMat;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            var desk = (GameObject)PrefabUtility.InstantiatePrefab(prefab); desk.transform.position = Vector3.zero;

            // key light (cool, from front-left above), fill and rim
            Light L(string n, LightType t, Vector3 pos, Vector3 euler, Color c, float i, float range = 10f, float angle = 60f)
            {
                var g = new GameObject(n); g.transform.position = pos; g.transform.rotation = Quaternion.Euler(euler);
                var l = g.AddComponent<Light>(); l.type = t; l.color = c; l.intensity = i * lightScale; l.range = range; l.spotAngle = angle; l.shadows = LightShadows.Soft;
                return l;
            }
            L("Key", LightType.Spot, new Vector3(-2.2f, 3.2f, -3.2f), Vector3.zero, new Color(0.8f, 0.88f, 1f), 160f, 12f, 75f);
            L("Fill", LightType.Point, new Vector3(2.6f, 1.6f, -2.8f), Vector3.zero, new Color(1f, 0.82f, 0.6f), 18f, 9f).shadows = LightShadows.None;
            L("RearFill", LightType.Point, new Vector3(-0.8f, 2.0f, 1.6f), Vector3.zero, new Color(0.9f, 0.85f, 0.8f), 14f, 7f).shadows = LightShadows.None;
            L("Rim", LightType.Spot, new Vector3(0.5f, 2.6f, 2.2f), Vector3.zero, new Color(0.5f, 0.7f, 1f), 70f, 8f, 80f);

            foreach (var li in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) if (li.type == LightType.Spot) li.transform.LookAt(lookAt);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            camGo.transform.position = camPos; camGo.transform.LookAt(lookAt);
            var cam = camGo.AddComponent<Camera>(); cam.fieldOfView = fov; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.02f, 0.025f, 0.03f);
            camGo.AddComponent<AudioListener>();
            var data = camGo.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;

            // reflection probe so the metal has something to reflect
            var pg = new GameObject("ReflectionProbe"); pg.transform.position = new Vector3(0, 1.3f, -1.2f);
            var rp = pg.AddComponent<ReflectionProbe>(); rp.size = new Vector3(8, 4, 8); rp.mode = ReflectionProbeMode.Realtime; rp.refreshMode = ReflectionProbeRefreshMode.OnAwake; rp.resolution = 128;

            var vp = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = vp.Add<Bloom>(true); bloom.intensity.Override(0.7f); bloom.threshold.Override(1.0f); bloom.scatter.Override(0.6f);
            var tone = vp.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var vig = vp.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.5f);
            var ca = vp.Add<ColorAdjustments>(true); ca.postExposure.Override(0.8f); ca.contrast.Override(8f);
            AssetDatabase.DeleteAsset(ProfilePath); AssetDatabase.CreateAsset(vp, ProfilePath);
            var vg = new GameObject("PostFX"); var vol = vg.AddComponent<Volume>(); vol.isGlobal = true; vol.sharedProfile = vp;

            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[Ward0] Showcase scene saved: " + scenePath);
        }
    }
}
