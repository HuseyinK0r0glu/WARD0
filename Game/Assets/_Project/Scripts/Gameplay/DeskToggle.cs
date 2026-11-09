using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>One of the six diegetic toggle switches on the desk.</summary>
    public class DeskToggle : Interactable
    {
        [SerializeField] private DeskRole role;
        [SerializeField] private DeskController controller;
        [SerializeField] private float tilt = 36f;

        private bool _on; private Quaternion _rest; private float _k;

        public override string Prompt => role == DeskRole.Clear ? "E: CLEAR the miner" : "E: " + role + (_on ? " off" : " on");

        private void Awake() { _rest = transform.localRotation; }

        public override void OnInteract(PlayerController player)
        {
            if (role == DeskRole.Clear) { _on = true; controller.OnToggle(role, true); StartCoroutine(Bounce()); return; }
            if (role == DeskRole.Analysis) { _on = true; controller.OnToggle(role, true); StartCoroutine(Bounce()); return; }
            if (role == DeskRole.Log) { controller.OnToggle(role, true); StartCoroutine(Bounce()); return; }
            if (role != DeskRole.Power && !controller.PowerOn) { if (ShiftManager.Instance != null) ShiftManager.Instance.Say("Main power is off.", 2f); ProceduralAudio.PlayAt(ProceduralAudio.Beep(), transform.position, 0.3f, 10f, 0.6f); return; }
            _on = !_on; controller.OnToggle(role, _on);
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), transform.position, 0.25f, 10f, 2f);
        }

        private System.Collections.IEnumerator Bounce()
        {
            ProceduralAudio.PlayAt(ProceduralAudio.Clank(), transform.position, 0.25f, 10f, 2f);
            _on = true; yield return new WaitForSeconds(0.35f); _on = false;
        }

        private void Update()
        {
            _k = Mathf.MoveTowards(_k, _on ? 1f : 0f, Time.deltaTime * 8f);
            transform.localRotation = _rest * Quaternion.Euler(-_k * tilt, 0, 0);
        }
    }
}
