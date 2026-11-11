using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Spare flashlight battery - placed in risky dead ends.</summary>
    public class BatteryPickup : Interactable
    {
        [SerializeField] private float seconds = 50f;
        private string _prompt;
        public override string Prompt => _prompt ?? (_prompt = GameText.PromptBattery(Mathf.RoundToInt(seconds)));
        public override void OnInteract(PlayerController player)
        {
            var f = player.GetComponentInChildren<FlashlightController>(); if (f != null) f.AddBattery(seconds);
            ProceduralAudio.Play2D(ProceduralAudio.Clank(), 0.25f);
            if (ShiftManager.Instance != null) ShiftManager.Instance.Say(GameText.BatteryTaken(Mathf.RoundToInt(seconds)), 2.5f, false, HudOverlay.Tone.Good);
            Destroy(gameObject);
        }
    }
}
