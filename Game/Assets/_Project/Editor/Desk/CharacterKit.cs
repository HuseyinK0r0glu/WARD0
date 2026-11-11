using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Ward0.Gameplay;
using B = Ward0.EditorTools.CharacterGeo.B;

namespace Ward0.EditorTools
{
    /// <summary>
    /// Builds the human characters procedurally: a Mixamo-named humanoid skeleton, a skinned multi-material body (3 LODs), a real head with eyeballs,
    /// hard hat + lamp, hair, goggles, respirators, and PBR materials from <see cref="CharacterTex"/>. Produces the MinerFigure (cell subject) and
    /// InfectedPatrol prefabs. Called from PropKit.Miner; menu: Ward0 / Characters / Build Characters.
    /// </summary>
    public static class CharacterKit
    {
        public const string Root = "Assets/_Project/Art/Characters";
        const string TexDir = Root + "/Textures", MeshDir = Root + "/Meshes", MatDir = Root + "/Materials";
        static bool _texReady;

        sealed class Tex { public CharacterTex.Set SkinH, SkinI, SkinP, SuitH, SuitI, SuitP, Gear, Band, BandP, Plastic, Hair, EyeH, EyeI; }
        static Tex T;

        [MenuItem("Ward0/Characters/Build Characters")]
        public static void BuildAll() { BuildMiner(false); BuildMiner(true); BuildMiner(false, true); AssetDatabase.SaveAssets(); AssetDatabase.Refresh(); }

        [MenuItem("Ward0/Characters/Rebuild Character Textures")]
        public static void RebuildTextures()
        {
            if (File.Exists(TexDir + "/version.txt")) File.Delete(TexDir + "/version.txt");
            _texReady = false; EnsureTextures(); AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------ specs
        static CharSpec HeroSpec() => new CharSpec { name = "MinerFigure", q = 0.82f, hiVisBraces = true, bands = true, fine = true };
        static CharSpec PatrolSpec() => new CharSpec { name = "InfectedPatrol", q = 0.28f, hunchDeg = 22f, headUpDeg = 12f, armLen = 1.12f, rolledSleeves = true, leatherHarness = true, bands = true, fine = true };
        static CharSpec Lod(CharSpec b, float q) { var s = b.Clone(); s.q = q; s.fine = false; s.bands = b.bands && s.Mid; s.leatherHarness = b.leatherHarness && s.Mid; return s; }

        // ------------------------------------------------------------ textures
        static void EnsureTextures()
        {
            if (_texReady && T != null) return;
            foreach (var d in new[] { Root, TexDir, MeshDir, MatDir }) Directory.CreateDirectory(d);
            if (!CharacterTex.UpToDate(TexDir))
            {
                CharacterTex.Skin(TexDir, "Skin_H", 2048, false); CharacterTex.Skin(TexDir, "Skin_I", 1024, true);
                CharacterTex.Suit(TexDir, "Suit_H", 1024, false); CharacterTex.Suit(TexDir, "Suit_I", 1024, true);
                CharacterTex.Gear(TexDir, "Gear", 1024); CharacterTex.Reflective(TexDir, "Band", 512); CharacterTex.Plastic(TexDir, "Plastic", 512); CharacterTex.Hair(TexDir, "Hair", 512);
                CharacterTex.Eye(TexDir, "Eye_H", 512, false); CharacterTex.Eye(TexDir, "Eye_I", 512, true);
                CharacterTex.MarkDone(TexDir); AssetDatabase.Refresh();
            }
            T = new Tex
            {
                SkinH = LoadSet("Skin_H"), SkinI = LoadSet("Skin_I", true), SkinP = LoadSet("Skin_I", true), SuitH = LoadSet("Suit_H"), SuitI = LoadSet("Suit_I"), SuitP = LoadSet("Suit_I"),
                Gear = LoadSet("Gear"), Band = LoadSet("Band"), BandP = LoadSet("Band"), Plastic = LoadSet("Plastic"), Hair = LoadSet("Hair"), EyeH = LoadSet("Eye_H", false, true), EyeI = LoadSet("Eye_I", true, true)
            };
            _texReady = true;
        }

        static Texture2D Tx(string n) => AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + n + ".png");
        static CharacterTex.Set LoadSet(string n, bool emis = false, bool albedoOnly = false) =>
            new CharacterTex.Set { Albedo = Tx(n + "_Albedo"), Normal = albedoOnly ? null : Tx(n + "_Normal"), MG = albedoOnly ? null : Tx(n + "_MG"), Emis = emis ? Tx(n + "_Emis") : null };

        // ------------------------------------------------------------ materials
        static Material Mat(string name, Color col, float metal, float smooth, CharacterTex.Set t = null, Color? emission = null, float bump = 1f)
        {
            string path = MatDir + "/" + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Universal Render Pipeline/Lit"); m.name = name;
            m.SetColor("_BaseColor", col); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth);
            m.SetTexture("_BaseMap", t != null ? t.Albedo : null);
            m.SetTexture("_BumpMap", t != null ? t.Normal : null); m.SetFloat("_BumpScale", bump);
            if (t != null && t.Normal != null) m.EnableKeyword("_NORMALMAP"); else m.DisableKeyword("_NORMALMAP");
            m.SetTexture("_MetallicGlossMap", t != null ? t.MG : null);
            if (t != null && t.MG != null) { m.EnableKeyword("_METALLICSPECGLOSSMAP"); m.SetFloat("_Smoothness", 1f); } else m.DisableKeyword("_METALLICSPECGLOSSMAP");
            m.SetTexture("_EmissionMap", t != null ? t.Emis : null);
            if (emission.HasValue) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", emission.Value); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None; }
            else { m.DisableKeyword("_EMISSION"); m.SetColor("_EmissionColor", Color.black); }
            EditorUtility.SetDirty(m); return m;
        }

        sealed class Mats { public Material skin, suit, band, gear, hardware, hat, lens, goggle, rubber, hair, eye, skinInf, suitInf, eyeInf; }

        static Mats MakeMats(bool patrol)
        {
            var m = new Mats();
            string p = patrol ? "Patrol" : "Miner";
            if (patrol)
            {
                m.skin = Mat("PatrolSkin", Color.white, 0f, 0.4f, T.SkinP, new Color(0.08f, 0.08f, 0.08f));
                m.suit = Mat("PatrolSuit", new Color(0.9f, 0.4f, 0.07f), 0f, 1f, T.SuitP, null, 1.3f);
                m.skinInf = m.skin; m.suitInf = m.suit;
                m.band = Mat("PatrolBand", new Color(0.85f, 0.85f, 0.82f), 0f, 1f, T.BandP, null, 0.8f);
            }
            else
            {
                m.skin = Mat("MinerSkin", Color.white, 0f, 0.4f, T.SkinH);
                m.suit = Mat("MinerSuit", new Color(0.12f, 0.14f, 0.3f), 0f, 1f, T.SuitH, null, 1.3f);
                m.skinInf = Mat("MinerSkinInfected", Color.white, 0f, 0.4f, T.SkinI, new Color(0.08f, 0.08f, 0.08f));
                m.suitInf = Mat("MinerSuitInfected", new Color(0.3f, 0.2f, 0.12f), 0f, 1f, T.SuitI, null, 1.3f);
                m.band = Mat("MinerBand", Color.white, 0f, 1f, T.Band, null, 0.8f);
            }
            m.gear = Mat("CharGear", Color.white, 0f, 1f, T.Gear, null, 1f);
            m.hardware = Mat("CharHardware", new Color(0.2f, 0.2f, 0.21f), 0.85f, 0.45f);
            m.hat = Mat("CharHat", new Color(0.6f, 0.6f, 0.62f), 0f, 1f, T.Plastic, null, 0.8f);
            m.lens = Mat("CharLampLens", new Color(0.85f, 0.85f, 0.8f), 0f, 0.9f, null, new Color(2.6f, 2.3f, 1.8f));
            m.goggle = Mat("CharGoggleLens", new Color(0.12f, 0.2f, 0.26f), 0.4f, 0.92f);
            m.rubber = Mat("CharRubber", new Color(0.2f, 0.22f, 0.22f), 0f, 0.3f);
            m.hair = Mat("CharHair", new Color(0.12f, 0.08f, 0.05f), 0f, 0.4f, T.Hair, null, 0.6f);
            m.eye = Mat("CharEye", Color.white, 0f, 0.92f, T.EyeH, Color.black);
            m.eyeInf = Mat("CharEyeInfected", Color.white, 0f, 0.92f, T.EyeI, new Color(2.2f, 0.9f, 0.6f));
            return m;
        }

        // ------------------------------------------------------------ assembly helpers
        /// <summary>Writes the mesh asset (replacing any previous one) and returns it.</summary>
        static Mesh SaveMesh(Mesh m, string name)
        {
            string p = MeshDir + "/" + name + ".asset"; m.name = name;
            AssetDatabase.DeleteAsset(p); AssetDatabase.CreateAsset(m, p); return m;
        }

        static GameObject Attach(Transform bone, Vector3 boneWorld, string name, SkinMesh sm, Material[] mats, CharSpec sp, string meshName)
        {
            CharacterGeo.Warp(sm, sp); sm.Offset(-boneWorld);
            var mesh = SaveMesh(sm.ToMesh(meshName, false), meshName);
            var go = new GameObject(name); go.transform.SetParent(bone, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
            return go;
        }

        static void Wire(SerializedObject so, string prop, UnityEngine.Object o) { so.FindProperty(prop).objectReferenceValue = o; }
        static void WireArr<T>(SerializedObject so, string prop, IList<T> a) where T : UnityEngine.Object
        {
            var p = so.FindProperty(prop); p.arraySize = a.Count; for (int i = 0; i < a.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = a[i];
        }

        sealed class LookDef { public string name; public Color suit, hat, skin, hair; public MinerVariant.Acc acc; public Vector3 build; public float height; }
        static LookDef L(string n, Color suit, Color hat, Color skin, Color hair, MinerVariant.Acc acc, Vector3 build, float h) => new LookDef { name = n, suit = suit, hat = hat, skin = skin, hair = hair, acc = acc, build = build, height = h };

        static List<LookDef> HeroLooks() => new List<LookDef>
        {
            L("Navy", new Color(0.13f, 0.15f, 0.34f), new Color(0.5f, 0.52f, 0.54f), new Color(1f, 0.96f, 0.94f), new Color(0.2f, 0.12f, 0.07f), MinerVariant.Acc.Hair | MinerVariant.Acc.Goggles, new Vector3(1f, 1f, 1f), 1f),
            L("Orange", new Color(0.95f, 0.42f, 0.06f), new Color(0.95f, 0.78f, 0.08f), new Color(0.8f, 0.66f, 0.55f), new Color(0.07f, 0.05f, 0.04f), MinerVariant.Acc.Hair | MinerVariant.Acc.Beard | MinerVariant.Acc.RespiratorNeck, new Vector3(1.1f, 1f, 1.14f), 0.97f),
            L("Olive", new Color(0.34f, 0.37f, 0.27f), new Color(0.92f, 0.92f, 0.88f), new Color(0.52f, 0.38f, 0.3f), new Color(0.05f, 0.05f, 0.05f), MinerVariant.Acc.RespiratorFace, new Vector3(0.92f, 1f, 0.92f), 1.04f),
            L("Khaki", new Color(0.62f, 0.5f, 0.32f), new Color(0.95f, 0.42f, 0.06f), new Color(0.9f, 0.74f, 0.64f), new Color(0.58f, 0.58f, 0.56f), MinerVariant.Acc.Hair | MinerVariant.Acc.Beard, new Vector3(1.13f, 1f, 1.2f), 0.96f)
        };

        // The player's own suit (Shift Technician concept): charcoal coverall, yellow harness, dark helmet, full respirator and visor.
        static List<LookDef> TechnicianLooks() => new List<LookDef>
        {
            L("Technician", new Color(0.2f, 0.21f, 0.22f), new Color(0.13f, 0.14f, 0.15f), new Color(0.8f, 0.7f, 0.62f), new Color(0.05f, 0.05f, 0.05f), MinerVariant.Acc.RespiratorFace | MinerVariant.Acc.Goggles, new Vector3(1.04f, 1f, 1.06f), 1f)
        };

        static List<LookDef> PatrolLooks() => new List<LookDef>
        {
            L("OrangeInfected", new Color(0.92f, 0.42f, 0.07f), new Color(0.92f, 0.4f, 0.06f), Color.white, new Color(0.08f, 0.06f, 0.05f), MinerVariant.Acc.Hair, Vector3.one, 1f),
            L("NavyInfected", new Color(0.16f, 0.18f, 0.34f), new Color(0.5f, 0.52f, 0.54f), Color.white, new Color(0.3f, 0.3f, 0.3f), MinerVariant.Acc.Beard, new Vector3(0.95f, 1f, 0.95f), 1.03f)
        };

        // ------------------------------------------------------------ build one prefab
        public static GameObject BuildMiner(bool patrol, bool technician = false)
        {
            EnsureTextures();
            var sp = patrol ? PatrolSpec() : HeroSpec(); string pn = patrol ? "InfectedPatrol" : (technician ? "TechnicianFigure" : "MinerFigure");
            var mats = MakeMats(patrol);
            var root = new GameObject(pn);
            // skeleton (world-aligned bind pose; every bone starts with identity rotation so gameplay scripts can set rotations directly)
            var bp = CharacterGeo.BonePositions(sp); var wp = new Vector3[bp.Length]; var bones = new Transform[bp.Length];
            for (int i = 0; i < bp.Length; i++)
            {
                wp[i] = CharacterGeo.WarpPoint(sp, bp[i]); bones[i] = new GameObject("mixamorig:" + CharacterGeo.BoneName[i]).transform;
                int par = CharacterGeo.BoneParent[i]; bones[i].SetParent(par < 0 ? root.transform : bones[par], false);
                bones[i].localPosition = par < 0 ? wp[i] : wp[i] - wp[par];
            }
            var bind = new Matrix4x4[bones.Length]; for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * root.transform.localToWorldMatrix;
            var bodyMats = new[] { mats.skin, mats.suit, mats.band, mats.gear, mats.hardware };
            // body LODs
            var smrs = new SkinnedMeshRenderer[3]; var specs = new[] { sp, Lod(sp, patrol ? 0.16f : 0.2f), Lod(sp, 0.05f) }; var triCounts = new int[3];
            for (int l = 0; l < 3; l++)
            {
                var sm = CharacterGeo.BuildBody(specs[l]); var mesh = sm.ToMesh(pn + "_Body_LOD" + l, true); mesh.bindposes = bind; mesh = SaveMesh(mesh, pn + "_Body_LOD" + l); triCounts[l] = sm.TriangleCount;
                var go = new GameObject("Body_LOD" + l); go.transform.SetParent(root.transform, false);
                var r = go.AddComponent<SkinnedMeshRenderer>(); r.sharedMesh = mesh; r.bones = bones; r.rootBone = bones[(int)B.Hips]; r.sharedMaterials = bodyMats; r.updateWhenOffscreen = false; r.localBounds = mesh.bounds;
                r.shadowCastingMode = l == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off; smrs[l] = r;
            }
            var head = bones[(int)B.Head]; var headW = wp[(int)B.Head]; var hc = CharacterGeo.HeadCenter;
            // hat + lamp
            var hatGo = Attach(head, headW, "HardHat", CharacterGeo.Hat(sp), new[] { mats.hat, mats.hardware, mats.lens }, sp, pn + "_Hat");
            var hatLow = Attach(head, headW, "HardHat_LOD", CharacterGeo.Hat(Lod(sp, 0.12f)), new[] { mats.hat, mats.hardware, mats.lens }, sp, pn + "_HatLOD");
            // eyes (separate renderers, centred on the eyeball)
            var eyes = new List<Renderer>();
            foreach (float sx in new[] { -1f, 1f })
            {
                var em = SaveMesh(CharacterGeo.Eyeball(0.0112f, 16, 10).ToMesh(pn + "_Eye", false), pn + "_Eye" + (sx < 0 ? "L" : "R"));
                var go = new GameObject(sx < 0 ? "Eye_L" : "Eye_R"); go.transform.SetParent(head, false);
                var c = CharacterGeo.WarpPoint(sp, CharacterGeo.EyeCenter(sx)); go.transform.localPosition = c - headW;
                go.transform.localRotation = Quaternion.Euler(CharacterGeo.WarpAngle(sp, c.y) * Mathf.Rad2Deg, 0f, 0f);
                go.AddComponent<MeshFilter>().sharedMesh = em; var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = patrol ? mats.eyeInf : mats.eye; r.shadowCastingMode = ShadowCastingMode.Off; eyes.Add(r);
            }
            // hair, beard, goggles, respirators (toggled per look)
            var hair = Attach(head, headW, "Hair", CharacterGeo.HairShell(sp, false, 0.0065f), new[] { mats.hair }, sp, pn + "_Hair");
            var beard = Attach(head, headW, "Beard", CharacterGeo.HairShell(sp, true, 0.0055f), new[] { mats.hair }, sp, pn + "_Beard");
            var goggles = Attach(head, headW, "Goggles", CharacterGeo.Goggles(sp), new[] { mats.hardware, mats.goggle }, sp, pn + "_Goggles");
            var faceMask = CharacterGeo.Respirator(sp, Matrix4x4.TRS(new Vector3(0f, 1.624f, 0.088f), Quaternion.identity, Vector3.one),
                new[] { new Vector3(-0.062f, 1.648f, 0.088f), new Vector3(-0.08f, 1.665f, 0.01f), new Vector3(-0.05f, 1.67f, -0.09f), new Vector3(0.05f, 1.67f, -0.09f), new Vector3(0.08f, 1.665f, 0.01f), new Vector3(0.062f, 1.648f, 0.088f) });
            var respFace = Attach(head, headW, "RespiratorFace", faceMask, new[] { mats.rubber, mats.hardware }, sp, pn + "_RespFace");
            var spine2 = bones[(int)B.Spine2];
            var hangMask = CharacterGeo.Respirator(sp, Matrix4x4.TRS(new Vector3(0f, 1.365f, 0.175f), Quaternion.Euler(-38f, 0f, 0f), Vector3.one),
                new[] { new Vector3(-0.058f, 1.36f, 0.16f), new Vector3(-0.075f, 1.49f, 0.06f), new Vector3(-0.045f, 1.53f, -0.06f), new Vector3(0.045f, 1.53f, -0.06f), new Vector3(0.075f, 1.49f, 0.06f), new Vector3(0.058f, 1.36f, 0.16f) });
            var respNeck = Attach(spine2, wp[(int)B.Spine2], "RespiratorNeck", hangMask, new[] { mats.rubber, mats.hardware }, sp, pn + "_RespNeck");
            // light for the glowing eyes
            var glowGo = new GameObject("EyeGlow"); glowGo.transform.SetParent(head, false); glowGo.transform.localPosition = (CharacterGeo.WarpPoint(sp, hc + new Vector3(0, 0.0f, 0.16f)) - headW);
            var glow = glowGo.AddComponent<Light>(); glow.type = LightType.Point; glow.color = new Color(1f, 0.12f, 0.05f); glow.intensity = patrol ? 1.4f : 0.7f; glow.range = patrol ? 3.5f : 2.2f; glow.shadows = LightShadows.None; glow.enabled = patrol;
            // LOD group
            var lod0 = new List<Renderer> { smrs[0], hatGo.GetComponent<Renderer>(), eyes[0], eyes[1] }; foreach (var g in new[] { hair, beard, goggles, respFace, respNeck }) lod0.AddRange(g.GetComponentsInChildren<Renderer>(true));
            var lod1 = new List<Renderer> { smrs[1], hatLow.GetComponent<Renderer>(), eyes[0], eyes[1], hair.GetComponent<Renderer>(), beard.GetComponent<Renderer>() };
            var lg = root.AddComponent<LODGroup>(); lg.fadeMode = LODFadeMode.None;
            lg.SetLODs(new[] { new LOD(0.2f, lod0.ToArray()), new LOD(0.07f, lod1.ToArray()), new LOD(0.018f, new Renderer[] { smrs[2], hatLow.GetComponent<Renderer>() }) }); lg.RecalculateBounds();
            // gameplay components
            var variant = root.AddComponent<MinerVariant>(); var vso = new SerializedObject(variant);
            WireArr(vso, "bodies", smrs); WireArr(vso, "hats", new[] { hatGo.GetComponent<Renderer>(), hatLow.GetComponent<Renderer>() }); Wire(vso, "hair", hair.GetComponent<Renderer>()); Wire(vso, "beard", beard.GetComponent<Renderer>()); WireArr(vso, "eyes", eyes);
            Wire(vso, "eyeGlow", glow); Wire(vso, "skinInfected", mats.skinInf); Wire(vso, "suitInfected", mats.suitInf); Wire(vso, "eyeInfected", mats.eyeInf);
            Wire(vso, "hips", bones[(int)B.Hips]); Wire(vso, "spine", bones[(int)B.Spine]); Wire(vso, "neck", bones[(int)B.Neck]);
            var looks = patrol ? PatrolLooks() : (technician ? TechnicianLooks() : HeroLooks()); var lp = vso.FindProperty("looks"); lp.arraySize = looks.Count;
            for (int i = 0; i < looks.Count; i++)
            {
                var e = lp.GetArrayElementAtIndex(i); var d = looks[i];
                e.FindPropertyRelative("name").stringValue = d.name; e.FindPropertyRelative("suit").colorValue = d.suit; e.FindPropertyRelative("hat").colorValue = d.hat; e.FindPropertyRelative("skin").colorValue = d.skin;
                e.FindPropertyRelative("hair").colorValue = d.hair; e.FindPropertyRelative("accessories").intValue = (int)d.acc; e.FindPropertyRelative("build").vector3Value = d.build; e.FindPropertyRelative("height").floatValue = d.height;
            }
            var acc = new[] { (MinerVariant.Acc.Hair, hair), (MinerVariant.Acc.Beard, beard), (MinerVariant.Acc.Goggles, goggles), (MinerVariant.Acc.RespiratorNeck, respNeck), (MinerVariant.Acc.RespiratorFace, respFace) };
            var ap = vso.FindProperty("accessories"); ap.arraySize = acc.Length;
            for (int i = 0; i < acc.Length; i++) { var e = ap.GetArrayElementAtIndex(i); e.FindPropertyRelative("flag").intValue = (int)acc[i].Item1; e.FindPropertyRelative("go").objectReferenceValue = acc[i].Item2; }
            vso.ApplyModifiedPropertiesWithoutUndo();
            if (patrol)
            {
                var pe = root.AddComponent<PatrolEnemy>(); var so = new SerializedObject(pe);
                Wire(so, "armL", bones[(int)B.LArm]); Wire(so, "armR", bones[(int)B.RArm]); Wire(so, "bodyVisual", bones[(int)B.Spine]); Wire(so, "hips", bones[(int)B.Hips]); Wire(so, "chest", bones[(int)B.Spine2]); Wire(so, "head", head);
                Wire(so, "legL", bones[(int)B.LUpLeg]); Wire(so, "legR", bones[(int)B.RUpLeg]); Wire(so, "kneeL", bones[(int)B.LLeg]); Wire(so, "kneeR", bones[(int)B.RLeg]);
                Wire(so, "forearmL", bones[(int)B.LFore]); Wire(so, "forearmR", bones[(int)B.RFore]); so.ApplyModifiedPropertiesWithoutUndo();
            }
            else if (!technician)
            {
                var ms = root.AddComponent<MinerSubject>(); var so = new SerializedObject(ms);
                Wire(so, "head", head); Wire(so, "armL", bones[(int)B.LArm]); Wire(so, "armR", bones[(int)B.RArm]); Wire(so, "body", bones[(int)B.Spine]); WireArr(so, "eyes", eyes);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            string dir = technician ? "Assets/_Project/Resources" : PropKit.Root + "/Prefabs";   // the technician is loaded on demand by PlayerBody
            Directory.CreateDirectory(dir);
            string path = dir + "/" + pn + ".prefab"; PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            LastTris[patrol ? 1 : 0] = triCounts;
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        /// <summary>Body triangle counts per LOD of the last build (0 = miner, 1 = patrol).</summary>
        public static readonly int[][] LastTris = new int[2][];

            }
}
