using System;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Loud actions (repairs, clangs) broadcast here so patrols can investigate.</summary>
    public static class NoiseEvents
    {
        public static event Action<Vector3, float> Emitted;
        public static void Emit(Vector3 pos, float radius) => Emitted?.Invoke(pos, radius);
    }
}
