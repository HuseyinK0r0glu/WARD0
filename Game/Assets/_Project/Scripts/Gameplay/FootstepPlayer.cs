using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Plays the player's footsteps: detects the floor surface with one cached downward raycast, picks a non-repeating clip
    /// (FootstepProfile recordings if assigned, otherwise FootstepSynth) and plays it from a small AudioSource pool.
    /// Loudness follows PlayerController.NoiseRadius so what patrols hear matches what the player hears.
    /// </summary>
    [DisallowMultipleComponent]
    public class FootstepPlayer : MonoBehaviour
    {
        [Header("Clips")]
        [Tooltip("Optional recordings. If empty, Resources/FootstepProfile is tried, then the synthesised footsteps are used.")]
        [SerializeField] private FootstepProfile profile;

        [Header("Detection")]
        [SerializeField] private float rayLength = 1.3f;
        [Tooltip("Floors above this world height are treated as metal when no material name matches.")]
        [SerializeField] private float metalAboveY = 15f;

        [Header("Output")]
        [Tooltip("Noise radius (m) that plays at full volume (the sprint radius).")]
        [SerializeField] private float loudRadius = 11f;
        [Tooltip("Curve from noise radius to volume; below 1 keeps quiet steps audible without lifting sprint.")]
        [SerializeField] private float loudnessExponent = 0.8f;
        [SerializeField, Range(1, 8)] private int voices = 4;

        private const int SurfaceCount = 3, MaxCacheEntries = 256;
        private AudioSource[] _pool;
        private int _next;
        private readonly int[] _last = { -1, -1, -1 };
        private readonly Dictionary<Collider, Surface> _cache = new Dictionary<Collider, Surface>(64);
        private Surface _surface;
        private int _fallbackIdx;

        /// <summary>Surface found under the player on the most recent step.</summary>
        public Surface CurrentSurface => _surface;

        private void Awake()
        {
            if (profile == null) profile = Resources.Load<FootstepProfile>("FootstepProfile");
            var go = new GameObject("FootstepVoices"); go.transform.SetParent(transform, false); go.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            _pool = new AudioSource[voices];
            for (int i = 0; i < voices; i++)
            {
                var a = go.AddComponent<AudioSource>();
                a.playOnAwake = false; a.spatialBlend = 1f; a.spread = 160f; a.dopplerLevel = 0f; a.priority = 100;
                a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 1.5f; a.maxDistance = 18f;
                _pool[i] = a;
            }
        }

        /// <summary>Plays one footstep for the surface under the player. noiseRadius is PlayerController.NoiseRadius.</summary>
        public void Step(FootstepStance stance, float noiseRadius)
        {
            _surface = Detect();
            var clip = PickClip(_surface, stance);
            if (clip == null) return;
            var set = profile != null ? profile.GetStance(stance) : FootstepProfile.Defaults(stance);
            float vol = Mathf.Pow(Mathf.Clamp01(noiseRadius / loudRadius), loudnessExponent) * UnityEngine.Random.Range(set.volumeMin, set.volumeMax);
            var src = NextVoice();
            src.clip = clip; src.volume = Mathf.Clamp01(vol); src.pitch = UnityEngine.Random.Range(set.pitchMin, set.pitchMax);
            src.Play();
        }

        private AudioSource NextVoice()
        {
            for (int i = 0; i < _pool.Length; i++) { var a = _pool[(_next + i) % _pool.Length]; if (!a.isPlaying) { _next = (_next + i + 1) % _pool.Length; return a; } }
            var s = _pool[_next]; _next = (_next + 1) % _pool.Length; return s;
        }

        private AudioClip PickClip(Surface surface, FootstepStance stance)
        {
            int si = (int)surface;
            var set = profile != null ? profile.GetClips(surface) : null;
            if (set != null && set.Length > 0)
            {
                int idx = PickIndex(set.Length, _last[si]);
                if (set[idx] != null) { _last[si] = idx; return set[idx]; }
            }
            int v = PickIndex(FootstepSynth.VariantCount, _last[si]);
            _last[si] = v;
            var clip = FootstepSynth.Get(surface, v, stance);
            return clip != null ? clip : ProceduralAudio.Step(_fallbackIdx++);
        }

        // Random index that never equals the previous one (when there is more than one to choose from).
        private static int PickIndex(int count, int last)
        {
            if (count <= 1) return 0;
            int i = UnityEngine.Random.Range(0, count - 1);
            return i >= last && last >= 0 ? i + 1 : i;
        }

        private Surface Detect()
        {
            if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var hit, rayLength, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return _surface;
            var col = hit.collider;
            if (_cache.TryGetValue(col, out var s)) return s;
            s = Classify(col, hit.point.y);
            if (_cache.Count >= MaxCacheEntries) _cache.Clear();
            _cache[col] = s;
            return s;
        }

        // Runs once per collider: material name first (Geo_<Material> meshes carry their material), then object name, then height.
        private Surface Classify(Collider col, float y)
        {
            var r = col.GetComponent<Renderer>(); if (r == null) r = col.GetComponentInParent<Renderer>();
            var mat = r != null ? r.sharedMaterial : null;
            if (mat != null && Match(mat.name, out var s)) return s;
            if (Match(col.name, out s)) return s;
            return y > metalAboveY ? Surface.Metal : Surface.Gravel;
        }

        private static bool Match(string name, out Surface surface)
        {
            surface = Surface.Gravel;
            if (string.IsNullOrEmpty(name)) return false;
            if (Has(name, "Puddle") || Has(name, "Water")) { surface = Surface.Puddle; return true; }
            if (Has(name, "Mine") || Has(name, "Rock") || Has(name, "Gravel") || Has(name, "Dirt") || Has(name, "Wood")) { surface = Surface.Gravel; return true; }
            if (Has(name, "Bunker") || Has(name, "Steel") || Has(name, "Metal") || Has(name, "Rust") || Has(name, "Grat") || Has(name, "Plate") || Has(name, "Iron")) { surface = Surface.Metal; return true; }
            return false;
        }

        private static bool Has(string s, string part) => s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
