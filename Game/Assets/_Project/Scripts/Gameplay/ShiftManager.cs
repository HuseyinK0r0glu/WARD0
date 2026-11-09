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

        private void Awake() { Instance = this; Time.timeScale = 1f; }

        private void Start()
        {
            if (hud != null) hud.SetFade(1f);
            StartCoroutine(Intro());
        }

        private IEnumerator Intro()
        {
            if (hud != null) yield return hud.Fade(0f, 2.2f);
            Say("SHIFT 07 // Evaluate each miner behind the glass.", 5f);
            yield return new WaitForSeconds(2.5f);
            SpawnNextMiner();
        }

        public string ObjectiveText
        {
            get
            {
                switch (Phase)
                {
                    case ShiftPhase.Triage: return "Triage: " + MinersProcessed + " / " + minersToProcess + " miners processed";
                    case ShiftPhase.Descent: return "Take the elevator down";
                    case ShiftPhase.Mine: return "Repair systems: " + Repairs + " / " + repairsNeeded;
                    case ShiftPhase.Return: return "Return to the elevator";
                    case ShiftPhase.Complete: return "Shift complete";
                    default: return "";
                }
            }
        }

        private void Update()
        {
            if (Phase == ShiftPhase.Mine || Phase == ShiftPhase.Return)
            {
                ElapsedMine += Time.deltaTime;
                TimeLeft = Mathf.Max(0f, TimeLeft - Time.deltaTime);
                if (TimeLeft <= 0f) Fail("The reactor failed before you finished. The mine is lost.");
                if (SanitySystem.Instance != null && SanitySystem.Instance.Effective <= 0.5f) Fail("Your mind broke in the dark.");
            }
            if (Phase == ShiftPhase.Descent && alarmLights != null)
            {
                _alarmT += Time.deltaTime; float k = 0.5f + 0.5f * Mathf.Sin(_alarmT * 6f);
                foreach (var l in alarmLights) if (l != null) { l.enabled = true; l.intensity = 3f * k; }
            }
            if ((Phase == ShiftPhase.Failed || Phase == ShiftPhase.Complete) && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Say(string msg, float seconds = 4f) { if (hud != null) hud.Say(msg, seconds); }

        // ------------------------------------------------------------ triage
        private void SpawnNextMiner()
        {
            if (Phase != ShiftPhase.Triage || minerPrefab == null) return;
            bool infected = infectedPattern[MinersProcessed % infectedPattern.Length];
            CurrentMiner = Instantiate(minerPrefab, minerSpawn.position, minerSpawn.rotation);
            CurrentMiner.Setup(infected, cellGlass);
            if (desk != null) desk.SetSubject(CurrentMiner);
            _busy = false;
            Say("A miner enters the cell. Use the scanners, then decide.", 4f);
        }

        public bool CanSubmitVerdict => Phase == ShiftPhase.Triage && CurrentMiner != null && !_busy && CurrentMiner.IsReady;

        public void SubmitVerdict(bool incinerate)
        {
            if (!CanSubmitVerdict) { Say("No miner awaiting a verdict.", 2f); return; }
            _busy = true; var m = CurrentMiner; CurrentMiner = null; if (desk != null) desk.SetSubject(null);
            StartCoroutine(VerdictRoutine(m, incinerate));
        }

        private IEnumerator VerdictRoutine(MinerSubject m, bool incinerate)
        {
            if (!incinerate)
            {
                if (m.Infected)
                {
                    Say("CLEARED.", 1.2f);
                    yield return new WaitForSeconds(1.0f);
                    yield return m.Ambush();
                    Fail("You cleared an infected miner. It was the last thing you saw.");
                    yield break;
                }
                Say("Cleared. The miner is escorted out.", 3f);
                yield return m.Release();
            }
            else
            {
                Say("INCINERATING.", 1.5f);
                if (incineratorFire != null) incineratorFire.Play();
                if (incineratorLight != null) incineratorLight.enabled = true;
                ProceduralAudio.PlayAt(ProceduralAudio.Whoosh(), minerSpawn.position, 1f, 30f, 0.6f);
                yield return m.Burn();
                if (incineratorFire != null) incineratorFire.Stop();
                if (incineratorLight != null) incineratorLight.enabled = false;
                if (!m.Infected)
                {
                    if (SanitySystem.Instance != null) SanitySystem.Instance.PunishInnocentIncineration();
                    Say("The miner was healthy. You will carry that.", 4.5f);
                    if (hud != null) hud.Flash(new Color(0.5f, 0f, 0f), 0.6f);
                }
                else Say("Infected. Threat removed.", 3f);
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
            Say("FACILITY BREAKDOWN. Take the elevator down and restore " + repairsNeeded + " systems.", 7f);
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
                if (Phase == ShiftPhase.Triage) { Say("The elevator is locked until triage is done.", 3f); return; }
                if (Phase != ShiftPhase.Descent) { Say("Nothing left to do down there.", 2.5f); return; }
                StartCoroutine(RideRoutine(car, ShiftPhase.Mine));
            }
            else
            {
                if (Phase == ShiftPhase.Return) StartCoroutine(RideRoutine(car, ShiftPhase.Complete));
                else Say("Systems are still failing. " + (repairsNeeded - Repairs) + " repair(s) remain.", 3.5f);
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
                Say("Restore " + repairsNeeded + " systems before the reactor fails. Stay quiet. Stay out of sight.", 7f);
            }
            else
            {
                Phase = ShiftPhase.Complete;
                if (director != null) director.StopPatrols();
                if (SanitySystem.Instance != null) SanitySystem.Instance.ClearTemporaryDecay();
                if (hud != null) hud.ShowEnd(true, BuildSummary());
                player.Frozen = true;
            }
            _busy = false;
        }

        private string BuildSummary()
        {
            var s = SanitySystem.Instance;
            return "Time in the mine: " + FormatTime(ElapsedMine) + "\nSanity baseline: " + (s != null ? Mathf.RoundToInt(s.Baseline) : 100) + "%\nMiners processed: " + MinersProcessed;
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
                Say("All systems stabilised. Get back to the elevator!", 6f);
            }
            else Say(st.StationName + " restored. " + (repairsNeeded - Repairs) + " to go. The patrols are getting restless.", 5f);
        }

        public void OnCorridorSwapped(MineCorridor c)
        {
            if (c != null) Say("The tunnel behind you has collapsed...", 4.5f);
        }

        public void PlayerCaught(PatrolEnemy e)
        {
            if (Phase == ShiftPhase.Failed) return;
            player.Frozen = true;
            e.Lunge(player.transform.position);
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), e.transform.position, 1f, 25f, 0.8f);
            Fail("A patrol found you.");
        }

        public void Fail(string reason)
        {
            if (Phase == ShiftPhase.Failed) return;
            Phase = ShiftPhase.Failed; FailReason = reason;
            player.Frozen = true;
            if (director != null) director.Pause();
            if (hud != null) hud.ShowEnd(false, reason);
        }
    }
}
