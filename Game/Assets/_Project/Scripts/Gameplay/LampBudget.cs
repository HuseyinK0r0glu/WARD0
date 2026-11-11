using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Keeps shadow-casting lamps to the few nearest the player. A point light shadow is six extra scene passes, so the
    /// budget is small (default 2) and every other lamp is lit but shadowless. Lamps are collected again whenever a mine zone loads.
    /// </summary>
    public class LampBudget : MonoBehaviour
    {
        private const int MaxBudget = 4;

        [SerializeField] private Transform player;
        [SerializeField, Range(0, MaxBudget)] private int maxShadowLamps = 2;
        [SerializeField] private float maxDistance = 22f;

        private readonly List<Light> _lamps = new List<Light>();
        private readonly HashSet<Light> _known = new HashSet<Light>();
        private readonly int[] _best = new int[MaxBudget];
        private readonly float[] _bestD = new float[MaxBudget];
        private float _t;

        private void OnEnable() { ZoneStreamer.ZonesChanged += Refresh; }
        private void OnDisable() { ZoneStreamer.ZonesChanged -= Refresh; }
        private void Start() { Refresh(); }

        /// <summary>Picks up shadow-casting point lights that appeared (zone loads) and drops destroyed ones.</summary>
        public void Refresh()
        {
            for (int i = _lamps.Count - 1; i >= 0; i--) if (_lamps[i] == null) { _lamps.RemoveAt(i); }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Point && l.shadows != LightShadows.None && _known.Add(l)) _lamps.Add(l);
            _t = 0f;
        }

        private void Update()
        {
            _t -= Time.deltaTime; if (_t > 0f || player == null) return; _t = 0.4f;
            int k = Mathf.Min(maxShadowLamps, MaxBudget), found = 0; var pp = player.position; float lim = maxDistance * maxDistance;
            for (int i = 0; k > 0 && i < _lamps.Count; i++)
            {
                var l = _lamps[i]; if (l == null) continue;
                float d = (l.transform.position - pp).sqrMagnitude; if (d >= lim) continue;
                if (found >= k && d >= _bestD[k - 1]) continue;
                int at = found < k ? found : k - 1;
                while (at > 0 && _bestD[at - 1] > d) { _bestD[at] = _bestD[at - 1]; _best[at] = _best[at - 1]; at--; }
                _bestD[at] = d; _best[at] = i; if (found < k) found++;
            }
            for (int i = 0; i < _lamps.Count; i++)
            {
                var l = _lamps[i]; if (l == null) continue;
                bool near = false; for (int j = 0; j < found; j++) if (_best[j] == i) { near = true; break; }
                var want = near ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want) l.shadows = want;
            }
        }
    }
}
