using UnityEngine;
using UnityEngine.InputSystem;

namespace Ward0.Gameplay
{
    /// <summary>
    /// First-person technician. WASD move, Shift sprint (limited stamina, loud), Ctrl/C crouch (quiet, lets you hide),
    /// mouse look, E interact, F flashlight. Sanity adds sway and slows movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private Transform cameraRoot;
        [SerializeField] private float walkSpeed = 2.7f, sprintSpeed = 4.6f, crouchSpeed = 1.3f;
        [SerializeField] private float lookSensitivity = 0.075f;
        [SerializeField] private float standHeight = 1.75f, crouchHeight = 1.05f;
        [SerializeField] private float maxStamina = 5f;

        private CharacterController _cc;
        private float _yaw, _pitch, _vy, _stamina, _staminaLockout, _stepDist, _bob, _camY;
        private bool _crouchToggle;
        private int _stepIdx;

        public bool Frozen { get; set; }
        public bool IsCrouched { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsMoving { get; private set; }
        public bool IsHidden { get; set; }
        public float Stamina01 => _stamina / maxStamina;
        public Vector3 Head => cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f;
        public Transform CameraRoot => cameraRoot;
        /// <summary>How far away enemies can hear the player right now.</summary>
        public float NoiseRadius => !IsMoving ? 0.5f : (IsSprinting ? 11f : (IsCrouched ? 1.8f : 4.5f));

        private void Awake()
        {
            _cc = GetComponent<CharacterController>(); _stamina = maxStamina; _camY = standHeight - 0.15f;
            _yaw = transform.eulerAngles.y;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        public void Teleport(Vector3 pos, float yaw)
        {
            _cc.enabled = false; transform.position = pos; _yaw = yaw; transform.rotation = Quaternion.Euler(0, yaw, 0); _vy = 0f; _cc.enabled = true;
        }

        private void Update()
        {
            var kb = Keyboard.current; var mouse = Mouse.current;
            if (kb == null || mouse == null) return;
            if (kb.escapeKey.wasPressedThisFrame) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            if (Cursor.lockState != CursorLockMode.Locked && mouse.leftButton.wasPressedThisFrame && !Frozen) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }

            var san = SanitySystem.Instance; int tier = san != null ? san.Tier : 0;
            if (!Frozen && Cursor.lockState == CursorLockMode.Locked)
            {
                var md = mouse.delta.ReadValue() * lookSensitivity;
                _yaw += md.x; _pitch = Mathf.Clamp(_pitch - md.y, -85f, 85f);
            }
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);

            float x = 0, z = 0;
            if (!Frozen)
            {
                x = (kb.dKey.isPressed ? 1 : 0) - (kb.aKey.isPressed ? 1 : 0);
                z = (kb.wKey.isPressed ? 1 : 0) - (kb.sKey.isPressed ? 1 : 0);
                if (kb.cKey.wasPressedThisFrame) _crouchToggle = !_crouchToggle;
                bool wantCrouch = kb.leftCtrlKey.isPressed || _crouchToggle;
                if (wantCrouch != IsCrouched)
                {
                    if (wantCrouch || CanStand()) IsCrouched = wantCrouch;
                }
                if (IsCrouched == false) _crouchToggle = false;
            }
            var input = new Vector3(x, 0, z); if (input.sqrMagnitude > 1f) input.Normalize();
            IsMoving = input.sqrMagnitude > 0.01f && !Frozen;

            bool wantSprint = !Frozen && kb.leftShiftKey.isPressed && z > 0.1f && !IsCrouched && _staminaLockout <= 0f && _stamina > 0.05f;
            IsSprinting = wantSprint && IsMoving;
            if (IsSprinting) { _stamina -= Time.deltaTime; if (_stamina <= 0f) { _stamina = 0f; _staminaLockout = 2.2f; } }
            else { _staminaLockout -= Time.deltaTime; _stamina = Mathf.Min(maxStamina, _stamina + Time.deltaTime * (IsMoving ? 0.5f : 1.1f)); }

            float speed = IsCrouched ? crouchSpeed : (IsSprinting ? sprintSpeed : walkSpeed);
            if (tier >= 2) speed *= tier >= 3 ? 0.72f : 0.88f;
            if (san != null && san.InGas) speed *= 0.85f;
            var move = transform.TransformDirection(input) * speed;
            if (_cc.isGrounded) _vy = -1f; else _vy -= 18f * Time.deltaTime;
            move.y = _vy;
            if (_cc.enabled) _cc.Move(move * Time.deltaTime);

            // crouch height + camera
            float targetH = IsCrouched ? crouchHeight : standHeight;
            _cc.height = Mathf.MoveTowards(_cc.height, targetH, Time.deltaTime * 5f); _cc.center = new Vector3(0, _cc.height / 2f, 0);
            float targetCam = targetH - 0.15f; _camY = Mathf.Lerp(_camY, targetCam, Time.deltaTime * 10f);

            // head bob, sanity sway, footsteps
            Vector3 flat = new Vector3(_cc.velocity.x, 0, _cc.velocity.z);
            float spd = flat.magnitude; _bob += spd * Time.deltaTime * 2.4f;
            float bobY = Mathf.Sin(_bob * 2f) * 0.035f * Mathf.Clamp01(spd / walkSpeed), bobX = Mathf.Cos(_bob) * 0.02f * Mathf.Clamp01(spd / walkSpeed);
            float sway = tier >= 1 ? (tier * 0.6f) : 0f; float swayRoll = Mathf.Sin(Time.time * 0.8f) * sway * 0.8f, swayPitch = Mathf.Sin(Time.time * 1.3f + 1f) * sway * 0.5f;
            if (tier >= 3 && Random.value < 0.0025f) _pitch += Random.Range(-4f, 4f);
            if (cameraRoot != null)
            {
                cameraRoot.localPosition = new Vector3(bobX, _camY + bobY, 0f);
                cameraRoot.localRotation = Quaternion.Euler(_pitch + swayPitch, 0f, swayRoll);
            }
            if (IsMoving && _cc.isGrounded && spd > 0.3f)
            {
                _stepDist += spd * Time.deltaTime; float stride = IsSprinting ? 1.5f : (IsCrouched ? 1.1f : 1.25f);
                if (_stepDist >= stride) { _stepDist = 0f; ProceduralAudio.PlayAt(ProceduralAudio.Step(_stepIdx++), transform.position, IsSprinting ? 0.9f : (IsCrouched ? 0.18f : 0.5f), 18f, Random.Range(0.9f, 1.1f)); }
            }
        }

        private bool CanStand() => !Physics.SphereCast(transform.position + Vector3.up * 0.4f, 0.28f, Vector3.up, out _, standHeight - 0.5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
    }
}
