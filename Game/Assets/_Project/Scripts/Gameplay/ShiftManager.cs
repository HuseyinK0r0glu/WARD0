using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Ward0.Gameplay
{
    public enum ShiftPhase { Triage, Descent, Mine, Return, Complete, Failed }

    /// <summary>
    /// Runs the whole shift: triage miners upstairs -> facility breakdown -> descend and repair three systems while
    /// patrols hunt you -> return to the elevator. Also owns win / fail handling.
    /// </summary>
    public class ShiftManager : MonoBehaviour
    {
        public static ShiftManager Instance { get; private set; }

        [Header("Scene references")]
        [SerializeField] private PlayerController player;
        [SerializeField] private DeskController desk;
        [SerializeField] private MinerSubject minerPrefab;
        [SerializeField] private Transform minerSpawn;
        [SerializeField] private Transform cellGlass;
        [SerializeField] private ElevatorSystem elevator;
        [SerializeField] private EnemyDirector director;
        [SerializeField] private HudOverlay hud;
        [SerializeField] private ParticleSystem incineratorFire;
        [SerializeField] private Light incineratorLight;
        [SerializeField] private Light[] alarmLights;
        [SerializeField] private Light[] boothLights;

        [Header("Tuning (difficulty)")]
        [SerializeField] private int minersToProcess = 3;
        [SerializeField] private int repairsNeeded = 3;
        [SerializeField] private float mineSeconds = 600f;
        [SerializeField] private bool[] infectedPattern = { true, false, true, false, true };

        public ShiftPhase Phase { get; private set; } = ShiftPhase.Triage;
        public float TimeLeft { get; private set; }
        public int Repairs { get; private set; }
        public int RepairsNeeded => repairsNeeded;
        public int MinersProcessed { get; private set; }
        public int MinersToProcess => minersToProcess;
        public string FailReason { get; private set; }
        public MinerSubject CurrentMiner { get; private set; }
        public float ElapsedMine { get; private set; }

        private bool _busy; private float _alarmT;
        private int _objKey = -1; private string _objText = "";

        private void Awake() { Instance = this; Time.timeScale = 1f; }

        private void Start()
        {
            if (hud != null) hud.SetFade(1f);
            StartCoroutine(Intro());
        }

        private IEnumerator Intro()
        {
            if (hud != null) yield return hud.Fade(0f, 2.2f);
            Say(GameText.Intro, 6f, true, HudOverlay.Tone.Info);
            yield return new WaitForSeconds(2.5f);
            SpawnNextMiner();
        }

        /// <summary>Current objective line for the HUD (cached, rebuilt only when the phase or progress changes).</summary>
        public string ObjectiveText
        {
            get
            {
                int key = ((int)Phase * 32 + MinersProcessed) * 32 + Repairs;
                if (key != _objKey) { _objKey = key; _objText = GameText.Objective(Phase, MinersProcessed, minersToProcess, Repairs, repairsNeeded); }
                return _objText;
            }
        }

        private void Update()
        {
            if (Phase == ShiftPhase.Mine || Phase == ShiftPhase.Return)
            {
                ElapsedMine += Time.deltaTime;
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                if (TimeLeft <= 0f) Fail(FailCause.Reactor);
                if (SanitySystem.Instance != null && SanitySystem.Instance.Effective <= 0.5f) Fail(FailCause.Mind);
            }
            if (Phase == ShiftPhase.Descent && alarmLights != null)
            {
                _alarmT += Time.deltaTime; float k = 0.5f + 0.5f * Mathf.Sin(_alarmT * 6f);
                foreach (var l in alarmLights) if (l != null) { l.enabled = true; l.intensity = 3f * k; }
            }
            if ((Phase == ShiftPhase.Failed || Phase == ShiftPhase.Complete) && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Queue a routine on-screen message.</summary>
        public void Say(string msg, float seconds = 4f) { if (hud != null) hud.Say(msg, seconds); }
        /// <summary>Queue an on-screen message; important ones are never overwritten or dropped in favour of routine ones.</summary>
        public void Say(string msg, float seconds, bool important, HudOverlay.Tone tone = HudOverlay.Tone.Info) { if (hud != null) hud.Say(msg, seconds, important, tone); }

        // ------------------------------------------------------------ triage
        private void SpawnNextMiner()
        {
            if (Phase != ShiftPhase.Triage || minerPrefab == null) return;
            bool infected = infectedPattern[MinersProcessed % infectedPattern.Length];
            CurrentMiner = Instantiate(minerPrefab, minerSpawn.position, minerSpawn.rotation);
            CurrentMiner.Setup(infected, cellGlass);
            if (desk != null) desk.SetSubject(CurrentMiner);
            _busy = false;
            Say(GameText.MinerArrives(MinersProcessed + 1, minersToProcess), 4f);
        }

        public bool CanSubmitVerdict => Phase == ShiftPhase.Triage && CurrentMiner != null && !_busy && CurrentMiner.IsReady;

        /// <summary>Tell the player why no verdict can be given right now (no miner, miner still arriving, triage over).</summary>
        public void ExplainNoVerdict()
        {
            if (Phase != ShiftPhase.Triage) Say(GameText.TriageDone, 2.5f);
            else if (CurrentMiner == null) Say(GameText.NoMinerInCell, 2.5f);
            else Say(GameText.MinerNotReady, 2.5f);
        }

        public void SubmitVerdict(bool incinerate)
        {
            if (!CanSubmitVerdict) { ExplainNoVerdict(); return; }
            _busy = true; var m = CurrentMiner; CurrentMiner = null; if (desk != null) desk.SetSubject(null);
            StartCoroutine(VerdictRoutine(m, incinerate));
        }

        private IEnumerator VerdictRoutine(MinerSubject m, bool incinerate)
        {
            if (!incinerate)
            {
                if (m.Infected)
                {
                    Say(GameText.VerdictClear, 1.2f, true);
                    yield return new WaitForSeconds(1.0f);
                    yield return m.Ambush();
                    Fail(FailCause.ClearedInfected);
                    yield break;
                }
                Say(GameText.ClearHealthy, 3f, true);
                yield return m.Release();
            }
            else
            {
                Say(GameText.VerdictIncinerate, 1.5f, true);
                if (incineratorFire != null) incineratorFire.Play();
                if (incineratorLight != null) incineratorLight.enabled = true;
                ProceduralAudio.PlayAt(ProceduralAudio.Whoosh(), minerSpawn.position, 1f, 30f, 0.6f);
                yield return m.Burn();
                if (incineratorFire != null) incineratorFire.Stop();
                if (incineratorLight != null) incineratorLight.enabled = false;
                if (!m.Infected)
                {
                    if (SanitySystem.Instance != null) SanitySystem.Instance.PunishInnocentIncineration();
                    Say(GameText.IncinerateHealthy, 5f, true, HudOverlay.Tone.Danger);
                    if (hud != null) hud.Flash(new Color(0.5f, 0f, 0f), 0.6f);
                }
                else Say(GameText.IncinerateInfected, 3f, true, HudOverlay.Tone.Good);
            }
            MinersProcessed++;
            yield return new WaitForSeconds(2.5f);
            if (MinersProcessed >= minersToProcess) StartBreakdown();
            else SpawnNextMiner();
        }

        private void StartBreakdown()
        {
            Phase = ShiftPhase.Descent; _busy = false;
            ProceduralAudio.Play2D(ProceduralAudio.Alarm(), 0.5f);
            StartCoroutine(AlarmLoop());
            Say(GameText.Breakdown(repairsNeeded), 8f, true, HudOverlay.Tone.Danger);
            if (hud != null) hud.Flash(new Color(0.6f, 0.05f, 0.02f), 0.5f);
        }

        private IEnumerator AlarmLoop()
        {
            while (Phase == ShiftPhase.Descent) { ProceduralAudio.Play2D(ProceduralAudio.Alarm(), 0.25f); yield return new WaitForSeconds(2.2f); }
            if (alarmLights != null) foreach (var l in alarmLights) if (l != null) l.enabled = false;
        }

        // ------------------------------------------------------------ elevator
        public void RequestElevator(ElevatorCar car)
        {
            if (_busy || elevator == null) return;
            if (car.IsUpper)
            {
                if (Phase == ShiftPhase.Triage) { Say(GameText.ElevatorLockedTriage(minersToProcess - MinersProcessed), 4f, false, HudOverlay.Tone.Warn); return; }
                if (Phase != ShiftPhase.Descent) { Say(GameText.ElevatorNotNeeded, 2.5f, false, HudOverlay.Tone.Warn); return; }
                StartCoroutine(RideRoutine(car, ShiftPhase.Mine));
            }
            else
            {
                if (Phase == ShiftPhase.Return) StartCoroutine(RideRoutine(car, ShiftPhase.Complete));
                else Say(GameText.ElevatorLockedRepairs(repairsNeeded - Repairs), 4f, false, HudOverlay.Tone.Warn);
            }
        }

        private IEnumerator RideRoutine(ElevatorCar from, ShiftPhase next)
        {
            _busy = true;
            yield return elevator.Ride(from, hud, player);
            if (next == ShiftPhase.Mine)
            {
                Phase = ShiftPhase.Mine; TimeLeft = mineSeconds;
                if (director != null) director.StartPatrols(1);
                Say(GameText.MineArrive(repairsNeeded), 8f, true);
            }
            else
            {
                Phase = ShiftPhase.Complete;
                if (director != null) director.StopPatrols();
                if (SanitySystem.Instance != null) SanitySystem.Instance.ClearTemporaryDecay();
                if (hud != null) hud.ShowEnd(true, GameText.WinTitle, BuildSummary(), GameText.WinNext);
                player.Frozen = true;
            }
            _busy = false;
        }

        private string BuildSummary()
        {
            var s = SanitySystem.Instance;
            return GameText.WinBody(FormatTime(ElapsedMine), s != null ? Mathf.RoundToInt(s.Baseline) : 100, MinersProcessed);
        }

        public static string FormatTime(float t) { int m = Mathf.FloorToInt(t / 60f), s = Mathf.FloorToInt(t % 60f); return m.ToString("0") + ":" + s.ToString("00"); }

        // ------------------------------------------------------------ mine events
        public void OnRepairDone(RepairStation st)
        {
            Repairs++;
            if (director != null) director.SetMax(Mathf.Min(3, 1 + Repairs));
            if (Repairs >= repairsNeeded)
            {
                Phase = ShiftPhase.Return;
                Say(GameText.AllRepaired, 7f, true, HudOverlay.Tone.Good);
            }
            else Say(GameText.RepairDone(st.StationName, repairsNeeded - Repairs), 5f, true, HudOverlay.Tone.Good);
        }

        public void OnCorridorSwapped(MineCorridor c)
        {
            if (c != null) Say(GameText.CorridorCollapsed, 5f, true, HudOverlay.Tone.Warn);
        }

        public void PlayerCaught(PatrolEnemy e)
        {
            if (Phase == ShiftPhase.Failed) return;
            player.Frozen = true;
            e.Lunge(player.transform.position);
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), e.transform.position, 1f, 25f, 0.8f);
            Fail(FailCause.Patrol);
        }

        /// <summary>End the shift in failure with a custom explanation (generic result screen).</summary>
        public void Fail(string reason) { Fail(FailCause.Other, reason); }

        /// <summary>End the shift in failure; the result screen text comes from <see cref="GameText"/>.</summary>
        public void Fail(FailCause cause) { Fail(cause, GameText.FailBody(cause)); }

        private void Fail(FailCause cause, string reason)
        {
            if (Phase == ShiftPhase.Failed) return;
            Phase = ShiftPhase.Failed; FailReason = reason;
            player.Frozen = true;
            if (director != null) director.Pause();
            if (hud != null) hud.ShowEnd(false, GameText.FailTitle(cause), reason, GameText.FailNext(cause));
        }
    }
}
