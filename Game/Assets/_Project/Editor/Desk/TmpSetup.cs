using System.IO;
using UnityEditor;
using UnityEngine;

namespace Ward0.EditorTools
{
    public static class TmpSetup
    {
        [MenuItem("Ward0/Import TMP Essentials (silent)")]
        public static void Import()
        {
            var guids = AssetDatabase.FindAssets("TMP Essential Resources");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                if (p.EndsWith(".unitypackage")) { AssetDatabase.ImportPackage(p, false); Debug.Log("[Ward0] Imported " + p); return; }
            }
            Debug.LogError("[Ward0] TMP Essential Resources package not found");
        }
    }
}
