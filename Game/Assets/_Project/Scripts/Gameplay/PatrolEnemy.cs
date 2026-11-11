using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Delusional miner. No NavMesh: it walks the mine graph centre-to-centre along the straight corridors, pauses to
    /// look around in chambers, and spots the player by field of view + line of sight. Light, crouching, cover and
    /// noise change how fast the detection meter fills. A full meter is fatal.
    /// </summary>
    public class PatrolEnemy : MonoBehaviour
    {
        public static float MaxDetection { get; private set; }
        private static readonly List<PatrolEnemy> All = new List<PatrolEnemy>();

        [SerializeField] private float walkSpeed = 1.9f;
        [SerializeField] private float viewRange = 13f;
        [SerializeField] private float halfFov = 52f;
        [SerializeField] private float detectSeconds = 1.25f;
        [SerializeField] private float eyeHeight = 1.65f;
        [SerializeField] private Transform armL, armR, bodyVisual;
        [Header("Rig (optional, for the walk cycle)")]
        [SerializeField] private Transform hips, chest, head, legL, legR, kneeL, kneeR, forearmL, forearmR;

        private MineMap _map; private PlayerController _player; private MineNode _from, _to; private int _turn;
        private float _pause, _detect, _look, _growlCool, _unseenFor, _noticed, _anim;
        private bool _caught; private float _gait; private Vector3 _hipsBase;
        private Vector3 _heading = Vector3.forward;

        public float Detection => _detect;
        public bool Caught => _caught;
        public float UnseenFor => _unseenFor;

        public void Init(MineMap map, PlayerController player, MineNode start, int turnSeed)
        {
            _map = map; _player = player; _from = start; _turn = turnSeed; transform.position = start.FloorPoint;
            _to = null; _pause = 0.3f; _detect = 0f;
            var look = GetComponent<MinerVariant>(); if (look != null) look.Apply(true, turnSeed);
        }

        private void Awake() { if (hips != null) _hipsBase = hips.localPosition; }

        private void OnEnable() { All.Add(this); NoiseEvents.Emitted += OnNoise; }
        private void OnDisable() { All.Remove(this); NoiseEvents.Emitted -= OnNoise; if (All.Count == 0) MaxDetection = 0f; }

        private void OnNoise(Vector3 pos, float radius)
        {
            if (_caught) return;
            if (Vector3.Distance(pos, transform.position) < radius) { _noticed = 3f; _heading = Flat(pos - transform.position).normalized; _detect = Mathf.Max(_detect, 0.25f); }
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        private void Update()
        {
            if (_map == null || _player == null || _caught) return;
            Move(); Detect(); Animate();
            float m = 0f; foreach (var e in All) if (e != null) m = Mathf.Max(m, e._detect);
            MaxDetection = m;
        }

        private void Move()
        {
            if (_noticed > 0f) { _noticed -= Time.deltaTime; if (_pause < 0.2f) _pause = 0.2f; }
            if (_pause > 0f)
            {
                _pause -= Time.deltaTime; _look += Time.deltaTime;
                var sweep = Quaternion.Euler(0, Mathf.Sin(_look * 1.2f) * 55f, 0) * _heading;
                transform.forward = Vector3.Slerp(transform.forward, sweep, Time.deltaTime * 4f);
                if (_pause <= 0f) PickNext();
                return;
            }
            if (_to == null) { PickNext(); return; }
            var target = _to.FloorPoint; var dir = Flat(target - transform.position);
            if (dir.magnitude < 0.25f) { _from = _to; _to = null; _pause = Random.Range(1.2f, 2.4f); _look = 0f; return; }
            _heading = dir.normalized;
            transform.forward = Vector3.Slerp(transform.forward, _heading, Time.deltaTime * 6f);
            float sp = walkSpeed * (1f + 0.35f * Mathf.Clamp01(_detect));
            transform.position += _heading * sp * Time.deltaTime;
        }

        private void PickNext()
        {
            var opts = new List<MineCorridor>();
            foreach (var c in _map.CorridorsOf(_from)) if (!c.Blocked) opts.Add(c);
            if (opts.Count == 0) { _pause = 2f; return; }
            var fwd = Flat(_heading).normalized; var good = new List<MineCorridor>();
            foreach (var c in opts)
            {
                var d = Flat(c.Other(_from).FloorPoint - _from.FloorPoint).normalized;
                if (opts.Count == 1 || Vector3.Dot(d, fwd) > -0.7f) good.Add(c);
            }
            if (good.Count == 0) good = opts;
            var pick = good[(_turn++) % good.Count];
            _to = pick.Other(_from);
        }

        private void Detect()
        {
            var eye = transform.position + Vector3.up * eyeHeight; var head = _player.Head; var chest = _player.transform.position + Vector3.up * 0.7f;
            var toP = head - eye; float dist = toP.magnitude; bool seen = false;
            bool hidden = _player.IsHidden && dist > 1.8f;
            if (dist < viewRange && !hidden)
            {
                float ang = Vector3.Angle(transform.forward, Flat(toP));
                if (ang < halfFov && (Visible(eye, head) || Visible(eye, chest))) seen = true;
            }
            if (seen)
            {
                float lit = Mathf.Lerp(0.55f, 1.5f, LightMeter.Level);
                float near = Mathf.Lerp(2.2f, 0.75f, dist / viewRange);
                float stance = _player.IsCrouched ? 0.65f : (_player.IsSprinting ? 1.25f : 1f);
                _detect += Time.deltaTime / detectSeconds * lit * near * stance;
                _unseenFor = 0f;
                if (_detect > 0.3f && _growlCool <= 0f) { _growlCool = 6f; ProceduralAudio.PlayAt(ProceduralAudio.Growl(), transform.position, 0.8f, 25f, Random.Range(0.85f, 1.1f)); }
            }
            else
            {
                _unseenFor += Time.deltaTime;
                if (dist < _player.NoiseRadius && !hidden) { _detect += 0.35f * Time.deltaTime; _heading = Flat(toP).normalized; _noticed = 1.5f; }
                else _detect = Mathf.Max(0f, _detect - Time.deltaTime * 0.45f);
            }
            _growlCool -= Time.deltaTime;
            if (_detect >= 1f) { _detect = 1f; _caught = true; if (ShiftManager.Instance != null) ShiftManager.Instance.PlayerCaught(this); }
        }

        private bool Visible(Vector3 from, Vector3 to)
        {
            if (Physics.Linecast(from, to, out var hit, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() != null;
            return true;
        }

        private void Animate()
        {
            bool walking = _pause <= 0f && _to != null;
            _gait = Mathf.MoveTowards(_gait, walking ? 1f : 0f, Time.deltaTime * 3.5f);
            _anim += Time.deltaTime * Mathf.Lerp(1.3f, 4.6f, _gait);
            Pose(_anim, _gait);
        }

        /// <summary>Stooped, long-armed shamble driven straight onto the bones: leg swing, knee flex, counter-swinging arms, hip bob, head sway. gait 0 = idle, 1 = walking.</summary>
        public void Pose(float phase, float gait)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase), sl = Mathf.Sin(phase + Mathf.PI), cl = Mathf.Cos(phase + Mathf.PI);
            float n = Mathf.PerlinNoise(phase * 0.35f, 3.1f) - 0.5f;
            if (armL != null) armL.localRotation = Quaternion.Euler(-26f + s * 24f * gait + n * 8f, 0, 10f);
            if (armR != null) armR.localRotation = Quaternion.Euler(-30f - s * 24f * gait - n * 8f, 0, -10f);
            if (forearmL != null) forearmL.localRotation = Quaternion.Euler(-24f - Mathf.Max(0f, s) * 14f * gait, 0, 0);
            if (forearmR != null) forearmR.localRotation = Quaternion.Euler(-30f - Mathf.Max(0f, -s) * 14f * gait, 0, 0);
            if (bodyVisual != null) bodyVisual.localRotation = Quaternion.Euler(6f + Mathf.Sin(phase * 2f) * 2.5f * gait + n * 4f, s * 7f * gait, s * 3f * gait + Mathf.Sin(phase * 0.5f) * 3f);
            if (chest != null) chest.localRotation = Quaternion.Euler(5f, -s * 6f * gait, -s * 2f * gait);
            if (head != null) head.localRotation = Quaternion.Euler(-4f + Mathf.Sin(phase * 1.7f) * 3f * gait, Mathf.Sin(phase * 0.5f) * (10f + 14f * (1f - gait)), Mathf.PerlinNoise(phase * 0.9f, 7.7f) * 22f - 11f);
            if (legL != null) legL.localRotation = Quaternion.Euler(-s * 27f * gait, 0, 0);
            if (legR != null) legR.localRotation = Quaternion.Euler(-sl * 27f * gait, 0, 0);
            if (kneeL != null) kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * 42f * gait + 5f, 0, 0);
            if (kneeR != null) kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, cl) * 42f * gait + 5f, 0, 0);
            if (hips != null)
            {
                hips.localRotation = Quaternion.Euler(0, s * 7f * gait, -s * 3f * gait);
                hips.localPosition = _hipsBase + Vector3.up * (-0.025f - Mathf.Abs(s) * 0.035f * gait);
            }
        }

        /// <summary>Snaps the enemy in front of the player for the kill sequence.</summary>
        public void Lunge(Vector3 playerPos)
        {
            var dir = Flat(transform.position - playerPos).normalized; if (dir.sqrMagnitude < 0.01f) dir = -transform.forward;
            transform.position = new Vector3(playerPos.x, transform.position.y, playerPos.z) + dir * 0.9f; transform.forward = -dir;
        }
    }
}
