using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Builds the synthesised sounds one per frame while the intro is still fading in, instead of in a hitch the first time each is heard,
    /// and starts the mine ambience (a runtime-synthesised clip cannot be saved into the generated scene, so the scene's drone source has no clip).
    /// Creates itself in any scene that has a <see cref="ShiftManager"/>; nothing needs wiring.
    /// </summary>
    public class AudioWarmup : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindFirstObjectByType<ShiftManager>() == null) return;
            new GameObject("AudioWarmup").AddComponent<AudioWarmup>();
        }

        private IEnumerator Start()
        {
            var amb = GameObject.Find("Ambience");
            var drone = amb != null ? amb.GetComponent<AudioSource>() : null;
            if (drone != null && drone.clip == null) { drone.clip = ProceduralAudio.Drone(); drone.loop = true; drone.Play(); }
            yield return ProceduralAudio.PrewarmRoutine();
            var steps = Resources.Load<FootstepProfile>("FootstepProfile");   // recorded steps need no synthesis
            bool recorded = steps != null; foreach (Surface s in System.Enum.GetValues(typeof(Surface))) { var c = recorded ? steps.GetClips(s) : null; if (c == null || c.Length == 0) recorded = false; }
            if (!recorded) yield return FootstepSynth.PrewarmRoutine();
            Destroy(gameObject);
        }
    }
}
