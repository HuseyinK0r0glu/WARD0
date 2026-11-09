using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Connects the upper checkpoint car and the lower mine car. Riding fades out, rumbles, and teleports the player.</summary>
    public class ElevatorSystem : MonoBehaviour
    {
        [SerializeField] private ElevatorCar upperCar;
        [SerializeField] private ElevatorCar lowerCar;
        [SerializeField] private float rideSeconds = 4f;

        public IEnumerator Ride(ElevatorCar from, HudOverlay hud, PlayerController player)
        {
            var to = from == upperCar ? lowerCar : upperCar;
            player.Frozen = true;
            if (from.Door != null) from.Door.Close();
            yield return new WaitForSeconds(1.2f);
            ProceduralAudio.Play2D(ProceduralAudio.Rumble(), 0.8f);
            if (hud != null) yield return hud.Fade(1f, 0.7f);
            yield return new WaitForSeconds(rideSeconds * 0.5f);
            player.Teleport(to.StandPoint.position, to.StandPoint.eulerAngles.y);
            if (SanitySystem.Instance != null && to == upperCar) SanitySystem.Instance.ClearTemporaryDecay();
            yield return new WaitForSeconds(rideSeconds * 0.5f);
            if (hud != null) yield return hud.Fade(0f, 0.8f);
            if (to.Door != null) to.Door.Open();
            player.Frozen = false;
        }
    }
}
