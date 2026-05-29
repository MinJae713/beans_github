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
            int sourceTick = GetSourceTick(runner);
            int sourceEventSequence = BuildSourceEventSequence(sourceTick, sequenceSalt);

            return CreateWithIdentity(
                eventType,
                cardKey,
                effectKey,
                sourceObject,
                sourcePlayer,
                targetPlayer,
                worldPosition,
                direction,
                radiusHint,
                durationHint,
                magnitudeHint,
                attachPoint,
                sourceTick,
                sourceEventSequence);
        }

        public static VfxEventPayload CreateWithIdentity(
            VfxEventType eventType,
            string cardKey,
            string effectKey,
            NetworkObject sourceObject,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            Vector2 worldPosition,
            Vector2 direction,
            float radiusHint,
            float durationHint,
            float magnitudeHint,
            VfxAttachPoint attachPoint,
            int sourceTick,
            int sourceEventSequence)
        {
            int objectId = sourceObject != null ? unchecked((int)sourceObject.Id.Raw) : 0;

            return CreateWithExplicitIdentity(
                eventType,
                cardKey,
                effectKey,
                objectId,
                sourcePlayer,
                targetPlayer,
                worldPosition,
                direction,
                radiusHint,
                durationHint,
                magnitudeHint,
                attachPoint,
                sourceTick,
                sourceEventSequence);
        }

        public static VfxEventPayload CreateWithExplicitIdentity(
            VfxEventType eventType,
            string cardKey,
            string effectKey,
            int sourceObjectId,
            PlayerRef sourcePlayer,
            PlayerRef targetPlayer,
            Vector2 worldPosition,
            Vector2 direction,
            float radiusHint,
            float durationHint,
            float magnitudeHint,
            VfxAttachPoint attachPoint,
            int sourceTick,
            int sourceEventSequence)
        {
            return new VfxEventPayload(
                eventType,
                cardKey,
                effectKey,
                sourceTick,
                sourceObjectId,
                sourceEventSequence,
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

        public static int GetSourceTick(NetworkRunner runner)
        {
            return runner != null ? runner.Tick.Raw : 0;
        }

        public static int BuildSourceEventSequence(int sourceTick, int sequenceSalt)
        {
            return sourceTick != 0 ? unchecked(sourceTick * 31 + sequenceSalt) : sequenceSalt;
        }

        public static string GetStatusEffectKey(StatusEffect status)
        {
            switch (status)
            {
                case StatusEffect.Freeze: return "hit_apply_frost";
                case StatusEffect.Burn: return "hit_apply_burn";
                case StatusEffect.Poison: return "hit_apply_poison";
                case StatusEffect.Electric: return "hit_apply_electric_stack";
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
            float gravityModifier,
            StatusEffect status,
            int extraBullets = 0)
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
                        gravityModifier,
                        status,
                        extraBullets);
            }

            string statusKey = GetStatusEffectKey(status);
            if (!string.IsNullOrEmpty(statusKey)) return statusKey;
            if (knockbackForce > 0f) return "hit_knockback_up";
            if (IsHeavyProjectileModifier(speedMultiplier, sizeMultiplier)) return "projectile_heavy";
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
            float gravityModifier,
            StatusEffect status,
            int extraBullets = 0)
        {
            return speedMultiplier > 1.01f ||
                sizeMultiplier > 1.01f ||
                IsHeavyProjectileModifier(speedMultiplier, sizeMultiplier) ||
                extraBullets > 0 ||
                bounceCount > 0 ||
                pierceCount > 0 ||
                knockbackForce > 0f ||
                explosionRadius > 0f ||
                Mathf.Abs(gravityModifier) > 0.001f ||
                status != StatusEffect.None;
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
                case VfxEventType.PlayerSpawn: return "player_spawn";
                case VfxEventType.PlayerDeath: return "player_death";
                case VfxEventType.PlayerRespawn: return "player_respawn";
                case VfxEventType.PlayerHealOnKill: return "heal_on_kill";
                case VfxEventType.PlayerInvulnerabilityStarted: return "player_invulnerable";
                default: return "player_health";
            }
        }

        public static string GetPlayerMovementEffectKey(VfxEventType eventType)
        {
            switch (eventType)
            {
                case VfxEventType.PlayerJumpStarted: return "player_jump_started";
                case VfxEventType.PlayerLanded: return "player_landed";
                default: return "player_movement";
            }
        }

        public static string GetEnvironmentEffectKey(VfxEventType eventType)
        {
            switch (eventType)
            {
                case VfxEventType.SegmentBoundaryAdvanced: return "segment_advance";
                case VfxEventType.VictoryZoneReached: return "victory_zone_confetti";
                case VfxEventType.PitFallDeath: return "pit_fall_death";
                default: return string.Empty;
            }
        }

        public static string GetEmojiEffectKey(byte emojiIndex)
        {
            return emojiIndex >= 1 && emojiIndex <= 6 ? $"emoji_{emojiIndex}" : string.Empty;
        }

        private static string GetProjectileModifierEffectKey(
            float speedMultiplier,
            float sizeMultiplier,
            int bounceCount,
            int pierceCount,
            float knockbackForce,
            float explosionRadius,
            float gravityModifier,
            StatusEffect status,
            int extraBullets)
        {
            string statusKey = GetStatusEffectKey(status);
            if (!string.IsNullOrEmpty(statusKey)) return statusKey;
            if (IsHeavyProjectileModifier(speedMultiplier, sizeMultiplier)) return "projectile_heavy";
            if (explosionRadius > 0f) return "projectile_explosion";
            if (pierceCount > 0) return "projectile_pierce";
            if (bounceCount > 0) return "projectile_reflect";
            if (Mathf.Abs(gravityModifier) > 0.001f) return "projectile_gravity_zero";
            if (knockbackForce > 0f) return "hit_knockback_up";
            if (extraBullets > 0) return "projectile_multishot";
            if (sizeMultiplier > 1.01f) return "projectile_size_up";
            if (speedMultiplier > 1.01f) return "projectile_speed_up";
            return "projectile_modifier";
        }

        private static bool IsHeavyProjectileModifier(float speedMultiplier, float sizeMultiplier)
        {
            return speedMultiplier < 0.99f && sizeMultiplier > 1.01f;
        }
    }
}
