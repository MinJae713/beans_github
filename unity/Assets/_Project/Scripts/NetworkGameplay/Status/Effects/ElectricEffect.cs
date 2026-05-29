using UnityEngine;

namespace Colosseum.Status.Effects
{
    /// <summary>
    /// 감전탄 (Common). 스택형 효과 — 일정 스택 도달 시 짧게 경직 (입력 잠금).
    /// PlayerController가 매 tick PlayerStatusController.IsStunned()를 체크해 입력 무시.
    /// 비주얼은 노란 tint + (선택) 스파크 파티클 (비주얼팀).
    /// </summary>
    [CreateAssetMenu(fileName = "ElectricEffect", menuName = "Colosseum/Status Effect/Electric")]
    public class ElectricEffect : StatusEffectDefinition, IStunEffect, IVisualTintEffect
    {
        [Header("Electric 전용 수치")]
        [Tooltip("이 스택 수에 도달하면 stack을 소비하고 별도 stun timer가 baseDuration 동안 경직을 유지.")]
        [SerializeField] private int _stunThreshold = 3;

        public override void OnApply(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // 스택 누적은 PlayerStatusController.Apply가 처리. 여기선 임계 체크만.
            // OnApply는 Apply 흐름 끝에서 호출되니 이미 Stacks +1 된 상태.
        }

        public override void OnTick(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // Electric은 주기 효과 없음 (tickInterval = 0 권장)
        }

        public override void OnExpire(PlayerStatusController target, ref StatusEffectInstance instance) { }

        public bool IsStunningNow(StatusEffectInstance instance)
        {
            // PlayerStatusController가 이 결과를 trigger로 사용해 stack을 0으로 소비하고 stun timer를 시작한다.
            return instance.Stacks >= _stunThreshold;
        }

        public Color GetVisualTint(StatusEffectInstance instance) => visualTint;
    }
}
