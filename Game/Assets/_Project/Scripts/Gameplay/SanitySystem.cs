using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Dual-source sanity: a permanent baseline (lost by incinerating innocents) and a temporary decay (toxic gas)
    /// that clears when the player is back in the safe booth. Effective sanity = baseline - decay.
    /// Threshold tiers drive the URP post-processing, camera sway, movement penalty and hallucination sounds.
    /// </summary>
    public class SanitySystem : MonoBehaviour
    {
        public static SanitySystem Instance { get; private set; }

        [Header("Values (0-100)")]
        [SerializeField] private float baseline = 100f;
        [SerializeField] private float decay;
        [SerializeField] private float gasDrainPerSecond = 2.2f;
        [SerializeField] private float incinerateInnocentLoss = 22f;

        [Header("Tier thresholds (effective sanity below which the tier starts)")]
        [SerializeField] private float tier1 = 75f;
        [SerializeField] private float tier2 = 50f;
        [SerializeField] private float tier3 = 25f;

        [Header("Post processing")]
        [SerializeField] private Volume volume;

        private Vignette _vig; private ChromaticAberration _ca; private FilmGrain _grain; private ColorAdjustments _col; private LensDistortion _lens;
        private int _gasSources; private float _whisperT; private float _heartT;

        public float Baseline => baseline;
        public float Decay => decay;
        public float Effective => Mathf.Clamp(baseline - decay, 0f, 100f);
        public bool InGas => _gasSources > 0;
        public int Tier => Effective < tier3 ? 3 : (Effective < tier2 ? 2 : (Effective < tier1 ? 1 : 0));
        /// <summary>0 (fine) .. 1 (broken)</summary>
        public float Distress => 1f - Effective / 100f;

        private void Awake()
        {
            Instance = this;
            if (volume != null && volume.profile != null)
            {
                volume.profile.TryGet(out _vig); volume.profile.TryGet(out _ca); volume.profile.TryGet(out _grain); volume.profile.TryGet(out _col); volume.profile.TryGet(out _lens);
            }
        }

        public void EnterGas() => _gasSources++;
        public void ExitGas() => _gasSources = Mathf.Max(0, _gasSources - 1);
        public void LoseBaseline(float amount) { baseline = Mathf.Max(0f, baseline - amount); }
        public void PunishInnocentIncineration() => LoseBaseline(incinerateInnocentLoss);
        /// <summary>Called when the player returns to the safe booth: temporary decay is cleared.</summary>
        public void ClearTemporaryDecay() { decay = 0f; _gasSources = 0; }

        private void Update()
        {
            if (_gasSources > 0) decay = Mathf.Min(100f, decay + gasDrainPerSecond * Time.deltaTime);
            Apply();
            int tier = Tier;
            if (tier >= 2 && Time.timeScale > 0f)
            {
                _whisperT -= Time.deltaTime;
                if (_whisperT <= 0f)
                {
                    _whisperT = Random.Range(7f, 16f) / tier;
                    var cam = Camera.main;
                    if (cam != null) ProceduralAudio.PlayAt(ProceduralAudio.Whisper(), cam.transform.position + Random.onUnitSphere * 2.2f, 0.55f, 12f, Random.Range(0.8f, 1.1f));
                }
            }
            if (tier >= 3)
            {
                _heartT -= Time.deltaTime;
                if (_heartT <= 0f) { _heartT = 1.05f; ProceduralAudio.Play2D(ProceduralAudio.Heartbeat(), 0.5f); }
            }
        }

        private void Apply()
        {
            float d = Distress; int tier = Tier;
            if (_vig != null) { _vig.active = true; _vig.intensity.Override(Mathf.Lerp(0.28f, 0.62f, d) + (_gasSources > 0 ? 0.08f : 0f)); _vig.smoothness.Override(0.55f); }
            if (_ca != null) { _ca.active = true; _ca.intensity.Override(tier >= 2 ? Mathf.Lerp(0.15f, 0.8f, d) : 0f); }
            if (_grain != null) { _grain.active = true; _grain.intensity.Override(Mathf.Lerp(0.15f, 0.65f, d)); }
            if (_col != null) { _col.active = true; _col.saturation.Override(Mathf.Lerp(0f, -55f, d)); }
            if (_lens != null) { _lens.active = tier >= 3; _lens.intensity.Override(-0.18f - 0.12f * Mathf.Sin(Time.time * 0.9f)); }
        }
    }
}
