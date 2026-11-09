using UnityEngine;
using UnityEngine.InputSystem;

namespace Ward0.Gameplay
{
    /// <summary>Anything the player can point at and press (or hold) E on.</summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected string prompt = "Interact";
        public virtual string Prompt => prompt;
        public virtual bool CanInteract => enabled;
        /// <summary>0 = press, >0 = hold for that many seconds.</summary>
        public virtual float HoldSeconds => 0f;
        public abstract void OnInteract(PlayerController player);
        public virtual void OnHoldProgress(float t01) { }
        public virtual void OnHoldCancel() { }
    }
}
