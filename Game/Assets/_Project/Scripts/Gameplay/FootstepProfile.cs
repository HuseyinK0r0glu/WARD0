using System;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Optional footstep overrides. Assign real recordings per surface (3-5 variations each); any empty surface falls back to the
    /// synthesised footsteps. Place an asset named "FootstepProfile" in a Resources folder, or assign it on the FootstepPlayer.
    /// </summary>
    [CreateAssetMenu(menuName = "Ward0/Footstep Profile", fileName = "FootstepProfile")]
    public class FootstepProfile : ScriptableObject
    {
        /// <summary>Per-stance randomisation. Volume is a multiplier on the noise-radius loudness, pitch is the playback pitch.</summary>
        [Serializable]
        public struct StanceSettings
        {
            [Range(0f, 1.5f)] public float volumeMin, volumeMax;
            [Range(0.5f, 1.5f)] public float pitchMin, pitchMax;
            public StanceSettings(float vMin, float vMax, float pMin, float pMax) { volumeMin = vMin; volumeMax = vMax; pitchMin = pMin; pitchMax = pMax; }
        }

        [Header("Clips (empty = synthesised fallback)")]
        [SerializeField] private AudioClip[] metal;
        [SerializeField] private AudioClip[] gravel;
        [SerializeField] private AudioClip[] puddle;

        [Header("Randomisation per stance")]
        [SerializeField] private StanceSettings walk = Defaults(FootstepStance.Walk);
        [SerializeField] private StanceSettings sprint = Defaults(FootstepStance.Sprint);
        [SerializeField] private StanceSettings crouch = Defaults(FootstepStance.Crouch);

        /// <summary>Clips assigned for a surface (may be null or empty).</summary>
        public AudioClip[] GetClips(Surface surface) => surface == Surface.Metal ? metal : (surface == Surface.Gravel ? gravel : puddle);

        /// <summary>Randomisation ranges for a stance.</summary>
        public StanceSettings GetStance(FootstepStance stance) => stance == FootstepStance.Sprint ? sprint : (stance == FootstepStance.Crouch ? crouch : walk);

        /// <summary>Built-in ranges used when no profile is assigned.</summary>
        public static StanceSettings Defaults(FootstepStance stance)
        {
            switch (stance)
            {
                case FootstepStance.Sprint: return new StanceSettings(0.92f, 1f, 0.96f, 1.08f);
                case FootstepStance.Crouch: return new StanceSettings(0.85f, 1f, 0.9f, 1f);
                default: return new StanceSettings(0.9f, 1f, 0.94f, 1.06f);
            }
        }
    }
}
