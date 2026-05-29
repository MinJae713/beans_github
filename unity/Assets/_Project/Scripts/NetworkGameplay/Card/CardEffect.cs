using UnityEngine;
using Fusion;
using Colosseum.NetworkGameplay.Diagnostics;

namespace Colosseum.Card
{
    /// <summary>
    /// 플레이어에 부착. 카드로 획득한 총알/이동 강화를 누적 저장.
    /// Gun이 총알을 생성할 때 이 값들을 참조.
    /// </summary>
    public class CardEffect : NetworkBehaviour
    {
        // 총알 강화 누적
        [Networked] public float DamageMultiplier { get; set; } = 1f;
        [Networked] public float BulletSpeedMultiplier { get; set; } = 1f;
        [Networked] public float BulletSizeMultiplier { get; set; } = 1f;
        [Networked] public int BonusBounce { get; set; } = 0;
        [Networked] public float GravityModifier { get; set; } = 0f;

        // 이동 강화 누적
        [Networked] public float MoveSpeedMultiplier { get; set; } = 1f;
        [Networked] public float JumpForceMultiplier { get; set; } = 1f;

        // 재장전 강화
        [Networked] public float ReloadSpeedMultiplier { get; set; } = 1f;

        // 탄창 / 관통 / 넉백
        [Networked] public int BonusAmmoCapacity { get; set; } = 0;
        [Networked] public int BonusPierce { get; set; } = 0;
        [Networked] public float KnockbackForce { get; set; } = 0f;

        // 킬 시 회복
        [Networked] public float HealOnKill { get; set; } = 0f;

        // 폭발
        [Networked] public float ExplosionRadius { get; set; } = 0f;
        [Networked] public float ExplosionRadiusMultiplier { get; set; } = 1f;
        [Networked] public float ExplosionDamageRatio { get; set; } = 0.5f;

        // 과충전 (재장전 직후 첫 발 데미지 배율)
        [Networked] public float OverchargeDamageMultiplier { get; set; } = 1f;

        // 상태이상
        [Networked] public int CurrentStatusEffect { get; set; } = 0;
        [Networked] public int CurrentStatusEffectMask { get; set; } = 0;
        [Networked] public float StatusDuration { get; set; } = 0f;
        [Networked] public float StatusMagnitude { get; set; } = 1f;
        [Networked] public float FreezeStatusMagnitude { get; set; } = 1f;
        [Networked] public float BurnStatusMagnitude { get; set; } = 1f;
        [Networked] public float PoisonStatusMagnitude { get; set; } = 1f;
        [Networked] public float ElectricStatusMagnitude { get; set; } = 1f;
        [Networked] public float FreezeStatusDuration { get; set; } = 0f;
        [Networked] public float BurnStatusDuration { get; set; } = 0f;
        [Networked] public float PoisonStatusDuration { get; set; } = 0f;
        [Networked] public float ElectricStatusDuration { get; set; } = 0f;

        // 특수 효과
        [Networked] public int CurrentSpecialEffect { get; set; } = 0;
        [Networked] public float SpecialValue { get; set; } = 0f;

        public void ApplyCard(CardData card, int cardStackCount = 1)
        {
            if (card == null) return;

            // 곱연산 누적
            DamageMultiplier *= card.damageMultiplier;
            BulletSpeedMultiplier *= card.speedMultiplier;
            BulletSizeMultiplier = CalculateBulletSizeMultiplier(BulletSizeMultiplier, card.cardKey, card.sizeMultiplier);
            MoveSpeedMultiplier *= card.moveSpeedMultiplier;
            JumpForceMultiplier *= card.jumpForceMultiplier;
            ReloadSpeedMultiplier *= card.reloadSpeedMultiplier;

            // 합연산 누적
            BonusBounce += card.extraBounce;
            BonusAmmoCapacity += Mathf.Max(0, card.extraAmmoCapacity);
            BonusPierce += card.extraPierce;
            KnockbackForce += card.knockbackForce;
            HealOnKill += card.healOnKill;
            GravityModifier += card.gravityModifier;
            ApplyExplosionModifiers(card);
            OverchargeDamageMultiplier *= card.overchargeDamageMultiplier;

            // 상태이상은 여러 종류를 누적해 한 발에 함께 실을 수 있다.
            if (card.statusEffect != StatusEffectType.None)
            {
                CurrentStatusEffect = (int)card.statusEffect;
                StatusDuration = Mathf.Max(0f, card.statusDuration);
                CurrentStatusEffectMask = AddStatusEffectToMask(CurrentStatusEffectMask, card.statusEffect);
                SetStatusDuration(card.statusEffect, StatusDuration);
                SetStatusMagnitude(card.statusEffect, CalculateStatusCardMagnitude(cardStackCount));
                // 상태이상 runtime stack은 피격 대상의 PlayerStatusController가 재적중 기준으로 관리한다.
                // StatusMagnitude는 구형 단일-status 경로 호환용이며, 실제 다중 상태 위력은 상태별 Magnitude 슬롯을 사용한다.
                StatusMagnitude = 1f;
            }

            if (card.specialEffect != SpecialEffect.None)
            {
                CurrentSpecialEffect = (int)card.specialEffect;
                SpecialValue = card.specialValue;
            }

            DiagLog.Log($"[Colosseum] CardEffect updated - DMG:{DamageMultiplier}x, SPD:{BulletSpeedMultiplier}x, " +
                $"Bounce:{BonusBounce}, AmmoBonus:{BonusAmmoCapacity}, Gravity:{GravityModifier}");
        }

        /// <summary>
        /// 강화 초기화 (필요 시)
        /// </summary>
        public void ResetAll()
        {
            DamageMultiplier = 1f;
            BulletSpeedMultiplier = 1f;
            BulletSizeMultiplier = 1f;
            BonusBounce = 0;
            BonusAmmoCapacity = 0;
            GravityModifier = 0f;
            MoveSpeedMultiplier = 1f;
            JumpForceMultiplier = 1f;
            ReloadSpeedMultiplier = 1f;
            BonusPierce = 0;
            KnockbackForce = 0f;
            HealOnKill = 0f;
            ExplosionRadius = 0f;
            ExplosionRadiusMultiplier = 1f;
            ExplosionDamageRatio = 0.5f;
            OverchargeDamageMultiplier = 1f;
            CurrentStatusEffect = 0;
            CurrentStatusEffectMask = 0;
            StatusDuration = 0f;
            StatusMagnitude = 1f;
            FreezeStatusMagnitude = 1f;
            BurnStatusMagnitude = 1f;
            PoisonStatusMagnitude = 1f;
            ElectricStatusMagnitude = 1f;
            FreezeStatusDuration = 0f;
            BurnStatusDuration = 0f;
            PoisonStatusDuration = 0f;
            ElectricStatusDuration = 0f;
            CurrentSpecialEffect = 0;
            SpecialValue = 0f;
        }

        public static int AddStatusEffectToMask(int currentMask, StatusEffectType status)
        {
            return currentMask | ToStatusEffectMask(status);
        }

        public static bool HasStatusEffect(int mask, StatusEffectType status)
        {
            int statusMask = ToStatusEffectMask(status);
            return statusMask != 0 && (mask & statusMask) != 0;
        }

        public static int ToStatusEffectMask(StatusEffectType status)
        {
            return status == StatusEffectType.None ? 0 : 1 << ((int)status - 1);
        }

        public static float CalculateBulletSizeMultiplier(float currentMultiplier, string cardKey, float cardSizeMultiplier)
        {
            if (IsLinearBulletSizeCard(cardKey))
            {
                return CalculateLinearBulletSizeMultiplier(currentMultiplier, cardSizeMultiplier);
            }

            return currentMultiplier * cardSizeMultiplier;
        }

        public static bool IsLinearBulletSizeCard(string cardKey)
        {
            return cardKey == "projectile_size_up" || cardKey == "projectile_heavy";
        }

        public static float CalculateLinearBulletSizeMultiplier(float currentMultiplier, float cardSizeMultiplier)
        {
            float current = Mathf.Max(1f, currentMultiplier);
            float perCardStep = Mathf.Max(0f, cardSizeMultiplier - 1f);
            return current + perCardStep;
        }

        public static float CalculateStatusCardMagnitude(int cardStackCount)
        {
            return Mathf.Max(1f, cardStackCount);
        }

        private void SetStatusDuration(StatusEffectType status, float duration)
        {
            switch (status)
            {
                case StatusEffectType.Freeze:
                    FreezeStatusDuration = duration;
                    break;
                case StatusEffectType.Burn:
                    BurnStatusDuration = duration;
                    break;
                case StatusEffectType.Poison:
                    PoisonStatusDuration = duration;
                    break;
                case StatusEffectType.Electric:
                    ElectricStatusDuration = duration;
                    break;
            }
        }

        private void SetStatusMagnitude(StatusEffectType status, float magnitude)
        {
            switch (status)
            {
                case StatusEffectType.Freeze:
                    FreezeStatusMagnitude = magnitude;
                    break;
                case StatusEffectType.Burn:
                    BurnStatusMagnitude = magnitude;
                    break;
                case StatusEffectType.Poison:
                    PoisonStatusMagnitude = magnitude;
                    break;
                case StatusEffectType.Electric:
                    ElectricStatusMagnitude = magnitude;
                    break;
            }
        }

        private void ApplyExplosionModifiers(CardData card)
        {
            if (card == null) return;

            float radiusBonus = Mathf.Max(0f, card.explosionRadius);
            if (radiusBonus > 0f)
            {
                bool hadExplosion = ExplosionRadius > 0f;
                ExplosionRadius += radiusBonus * GetActiveExplosionRadiusMultiplier();

                if (card.explosionDamageRatio > 0f)
                {
                    ExplosionDamageRatio = hadExplosion
                        ? ExplosionDamageRatio + card.explosionDamageRatio
                        : card.explosionDamageRatio;
                }
            }

            float radiusMultiplier = ResolveCardExplosionRadiusMultiplier(card);
            if (!Mathf.Approximately(radiusMultiplier, 1f))
            {
                ExplosionRadiusMultiplier *= radiusMultiplier;
                if (ExplosionRadius > 0f)
                {
                    ExplosionRadius *= radiusMultiplier;
                }
            }
        }

        private float GetActiveExplosionRadiusMultiplier()
        {
            return ExplosionRadiusMultiplier > 0f ? ExplosionRadiusMultiplier : 1f;
        }

        private static float ResolveCardExplosionRadiusMultiplier(CardData card)
        {
            if (card == null || card.explosionRadiusMultiplier <= 0f)
            {
                return 1f;
            }

            return card.explosionRadiusMultiplier;
        }
    }
}
