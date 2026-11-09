using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Graph of chambers and corridors. Drives corridor swapping (never trapping the player) and enemy patrol routes.</summary>
    public class MineMap : MonoBehaviour
    {
        public static MineMap Instance { get; private set; }

        [SerializeField] private MineNode[] nodes;
        [SerializeField] private MineCorridor[] corridors;
        [SerializeField] private MineNode landing;

        public IReadOnlyList<MineNode> Nodes => nodes;
        public IReadOnlyList<MineCorridor> Corridors => corridors;
        public MineNode Landing => landing;

        private void Awake() { Instance = this; }

        public MineNode Nearest(Vector3 p)
        {
            MineNode best = null; float bd = float.MaxValue;
            foreach (var n in nodes) { var d = Flat(n.FloorPoint - p).sqrMagnitude; if (d < bd) { bd = d; best = n; } }
            return best;
        }

        public IEnumerable<MineCorridor> CorridorsOf(MineNode n)
        {
            foreach (var c in corridors) if (c.a == n || c.b == n) yield return c;
        }

        private static Vector3 Flat(Vector3 v) { v.y = 0; return v; }

        public bool Connected(MineNode from, MineNode to, MineCorridor extraBlocked = null)
        {
            var seen = new HashSet<MineNode> { from }; var q = new Queue<MineNode>(); q.Enqueue(from);
            while (q.Count > 0)
            {
                var n = q.Dequeue(); if (n == to) return true;
                foreach (var c in CorridorsOf(n))
                {
                    if (c.Blocked || c == extraBlocked) continue;
                    var o = c.Other(n); if (seen.Add(o)) q.Enqueue(o);
                }
            }
            return false;
        }

        /// <summary>Seals the corridor that is most "behind" the player, as long as the station can still reach the landing.</summary>
        public MineCorridor BlockRear(MineNode station, Vector3 playerPos, Vector3 playerForward)
        {
            var cands = new List<MineCorridor>();
            foreach (var c in CorridorsOf(station)) if (!c.Blocked) cands.Add(c);
            Vector3 fwd = Flat(playerForward).normalized;
            cands.Sort((x, y) => Score(x, station, fwd, playerPos).CompareTo(Score(y, station, fwd, playerPos)));
            foreach (var c in cands)
                if (Connected(station, landing, c)) { c.Block(); return c; }
            return null;
        }

        private float Score(MineCorridor c, MineNode station, Vector3 fwd, Vector3 pos)
        {
            var dir = Flat(c.Other(station).FloorPoint - station.FloorPoint).normalized;
            return Vector3.Dot(fwd, dir);   // most negative = directly behind
        }
    }
}
