using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Keeps shadow-casting lamps to the few nearest the player so dozens of tunnel lamps stay cheap.</summary>
    public class LampBudget : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int maxShadowLamps = 4;
        [SerializeField] private float maxDistance = 22f;

        private readonly List<Light> _lamps = new List<Light>(); private float _t;

        private void Start()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Point && l.shadows != LightShadows.None) _lamps.Add(l);
        }

        private void Update()
        {
            _t -= Time.deltaTime; if (_t > 0f || player == null) return; _t = 0.4f;
            _lamps.Sort((a, b) => (a.transform.position - player.position).sqrMagnitude.CompareTo((b.transform.position - player.position).sqrMagnitude));
            for (int i = 0; i < _lamps.Count; i++)
            {
                var l = _lamps[i]; if (l == null) continue;
                bool near = i < maxShadowLamps && (l.transform.position - player.position).sqrMagnitude < maxDistance * maxDistance;
                var want = near ? LightShadows.Soft : LightShadows.None;
                if (l.shadows != want) l.shadows = want;
                l.enabled = l.enabled; // keep state
            }
        }
    }
}
