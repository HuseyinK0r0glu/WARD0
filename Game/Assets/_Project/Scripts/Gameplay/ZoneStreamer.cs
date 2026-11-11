using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Streams the mine in and out as additive scenes, one per chamber (with the tunnel segments nearest to it), so only
    /// the part of the level the player can reach is in memory. Nothing in the mine is loaded while the player is in the
    /// checkpoint: the landing and its neighbours are prepared during the breakdown, and the elevator waits (behind its
    /// fade) until they are ready. In the mine the chamber the player is in and its neighbours stay loaded; the rest unload.
    /// Loads and unloads run one at a time at a low integration budget so no single frame pays for a whole zone.
    /// </summary>
    public class ZoneStreamer : MonoBehaviour
    {
        public static ZoneStreamer Instance { get; private set; }
        /// <summary>Raised after a zone finished loading or unloading (light caches refresh from this).</summary>
        public static event Action ZonesChanged;

        private enum State { Unloaded, Loading, Loaded, Unloading }

        [Serializable] private class Zone { public string sceneName; public MineNode node; [NonSerialized] public State state; [NonSerialized] public float lastWanted; }

        [SerializeField] private Zone[] zones;
        [SerializeField] private PlayerController player;
        [SerializeField] private MineMap map;
        [Tooltip("Seconds a zone stays loaded after the player leaves its neighbourhood (stops thrash at borders).")]
        [SerializeField] private float unloadDelay = 8f;
        [Tooltip("Player Y below this means the player is in the mine.")]
        [SerializeField] private float mineCeilingY = 15f;
        [SerializeField] private float switchMargin = 2f;

        private readonly List<Zone> _wanted = new List<Zone>(8);
        private MineNode _anchor; private float _t; private bool _busy; private bool _hurry;

        /// <summary>True while a load or unload is in flight.</summary>
        public bool Busy => _busy;

        private void Awake()
        {
            Instance = this; Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
            foreach (var z in zones) { z.state = IsSceneLoaded(z.sceneName) ? State.Loaded : State.Unloaded; z.lastWanted = Time.time; }
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private static bool IsSceneLoaded(string n) { var s = SceneManager.GetSceneByName(n); return s.IsValid() && s.isLoaded; }

        private Zone Of(MineNode n) { foreach (var z in zones) if (z.node == n) return z; return null; }

        /// <summary>Is the zone that owns this chamber in memory and active?</summary>
        public bool IsLoaded(MineNode n) { var z = Of(n); return z == null || z.state == State.Loaded; }

        /// <summary>Are this chamber and every neighbour it connects to loaded (what the player needs on arrival)?</summary>
        public bool IsReady(MineNode n)
        {
            if (!IsLoaded(n)) return false;
            foreach (var c in map.CorridorsOf(n)) if (!c.Blocked && !IsLoaded(c.Other(n))) return false;
            return true;
        }

        /// <summary>Loads fast (uncapped integration time) until <see cref="IsReady"/>; use behind a black fade.</summary>
        public IEnumerator PrepareLanding()
        {
            var landing = map.Landing; if (landing == null) yield break;
            _hurry = true; Application.backgroundLoadingPriority = ThreadPriority.High;
            _anchor = landing; Tick();
            float guard = 0f;
            while (!IsReady(landing) && guard < 20f) { guard += Time.unscaledDeltaTime; Tick(); yield return null; }
            _hurry = false; Application.backgroundLoadingPriority = ThreadPriority.BelowNormal;
        }

        private void Update()
        {
            _t -= Time.unscaledDeltaTime; if (_t > 0f && !_hurry) return; _t = 0.25f;
            Tick();
        }

        private void Tick()
        {
            if (zones == null || map == null || player == null) return;
            var sm = ShiftManager.Instance; var phase = sm != null ? sm.Phase : ShiftPhase.Triage;
            bool inMine = player.transform.position.y < mineCeilingY;
            if (inMine) UpdateAnchor(); else if (!_hurry) _anchor = phase == ShiftPhase.Descent ? map.Landing : null;

            _wanted.Clear();
            if (_anchor != null)
            {
                Add(_anchor);
                foreach (var c in map.CorridorsOf(_anchor)) if (!c.Blocked) Add(c.Other(_anchor));
            }
            foreach (var z in _wanted) z.lastWanted = Time.time;

            if (_busy) return;
            // one load at a time, nearest to the player first (the anchor was added first)
            foreach (var z in _wanted) if (z.state == State.Unloaded) { StartCoroutine(Load(z)); return; }
            // one unload at a time, only after the grace period
            foreach (var z in zones)
                if (z.state == State.Loaded && !_wanted.Contains(z) && Time.time - z.lastWanted > unloadDelay) { StartCoroutine(Unload(z)); return; }
        }

        private void Add(MineNode n) { var z = Of(n); if (z != null && !_wanted.Contains(z)) _wanted.Add(z); }

        /// <summary>Nearest chamber to the player, with a margin so standing on a border does not flip-flop.</summary>
        private void UpdateAnchor()
        {
            var near = map.Nearest(player.transform.position);
            if (_anchor == null || near == _anchor) { _anchor = near; return; }
            var p = player.transform.position; p.y = 0f;
            float dNew = (near.FloorPoint - p).magnitude, dOld = (_anchor.FloorPoint - p).magnitude;
            if (dNew + switchMargin < dOld) _anchor = near;
        }

        private IEnumerator Load(Zone z)
        {
            _busy = true; z.state = State.Loading;
            var op = SceneManager.LoadSceneAsync(z.sceneName, LoadSceneMode.Additive);
            if (op == null) { z.state = State.Unloaded; _busy = false; yield break; }
            yield return op;
            z.state = State.Loaded; z.lastWanted = Time.time; _busy = false;
            ZonesChanged?.Invoke();
        }

        private IEnumerator Unload(Zone z)
        {
            _busy = true; z.state = State.Unloading;
            var op = SceneManager.UnloadSceneAsync(z.sceneName);
            if (op != null) yield return op;
            z.state = State.Unloaded; _busy = false;
            ZonesChanged?.Invoke();
            bool any = false; foreach (var o in zones) if (o.state != State.Unloaded) any = true;
            if (!any) Resources.UnloadUnusedAssets();   // back upstairs: release mine textures and meshes
        }
    }
}
