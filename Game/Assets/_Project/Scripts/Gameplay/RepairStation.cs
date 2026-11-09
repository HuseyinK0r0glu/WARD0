using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// A breaker or valve that must be repaired by HOLDING E (it is loud and takes time). Finishing it vents a steam
    /// burst; while the player is blinded, the corridor behind them is swapped.
    /// </summary>
    public class RepairStation : Interactable
    {
        [SerializeField] private string stationName = "Breaker";
        [SerializeField] private float repairSeconds = 7f;
        [SerializeField] private MineNode node;
        [SerializeField] private SteamBurst steam;
        [SerializeField] private Transform spinner;
        [SerializeField] private Vector3 spinAxis = Vector3.forward;
        [SerializeField] private float spinDegrees = 720f;
        [SerializeField] private Renderer[] ledsToGreen;
        [SerializeField] private Material greenMat;

        private float _progress; private bool _done, _swapped; private PlayerController _player; private float _clankT; private Quaternion _spinRest;

        public bool IsDone => _done;
        public string StationName => stationName;
        public override bool CanInteract => !_done;
        public override float HoldSeconds => _done ? 0f : repairSeconds;
        public override string Prompt => _done ? stationName + " repaired" : "Hold E: repair " + stationName;

        private void Awake() { if (spinner != null) _spinRest = spinner.localRotation; if (steam != null) steam.Peak += DoSwap; }

        public override void OnHoldProgress(float t01)
        {
            _progress = t01;
            if (spinner != null) spinner.localRotation = _spinRest * Quaternion.AngleAxis(t01 * spinDegrees, spinAxis);
            _clankT -= Time.deltaTime;
            if (_clankT <= 0f) { _clankT = Random.Range(0.7f, 1.2f); ProceduralAudio.PlayAt(ProceduralAudio.Clank(), transform.position, 0.8f, 30f, Random.Range(0.8f, 1.2f)); NoiseEvents.Emit(transform.position, 14f); }
        }

        public override void OnHoldCancel() { }

        public override void OnInteract(PlayerController player)
        {
            if (_done) return;
            _done = true; _player = player;
            foreach (var r in ledsToGreen) if (r != null && greenMat != null) r.sharedMaterial = greenMat;
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), transform.position, 1f, 30f, 0.7f);
            if (steam != null) steam.Play(); else DoSwap();
            if (ShiftManager.Instance != null) ShiftManager.Instance.OnRepairDone(this);
        }

        private void DoSwap()
        {
            if (_swapped || _player == null) return; _swapped = true;
            var map = MineMap.Instance; if (map == null || node == null) return;
            var blocked = map.BlockRear(node, _player.transform.position, _player.transform.forward);
            if (blocked != null) ProceduralAudio.PlayAt(ProceduralAudio.Rumble(), blocked.Midpoint + Vector3.up, 1f, 40f);
            if (ShiftManager.Instance != null) ShiftManager.Instance.OnCorridorSwapped(blocked);
        }
    }
}
