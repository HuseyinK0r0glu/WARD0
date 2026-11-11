using UnityEngine;
using UnityEngine.InputSystem;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Contextual step-by-step guidance. Reads the real game state (desk power / scan / analysis, shift phase, patrols, gas)
    /// and exposes ONE short current instruction plus the world positions the HUD should point at: the desk controls to
    /// use next, and the elevator after the breakdown. Pure data - <see cref="HudOverlay"/> draws it. H toggles hints
    /// (on by default). Created at runtime by the HUD, so the scene needs no extra wiring.
    /// </summary>
    public class HintDirector : MonoBehaviour
    {
        private enum HintId { None, Power, Scan, Analyse, Analysing, Decide, Descent, Return, MineFirst, MineNext, Repairing, Detected, Hidden, Gas }

        private const float TickSeconds = 0.1f;
        private const float MineHintSeconds = 45f;

        /// <summary>Hints are on by default; the choice survives a scene reload (restart).</summary>
        public static bool HintsEnabled { get; private set; } = true;

        /// <summary>Current instruction, empty when there is nothing to say or hints are off.</summary>
        public string Text { get; private set; } = "";
        /// <summary>Step badge such as "2/4" for the scanner flow, empty otherwise.</summary>
        public string StepLabel { get; private set; } = "";
        /// <summary>Colour tone of the hint plate.</summary>
        public HudOverlay.Tone Tone { get; private set; }
        /// <summary>Time.unscaledTime when the hint last changed (the HUD pulses the plate).</summary>
        public float ChangedAt { get; private set; }
        /// <summary>Number of desk controls to highlight (0-2).</summary>
        public int ControlCount { get; private set; }
        /// <summary>True when the HUD should draw the elevator waypoint.</summary>
        public bool HasWaypoint { get; private set; }
        /// <summary>World position of the elevator waypoint.</summary>
        public Vector3 WaypointPos { get; private set; }

        private readonly Vector3[] _controlPos = new Vector3[2];
        private Interactor _interactor; private PlayerController _player;
        private DeskController _desk; private DeskToggle[] _toggles; private Collider[] _toggleCols; private DeskLever _lever; private Collider _leverCol;
        private Transform _upperPoint, _lowerPoint;
        private int _resolveTries; private float _resolveT, _tickT;
        private int _key = -1; private float _keyT;
        private float _detectUntil, _recentDetectUntil;

        /// <summary>Create the director next to the HUD. Call once.</summary>
        public static HintDirector Create(Interactor interactor, PlayerController player)
        {
            var d = new GameObject("Hint Director").AddComponent<HintDirector>();
            d._interactor = interactor; d._player = player;
            return d;
        }

        /// <summary>World position of highlighted desk control <paramref name="i"/> (valid for i &lt; ControlCount).</summary>
        public Vector3 ControlPos(int i) { return _controlPos[i]; }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb.hKey.wasPressedThisFrame)
            {
                HintsEnabled = !HintsEnabled; _key = -1;
                if (ShiftManager.Instance != null) ShiftManager.Instance.Say(HintsEnabled ? GameText.HintsOn : GameText.HintsOff, 2.5f);
            }
            _tickT -= Time.deltaTime; if (_tickT > 0f) return; _tickT = TickSeconds;
            if (_desk == null && _resolveTries < 6 && Time.unscaledTime >= _resolveT) Resolve();
            Evaluate();
        }

        /// <summary>One-time lookup of the desk controls and elevator cars (retried briefly if the scene is still waking up).</summary>
        private void Resolve()
        {
            _resolveTries++; _resolveT = Time.unscaledTime + 1f;
            _desk = FindFirstObjectByType<DeskController>();
            _toggles = FindObjectsByType<DeskToggle>(FindObjectsSortMode.None);
            _toggleCols = new Collider[_toggles.Length];
            for (int i = 0; i < _toggles.Length; i++) _toggleCols[i] = _toggles[i].GetComponent<Collider>();
            _lever = FindFirstObjectByType<DeskLever>(); if (_lever != null) _leverCol = _lever.GetComponent<Collider>();
            foreach (var car in FindObjectsByType<ElevatorCar>(FindObjectsSortMode.None))
            {
                var p = car.StandPoint != null ? car.StandPoint : car.transform;
                if (car.IsUpper) _upperPoint = p; else _lowerPoint = p;
            }
        }

        private void Evaluate()
        {
            var sm = ShiftManager.Instance; HintId id = HintId.None; int arg = 0; bool first = false;
            ControlCount = 0; HasWaypoint = false;
            if (sm != null && HintsEnabled)
            {
                float det = PatrolEnemy.MaxDetection, now = Time.time;
                switch (sm.Phase)
                {
                    case ShiftPhase.Triage: id = TriageStep(sm, out first); break;
                    case ShiftPhase.Descent: id = HintId.Descent; SetWaypoint(_upperPoint); break;
                    case ShiftPhase.Mine:
                    case ShiftPhase.Return:
                        if (det > 0.05f) { _detectUntil = now + 2f; _recentDetectUntil = now + 10f; }
                        bool ret = sm.Phase == ShiftPhase.Return;
                        if (ret) SetWaypoint(_lowerPoint);
                        if (now < _detectUntil) id = HintId.Detected;
                        else if (_player != null && _player.IsHidden && now < _recentDetectUntil) id = HintId.Hidden;
                        else if (SanitySystem.Instance != null && SanitySystem.Instance.InGas) id = HintId.Gas;
                        else if (_interactor != null && _interactor.HoldProgress > 0f && _interactor.Focus is RepairStation) id = HintId.Repairing;
                        else if (ret) id = HintId.Return;
                        else { id = sm.Repairs == 0 ? HintId.MineFirst : HintId.MineNext; arg = sm.RepairsNeeded - sm.Repairs; }
                        break;
                }
            }
            int key = ((int)id * 8 + arg) * 2 + (first ? 1 : 0);
            if (key != _key) { _key = key; _keyT = Time.time; ChangedAt = Time.unscaledTime; Rebuild(id, arg, first); }
            // reminders in the mine fade away after a while; they return when the situation changes
            if ((id == HintId.MineFirst || id == HintId.MineNext) && Time.time - _keyT > MineHintSeconds) Text = "";
        }

        private HintId TriageStep(ShiftManager sm, out bool first)
        {
            first = sm.MinersProcessed == 0;
            if (_desk == null || sm.CurrentMiner == null) return HintId.None;
            HintId id;
            if (!_desk.PowerOn) { id = HintId.Power; AddControl(DeskRole.Power); }
            else if (_desk.Analysing) id = HintId.Analysing;
            else if (_desk.Analysed) { id = HintId.Decide; AddControl(DeskRole.Clear); AddLever(); }
            else if (!_desk.ScanActive) { id = HintId.Scan; if (!_desk.XRayOn) AddControl(DeskRole.XRay); if (!_desk.BioscanOn) AddControl(DeskRole.Bioscan); }
            else { id = HintId.Analyse; AddControl(DeskRole.Analysis); }
            return id;
        }

        private void AddControl(DeskRole role)
        {
            if (_toggles == null || ControlCount >= _controlPos.Length) return;
            for (int i = 0; i < _toggles.Length; i++)
                if (_toggles[i] != null && _toggles[i].Role == role) { _controlPos[ControlCount++] = _toggleCols[i] != null ? _toggleCols[i].bounds.center : _toggles[i].transform.position; return; }
        }

        private void AddLever()
        {
            if (_lever == null || ControlCount >= _controlPos.Length) return;
            _controlPos[ControlCount++] = _leverCol != null ? _leverCol.bounds.center : _lever.transform.position;
        }

        private void SetWaypoint(Transform t) { if (t == null) return; HasWaypoint = true; WaypointPos = t.position + Vector3.up * 1.2f; }

        private void Rebuild(HintId id, int arg, bool first)
        {
            Tone = HudOverlay.Tone.Info; StepLabel = "";
            switch (id)
            {
                case HintId.Power: Text = first ? GameText.HintPower : GameText.HintPowerShort; StepLabel = GameText.StepLabel(1); break;
                case HintId.Scan: Text = first ? GameText.HintScan : GameText.HintScanShort; StepLabel = GameText.StepLabel(2); break;
                case HintId.Analyse: Text = first ? GameText.HintAnalyse : GameText.HintAnalyseShort; StepLabel = GameText.StepLabel(3); break;
                case HintId.Analysing: Text = GameText.HintAnalysing; StepLabel = GameText.StepLabel(3); break;
                case HintId.Decide: Text = first ? GameText.HintDecide : GameText.HintDecideShort; StepLabel = GameText.StepLabel(4); break;
                case HintId.Descent: Text = GameText.HintDescent; break;
                case HintId.Return: Text = GameText.HintReturn; break;
                case HintId.MineFirst: Text = GameText.HintMineFirst; break;
                case HintId.MineNext: Text = GameText.HintMineNext(arg); break;
                case HintId.Repairing: Text = GameText.HintRepairing; Tone = HudOverlay.Tone.Warn; break;
                case HintId.Detected: Text = GameText.HintDetected; Tone = HudOverlay.Tone.Danger; break;
                case HintId.Hidden: Text = GameText.HintHidden; Tone = HudOverlay.Tone.Good; break;
                case HintId.Gas: Text = GameText.HintGas; Tone = HudOverlay.Tone.Warn; break;
                default: Text = ""; break;
            }
        }
    }
}
