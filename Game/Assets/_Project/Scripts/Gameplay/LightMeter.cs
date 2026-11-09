using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Approximates how brightly lit the player is (0 dark .. 1 bright). Patrols spot you faster in light.</summary>
    public class LightMeter : MonoBehaviour
    {
        public static float Level { get; private set; }
        public static bool FlashlightOn;

        private readonly List<Light> _lights = new List<Light>(); private float _t;

        private void Start() { Refresh(); }

        public void Refresh()
        {
            _lights.Clear();
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Point && l.gameObject.scene.IsValid()) _lights.Add(l);
        }

        private void Update()
        {
            _t -= Time.deltaTime; if (_t > 0f) return; _t = 0.15f;
            float sum = 0f; Vector3 p = transform.position + Vector3.up * 1.2f;
            foreach (var l in _lights)
            {
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                float d = Vector3.Distance(p, l.transform.position); if (d > l.range) continue;
                sum += l.intensity * (1f - d / l.range) / (1f + d * d) * 0.9f;
            }
            if (FlashlightOn) sum += 0.55f;
            Level = Mathf.Lerp(Level, Mathf.Clamp01(sum), 0.6f);
        }
    }
}
