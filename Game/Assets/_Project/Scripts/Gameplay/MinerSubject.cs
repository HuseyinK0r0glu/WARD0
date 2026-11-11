using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>A miner presented in the quarantine cell. Infected ones twitch, mutter and (if wrongly cleared) ambush the glass.</summary>
    public class MinerSubject : MonoBehaviour
    {
        [SerializeField] private Transform head, armL, armR, body;
        [SerializeField] private Renderer[] eyes;

        private bool _infected; private Transform _glass; private float _t, _mutterT, _twitch;
        private Vector3 _home; private bool _leaving;
        private Material _skin; private Transform _foreL, _foreR;
        private static readonly Color VeinIdle = new Color(1.5f, 0.1f, 0.38f);
        private static int _spawned;   // deterministic look rotation: miner n wears look n


        public bool Infected => _infected;
        public bool IsReady { get; private set; }

        public void Setup(bool infected, Transform glass)
        {
            _infected = infected; _glass = glass; _home = transform.position; _mutterT = Random.Range(2f, 5f);
            var look = GetComponent<MinerVariant>(); if (look != null) look.Apply(infected, _spawned++);
            if (infected)
            {   // wrong in small ways: arms a little too long, hunched, veins pulsing under the skin
                _skin = look != null ? look.SkinMaterial : null;
                _foreL = armL != null ? armL.Find("mixamorig:LeftForeArm") : null; _foreR = armR != null ? armR.Find("mixamorig:RightForeArm") : null;
                var longArm = new Vector3(0.92f, 1.28f, 0.92f); if (_foreL != null) _foreL.localScale = longArm; if (_foreR != null) _foreR.localScale = longArm;
            }
            StartCoroutine(Ready());
        }

        private IEnumerator Ready() { yield return new WaitForSeconds(1.2f); IsReady = true; }

        private void Update()
        {
            if (_leaving) return;
            _t += Time.deltaTime;
            float sway = Mathf.Sin(_t * 1.1f) * (_infected ? 4f : 1.5f);
            if (body != null) body.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 2f + (_infected ? 10f : 0f), 0, sway);
            if (armL != null) armL.localRotation = Quaternion.Euler(8f + Mathf.Sin(_t * 1.3f) * 4f, 0, 6f);
            if (armR != null) armR.localRotation = Quaternion.Euler(8f + Mathf.Sin(_t * 1.1f + 1f) * 4f, 0, -6f);
            if (head != null)
            {
                float yaw = Mathf.Sin(_t * 0.5f) * 12f; float tilt = 0f;
                if (_infected)
                {
                    _twitch -= Time.deltaTime;
                    if (_twitch < 0f) { _twitch = Random.Range(0.4f, 2.2f); head.localRotation = Quaternion.Euler(Random.Range(-14f, 14f), Random.Range(-35f, 35f), Random.Range(-18f, 18f)); }
                    head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0, yaw, tilt), Time.deltaTime * 2.5f);
                }
                else head.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.6f) * 3f, yaw, 0);
            }
            if (_infected)
            {
                if (_skin != null) _skin.SetColor("_EmissionColor", VeinIdle * (0.35f + 0.3f * Mathf.Sin(_t * 5.6f) * Mathf.Sin(_t * 1.9f + 0.6f)));   // slow heartbeat throb
                _mutterT -= Time.deltaTime;
                if (_mutterT < 0f) { _mutterT = Random.Range(5f, 10f); ProceduralAudio.PlayAt(ProceduralAudio.Whisper(), transform.position + Vector3.up * 1.5f, 0.35f, 12f, Random.Range(0.7f, 0.95f)); }
            }
        }

        public IEnumerator Release()
        {
            _leaving = true; Vector3 start = transform.position; Vector3 end = start + transform.right * 6f; float t = 0f;
            while (t < 3.5f) { t += Time.deltaTime; transform.position = Vector3.Lerp(start, end, t / 3.5f); transform.forward = Vector3.Slerp(transform.forward, (end - start).normalized, Time.deltaTime * 3f); yield return null; }
            Destroy(gameObject);
        }

        public IEnumerator Burn()
        {
            _leaving = true; float t = 0f; var rends = GetComponentsInChildren<Renderer>();
            foreach (var r in rends) foreach (var m in r.materials)
            {
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * 0.25f);
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);   // veins and eyes go dark
            }
            while (t < 2.6f)
            {
                t += Time.deltaTime; if (body != null) body.localRotation = Quaternion.Euler(Mathf.Sin(t * 18f) * 6f, 0, Mathf.Sin(t * 14f) * 8f);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, Mathf.Clamp01((t - 1.2f) / 1.4f)); yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>
        /// Wrongly cleared: the infection takes over in front of the glass. It seizes up and its veins flare, the arms unfold and stretch,
        /// the head twists past what a neck should do, then it hurls itself at the glass (screen shake, red flash) and hammers on it.
        /// </summary>
        public IEnumerator Ambush()
        {
            _leaving = true;
            var player = FindFirstObjectByType<PlayerController>(); var hud = FindFirstObjectByType<HudOverlay>();
            var look = GetComponent<MinerVariant>(); var skin = look != null ? look.SkinMaterial : null;
            Transform foreL = armL != null ? armL.Find("mixamorig:LeftForeArm") : null, foreR = armR != null ? armR.Find("mixamorig:RightForeArm") : null;
            Vector3 start = transform.position;
            Vector3 end = _glass != null ? new Vector3(_glass.position.x, start.y, _glass.position.z) + (start - _glass.position).normalized * 0.45f : start;
            Vector3 headPos = transform.position + Vector3.up * 1.5f;
            var eyeCol = new Color(4.5f, 0.06f, 0.03f); var veinHot = new Color(2.2f, 0.18f, 0.45f);
            foreach (var e in eyes) if (e != null) { e.material.SetColor("_EmissionColor", eyeCol); }
            Light glow = look != null ? look.EyeGlow : null; if (glow != null) { glow.enabled = true; glow.intensity = 2f; glow.range = 3f; }
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), headPos, 1f, 25f, 0.6f);

            // 1. seize up: violent twitching, veins flaring
            float t = 0f;
            while (t < 0.9f)
            {
                t += Time.deltaTime; float k = t / 0.9f;
                if (skin != null) skin.SetColor("_EmissionColor", veinHot * (k * (0.6f + 0.4f * Mathf.Sign(Mathf.Sin(t * 60f)))));
                if (body != null) body.localRotation = Quaternion.Euler(Random.Range(-6f, 6f) * k, Random.Range(-8f, 8f) * k, Random.Range(-7f, 7f) * k);
                if (head != null && Time.frameCount % 3 == 0) head.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-55f, 55f), Random.Range(-30f, 30f));
                if (player != null) player.Shake(0.01f + 0.015f * k, 0.15f);
                yield return null;
            }
            // 2. unfold: arms rise and stretch, spine hunches, head cranks back and sideways
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), headPos, 1f, 25f, 0.5f);
            t = 0f; const float unfold = 0.6f;
            while (t < unfold)
            {
                t += Time.deltaTime; float k = Mathf.SmoothStep(0f, 1f, t / unfold);
                if (body != null) body.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 28f, k) + Random.Range(-2f, 2f), 0f, Mathf.Sin(t * 30f) * 3f);
                if (armL != null) armL.localRotation = Quaternion.Euler(Mathf.Lerp(8f, -75f, k), 0f, Mathf.Lerp(6f, 25f, k));
                if (armR != null) armR.localRotation = Quaternion.Euler(Mathf.Lerp(8f, -75f, k), 0f, Mathf.Lerp(-6f, -25f, k));
                var stretch = new Vector3(Mathf.Lerp(1f, 0.8f, k), Mathf.Lerp(1f, 1.9f, k), Mathf.Lerp(1f, 0.8f, k));
                if (foreL != null) foreL.localScale = stretch; if (foreR != null) foreR.localScale = stretch;
                if (head != null) head.localRotation = Quaternion.Euler(Mathf.Lerp(0f, -32f, k), Mathf.Lerp(0f, 58f, k), Mathf.Lerp(0f, 34f, k)) * Quaternion.Euler(Random.Range(-1.5f, 1.5f), 0f, 0f);
                if (skin != null) skin.SetColor("_EmissionColor", veinHot * (0.8f + 0.2f * Mathf.Sin(t * 50f)));
                yield return null;
            }
            // 3. lunge at the glass
            if (hud != null) hud.Flash(new Color(0.7f, 0.02f, 0.02f), 0.45f);
            t = 0f;
            while (t < 0.28f)
            {
                t += Time.deltaTime; float k = t / 0.28f; transform.position = Vector3.Lerp(start, end, k * k);
                if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(10f, 0f, 0f), k);
                yield return null;
            }
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), end + Vector3.up, 1f, 25f, 1.2f);
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), end + Vector3.up * 1.5f, 1f, 25f, 0.8f);
            if (player != null) player.Shake(0.09f, 0.7f);
            // 4. hammering on the glass
            t = 0f;
            while (t < 1.1f)
            {
                t += Time.deltaTime; float hit = Mathf.Abs(Mathf.Sin(t * 14f));
                if (armL != null) armL.localRotation = Quaternion.Euler(-95f + hit * 28f, 0f, 14f); if (armR != null) armR.localRotation = Quaternion.Euler(-95f + (1f - hit) * 28f, 0f, -14f);
                if (head != null) head.localRotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(-20f, 20f), Random.Range(-14f, 14f));
                if (body != null) body.localRotation = Quaternion.Euler(30f + hit * 8f, Random.Range(-4f, 4f), Random.Range(-5f, 5f));
                if (skin != null) skin.SetColor("_EmissionColor", veinHot * (1f + 0.5f * hit));
                if (hit > 0.97f) ProceduralAudio.PlayAt(ProceduralAudio.Clank(), end + Vector3.up * 1.3f, 0.8f, 25f, Random.Range(0.9f, 1.4f));
                if (player != null && hit > 0.95f) player.Shake(0.03f, 0.12f);
                yield return null;
            }
        }
    }
}
