using UnityEngine;
using Ward0.Props;

namespace Ward0.Gameplay
{
    /// <summary>Lets the player open / close a blast door. Opening is loud, so patrols can hear it.</summary>
    [RequireComponent(typeof(QuarantineDoor))]
    public class DoorInteractable : Interactable
    {
        [SerializeField] private bool locked;
        private QuarantineDoor _door;
        private void Awake() { _door = GetComponent<QuarantineDoor>(); }
        public override string Prompt => locked ? GameText.PromptDoorLocked : (_door != null && _door.IsOpen ? GameText.PromptDoorClose : GameText.PromptDoorOpen);
        public void SetLocked(bool l) { locked = l; }
        public override void OnInteract(PlayerController player)
        {
            if (locked) { if (ShiftManager.Instance != null) ShiftManager.Instance.Say(GameText.DoorLocked, 2.5f); return; }
            if (_door == null || _door.IsMoving) return;
            _door.Toggle();
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), transform.position + Vector3.up, 0.8f, 25f, 0.6f);
            NoiseEvents.Emit(transform.position, 9f);
        }
    }
}
