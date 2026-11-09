using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Toxic neurotoxin pocket: while the player stands inside, temporary sanity decay accumulates.</summary>
    [RequireComponent(typeof(Collider))]
    public class GasZone : MonoBehaviour
    {
        private bool _in;

        private void Reset() { GetComponent<Collider>().isTrigger = true; }

        private void OnTriggerEnter(Collider other)
        {
            if (_in || other.GetComponentInParent<PlayerController>() == null) return;
            _in = true; if (SanitySystem.Instance != null) SanitySystem.Instance.EnterGas();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!_in || other.GetComponentInParent<PlayerController>() == null) return;
            _in = false; if (SanitySystem.Instance != null) SanitySystem.Instance.ExitGas();
        }

        private void OnDisable() { if (_in && SanitySystem.Instance != null) SanitySystem.Instance.ExitGas(); _in = false; }
    }
}
