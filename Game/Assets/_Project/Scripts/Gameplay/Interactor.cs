using UnityEngine;
using UnityEngine.InputSystem;

namespace Ward0.Gameplay
{
    /// <summary>Casts from the camera each frame, tracks the focused interactable and handles press / hold input.</summary>
    public class Interactor : MonoBehaviour
    {
        [SerializeField] private float range = 2.8f;
        [SerializeField] private PlayerController player;

        private Interactable _focus, _holding;
        private float _holdT;

        public Interactable Focus => _focus;
        public float HoldProgress => _holding != null && _holding.HoldSeconds > 0f ? Mathf.Clamp01(_holdT / _holding.HoldSeconds) : 0f;

        private void Awake() { if (player == null) player = GetComponentInParent<PlayerController>(); }

        private void Update()
        {
            if (player != null && player.Frozen) { CancelHold(); _focus = null; return; }
            _focus = null;
            if (Physics.Raycast(transform.position, transform.forward, out var hit, range, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                var it = hit.collider.GetComponentInParent<Interactable>();
                if (it != null && it.CanInteract) _focus = it;
            }
            var kb = Keyboard.current; if (kb == null) return;
            if (_holding != null)
            {
                if (_focus != _holding || !kb.eKey.isPressed) { CancelHold(); }
                else
                {
                    _holdT += Time.deltaTime; _holding.OnHoldProgress(Mathf.Clamp01(_holdT / _holding.HoldSeconds));
                    if (_holdT >= _holding.HoldSeconds) { var h = _holding; _holding = null; _holdT = 0f; h.OnInteract(player); }
                }
                return;
            }
            if (_focus != null && kb.eKey.wasPressedThisFrame)
            {
                if (_focus.HoldSeconds > 0f) { _holding = _focus; _holdT = 0f; }
                else _focus.OnInteract(player);
            }
        }

        private void CancelHold() { if (_holding != null) _holding.OnHoldCancel(); _holding = null; _holdT = 0f; }
    }
}
