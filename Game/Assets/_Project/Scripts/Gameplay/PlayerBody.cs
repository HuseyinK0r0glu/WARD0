using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Lets the player see their own character: V swings the camera out behind the technician (spring-arm with wall collision) and shows
    /// a body that walks, crouches and jumps with the player. The body is loaded from Resources the first time it is needed, so a normal
    /// first-person run pays nothing for it. Purely visual: no colliders, nothing the AI can see.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerBody : MonoBehaviour
    {
        private const string PrefabName = "TechnicianFigure";

        [SerializeField] private Vector3 cameraOffset = new Vector3(0.45f, 0.35f, -2.5f);
        [SerializeField] private float blendSeconds = 0.35f;
        [SerializeField] private float cameraRadius = 0.22f;

        private PlayerController _player; private Transform _camT, _camRoot; private Vector3 _camBase;
        private GameObject _body; private float _blend; private bool _third;
        private Transform _hips, _armL, _armR, _legL, _legR, _kneeL, _kneeR, _foreL, _foreR, _spine;
        private Vector3 _hipsBase, _lastPos; private float _phase, _gait, _crouch;

        /// <summary>True while the third-person view is on (or still easing in or out).</summary>
        public bool ThirdPerson => _third || _blend > 0.001f;

        private void Awake() { _player = GetComponent<PlayerController>(); _lastPos = transform.position; }

        /// <summary>Switches between first and third person.</summary>
        public void Toggle()
        {
            _third = !_third;
            if (_third && _body == null) CreateBody();
            if (_third && _camT == null) FindCamera();
        }

        private void FindCamera()
        {
            _camRoot = _player.CameraRoot; if (_camRoot == null) return;
            var cam = _camRoot.GetComponentInChildren<Camera>(); _camT = cam != null ? cam.transform : null;
            if (_camT != null && _camT != _camRoot) _camBase = _camT.localPosition;
        }

        private void CreateBody()
        {
            var prefab = Resources.Load<GameObject>(PrefabName); if (prefab == null) { _third = false; return; }
            _body = Instantiate(prefab, transform); _body.name = "PlayerBody"; _body.transform.localPosition = Vector3.zero; _body.transform.localRotation = Quaternion.identity;
            SetLayer(_body.transform, gameObject.layer);
            var look = _body.GetComponent<MinerVariant>(); if (look != null) look.Apply(false, 0);
            _hips = Bone("Hips"); _spine = Bone("Spine"); _armL = Bone("LeftArm"); _armR = Bone("RightArm"); _foreL = Bone("LeftForeArm"); _foreR = Bone("RightForeArm");
            _legL = Bone("LeftUpLeg"); _legR = Bone("RightUpLeg"); _kneeL = Bone("LeftLeg"); _kneeR = Bone("RightLeg");
            if (_hips != null) _hipsBase = _hips.localPosition;
            _body.SetActive(false);
        }

        private static void SetLayer(Transform t, int layer) { t.gameObject.layer = layer; foreach (Transform c in t) SetLayer(c, layer); }

        private Transform Bone(string n)
        {
            foreach (var t in _body.GetComponentsInChildren<Transform>(true)) if (t.name == "mixamorig:" + n) return t;
            return null;
        }

        private void LateUpdate()
        {
            _blend = Mathf.MoveTowards(_blend, _third ? 1f : 0f, Time.unscaledDeltaTime / Mathf.Max(0.05f, blendSeconds));
            if (_body != null && _body.activeSelf != (_blend > 0.001f)) _body.SetActive(_blend > 0.001f);
            if (_camT == null && _blend > 0f) FindCamera();
            if (_camT != null)
            {
                float k = Mathf.SmoothStep(0f, 1f, _blend); var want = cameraOffset * k; float len = want.magnitude;
                if (len > 0.01f && _camRoot != null)
                {
                    var origin = _camRoot.position; var dir = _camRoot.TransformDirection(want / len);
                    if (Physics.SphereCast(origin, cameraRadius, dir, out var hit, len + cameraRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                        want = want / len * Mathf.Max(0f, hit.distance - cameraRadius);
                }
                // the camera may be the camera root itself (PlayerController has just placed it this frame): then add the offset to that
                _camT.localPosition = (_camT == _camRoot ? _camT.localPosition : _camBase) + want;
            }
            if (_body != null && _body.activeSelf) Animate();
            _lastPos = transform.position;
        }

        private void Animate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            var d = transform.position - _lastPos; d.y = 0f; float speed = Mathf.Min(6f, d.magnitude / dt);
            _gait = Mathf.MoveTowards(_gait, Mathf.Clamp01(speed / 2.7f), dt * 6f);
            _phase += speed * dt * 2.85f;
            _crouch = Mathf.MoveTowards(_crouch, _player.IsCrouched ? 1f : 0f, dt * 5f);
            float s = Mathf.Sin(_phase), c = Mathf.Cos(_phase), g = _gait, air = _player.IsAirborne ? 1f : 0f;
            if (_armL != null) _armL.localRotation = Quaternion.Euler(Mathf.Lerp(s * 28f * g + 4f, -35f, air), 0f, 8f + air * 18f);
            if (_armR != null) _armR.localRotation = Quaternion.Euler(Mathf.Lerp(-s * 28f * g + 4f, -35f, air), 0f, -8f - air * 18f);
            if (_foreL != null) _foreL.localRotation = Quaternion.Euler(-12f - Mathf.Max(0f, -s) * 20f * g - air * 30f, 0f, 0f);
            if (_foreR != null) _foreR.localRotation = Quaternion.Euler(-12f - Mathf.Max(0f, s) * 20f * g - air * 30f, 0f, 0f);
            float thighL = Mathf.Lerp(-s * 30f * g, -35f, air) - _crouch * 50f, thighR = Mathf.Lerp(s * 30f * g, 10f, air) - _crouch * 50f;
            if (_legL != null) _legL.localRotation = Quaternion.Euler(thighL, 0f, 0f);
            if (_legR != null) _legR.localRotation = Quaternion.Euler(thighR, 0f, 0f);
            if (_kneeL != null) _kneeL.localRotation = Quaternion.Euler(Mathf.Max(0f, c) * 42f * g + 4f + air * 55f + _crouch * 95f, 0f, 0f);
            if (_kneeR != null) _kneeR.localRotation = Quaternion.Euler(Mathf.Max(0f, -c) * 42f * g + 4f + air * 20f + _crouch * 95f, 0f, 0f);
            if (_spine != null) _spine.localRotation = Quaternion.Euler(4f + g * 4f + _crouch * 22f, -s * 5f * g, 0f);
            if (_hips != null) _hips.localPosition = _hipsBase + Vector3.up * (-0.02f - Mathf.Abs(s) * 0.03f * g - _crouch * 0.42f);
        }
    }
}
