using UnityEngine;
using UnityEngine.InputSystem;

namespace Ward0.Gameplay
{
    /// <summary>Hand torch with a limited battery. Toggle with F. Lit areas make the player easier for patrols to spot.</summary>
    public class FlashlightController : MonoBehaviour
    {
        [SerializeField] private Light beam;
        [SerializeField] private float batterySeconds = 150f;
        [SerializeField] private float startSeconds = 100f;

        private float _charge; private bool _on; private float _flickerT;
        public bool IsOn => _on && _charge > 0f;
        public float Charge01 => _charge / batterySeconds;
        public float ChargeSeconds => _charge;

        private void Awake() { _charge = startSeconds; if (beam != null) beam.enabled = false; }

        public void AddBattery(float seconds) { _charge = Mathf.Min(batterySeconds, _charge + seconds); }

        private void Update()
        {
            var kb = Keyboard.current; if (kb == null) return;
            if (kb.fKey.wasPressedThisFrame && _charge > 0f) { _on = !_on; ProceduralAudio.Play2D(ProceduralAudio.Beep(), 0.15f); }
            if (_on && _charge > 0f)
            {
                _charge -= Time.deltaTime;
                float f = 1f; if (_charge < 15f) { _flickerT -= Time.deltaTime; if (_flickerT < 0f) _flickerT = Random.Range(0.05f, 0.5f); f = _flickerT < 0.08f ? 0.1f : 0.6f + _charge / 40f; }
                if (beam != null) { beam.enabled = true; beam.intensity = 26f * f; }
                if (_charge <= 0f) { _charge = 0f; _on = false; }
            }
            else if (beam != null) beam.enabled = false;
            LightMeter.FlashlightOn = IsOn;
        }
    }
}
