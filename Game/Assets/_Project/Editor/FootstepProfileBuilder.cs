using System.IO;
using UnityEditor;
using UnityEngine;
using Ward0.Gameplay;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds Resources/FootstepProfile from the recorded clips in Audio/Footsteps (CC0, see CREDITS.txt there). The player uses these
    /// recordings for metal, gravel and puddle surfaces; the synthesised footsteps remain only as a fallback. Menu: Ward0 / Audio / Build Footstep Profile.
    /// </summary>
    public static class FootstepProfileBuilder
    {
        const string Dir = "Assets/_Project/Audio/Footsteps", Out = "Assets/_Project/Resources/FootstepProfile.asset";

        [MenuItem("Ward0/Audio/Build Footstep Profile")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Project/Resources");
            var profile = AssetDatabase.LoadAssetAtPath<FootstepProfile>(Out);
            if (profile == null) { profile = ScriptableObject.CreateInstance<FootstepProfile>(); AssetDatabase.CreateAsset(profile, Out); }
            var so = new SerializedObject(profile);
            Assign(so, "metal", "Metal"); Assign(so, "gravel", "Gravel"); Assign(so, "puddle", "Puddle");
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(profile); AssetDatabase.SaveAssets();
            Debug.Log("[Ward0] FootstepProfile built from " + Dir);
        }

        static void Assign(SerializedObject so, string prop, string folder)
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip", new[] { Dir + "/" + folder }); System.Array.Sort(guids, (a, b) => string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(a), AssetDatabase.GUIDToAssetPath(b)));
            var p = so.FindProperty(prop); p.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var imp = (AudioImporter)AssetImporter.GetAtPath(path);
                if (imp != null && (!imp.forceToMono || imp.defaultSampleSettings.loadType != AudioClipLoadType.DecompressOnLoad))
                {
                    imp.forceToMono = true; var ss = imp.defaultSampleSettings; ss.loadType = AudioClipLoadType.DecompressOnLoad; ss.compressionFormat = AudioCompressionFormat.ADPCM; imp.defaultSampleSettings = ss; imp.SaveAndReimport();
                }
                p.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            }
        }
    }
}
