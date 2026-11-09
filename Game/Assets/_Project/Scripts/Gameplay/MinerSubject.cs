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

        public bool Infected => _infected;
        public bool IsReady { get; private set; }

        public void Setup(bool infected, Transform glass)
        {
            _infected = infected; _glass = glass; _home = transform.position; _mutterT = Random.Range(2f, 5f);
            StartCoroutine(Ready());
        }

        private IEnumerator Ready() { yield return new WaitForSeconds(1.2f); IsReady = true; }

        private void Update()
        {
            if (_leaving) return;
            _t += Time.deltaTime;
            float sway = Mathf.Sin(_t * 1.1f) * (_infected ? 4f : 1.5f);
            if (body != null) body.localRotation = Quaternion.Euler(Mathf.Sin(_t * 0.7f) * 2f, 0, sway);
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
            foreach (var r in rends) foreach (var m in r.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * 0.25f); }
            while (t < 2.6f)
            {
                t += Time.deltaTime; if (body != null) body.localRotation = Quaternion.Euler(Mathf.Sin(t * 18f) * 6f, 0, Mathf.Sin(t * 14f) * 8f);
                transform.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, Mathf.Clamp01((t - 1.2f) / 1.4f)); yield return null;
            }
            Destroy(gameObject);
        }

        /// <summary>Wrongly cleared: it lunges at the glass.</summary>
        public IEnumerator Ambush()
        {
            _leaving = true; Vector3 start = transform.position; Vector3 end = _glass != null ? new Vector3(_glass.position.x, start.y, _glass.position.z) + (start - _glass.position).normalized * 0.5f : start;
            ProceduralAudio.PlayAt(ProceduralAudio.Growl(), transform.position, 1f, 25f, 0.75f);
            foreach (var e in eyes) if (e != null) e.material.SetColor("_EmissionColor", new Color(6f, 0.1f, 0.05f));
            float t = 0f;
            while (t < 0.35f) { t += Time.deltaTime; transform.position = Vector3.Lerp(start, end, t / 0.35f); yield return null; }
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), end + Vector3.up, 1f, 25f, 1.4f);
            if (armL != null) armL.localRotation = Quaternion.Euler(-100f, 0, 0); if (armR != null) armR.localRotation = Quaternion.Euler(-100f, 0, 0);
            yield return new WaitForSeconds(0.5f);
        }
    }
}
