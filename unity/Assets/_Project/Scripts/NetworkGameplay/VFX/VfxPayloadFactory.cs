using Fusion;
using UnityEngine;
using Colosseum.Contracts;
using Colosseum.Weapon;

namespace Colosseum.NetworkGameplay.VFX
{
    internal static class VfxPayloadFactory
    {
        public static VfxEventPayload Create(
            VfxEventType eventType,
            string cardKey,
            string effectKey,
            NetworkRunner runner,
            NetworkObject sourceObject,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            Vector2 worldPosition,
            Vector2 direction,
            float radiusHint,
            float durationHint,
            float magnitudeHint,
            VfxAttachPoint attachPoint,
            int sequenceSalt = 0)
        {
            int tick = runner != null ? runner.Tick.Raw : 0;
            int objectId = sourceObject != null ? unchecked((int)sourceObject.Id.Raw) : 0;
            int sequence = tick != 0 ? unchecked(tick * 31 + sequenceSalt) : sequenceSalt;

            return new VfxEventPayload(
                eventType,
                cardKey,
                effectKey,
                tick,
                objectId,
                sequence,
                VfxPredictionState.Confirmed,
                sourcePlayer.PlayerId,
                targetPlayer.PlayerId,
                new VfxVector2(worldPosition.x, worldPosition.y),
                new VfxVector2(direction.x, direction.y),
                radiusHint,
                durationHint,
                magnitudeHint,
                attachPoint);
        }

        public static string GetStatusEffectKey(BulletController.StatusEffect status)
        {
            switch (status)
            {
                case BulletController.StatusEffect.Freeze: return "hit_apply_frost";
                case BulletController.StatusEffect.Burn: return "hit_apply_burn";
                case BulletController.StatusEffect.Poison: return "hit_apply_poison";
                case BulletController.StatusEffect.Electric: return "hit_apply_electric_stack";
                default: return string.Empty;
            }
        }

        public static string GetProjectileEffectKey(
            VfxEventType eventType,
            float speedMultiplier,
            float sizeMultiplier,
            int bounceCount,
            int pierceCount,
            float knockbackForce,
            float explosionRadius,
            BulletController.StatusEffect status)
        {
            switch (eventType)
            {
                case VfxEventType.ExplosionTriggered: return "projectile_explosion";
                case VfxEventType.ProjectileReflected: return "projectile_reflect";
                case VfxEventType.ProjectilePierced: return "projectile_pierce";
                case VfxEventType.ProjectileSpawnedWithModifier:
                    return GetProjectileModifierEffectKey(
                        speedMultiplier,
                        sizeMultiplier,
                        bounceCount,
                        pierceCount,
                        knockbackForce,
                        explosionRadius,
                        status);
            }

            string statusKey = GetStatusEffectKey(status);
            if (!string.IsNullOrEmpty(statusKey)) return statusKey;
            if (knockbackForce > 0f) return "hit_knockback_up";
            if (sizeMultiplier > 1.01f) return "projectile_size_up";
            if (speedMultiplier > 1.01f) return "projectile_speed_up";
            return "projectile_impact";
        }

        public static bool HasProjectileModifier(
            float speedMultiplier,
            float sizeMultiplier,
            int bounceCount,
            int pierceCount,
            float knockbackForce,
            float explosionRadius,
            BulletController.StatusEffect status)
        {
            return speedMultiplier > 1.01f ||
                sizeMultiplier > 1.01f ||
                bounceCount > 0 ||
                pierceCount > 0 ||
                knockbackForce > 0f ||
                explosionRadius > 0f ||
                status != BulletController.StatusEffect.None;
        }

        public static string GetReloadEffectKey(VfxEventType eventType)
        {
            switch (eventType)
            {
                case VfxEventType.ReloadStarted: return "reload_started";
                case VfxEventType.ReloadCompleted: return "reload_completed";
                case VfxEventType.OverchargeReady:
                case VfxEventType.OverchargeShotFired:
                    return "reload_overcharge";
                default: return "reload";
            }
        }

        public static string GetPlayerHealthEffectKey(VfxEventType eventType)
        {
            switch (eventType)
            {
                case VfxEventType.PlayerDeath: return "player_death";
                case VfxEventType.PlayerRespawn: return "player_respawn";
                case VfxEventType.PlayerHealOnKill: return "heal_on_kill";
                default: return "player_health";
            }
        }

        public static string GetEnvironmentEffectKey(VfxEventType eventType)
        {
            switch (eventType)
            {
                case VfxEventType.SegmentBoundaryAdvanced: return "segment_advance";
                case VfxEventType.PitFallDeath: return "pit_fall_death";
                default: return string.Empty;
            }
        }

        private static string GetProjectileModifierEffectKey(
            float speedMultiplier,
            float sizeMultiplier,
            int bounceCount,
            int pierceCount,
            float knockbackForce,
            float explosionRadius,
            BulletController.StatusEffect status)
        {
            string statusKey = GetStatusEffectKey(status);
            if (!string.IsNullOrEmpty(statusKey)) return statusKey;
            if (explosionRadius > 0f) return "projectile_explosion";
            if (pierceCount > 0) return "projectile_pierce";
            if (bounceCount > 0) return "projectile_reflect";
            if (knockbackForce > 0f) return "hit_knockback_up";
            if (sizeMultiplier > 1.01f) return "projectile_size_up";
            if (speedMultiplier > 1.01f) return "projectile_speed_up";
            return "projectile_modifier";
        }
    }
}
