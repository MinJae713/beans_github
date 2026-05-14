using UnityEngine;
using Colosseum.Contracts;

namespace Colosseum.Presentation.VFX
{
    [CreateAssetMenu(fileName = "VfxFeedbackProfile", menuName = "Colosseum/Presentation/VFX Feedback Profile")]
    public class VfxFeedbackProfile : ScriptableObject
    {
        [SerializeField] private CardVfxPreset[] _presets = new CardVfxPreset[0];

        public bool TryFindPreset(VfxEventPayload payload, out CardVfxPreset preset)
        {
            preset = null;
            if (_presets == null)
            {
                return false;
            }

            int bestScore = -1;
            for (int i = 0; i < _presets.Length; i++)
            {
                CardVfxPreset candidate = _presets[i];
                if (candidate == null)
                {
                    continue;
                }

                int score = candidate.GetMatchScore(payload);
                if (score > bestScore)
                {
                    bestScore = score;
                    preset = candidate;
                }
            }

            return preset != null;
        }
    }
}
