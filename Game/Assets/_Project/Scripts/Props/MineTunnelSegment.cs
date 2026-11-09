using UnityEngine;

namespace Ward0.Props
{
    /// <summary>
    /// One modular, tileable 6 m mine tunnel segment. Place segments end to end by matching
    /// <see cref="EndAnchor"/> of one to <see cref="StartAnchor"/> of the next (the geometry tiles seamlessly along Z).
    /// </summary>
    public class MineTunnelSegment : MonoBehaviour
    {
        public const float SegmentLength = 6f;

        [Header("Anchors (for corridor swapping / spawning)")]
        [SerializeField] private Transform startAnchor;
        [SerializeField] private Transform endAnchor;

        [Header("Lamp")]
        [SerializeField] private Light lamp;
        [SerializeField] private Renderer bulb;
        [SerializeField] private bool flicker = true;
        [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.18f;

        private float _lampBase;
        private Color _bulbEmission;
        private MaterialPropertyBlock _mpb;
        private float _seed;

        public Transform StartAnchor => startAnchor;
        public Transform EndAnchor => endAnchor;
        public Light Lamp => lamp;

        private void Awake()
        {
            _seed = Random.value * 100f;
            if (lamp != null) _lampBase = lamp.intensity;
            if (bulb != null) { _bulbEmission = bulb.sharedMaterial.GetColor("_EmissionColor"); _mpb = new MaterialPropertyBlock(); }
        }

        private void Update()
        {
            if (!flicker || lamp == null || !lamp.enabled) return;
            float n = Mathf.PerlinNoise(Time.time * 6f, _seed);
            float drop = n > 0.84f ? Random.Range(0.3f, 0.75f) : 1f;
            float k = (1f - flickerAmount + flickerAmount * n) * drop;
            lamp.intensity = _lampBase * k;
            if (bulb != null) { _mpb.SetColor("_EmissionColor", _bulbEmission * k); bulb.SetPropertyBlock(_mpb); }
        }

        public void SetLamp(bool on)
        {
            if (lamp != null) lamp.enabled = on;
            if (bulb != null && _mpb != null) { _mpb.SetColor("_EmissionColor", on ? _bulbEmission : Color.black); bulb.SetPropertyBlock(_mpb); }
        }
    }
}
