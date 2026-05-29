using UnityEngine;
using Colosseum.Player;

namespace Colosseum.Status.Effects
{
    /// <summary>
    /// 중독탄 (Common). 긴 시간 동안 약한 DoT.
    /// Burn보다 길고 약함. 비주얼은 보라 tint.
    /// </summary>
    [CreateAssetMenu(fileName = "PoisonEffect", menuName = "Colosseum/Status Effect/Poison")]
    public class PoisonEffect : StatusEffectDefinition, IVisualTintEffect
    {
        [Header("Poison 전용 수치")]
        [Tooltip("OnTick 1회당 데미지 (HP).")]
        [SerializeField] private float _damagePerTick = 6f;

        public override void OnApply(PlayerStatusController target, ref StatusEffectInstance instance) { }

        public override void OnTick(PlayerStatusController target, ref StatusEffectInstance instance)
        {
            var health = target.GetComponent<PlayerHealth>();
            if (health == null) return;
            health.TakeDamage(StatusEffectDefinition.CalculateStackedTickDamage(_damagePerTick, instance.Magnitude, instance.Stacks), instance.Source, Weapon.StatusEffect.None);
        }

        public override void OnExpire(PlayerStatusController target, ref StatusEffectInstance instance) { }

        public Color GetVisualTint(StatusEffectInstance instance) => visualTint;
    }
}
