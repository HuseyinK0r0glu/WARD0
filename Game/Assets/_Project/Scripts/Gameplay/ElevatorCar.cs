using UnityEngine;
using Ward0.Props;

namespace Ward0.Gameplay
{
    /// <summary>One end of the elevator (upper checkpoint or lower mine landing). The call button is an interactable that asks the ShiftManager for permission.</summary>
    public class ElevatorCar : MonoBehaviour
    {
        [SerializeField] private bool isUpper;
        [SerializeField] private Transform standPoint;
        [SerializeField] private QuarantineDoor door;

        public bool IsUpper => isUpper;
        public Transform StandPoint => standPoint;
        public QuarantineDoor Door => door;
    }
}
