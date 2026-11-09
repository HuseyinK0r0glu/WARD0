using System.Collections;
using UnityEngine;
using Ward0.Props;

namespace Ward0.Gameplay
{
    public enum DeskRole { XRay, Bioscan, Analysis, Log, Power, Clear }

    /// <summary>
    /// Brains of the triage desk: power, X-ray / bioscan screens that show the current miner's scan (anomalous if infected),
    /// the analysis run that swings the gauge needles, and the Clear / Incinerate verdict controls.
    /// </summary>
    public class DeskController : MonoBehaviour
    {
        [SerializeField] private QuarantineDesk desk;
        [SerializeField] private Texture xrayHealthy, xrayInfected, bioHealthy, bioInfected;
        [SerializeField] private Transform needleRadiation, needleAir, needleContainment;

        private Renderer _xray, _bio; private Material _xrayMat, _bioMat;
        private bool _power, _xrayOn, _bioOn, _analysing, _analysed;
        private MinerSubject _subject;
        private Quaternion _nr, _na, _nc; private float _needleK; private float _leverLock;

        private void Start()
        {
            _xray = desk.XRayScreen; _bio = desk.BioscanScreen;
            _xrayMat = _xray.material; _bioMat = _bio.material;
            _nr = needleRadiation.localRotation; _na = needleAir.localRotation; _nc = needleContainment.localRotation;
            for (int i = 0; i < 8; i++) desk.SetStatusLed(i, false);
            Refresh();
        }

        public void SetSubject(MinerSubject s) { _subject = s; _analysed = false; _analysing = false; if (_power) { desk.SetStatusLed(5, false); desk.SetStatusLed(6, false); desk.SetStatusLed(7, false); } Refresh(); }

        public void OnToggle(DeskRole role, bool on)
        {
            switch (role)
            {
                case DeskRole.Power: _power = on; desk.SetStatusLed(4, on); if (!on) { _xrayOn = _bioOn = false; } break;
                case DeskRole.XRay: _xrayOn = on; if (on && _power) desk.SetStatusLed(5, true); break;
                case DeskRole.Bioscan: _bioOn = on; if (on && _power) desk.SetStatusLed(5, true); break;
                case DeskRole.Analysis: if (on && _power && _subject != null && !_analysing) StartCoroutine(Analyse()); break;
                case DeskRole.Log: if (ShiftManager.Instance != null) ShiftManager.Instance.Say(_subject == null ? "Log: no subject." : "Log: subject " + (_analysed ? "analysed" : "not yet analysed") + ".", 3f); break;
                case DeskRole.Clear: if (ShiftManager.Instance != null) ShiftManager.Instance.SubmitVerdict(false); break;
            }
            Refresh();
        }

        public bool PowerOn => _power;

        public void PullLever(System.Action onPulled)
        {
            if (_leverLock > Time.time) return; _leverLock = Time.time + 1.5f;
            StartCoroutine(LeverRoutine(onPulled));
        }

        private IEnumerator LeverRoutine(System.Action onPulled)
        {
            float t = 0f;
            while (t < 0.4f) { t += Time.deltaTime; desk.SetLeverPull(t / 0.4f); yield return null; }
            onPulled?.Invoke();
            yield return new WaitForSeconds(0.6f);
            while (t > 0f) { t -= Time.deltaTime * 1.5f; desk.SetLeverPull(Mathf.Max(0f, t / 0.4f)); yield return null; }
            desk.SetLeverPull(0f);
        }

        private IEnumerator Analyse()
        {
            _analysing = true; desk.SetStatusLed(6, true);
            ProceduralAudio.PlayAt(ProceduralAudio.Beep(), transform.position, 0.5f);
            yield return new WaitForSeconds(3.2f);
            if (_subject != null) { _analysed = true; desk.SetStatusLed(7, true); ProceduralAudio.PlayAt(ProceduralAudio.Beep(), transform.position, 0.6f, 20f, 1.4f); }
            _analysing = false; desk.SetStatusLed(6, false);
        }

        private void Refresh()
        {
            bool show = _power && _subject != null;
            Apply(_xrayMat, show && _xrayOn, _subject != null && _subject.Infected ? xrayInfected : xrayHealthy);
            Apply(_bioMat, show && _bioOn, _subject != null && _subject.Infected ? bioInfected : bioHealthy);
        }

        private static void Apply(Material m, bool on, Texture tex)
        {
            if (tex != null) { m.SetTexture("_BaseMap", tex); m.SetTexture("_EmissionMap", tex); }
            m.SetColor("_EmissionColor", on ? Color.white * 1.5f : Color.black);
            m.SetColor("_BaseColor", on ? Color.white * 0.2f : Color.black);
        }

        private void Update()
        {
            // gauge needles: rest by default, swing into the red when an infected miner has been analysed
            float target = (_analysed && _subject != null && _subject.Infected) ? 1f : 0f;
            _needleK = Mathf.MoveTowards(_needleK, target, Time.deltaTime * 0.6f);
            float jitter = (_needleK > 0.5f) ? Mathf.Sin(Time.time * 31f) * 2.5f : 0f;
            needleRadiation.localRotation = _nr * Quaternion.Euler(0, 0, -_needleK * 78f + jitter);
            needleAir.localRotation = _na * Quaternion.Euler(0, 0, -_needleK * 52f - jitter);
            needleContainment.localRotation = _nc * Quaternion.Euler(0, 0, -_needleK * 55f + jitter);
            if (_analysed && _subject != null && !_subject.Infected) { }
        }
    }
}
