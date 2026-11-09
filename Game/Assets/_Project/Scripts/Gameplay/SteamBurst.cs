using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Dense steam that blinds the player. While the view is fully occluded <see cref="Peak"/> fires, which is where
    /// the corridor swap happens. <see cref="Occlusion"/> reports how white-out the camera currently is.
    /// </summary>
    public class SteamBurst : MonoBehaviour
    {
        private static readonly List<SteamBurst> Active = new List<SteamBurst>();

        [SerializeField] private ParticleSystem[] puffs;
        [SerializeField] private float duration = 4.2f;
        [SerializeField] private float radius = 7f;
        [SerializeField, Range(0.2f, 0.8f)] private float peakAt = 0.42f;

        private float _t = -1f; private bool _peaked; private AudioSource _hiss;
        public event Action Peak;
        public bool IsPlaying => _t >= 0f;

        private float Envelope(float p) => Mathf.SmoothStep(0f, 1f, p / 0.18f) * (1f - Mathf.SmoothStep(0f, 1f, (p - 0.72f) / 0.28f));

        public static float Occlusion(Vector3 camPos)
        {
            float o = 0f;
            foreach (var s in Active)
            {
                if (s == null || s._t < 0f) continue;
                float d = Vector3.Distance(camPos, s.transform.position);
                o = Mathf.Max(o, Mathf.Clamp01(1f - d / s.radius) * s.Envelope(s._t / s.duration));
            }
            return Mathf.Clamp01(o * 1.25f);
        }

        public void Play()
        {
            if (_t >= 0f) return;
            _t = 0f; _peaked = false;
            foreach (var p in puffs) if (p != null) p.Play();
            _hiss = ProceduralAudio.PlayAt(ProceduralAudio.Hiss(), transform.position, 1f, 25f, 0.8f);
            NoiseEvents.Emit(transform.position, 18f);
            if (!Active.Contains(this)) Active.Add(this);
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime;
            if (!_peaked && _t / duration >= peakAt) { _peaked = true; Peak?.Invoke(); }
            if (_t >= duration)
            {
                _t = -1f; Active.Remove(this);
                foreach (var p in puffs) if (p != null) p.Stop();
            }
        }

        private void OnDisable() { Active.Remove(this); }
    }
}
