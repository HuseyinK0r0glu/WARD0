using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Spawns and despawns patrols out of the player's field of view. More patrols appear as repairs complete.</summary>
    public class EnemyDirector : MonoBehaviour
    {
        [SerializeField] private PatrolEnemy enemyPrefab;
        [SerializeField] private MineMap map;
        [SerializeField] private PlayerController player;
        [SerializeField] private float spawnInterval = 14f;
        [SerializeField] private float minSpawnDistance = 24f;
        [SerializeField] private float despawnDistance = 48f;

        private readonly List<PatrolEnemy> _active = new List<PatrolEnemy>();
        private int _maxActive; private float _timer = 4f; private int _seq;
        public bool Running { get; private set; }
        public int ActiveCount => _active.Count;

        public void SetMax(int n) { _maxActive = n; }
        public void StartPatrols(int initialMax) { Running = true; _maxActive = initialMax; _timer = 35f; }
        public void Pause() { Running = false; }
        public void StopPatrols() { Running = false; foreach (var e in _active) if (e != null) Destroy(e.gameObject); _active.Clear(); }

        private void Update()
        {
            if (!Running || map == null || player == null || enemyPrefab == null) return;
            _active.RemoveAll(e => e == null);
            _timer -= Time.deltaTime;
            if (_active.Count < _maxActive && _timer <= 0f) { TrySpawn(); _timer = spawnInterval * Random.Range(0.7f, 1.2f); }

            var cam = Camera.main;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var e = _active[i]; if (e == null || e.Caught) continue;
                float d = Vector3.Distance(e.transform.position, player.transform.position);
                bool visible = cam != null && InFrustum(cam, e.transform.position + Vector3.up);
                if (d > despawnDistance || (!visible && d > 26f && e.UnseenFor > 18f)) { Destroy(e.gameObject); _active.RemoveAt(i); }
            }
        }

        private void TrySpawn()
        {
            var cam = Camera.main; var cands = new List<MineNode>();
            foreach (var n in map.Nodes)
            {
                if (n.isLanding) continue;
                float d = Vector3.Distance(n.FloorPoint, player.transform.position);
                if (d < minSpawnDistance) continue;
                if (cam != null && InFrustum(cam, n.FloorPoint + Vector3.up) && !Physics.Linecast(cam.transform.position, n.FloorPoint + Vector3.up * 1.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                cands.Add(n);
            }
            if (cands.Count == 0) return;
            var node = cands[(_seq * 7 + Random.Range(0, cands.Count)) % cands.Count];
            var e = Instantiate(enemyPrefab, node.FloorPoint, Quaternion.identity);
            e.Init(map, player, node, _seq++);
            _active.Add(e);
        }

        private static bool InFrustum(Camera cam, Vector3 p)
        {
            var v = cam.WorldToViewportPoint(p); return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.05f && v.y < 1.05f;
        }
    }
}
