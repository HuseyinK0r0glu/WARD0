using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Trigger volume behind cover (minecart, crates). A crouched player inside is invisible to patrols unless they come very close.</summary>
    [RequireComponent(typeof(Collider))]
    public class HideZone : MonoBehaviour
    {
        private PlayerController _inside;

        private void Reset() { var c = GetComponent<Collider>(); c.isTrigger = true; }

        private void OnTriggerStay(Collider other)
        {
            var p = other.GetComponentInParent<PlayerController>(); if (p == null) return;
            _inside = p; p.IsHidden = p.IsCrouched;
        }

        private void OnTriggerExit(Collider other)
        {
            var p = other.GetComponentInParent<PlayerController>(); if (p == null) return;
            p.IsHidden = false; _inside = null;
        }

        private void OnDisable() { if (_inside != null) _inside.IsHidden = false; }
    }
}
