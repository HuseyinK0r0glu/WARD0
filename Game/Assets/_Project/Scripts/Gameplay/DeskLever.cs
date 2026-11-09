using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>The red incinerate lever.</summary>
    public class DeskLever : Interactable
    {
        [SerializeField] private DeskController controller;
        public override string Prompt => "E: pull lever (INCINERATE)";
        public override void OnInteract(PlayerController player)
        {
            var sm = ShiftManager.Instance; if (sm == null) return;
            if (!sm.CanSubmitVerdict) { sm.Say("No miner awaiting a verdict.", 2f); return; }
            controller.PullLever(() => sm.SubmitVerdict(true));
        }
    }
}
