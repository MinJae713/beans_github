using UnityEngine;
using Fusion;

namespace Colosseum.Status
{
    /// <summary>
    /// 상태이상 한 종류를 데이터로 정의하는 abstract ScriptableObject.
    ///
    /// 새 상태이상 카드 추가 절차 (OCP — 기존 코드 수정 0건):
    ///   1. 본 클래스 상속한 새 SO 클래스 작성 (예: BleedEffect : StatusEffectDefinition)
    ///   2. OnApply / OnTick / OnExpire 구현
    ///   3. Assets/_Project/Data/StatusEffects/ 에 .asset 인스턴스 생성
    ///   4. Cards/{새카드}.asset Inspector에서 statusEffectDef 슬롯에 드래그 (김범승 R)
    ///
    /// PlayerHealth / PlayerController / BulletController 코드 수정 0건.
    ///
    /// 효과별 동작은 abstract hook 3개로 분리:
    ///   - OnApply  : 적용 직후 1회 (modifier 적용, 시각 변경 등)
    ///   - OnTick   : NextTickTimer 만료마다 (DoT 데미지 발행 등)
    ///   - OnExpire : 전체 Timer 만료 시 1회 (modifier 복원, 시각 원복)
    ///
    /// 모든 hook은 PlayerStatusController.FixedUpdateNetwork 안에서 호출되므로
    /// Fusion 결정론을 따른다 (Time.deltaTime 금지, TickTimer/Runner 사용).
    /// </summary>
    public abstract class StatusEffectDefinition : ScriptableObject
    {
        [Header("식별")]
        [Tooltip("StatusEffectInstance.EffectKey로 사용. 영문 소문자 권장 (burn, poison, frost, electric).")]
        public string effectKey = "";

        [Header("기본 수치")]
        [Tooltip("전체 지속 시간 (초). 스택형은 적용 시마다 갱신.")]
        public float baseDuration = 3f;

        [Tooltip("OnTick 호출 주기 (초). DoT용. 0 또는 음수면 OnTick 호출 안 함.")]
        public float tickInterval = 1f;

        [Header("스택 정책")]
        [Tooltip("true면 같은 키 재적용 시 Stacks +1 (Electric 패턴). false면 Timer만 갱신 (Burn 패턴).")]
        public bool isStackable = false;

        [Tooltip("isStackable=true 일 때 최대 스택 수.")]
        public int maxStack = 1;

        [Header("시각 (비주얼팀 R)")]
        [Tooltip("효과 적용 중 캐릭터 스프라이트에 곱연산되는 색. 비주얼팀이 운영 값 결정.")]
        public Color visualTint = Color.white;

        // ──────────────────────────────────────────────
        //  Hook (효과별 클래스에서 구현)
        // ──────────────────────────────────────────────

        /// <summary>
        /// 효과 적용 직후 1회 호출. modifier 등록·시각 변경 등 즉시 반영.
        /// PlayerStatusController가 instance를 NetworkArray에 막 추가한 직후.
        /// </summary>
        public abstract void OnApply(PlayerStatusController target, ref StatusEffectInstance instance);

        /// <summary>
        /// instance.NextTickTimer 만료마다 호출 (tickInterval > 0 일 때).
        /// DoT 데미지 발행, 주기 효과 등.
        /// 호출 후 PlayerStatusController가 NextTickTimer를 자동 재시작.
        /// </summary>
        public abstract void OnTick(PlayerStatusController target, ref StatusEffectInstance instance);

        /// <summary>
        /// instance.Timer 만료 시 1회 호출 + 슬롯 해제 직전.
        /// modifier 복원·시각 원복 등 cleanup.
        /// </summary>
        public abstract void OnExpire(PlayerStatusController target, ref StatusEffectInstance instance);
    }
}
