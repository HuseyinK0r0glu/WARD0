using UnityEngine;

namespace Ward0.Gameplay
{
    public class ElevatorButton : Interactable
    {
        [SerializeField] private ElevatorCar car;
        public override string Prompt => "E: call elevator";
        public override void OnInteract(PlayerController player)
        {
            ProceduralAudio.PlayAt(ProceduralAudio.Beep(), transform.position, 0.6f);
            if (ShiftManager.Instance != null) ShiftManager.Instance.RequestElevator(car);
        }
    }
}
