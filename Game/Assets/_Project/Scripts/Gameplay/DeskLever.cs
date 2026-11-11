using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>The red incinerate lever.</summary>
    public class DeskLever : Interactable
    {
        [SerializeField] private DeskController controller;
        public override string Prompt => GameText.PromptIncinerate;
        public override void OnInteract(PlayerController player)
        {
            var sm = ShiftManager.Instance; if (sm == null) return;
            if (!sm.CanSubmitVerdict) { sm.ExplainNoVerdict(); return; }
            controller.PullLever(() => sm.SubmitVerdict(true));
        }
    }
}
