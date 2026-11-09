using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>A straight tunnel between two chambers. Can be sealed by a cave-in (the "gate") when the corridor swaps.</summary>
    public class MineCorridor : MonoBehaviour
    {
        public MineNode a, b;
        public GameObject gate;
        public bool Blocked { get; private set; }

        public MineNode Other(MineNode n) => n == a ? b : a;
        public Vector3 Midpoint => (a.FloorPoint + b.FloorPoint) * 0.5f;

        public void Block()
        {
            if (Blocked) return;
            Blocked = true;
            if (gate != null) gate.SetActive(true);
        }
    }
}
