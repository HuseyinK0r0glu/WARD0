using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Dresses one shared miner body: picks a look (suit / hat / skin colours, hair, accessories, build) from a deterministic index and,
    /// for infected subjects, swaps in the veined skin, worn suit and bloodshot glowing eyes. Materials are instanced per spawn and released on destroy.
    /// </summary>
    public class MinerVariant : MonoBehaviour
    {
        [Flags] public enum Acc { None = 0, Hair = 1, Beard = 2, Goggles = 4, RespiratorNeck = 8, RespiratorFace = 16 }

        [Serializable]
        public struct Look
        {
            public string name; public Color suit, hat, skin, hair; public Acc accessories;
            [Tooltip("Spine bone scale (build).")] public Vector3 build; [Tooltip("Whole-body height multiplier (applied on the Hips bone).")] public float height;
        }

        [Serializable] public struct Accessory { public Acc flag; public GameObject go; }

        [Header("Renderers")]
        [SerializeField] private Renderer[] bodies;
        [SerializeField] private Renderer[] hats;
        [SerializeField] private Renderer hair, beard;
        [SerializeField] private Renderer[] eyes;
        [SerializeField] private Light eyeGlow;
        [Header("Material slots on the body mesh")]
        [SerializeField] private int skinSlot = 0, suitSlot = 1;
        [Header("Infected swaps")]
        [SerializeField] private Material skinInfected, suitInfected, eyeInfected;
        [SerializeField, ColorUsage(false, true)] private Color veinGlow = new Color(0.08f, 0.08f, 0.08f);
        [Header("Looks")]
        [SerializeField] private Look[] looks;
        [SerializeField] private Accessory[] accessories;
        [SerializeField] private Transform hips, spine, neck;

        private readonly List<Material> _own = new List<Material>();
        private Vector3 _hipsBase;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor"), EmissionColor = Shader.PropertyToID("_EmissionColor");

        /// <summary>This spawn's own skin material (instanced), so scripts can drive vein emission. Null before Apply.</summary>
        public Material SkinMaterial { get; private set; }
        /// <summary>The red light parented to the head (enabled for infected looks).</summary>
        public Light EyeGlow => eyeGlow;
        public int LookCount => looks != null ? looks.Length : 0;
        public bool Infected { get; private set; }
        public string LookName { get; private set; }

        private void Awake() { if (hips != null) _hipsBase = hips.localPosition; }

        private void OnDestroy() { foreach (var m in _own) if (m != null) Destroy(m); _own.Clear(); }

        private Material Own(Material src, Color tint)
        {
            var m = new Material(src); m.SetColor(BaseColor, tint); _own.Add(m); return m;
        }

        /// <summary>Applies look number <paramref name="index"/> (wraps) and the infection state. Deterministic; no randomness.</summary>
        public void Apply(bool infected, int index)
        {
            if (looks == null || looks.Length == 0 || bodies == null || bodies.Length == 0) return;
            var look = looks[((index % looks.Length) + looks.Length) % looks.Length]; Infected = infected; LookName = look.name;
            var first = bodies[0].sharedMaterials;
            Color suitTint = infected ? Color.Lerp(look.suit, new Color(0.22f, 0.18f, 0.15f), 0.3f) * 0.85f : look.suit;
            Color skinTint = infected ? Color.Lerp(Color.white, look.skin, 0.3f) : look.skin;
            var skin = Own(infected && skinInfected != null ? skinInfected : first[skinSlot], skinTint);
            var suit = Own(infected && suitInfected != null ? suitInfected : first[suitSlot], suitTint);
            SkinMaterial = skin;
            if (infected && skinInfected != null) { skin.EnableKeyword("_EMISSION"); skin.SetColor(EmissionColor, veinGlow); }
            foreach (var b in bodies) { var arr = b.sharedMaterials; arr[skinSlot] = skin; arr[suitSlot] = suit; b.sharedMaterials = arr; }
            if (hats != null && hats.Length > 0)
            {
                var hatMat = Own(hats[0].sharedMaterials[0], look.hat);
                foreach (var h in hats) { var arr = h.sharedMaterials; arr[0] = hatMat; h.sharedMaterials = arr; }
            }
            foreach (var r in new[] { hair, beard }) if (r != null) r.sharedMaterial = Own(r.sharedMaterial, look.hair);
            if (infected && eyeInfected != null) foreach (var e in eyes) if (e != null) e.sharedMaterial = eyeInfected;
            if (eyeGlow != null) eyeGlow.enabled = infected;
            foreach (var a in accessories) if (a.go != null) a.go.SetActive((look.accessories & a.flag) != 0);
            if (spine != null) { spine.localScale = look.build; if (neck != null) neck.localScale = new Vector3(1f / look.build.x, 1f / look.build.y, 1f / look.build.z); }
            if (hips != null) { float h = look.height <= 0f ? 1f : look.height; hips.localScale = Vector3.one * h; hips.localPosition = _hipsBase * h; }
        }
    }
}
