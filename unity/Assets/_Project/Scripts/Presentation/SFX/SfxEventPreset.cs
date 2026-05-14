using System;
using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    [Serializable]
    public class SfxEventPreset
    {
        public string cardKey = string.Empty;
        public string effectKey = string.Empty;
        public VfxEventType eventType = VfxEventType.ProjectileImpact;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.85f;
        [Range(0.1f, 3f)] public float pitchMin = 1f;
        [Range(0.1f, 3f)] public float pitchMax = 1f;
        [Range(0f, 1f)] public float spatialBlend = 0.75f;
        public bool playAtWorldPosition = true;

        public bool Matches(VfxEventPayload payload)
        {
            bool eventMatches = eventType == payload.EventType;
            bool cardMatches = string.IsNullOrEmpty(cardKey) || cardKey == payload.CardKey;
            bool effectMatches = string.IsNullOrEmpty(effectKey) || effectKey == payload.EffectKey;
            return eventMatches && cardMatches && effectMatches;
        }
    }
}
