using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Spare flashlight battery - placed in risky dead ends.</summary>
    public class BatteryPickup : Interactable
    {
        [SerializeField] private float seconds = 50f;
        public override string Prompt => "E: take battery";
        public override void OnInteract(PlayerController player)
        {
            var f = player.GetComponentInChildren<FlashlightController>(); if (f != null) f.AddBattery(seconds);
            ProceduralAudio.Play2D(ProceduralAudio.Clank(), 0.25f);
            if (ShiftManager.Instance != null) ShiftManager.Instance.Say("Battery +" + Mathf.RoundToInt(seconds) + "s", 2f);
            Destroy(gameObject);
        }
    }
}
