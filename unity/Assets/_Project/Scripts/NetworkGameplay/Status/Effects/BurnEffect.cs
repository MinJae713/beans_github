using UnityEngine;
using Colosseum.Player;

namespace Colosseum.Status.Effects
{
    /// <summary>
    /// 화염탄 (Common). 짧은 시간 동안 강한 DoT.
    /// 비주얼은 빨간 tint. PlayerHealth가 IsHurt 트리거를 다시 받지는 않음 — 시각/사운드는 비주얼팀.
    /// </summary>
    [CreateAssetMenu(fileName = "BurnEffect", menuName = "Colosseum/Status Effect/Burn")]
    public class BurnEffect : StatusEffectDefinition, IVisualTintEffect
    {
        [Header("Burn 전용 수치")]
        [Tooltip("OnTick 1회당 데미지 (HP). baseDuration / tickInterval 회만큼 누적 발행됨.")]
        [SerializeField] private float _damagePerTick = 6f;

        public override void OnApply(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // 즉시 효과 없음 — 첫 tick부터 데미지 발행
        }

        public override void OnTick(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // attacker = instance.Source (kill credit 보존)
            var health = target.GetComponent<PlayerHealth>();
            if (health == null) return;

            // status 인자는 None — 재귀 적용 방지 (Burn이 또 Burn을 적용하지 않게)
            health.TakeDamage(_damagePerTick * instance.Magnitude, instance.Source, Weapon.BulletController.StatusEffect.None);
        }

        public override void OnExpire(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            // 시각 복원은 Controller가 매 tick visual tint 재계산하니 자동 처리
        }

        public Color GetVisualTint(StatusEffectInstance instance)
        {
            // 운영 시 비주얼팀이 visualTint 필드 자체를 조정. 본 메서드는 단순 위임.
            return visualTint;
        }
    }
}
