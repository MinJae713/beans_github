using UnityEngine;

namespace Colosseum.Status.Effects
{
    /// <summary>
    /// 냉기탄 (Common). 이동 속도 둔화.
    /// PlayerController가 매 stat 계산 시 PlayerStatusController.GetMoveSpeedMultiplier를 곱한다.
    /// 비주얼은 파란 tint.
    /// </summary>
    [CreateAssetMenu(fileName = "FrostEffect", menuName = "Colosseum/Status Effect/Frost")]
    public class FrostEffect : StatusEffectDefinition, IMovementModifierEffect, IVisualTintEffect
    {
        [Header("Frost 전용 수치")]
        [Tooltip("이동 속도 배율. 0.5 = 절반, 1.0 = 영향 없음.")]
        [Range(0f, 1f)]
        [SerializeField] private float _moveSpeedMultiplier = 0.6f;

        public override void OnApply(PlayerStatusController target, ref StatusEffectInstance instance) { }

        public override void OnTick(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // Frost는 주기 효과 없음 (tickInterval = 0 으로 .asset 설정 권장)
        }

        public override void OnExpire(PlayerStatusController target, ref StatusEffectInstance instance) { }

        public float GetMoveSpeedMultiplier(StatusEffectInstance instance) => _moveSpeedMultiplier;
        public Color GetVisualTint(StatusEffectInstance instance) => visualTint;
    }
}
