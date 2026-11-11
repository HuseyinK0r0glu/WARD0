using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ward0.EditorTools
{
    /// <summary>
    /// One-click performance baseline for the project: compresses the generated textures (they were saved as raw RGBA32),
    /// and sizes the URP shadow atlas for the shadow-lamp budget (<c>LampBudget</c>: 2 lamps = 12 cube faces) so the
    /// "resolution reduced to fit shadow maps" warning goes away. Menu: Ward0 / Apply Performance Settings (safe to re-run).
    /// </summary>
    public static class PerfSettings
    {
        const string ArtRoot = "Assets/_Project/Art";
        const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";

        [MenuItem("Ward0/Apply Performance Settings")]
        public static void Apply()
        {
            int tex = CompressTextures();
            bool rp = TuneUrpAsset();
            AssetDatabase.SaveAssets();
            Debug.Log("[Ward0] Performance settings applied: " + tex + " textures compressed, URP asset " + (rp ? "tuned" : "not found"));
        }

        static int CompressTextures()
        {
            int n = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
                {
                    var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter; if (imp == null) continue;
                    if (imp.textureCompression == TextureImporterCompression.CompressedHQ && !imp.crunchedCompression) continue;
                    imp.textureCompression = TextureImporterCompression.CompressedHQ; imp.crunchedCompression = false;
                    imp.SaveAndReimport(); n++;
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            return n;
        }

        static bool TuneUrpAsset()
        {
            var rp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath); if (rp == null) return false;
            var so = new SerializedObject(rp);
            void I(string f, int v) { var p = so.FindProperty(f); if (p != null) p.intValue = v; }
            // 12 point-light cube faces (2 shadow lamps) at 512 px fit a 2048 atlas without being scaled down
            I("m_AdditionalLightsShadowmapResolution", 2048);
            I("m_AdditionalLightsShadowResolutionTierLow", 256); I("m_AdditionalLightsShadowResolutionTierMedium", 512); I("m_AdditionalLightsShadowResolutionTierHigh", 512);
            // there is no directional light: do not allocate cascades for one
            I("m_MainLightShadowmapResolution", 1024); I("m_ShadowCascadeCount", 1);
            I("m_SoftShadowQuality", 2);
            var dist = so.FindProperty("m_ShadowDistance"); if (dist != null) dist.floatValue = 30f;
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(rp);
            return true;
        }
    }
}
