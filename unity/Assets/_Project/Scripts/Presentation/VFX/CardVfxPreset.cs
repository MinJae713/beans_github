using System;
using UnityEngine;
using Colosseum.Contracts;

namespace Colosseum.Presentation.VFX
{
    [Serializable]
    public class CardVfxPreset
    {
        [Tooltip("비워두면 모든 카드에 대한 fallback으로 사용됩니다.")]
        public string cardKey = string.Empty;

        [Tooltip("VFX preset lookup용 효과 키. 보통 cardKey와 동일합니다.")]
        public string effectKey = string.Empty;

        public VfxEventType eventType = VfxEventType.CardEffectApplied;
        public VfxPlaceholderStyle style = VfxPlaceholderStyle.Pulse;
        public Color color = Color.white;
        public float durationSeconds = 0.75f;
        public float radiusScale = 1f;
        public GameObject effectPrefab;
        public float prefabScale = 1f;
        public bool showToast = true;
        public string toastLabel = string.Empty;

        public bool Matches(VfxEventPayload payload)
        {
            return GetMatchScore(payload) >= 0;
        }

        public int GetMatchScore(VfxEventPayload payload)
        {
            if (eventType != payload.EventType)
            {
                return -1;
            }

            int score = 0;
            if (!string.IsNullOrEmpty(cardKey))
            {
                if (cardKey != payload.CardKey)
                {
                    return -1;
                }

                score += 2;
            }

            if (!string.IsNullOrEmpty(effectKey))
            {
                if (effectKey != payload.EffectKey)
                {
                    return -1;
                }

                score += 4;
            }

            return score;
        }
    }
}
