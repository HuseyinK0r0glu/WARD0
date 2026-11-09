using UnityEngine;

namespace Ward0.Props
{
    /// <summary>
    /// Runtime handle for the quarantine control desk prefab. Exposes the parts gameplay code needs
    /// (screens, lever, toggles, lights) so other systems never have to search the hierarchy.
    /// </summary>
    public class QuarantineDesk : MonoBehaviour
    {
        [Header("Screens (swap their material texture for Render-to-Texture feeds)")]
        [SerializeField] private Renderer xrayScreen;
        [SerializeField] private Renderer bioscanScreen;

        [Header("Verdict lever")]
        [SerializeField] private Transform incinerateLever;
        [SerializeField, Tooltip("Degrees about local X the lever travels when pulled (negative = toward the player)")] private float leverPullAngle = -65f;

        [Header("Lights")]
        [SerializeField] private Light cageLamp;
        [SerializeField] private Renderer[] statusLeds;

        [Header("Lamp flicker")]
        [SerializeField] private bool flicker = true;
        [SerializeField, Range(0f, 1f)] private float flickerAmount = 0.12f;

        private float _lampBase;
        private Quaternion _leverRest;

        public Renderer XRayScreen => xrayScreen;
        public Renderer BioscanScreen => bioscanScreen;
        public Transform IncinerateLever => incinerateLever;
        public Light CageLamp => cageLamp;

        private void Awake()
        {
            if (cageLamp != null) _lampBase = cageLamp.intensity;
            if (incinerateLever != null) _leverRest = incinerateLever.localRotation;
        }

        private void Update()
        {
            if (!flicker || cageLamp == null) return;
            float n = Mathf.PerlinNoise(Time.time * 7f, 0.37f);
            float drop = n > 0.82f ? Random.Range(0.35f, 0.8f) : 1f;
            cageLamp.intensity = _lampBase * (1f - flickerAmount + flickerAmount * n) * drop;
        }

        /// <summary>0 = rest, 1 = fully pulled.</summary>
        public void SetLeverPull(float t)
        {
            if (incinerateLever == null) return;
            incinerateLever.localRotation = _leverRest * Quaternion.Euler(Mathf.Clamp01(t) * leverPullAngle, 0f, 0f);
        }

        public void SetStatusLed(int index, bool on)
        {
            if (statusLeds == null || index < 0 || index >= statusLeds.Length || statusLeds[index] == null) return;
            var m = statusLeds[index].material;
            var c = m.GetColor("_EmissionColor");
            float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            if (peak <= 0.0001f) return;
            m.SetColor("_EmissionColor", on ? c / peak * 2.2f : c / peak * 0.05f);
        }
    }
}
