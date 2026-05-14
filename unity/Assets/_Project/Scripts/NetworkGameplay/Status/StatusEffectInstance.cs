using Fusion;

namespace Colosseum.Status
{
    /// <summary>
    /// 한 플레이어에게 적용된 상태이상의 런타임 인스턴스.
    /// PlayerStatusController.NetworkArray에 담기며 Fusion이 자동 동기화한다.
    /// 새 효과가 추가되어도 본 struct 시그니처는 변경 없음 — effectKey로 식별, 효과별 동작은 StatusEffectDefinition가 담당.
    /// </summary>
    public struct StatusEffectInstance : INetworkStruct
    {
        /// <summary>
        /// 효과 식별자. StatusEffectDefinition.effectKey와 매칭.
        /// 빈 문자열이면 슬롯 비어있음.
        /// </summary>
        public NetworkString<_16> EffectKey;

        /// <summary>전체 만료 타이머. Expired 시 OnExpire 호출 + 슬롯 해제.</summary>
        public TickTimer Timer;

        /// <summary>주기 효과(DoT 등)용 다음 tick 타이머. Expired 시 OnTick 호출 + 재시작.</summary>
        public TickTimer NextTickTimer;

        /// <summary>스택형 효과(Electric 등)의 누적 카운트.</summary>
        public int Stacks;

        /// <summary>효과 적용자. DoT 데미지의 attacker로 사용 (kill credit용).</summary>
        public PlayerRef Source;

        /// <summary>효과 강도. DoT 배율, 둔화 강도 등 SO에서 해석.</summary>
        public float Magnitude;

        /// <summary>슬롯 비어있는지 검사용.</summary>
        public bool IsEmpty => EffectKey.Length == 0;
    }
}
