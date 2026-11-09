using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>Organic flame / failing-bulb flicker for torches and fixtures.</summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        [SerializeField] private float speed = 7f, amount = 0.3f;
        private Light _l; private float _base, _seed;
        private void Awake() { _l = GetComponent<Light>(); _base = _l.intensity; _seed = Random.value * 50f; }
        private void Update() { _l.intensity = _base * (1f - amount + amount * 2f * Mathf.PerlinNoise(Time.time * speed, _seed)); }
    }
}
