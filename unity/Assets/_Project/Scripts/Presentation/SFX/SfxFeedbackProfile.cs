using Colosseum.Contracts;
using UnityEngine;

namespace Colosseum.Presentation.SFX
{
    [CreateAssetMenu(fileName = "SfxFeedbackProfile", menuName = "Colosseum/Presentation/SFX Feedback Profile")]
    public class SfxFeedbackProfile : ScriptableObject
    {
        [SerializeField] private SfxEventPreset[] _presets = new SfxEventPreset[0];

        public bool TryFindPreset(VfxEventPayload payload, out SfxEventPreset preset)
        {
            preset = null;
            if (_presets == null)
            {
                return false;
            }

            SfxEventPreset bestPreset = null;
            int bestScore = -1;
            for (int i = 0; i < _presets.Length; i++)
            {
                SfxEventPreset candidate = _presets[i];
                if (candidate == null || candidate.eventType != payload.EventType)
                {
                    continue;
                }

                if (!candidate.Matches(payload) || candidate.clip == null)
                {
                    continue;
                }

                int score = 0;
                if (!string.IsNullOrEmpty(candidate.effectKey))
                {
                    score += 2;
                }

                if (!string.IsNullOrEmpty(candidate.cardKey))
                {
                    score += 1;
                }

                if (score > bestScore)
                {
                    bestPreset = candidate;
                    bestScore = score;
                }
            }

            preset = bestPreset;
            return preset != null;
        }
    }
}
