using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>A chamber (junction) in the mine graph. Its transform position is the chamber centre on the floor.</summary>
    public class MineNode : MonoBehaviour
    {
        public string label = "Chamber";
        public bool isLanding;
        public Vector3 FloorPoint => transform.position;
    }
}
