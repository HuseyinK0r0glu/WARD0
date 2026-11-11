using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Connects the upper checkpoint car and the lower mine car. Riding fades out, rumbles, and teleports the player.
    /// The lower car lives in a streamed mine zone, so it registers itself when that zone loads and the ride waits (under the fade) until the landing is ready.
    /// </summary>
    public class ElevatorSystem : MonoBehaviour
    {
        public static ElevatorSystem Instance { get; private set; }

        [SerializeField] private ElevatorCar upperCar;
        [SerializeField] private ElevatorCar lowerCar;
        [SerializeField] private float rideSeconds = 4f;

        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        /// <summary>Called by an <see cref="ElevatorCar"/> when it becomes active (the lower car arrives with its zone).</summary>
        public void Register(ElevatorCar car) { if (car.IsUpper) upperCar = car; else lowerCar = car; }
        /// <summary>Called when a car's zone unloads, so the ride never targets a destroyed car.</summary>
        public void Unregister(ElevatorCar car) { if (lowerCar == car) lowerCar = null; }

        public IEnumerator Ride(ElevatorCar from, HudOverlay hud, PlayerController player)
        {
            bool down = from == upperCar;
            player.Frozen = true;
            if (from.Door != null) from.Door.Close();
            yield return new WaitForSeconds(1.2f);
            ProceduralAudio.Play2D(ProceduralAudio.Rumble(), 0.8f);
            if (hud != null) yield return hud.Fade(1f, 0.7f);
            var streamer = ZoneStreamer.Instance;
            if (down && streamer != null) yield return streamer.PrepareLanding();   // load the landing chamber and its neighbours before arriving
            yield return new WaitForSeconds(rideSeconds * 0.5f);
            var to = down ? lowerCar : upperCar;
            while (to == null) { yield return null; to = down ? lowerCar : upperCar; }
            player.Teleport(to.StandPoint.position, to.StandPoint.eulerAngles.y);
            if (SanitySystem.Instance != null && to == upperCar) SanitySystem.Instance.ClearTemporaryDecay();
            yield return new WaitForSeconds(rideSeconds * 0.5f);
            if (hud != null) yield return hud.Fade(0f, 0.8f);
            if (to.Door != null) to.Door.Open();
            player.Frozen = false;
        }
    }
}
