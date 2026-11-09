using System;
using UnityEngine;

namespace Ward0.Props
{
    /// <summary>
    /// Quarantine blast door. The leaf swings on its hinge while the lock wheel spins.
    /// Drive it from gameplay code with <see cref="Open"/>, <see cref="Close"/> or <see cref="SetOpen"/>.
    /// </summary>
    public class QuarantineDoor : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField] private Transform leaf;
        [SerializeField] private Transform wheel;

        [Header("Motion")]
        [SerializeField, Tooltip("Degrees the leaf swings about local Y (positive = toward the front)")] private float openAngle = 100f;
        [SerializeField] private float openSeconds = 3.2f;
        [SerializeField, Tooltip("Wheel revolutions during one full open/close")] private float wheelTurns = 1.5f;
        [SerializeField] private bool startOpen;

        private float _t;       // 0 closed .. 1 open
        private float _target;
        private Quaternion _leafClosed;
        private Quaternion _wheelRest;

        public event Action Opened;
        public event Action Closed;

        public bool IsOpen => _t >= 1f;
        public bool IsMoving => !Mathf.Approximately(_t, _target);
        public float Progress => _t;

        private void Awake()
        {
            if (leaf != null) _leafClosed = leaf.localRotation;
            if (wheel != null) _wheelRest = wheel.localRotation;
            _t = _target = startOpen ? 1f : 0f;
            Apply();
        }

        private void Update()
        {
            if (!IsMoving) return;
            float step = Time.deltaTime / Mathf.Max(0.01f, openSeconds);
            _t = Mathf.MoveTowards(_t, _target, step);
            Apply();
            if (!IsMoving) (_target >= 1f ? Opened : Closed)?.Invoke();
        }

        public void Open() => _target = 1f;
        public void Close() => _target = 0f;
        public void SetOpen(bool open) => _target = open ? 1f : 0f;
        public void Toggle() => _target = _target >= 0.5f ? 0f : 1f;

        private void Apply()
        {
            float eased = Mathf.SmoothStep(0f, 1f, _t);
            // wheel spins first (unlock), leaf swings during the back 80%
            float swing = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.2f, 1f, _t));
            if (leaf != null) leaf.localRotation = _leafClosed * Quaternion.Euler(0f, swing * openAngle, 0f);
            if (wheel != null) wheel.localRotation = _wheelRest * Quaternion.Euler(0f, 0f, eased * wheelTurns * 360f);
        }
    }
}
