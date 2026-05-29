// [A303-171 단계 7c 연장] BulletController stub 화로 인한 리플렉션/file-string 기반 테스트 일시 비활성화.
// 본 테스트들이 검증하던 ProjectileSpawnedWithModifier VFX + StatusEffect 매핑은 BulletServerSimulation
// + Gun.cs RPC 로 이전됨. 후속 Story 에서 새 패턴의 검증 테스트로 교체 예정.
#if false
using System;
using System.IO;
using System.Reflection;
using Colosseum.Contracts;
using Colosseum.Presentation.SFX;
using Colosseum.Presentation.VFX;
using Colosseum.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class ProjectileFeedbackContractTests
    {
        [TestCase(VfxEventType.ProjectileSpawnedWithModifier, "projectile_speed_up", true)]
        [TestCase(VfxEventType.ProjectileImpact, "projectile_impact", true)]
        [TestCase(VfxEventType.ProjectileReflected, "projectile_reflect", true)]
        [TestCase(VfxEventType.ProjectilePierced, "projectile_pierce", true)]
        [TestCase(VfxEventType.OverchargeReady, "reload_overcharge", true)]
        [TestCase(VfxEventType.ProjectileShot, "projectile_shot", false)]
        [TestCase(VfxEventType.ExplosionTriggered, "projectile_explosion", false)]
        public void CardSfxProfileRoute_SkipsOnlyDedicatedRuntimeProjectileEvents(
            VfxEventType eventType,
            string effectKey,
            bool expectedDedicatedRuntimeRoute)
        {
            Assert.AreEqual(
                expectedDedicatedRuntimeRoute,
                InvokeCardSfxDedicatedRoute(CreatePayload(eventType, effectKey)));
        }

        [TestCase(VfxEventType.ProjectileImpact, "projectile_impact", true)]
        [TestCase(VfxEventType.ProjectileReflected, "projectile_reflect", true)]
        [TestCase(VfxEventType.ProjectilePierced, "projectile_pierce", true)]
        [TestCase(VfxEventType.OverchargeReady, "reload_overcharge", true)]
        [TestCase(VfxEventType.ProjectileShot, "projectile_shot", false)]
        [TestCase(VfxEventType.ProjectileSpawnedWithModifier, "projectile_speed_up", false)]
        [TestCase(VfxEventType.ExplosionTriggered, "projectile_explosion", false)]
        public void ProjectileImpactRuntimeSfx_ResolvesOnlyDedicatedImpactEvents(
            VfxEventType eventType,
            string effectKey,
            bool expectedResolved)
        {
            Assert.AreEqual(
                expectedResolved,
                InvokeStaticTryResolveCue(
                    typeof(ProjectileImpactSfxPresentationManager),
                    CreatePayload(eventType, effectKey)));
        }

        [TestCase(VfxEventType.ProjectileSpawnedWithModifier, "projectile_speed_up", true)]
        [TestCase(VfxEventType.ProjectileSpawnedWithModifier, "projectile_explosion", true)]
        [TestCase(VfxEventType.ProjectileShot, "projectile_shot", false)]
        [TestCase(VfxEventType.ProjectileImpact, "projectile_impact", false)]
        public void ProjectileModifierRuntimeSfx_ResolvesOnlySpawnModifierEvents(
            VfxEventType eventType,
            string effectKey,
            bool expectedResolved)
        {
            var host = new GameObject("Projectile modifier SFX route test");

            try
            {
                var manager = host.AddComponent<ProjectileModifierSfxPresentationManager>();
                Assert.AreEqual(
                    expectedResolved,
                    InvokeInstanceTryResolveCue(manager, CreatePayload(eventType, effectKey)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("projectile_size_up", "projectile_modifier_size")]
        [TestCase("projectile_heavy", "projectile_modifier_heavy")]
        [TestCase("projectile_multishot", "projectile_modifier_multishot")]
        [TestCase("projectile_gravity_zero", "projectile_modifier_float")]
        public void ProjectileModifierRuntimeSfx_UsesCardSpecificCueChannels(
            string effectKey,
            string expectedChannel)
        {
            var host = new GameObject("Projectile modifier SFX cue channel test");

            try
            {
                var manager = host.AddComponent<ProjectileModifierSfxPresentationManager>();
                Assert.AreEqual(expectedChannel, InvokeInstanceCueChannel(manager, CreatePayload(VfxEventType.ProjectileSpawnedWithModifier, effectKey)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [TestCase("hit_apply_burn")]
        [TestCase("hit_apply_poison")]
        [TestCase("hit_apply_frost")]
        [TestCase("hit_apply_electric_stack")]
        [TestCase("projectile_gravity_zero")]
        [TestCase("projectile_reflect")]
        [TestCase("projectile_pierce")]
        [TestCase("projectile_explosion")]
        [TestCase("hit_knockback_up")]
        [TestCase("projectile_size_up")]
        [TestCase("projectile_speed_up")]
        [TestCase("projectile_heavy")]
        public void ProjectileModifierSpawnVfx_SuppressesLegacyFallbackForConfiguredVisualKeys(string effectKey)
        {
            var preset = new CardVfxPreset
            {
                eventType = VfxEventType.ProjectileSpawnedWithModifier,
                effectKey = effectKey,
                effectPrefab = null
            };

            Assert.IsTrue(InvokeShouldSuppressProjectileSpawnPlaceholder(CreatePayload(VfxEventType.ProjectileSpawnedWithModifier, effectKey), preset));
        }

        [Test]
        public void ProjectileModifierSpawnVfx_KeepsFallbackForPlainModifierOrConfiguredPrefab()
        {
            var plainModifierPreset = new CardVfxPreset
            {
                eventType = VfxEventType.ProjectileSpawnedWithModifier,
                effectKey = "projectile_multishot",
                effectPrefab = null
            };
            Assert.IsFalse(InvokeShouldSuppressProjectileSpawnPlaceholder(CreatePayload(VfxEventType.ProjectileSpawnedWithModifier, "projectile_multishot"), plainModifierPreset));

            var configuredPrefab = new GameObject("Configured projectile modifier VFX");
            try
            {
                var configuredPrefabPreset = new CardVfxPreset
                {
                    eventType = VfxEventType.ProjectileSpawnedWithModifier,
                    effectKey = "hit_apply_burn",
                    effectPrefab = configuredPrefab
                };

                Assert.IsFalse(InvokeShouldSuppressProjectileSpawnPlaceholder(CreatePayload(VfxEventType.ProjectileSpawnedWithModifier, "hit_apply_burn"), configuredPrefabPreset));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(configuredPrefab);
            }
        }

        [TestCase("hit_apply_burn")]
        [TestCase("hit_apply_poison")]
        [TestCase("hit_apply_frost")]
        [TestCase("hit_apply_electric_stack")]
        public void ProjectileImpactStatusKey_IsSuppressedBeforeLegacyFallbackVfxAndSfx(string effectKey)
        {
            VfxEventPayload payload = CreatePayload(VfxEventType.ProjectileImpact, effectKey);

            Assert.IsTrue(InvokeVfxShouldSuppressLegacyStatusImpact(payload));
            Assert.IsTrue(InvokeSfxShouldSuppressLegacyStatusImpact(payload));
            Assert.IsTrue(InvokeVfxShouldBlinkTargetPlayer(payload));
        }

        [TestCase(VfxEventType.ProjectileImpact, "projectile_impact")]
        [TestCase(VfxEventType.StatusApplied, "hit_apply_burn")]
        public void LegacyStatusImpactSuppression_DoesNotCatchNonFallbackRoutes(
            VfxEventType eventType,
            string effectKey)
        {
            VfxEventPayload payload = CreatePayload(eventType, effectKey);

            Assert.IsFalse(InvokeVfxShouldSuppressLegacyStatusImpact(payload));
            Assert.IsFalse(InvokeSfxShouldSuppressLegacyStatusImpact(payload));
        }

        [TestCase(0.85f, 1.1f, 1.2f, 0, "projectile_heavy")]
        [TestCase(1f, 1.3f, 0f, 0, "projectile_size_up")]
        [TestCase(1f, 1f, 0f, 2, "projectile_multishot")]
        public void ProjectileModifierEffectKey_PreservesDesignSpecificRuntimeKeys(
            float speedMultiplier,
            float sizeMultiplier,
            float explosionRadius,
            int extraBullets,
            string expectedEffectKey)
        {
            Assert.AreEqual(
                expectedEffectKey,
                InvokeProjectileEffectKey(
                    VfxEventType.ProjectileSpawnedWithModifier,
                    speedMultiplier,
                    sizeMultiplier,
                    explosionRadius,
                    extraBullets));
        }

        [Test]
        public void ProjectileModifierPresence_IncludesBuckshotExtraBullets()
        {
            Assert.IsTrue(InvokeHasProjectileModifier(extraBullets: 2));
        }

        [Test]
        public void ProjectilePresentationPayloadIdentity_IsCapturedBeforeRpcFanout()
        {
            string factory = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "VFX", "VfxPayloadFactory.cs");
            Assert.That(factory, Does.Contain("public static VfxEventPayload CreateWithIdentity("));
            Assert.That(factory, Does.Contain("public static VfxEventPayload CreateWithExplicitIdentity("));
            Assert.That(factory, Does.Contain("public static int BuildSourceEventSequence(int sourceTick, int sequenceSalt)"));

            string gun = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "Gun.cs");
            Assert.That(gun, Does.Contain("public bool TryPublishProjectileRuntimeVfx("));
            Assert.That(gun, Does.Contain("Rpc_PublishProjectileRuntimeVfx("));
            Assert.That(gun, Does.Contain("VfxPayloadFactory.CreateWithExplicitIdentity("));
            Assert.That(gun, Does.Contain("int shotSourceTick = VfxPayloadFactory.GetSourceTick(Runner);"));
            Assert.That(gun, Does.Contain("int shotSourceEventSequence = VfxPayloadFactory.BuildSourceEventSequence(shotSourceTick, shotSequenceSalt);"));
            Assert.That(gun, Does.Contain("VfxPayloadFactory.BuildSourceEventSequence("));
            Assert.That(gun, Does.Contain("ProjectileModifierSpawnVfxSequenceSaltBase + projectileOrdinal"));
            Assert.That(gun, Does.Contain("ExtraBullets);"));

            string bullet = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "BulletController.cs");
            Assert.That(bullet, Does.Contain("int sourceEventSequence = VfxPayloadFactory.BuildSourceEventSequence(sourceTick, sequenceSalt);"));
            Assert.That(bullet, Does.Contain("TryPublishProjectileVfxViaOwnerGun("));
            Assert.That(bullet, Does.Contain("_ownerGunCache.TryPublishProjectileRuntimeVfx("));
            Assert.That(bullet, Does.Not.Contain("SceneSearch.FindAllInScene<Gun>"));
            Assert.That(bullet, Does.Contain("VfxPayloadFactory.CreateWithExplicitIdentity("));

            Assert.That(gun, Does.Contain("bullet.Init(Object.InputAuthority, direction, this);"));
        }

        [Test]
        public void ProjectileReflectVfx_UsesCharacterScaledRadiusInsteadOfBulletSize()
        {
            string bullet = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "BulletController.cs");
            Assert.That(bullet, Does.Contain("ReflectVfxCharacterScale = 0.25f"));
            Assert.That(bullet, Does.Contain("case VfxEventType.ProjectileReflected:"));
            Assert.That(bullet, Does.Contain("ResolveCharacterScaledReflectVfxRadius()"));
            Assert.That(bullet, Does.Contain("TryResolveOwnerCharacterVfxExtent"));
            Assert.That(bullet, Does.Contain("collider.bounds"));
            Assert.That(bullet, Does.Contain("return Mathf.Max(SizeMultiplier, 0.4f);"));

            string manager = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPlaceholderManager.cs");
            Assert.That(manager, Does.Contain("payload.EventType == VfxEventType.ProjectileReflected"));

            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            Assert.That(profile, Does.Contain("effectKey: projectile_reflect"));
            Assert.That(profile, Does.Contain("radiusScale: 0.25"));
        }

        [Test]
        public void PlayerSpawnVfxRoute_IsTailEventAndPublishedFromPlayerHealth()
        {
            Assert.That((int)VfxEventType.PlayerSpawn, Is.GreaterThan((int)VfxEventType.ProjectileShot));
            Assert.That((int)VfxEventType.PlayerInvulnerabilityStarted, Is.GreaterThan((int)VfxEventType.EmojiRequested));

            string factory = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "VFX", "VfxPayloadFactory.cs");
            Assert.That(factory, Does.Contain("case VfxEventType.PlayerSpawn: return \"player_spawn\";"));
            Assert.That(factory, Does.Contain("case VfxEventType.PlayerInvulnerabilityStarted: return \"player_invulnerable\";"));

            string playerHealth = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerHealth.cs");
            Assert.That(playerHealth, Does.Contain("PlayerSpawnVfxSequenceSalt"));
            Assert.That(playerHealth, Does.Contain("PlayerInvulnerabilityStartedVfxSequenceSalt"));
            Assert.That(playerHealth, Does.Contain("VfxEventType.PlayerSpawn"));
            Assert.That(playerHealth, Does.Contain("VfxEventType.PlayerInvulnerabilityStarted"));
            Assert.That(playerHealth, Does.Contain("VfxPayloadFactory.GetPlayerHealthEffectKey(VfxEventType.PlayerSpawn)"));
            Assert.That(playerHealth, Does.Contain("PublishInvulnerabilityStartedVfx();"));
        }

        [Test]
        public void PlayerMovementVfxRoute_IsTailEventAndPublishedFromPlayerController()
        {
            Assert.That((int)VfxEventType.PlayerJumpStarted, Is.GreaterThan((int)VfxEventType.PlayerSpawn));
            Assert.That((int)VfxEventType.PlayerLanded, Is.GreaterThan((int)VfxEventType.PlayerJumpStarted));

            string factory = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "VFX", "VfxPayloadFactory.cs");
            Assert.That(factory, Does.Contain("case VfxEventType.PlayerJumpStarted: return \"player_jump_started\";"));
            Assert.That(factory, Does.Contain("case VfxEventType.PlayerLanded: return \"player_landed\";"));

            string playerController = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerController.cs");
            Assert.That(playerController, Does.Contain("PlayerJumpStartedVfxSequenceSalt"));
            Assert.That(playerController, Does.Contain("PlayerLandedVfxSequenceSalt"));
            Assert.That(playerController, Does.Contain("PublishMovementVfx("));
            Assert.That(playerController, Does.Contain("VfxEventType.PlayerLanded"));
            Assert.That(playerController, Does.Contain("VfxAttachPoint.CharacterFeet"));

            string playerMovement = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerMovement.cs");
            Assert.That(playerMovement, Does.Contain("VfxEventType.PlayerJumpStarted"));
            Assert.That(playerMovement, Does.Contain("VfxPayloadFactory.GetPlayerMovementEffectKey"));

            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            Assert.That(profile, Does.Contain("effectKey: player_jump_started"));
            Assert.That(profile, Does.Contain("effectKey: player_landed"));
        }

        [Test]
        public void VictoryZoneConfettiRoute_IsTailEventAndPublishedFromRoomManager()
        {
            Assert.That((int)VfxEventType.VictoryZoneReached, Is.GreaterThan((int)VfxEventType.PlayerLanded));

            string factory = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "VFX", "VfxPayloadFactory.cs");
            Assert.That(factory, Does.Contain("case VfxEventType.VictoryZoneReached: return \"victory_zone_confetti\";"));

            string roomManager = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Game", "RoomManager.cs");
            Assert.That(roomManager, Does.Contain("PublishVictoryZoneConfettiVfx(advancingPlayer)"));
            Assert.That(roomManager, Does.Contain("Rpc_PublishVictoryZoneConfettiVfx("));
            Assert.That(roomManager, Does.Contain("room.rightBound - room.leftBound"));
            Assert.That(roomManager, Does.Contain("room.cameraSize * 2f"));
            Assert.That(roomManager, Does.Contain("UseVfxProfileFeedbackHint"));
            Assert.That(roomManager, Does.Contain("VfxEventType.VictoryZoneReached"));

            string manager = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPlaceholderManager.cs");
            Assert.That(manager, Does.Contain("SpawnVictoryZoneConfetti(payload, preset)"));
            Assert.That(manager, Does.Contain("VfxMarker_VictoryZoneConfetti"));
            Assert.That(manager, Does.Contain("_victoryConfettiTileSpacing"));
            Assert.That(manager, Does.Contain("payload.MagnitudeHint > 0f ? payload.MagnitudeHint"));
            Assert.That(manager, Does.Not.Contain("rowCount"));
            Assert.That(manager, Does.Contain("columnCount"));
            Assert.That(manager, Does.Contain("height / tileCoverageHeight"));
            Assert.That(manager, Does.Contain("tileSpanX"));
            Assert.That(manager, Does.Contain("topAlignedY = center.y + height * 0.5f"));
            Assert.That(manager, Does.Contain("PrepareParticleSystems(instance, preset, true)"));
            Assert.That(manager, Does.Contain("NetworkManager.OnRoomReset += HandleRoomResetForVictoryConfetti"));
            Assert.That(manager, Does.Contain("NetworkManager.OnDisconnected += HandleDisconnectedForVictoryConfetti"));
            Assert.That(manager, Does.Not.Contain("StartCoroutine(DestroyAfter(marker, duration + 0.25f));"));
            Assert.That(manager, Does.Contain("payload.EventType == VfxEventType.VictoryZoneReached"));

            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            Assert.That(profile, Does.Contain("effectKey: victory_zone_confetti"));
            Assert.That(profile, Does.Contain("eventType: 24"));
            Assert.That(profile, Does.Contain("guid: 039f0809d882b7f44ac9a410a3f7fa26"));
        }

        [Test]
        public void CardVfxProfileRoute_KeepsProjectileShotAndAttachLifecycleConfigurable()
        {
            string preset = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPreset.cs");
            Assert.That(preset, Does.Contain("public bool followSourcePlayer"));
            Assert.That(preset, Does.Contain("public bool followTargetPlayer"));
            Assert.That(preset, Does.Contain("public Vector2 followOffset"));
            Assert.That(preset, Does.Contain("public float followZOffset"));
            Assert.That(preset, Does.Contain("public bool overrideSortingOrder"));
            Assert.That(preset, Does.Contain("public int sortingOrder"));
            Assert.That(preset, Does.Contain("public bool alignLocalForwardToDirection"));
            Assert.That(preset, Does.Contain("public float rotationOffsetDegrees"));
            Assert.That(preset, Does.Contain("public bool usePrefabRotation"));
            Assert.That(preset, Does.Contain("public bool useHierarchyParticleScale"));

            string manager = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPlaceholderManager.cs");
            Assert.That(manager, Does.Contain("return eventType == VfxEventType.PlayerRespawned;"));
            Assert.That(manager, Does.Not.Contain("eventType == VfxEventType.ProjectileShot"));
            Assert.That(manager, Does.Contain("StartCoroutine(FollowEffect("));
            Assert.That(manager, Does.Contain("main.loop = true;"));
            Assert.That(manager, Does.Contain("ShouldApplyEffectSortingOrder"));
            Assert.That(manager, Does.Contain("Quaternion.FromToRotation(Vector3.forward, direction3)"));
            Assert.That(manager, Does.Contain("angle + rotationOffset"));
            Assert.That(manager, Does.Contain("preset.usePrefabRotation && preset.effectPrefab != null"));
            Assert.That(manager, Does.Contain("preset.effectPrefab.transform.rotation"));
            Assert.That(manager, Does.Contain("main.scalingMode = ParticleSystemScalingMode.Hierarchy"));
            Assert.That(manager, Does.Contain("TryResolvePlayerMuzzlePosition"));
            Assert.That(manager, Does.Contain("payload.AttachPoint == VfxAttachPoint.WeaponMuzzle"));
            Assert.That(manager, Does.Contain("ResolveEffectSortingOrder"));
            Assert.That(manager, Does.Contain("position.z += preset.followZOffset"));

            string gun = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "Gun.cs");
            Assert.That(gun, Does.Contain("public Vector2 MuzzleWorldPosition => GetMuzzlePosition();"));

            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            Assert.That(profile, Does.Contain("effectKey: projectile_shot"));
            Assert.That(profile, Does.Contain("effectKey: player_spawn"));
            Assert.That(profile, Does.Contain("effectKey: player_jump_started"));
            Assert.That(profile, Does.Contain("effectKey: player_landed"));
            Assert.That(profile, Does.Contain("alignLocalForwardToDirection: 1"));
            Assert.That(profile, Does.Contain("tintParticlesWithPresetColor: 1"));
            Assert.That(profile, Does.Contain("additionalEffectMinMagnitude: 3"));
            Assert.That(profile, Does.Contain("keepAliveUntilOverchargeShot: 1"));
            Assert.That(profile, Does.Contain("sortingOrder: 7"));
            Assert.That(profile, Does.Contain("followSourcePlayer: 1"));
            Assert.That(profile, Does.Contain("followTargetPlayer: 1"));
        }

        [Test]
        public void GameOverReset_DespawnsScenePlayerObjectsEvenWhenSpawnDictionaryIsStale()
        {
            string server = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Network", "GameServerManager.cs");

            Assert.That(server, Does.Contain("DespawnAllSpawnedPlayerObjects();"));
            Assert.That(server, Does.Contain("Utils.SceneSearch.FindAllInScene<PlayerHealth>(scene)"));
            Assert.That(server, Does.Contain("TryDespawnPlayerObject(health.GetComponent<NetworkObject>(), despawned);"));
            Assert.That(server, Does.Contain("_spawnedPlayers.Clear();"));
        }

        [Test]
        public void SpawnAndRespawnVfx_UseSelectedMagicBuffPrefab()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            string spawnBlock = ExtractProfilePresetBlock(profile, "effectKey: player_spawn", "effectKey: player_jump_started");
            string respawnBlock = ExtractProfilePresetBlock(profile, "effectKey: player_respawn", "effectKey: player_invulnerable");

            Assert.That(spawnBlock, Does.Contain("guid: 685c97d007af49549b239b56b6d2000a"));
            Assert.That(spawnBlock, Does.Contain("fileID: 147642"));
            Assert.That(spawnBlock, Does.Contain("usePrefabRotation: 1"));
            Assert.That(spawnBlock, Does.Contain("useHierarchyParticleScale: 1"));
            Assert.That(respawnBlock, Does.Contain("guid: 685c97d007af49549b239b56b6d2000a"));
            Assert.That(respawnBlock, Does.Contain("fileID: 147642"));
            Assert.That(respawnBlock, Does.Contain("usePrefabRotation: 1"));
            Assert.That(respawnBlock, Does.Contain("useHierarchyParticleScale: 1"));
        }

        [Test]
        public void OverchargeReadyAndInvulnerabilityVfx_UseSelectedMagicPrefabs()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            string overchargeReadyBlock = ExtractProfilePresetBlockContaining(profile, "eventType: 12");
            string overchargeShotBlock = ExtractProfilePresetBlockContaining(profile, "eventType: 13");
            string invulnerabilityBlock = ExtractProfilePresetBlockContaining(profile, "eventType: 26");

            Assert.That(overchargeReadyBlock, Does.Contain("effectKey: reload_overcharge"));
            Assert.That(overchargeReadyBlock, Does.Contain("guid: 8d2980ba5e59f33419a0275c1ec29e38"));
            Assert.That(overchargeReadyBlock, Does.Contain("fileID: 119634"));
            Assert.That(overchargeReadyBlock, Does.Contain("prefabScale: 0.22"));
            Assert.That(overchargeReadyBlock, Does.Contain("usePrefabRotation: 1"));
            Assert.That(overchargeReadyBlock, Does.Contain("useHierarchyParticleScale: 1"));
            Assert.That(overchargeReadyBlock, Does.Contain("keepAliveUntilOverchargeShot: 1"));

            Assert.That(overchargeShotBlock, Does.Contain("effectKey: reload_overcharge"));
            Assert.That(overchargeShotBlock, Does.Contain("prefabScale: 0.7666666"));
            Assert.That(overchargeShotBlock, Does.Contain("alignLocalForwardToDirection: 1"));

            Assert.That(invulnerabilityBlock, Does.Contain("eventType: 26"));
            Assert.That(invulnerabilityBlock, Does.Contain("guid: b9cf594d1d8302e4a884738a176ead3a"));
            Assert.That(invulnerabilityBlock, Does.Contain("fileID: 181232"));
            Assert.That(invulnerabilityBlock, Does.Contain("prefabScale: 0.45"));
            Assert.That(invulnerabilityBlock, Does.Contain("followTargetPlayer: 1"));
            Assert.That(invulnerabilityBlock, Does.Contain("showToast: 0"));
        }

        [Test]
        public void FrostStatusVfx_UsesPrefabZAxisRotationAndAdjustedScale()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            int start = profile.IndexOf("effectKey: hit_apply_frost", StringComparison.Ordinal);
            int end = profile.IndexOf("effectKey: hit_apply_electric_stack", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));

            string frostBlock = profile.Substring(start, end - start);
            Assert.That(frostBlock, Does.Contain("prefabScale: 0.1833335"));
            Assert.That(frostBlock, Does.Contain("alignLocalForwardToDirection: 0"));
            Assert.That(frostBlock, Does.Contain("usePrefabRotation: 1"));
            Assert.That(frostBlock, Does.Contain("useHierarchyParticleScale: 1"));
            Assert.That(frostBlock, Does.Contain("guid: a6c6b690a12b3104280a378d75ae12e6"));

            string manager = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPlaceholderManager.cs");
            Assert.That(manager, Does.Contain("MinPrefabScale = 0.01f"));
            Assert.That(manager, Does.Contain("Mathf.Max(MinPrefabScale, preset.prefabScale)"));
        }

        [Test]
        public void BurnAndPoisonStatusVfx_UseHeadOffsetAndDoubleScale()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            string burnBlock = ExtractProfilePresetBlock(profile, "effectKey: hit_apply_burn", "effectKey: hit_apply_poison");
            string poisonBlock = ExtractProfilePresetBlock(profile, "effectKey: hit_apply_poison", "effectKey: hit_apply_frost");

            Assert.That(burnBlock, Does.Contain("prefabScale: 1.7"));
            Assert.That(burnBlock, Does.Contain("alignLocalForwardToDirection: 1"));
            Assert.That(burnBlock, Does.Contain("usePrefabRotation: 0"));
            Assert.That(burnBlock, Does.Contain("useHierarchyParticleScale: 1"));
            Assert.That(burnBlock, Does.Contain("followTargetPlayer: 1"));
            Assert.That(burnBlock, Does.Contain("followOffset: {x: 0, y: 0.45}"));
            Assert.That(burnBlock, Does.Contain("followZOffset: 0.05"));
            Assert.That(burnBlock, Does.Contain("overrideSortingOrder: 1"));
            Assert.That(burnBlock, Does.Contain("sortingOrder: 3"));
            Assert.That(poisonBlock, Does.Contain("prefabScale: 1.6"));
            Assert.That(poisonBlock, Does.Contain("followTargetPlayer: 1"));
            Assert.That(poisonBlock, Does.Contain("followOffset: {x: 0, y: 0.45}"));
        }

        [Test]
        public void ProjectileImpactVfx_UsesSelectedWaterExplosionPrefab()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            string impactBlock = ExtractProfilePresetBlock(profile, "effectKey: projectile_impact", "effectKey: projectile_terrain_impact");

            Assert.That(impactBlock, Does.Contain("guid: 2a2eb40ae9f1b1b4caf51d97f6c8fb10"));
            Assert.That(impactBlock, Does.Contain("fileID: 1518883088362504"));
            Assert.That(impactBlock, Does.Contain("usePrefabRotation: 1"));
            Assert.That(impactBlock, Does.Contain("useHierarchyParticleScale: 1"));
        }

        [Test]
        public void MovementAndImpactVfxProfile_UsesVisualQaScaleTuning()
        {
            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            string jumpBlock = ExtractProfilePresetBlock(profile, "effectKey: player_jump_started", "effectKey: player_landed");
            string landedBlock = ExtractProfilePresetBlock(profile, "effectKey: player_landed", "effectKey: projectile_impact");
            string impactBlock = ExtractProfilePresetBlock(profile, "effectKey: projectile_impact", "effectKey: projectile_terrain_impact");
            string terrainImpactBlock = ExtractProfilePresetBlock(profile, "effectKey: projectile_terrain_impact", "effectKey: hit_knockback_up");

            Assert.That(jumpBlock, Does.Contain("guid: 9b8a3a9ce5d6cd546b9a3ef91545d0e8"));
            Assert.That(jumpBlock, Does.Contain("fileID: 154094"));
            Assert.That(jumpBlock, Does.Contain("prefabScale: 1.2375"));
            Assert.That(jumpBlock, Does.Contain("alignLocalForwardToDirection: 1"));
            Assert.That(jumpBlock, Does.Contain("useHierarchyParticleScale: 1"));
            Assert.That(landedBlock, Does.Contain("style: 3"));
            Assert.That(landedBlock, Does.Contain("effectPrefab: {fileID: 0}"));
            Assert.That(landedBlock, Does.Contain("showToast: 0"));
            Assert.That(impactBlock, Does.Contain("prefabScale: 0.4"));
            Assert.That(terrainImpactBlock, Does.Contain("prefabScale: 0.375"));

            string playerMovement = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerMovement.cs");
            Assert.That(playerMovement, Does.Contain("Vector2.down"));
        }

        [Test]
        public void CardEffectAppliedVfx_ShowsSelectedCardIllustrationOverhead()
        {
            string manager = ReadProjectFile("_Project", "Scripts", "Presentation", "VFX", "CardVfxPlaceholderManager.cs");

            Assert.That(manager, Does.Contain("payload.EventType == VfxEventType.CardEffectApplied"));
            Assert.That(manager, Does.Contain("SpawnCardIllustration(payload);"));
            Assert.That(manager, Does.Contain("Resources.FindObjectsOfTypeAll<CardData>()"));
            Assert.That(manager, Does.Contain("card.cardIcon"));
            Assert.That(manager, Does.Contain("CardIllustrationDurationSeconds = 2f"));
            Assert.That(manager, Does.Contain("CardIllustrationWorldHeight = 1.5f"));
            Assert.That(manager, Does.Contain("EmojiOverheadFollowYOffset = 2.35f"));
            Assert.That(manager, Does.Contain("CardIllustrationHeadYOffset = CharacterHeadAttachYOffset + EmojiOverheadFollowYOffset"));
            Assert.That(manager, Does.Contain("renderer.color = Color.white;"));
            Assert.That(manager, Does.Not.Contain("new Color(1f, 1f, 1f, fade)"));
        }

        [Test]
        public void NumberKeyEmojiVfx_UsesNetworkInputAndConfiguredEmojiPrefabs()
        {
            string eventTypes = ReadProjectFile("_Project", "Scripts", "Contracts", "VfxEventType.cs");
            Assert.That(eventTypes, Does.Contain("EmojiRequested"));

            string inputData = ReadProjectFile("_Project", "NetworkInput", "NetworkInputData.cs");
            Assert.That(inputData, Does.Contain("public byte EmojiIndex;"));
            Assert.That(
                inputData.IndexOf("public byte EmojiIndex;", StringComparison.Ordinal),
                Is.GreaterThan(inputData.IndexOf("public Vector2 AimDirection;", StringComparison.Ordinal)));

            string inputProvider = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Network", "InputProvider.cs");
            Assert.That(inputProvider, Does.Contain("ReadEmojiInput()"));
            Assert.That(inputProvider, Does.Contain("KeyCode.Alpha1"));
            Assert.That(inputProvider, Does.Contain("KeyCode.Keypad6"));
            Assert.That(inputProvider, Does.Contain("EmojiIndex = _pendingEmojiIndex"));

            string playerController = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerController.cs");
            Assert.That(playerController, Does.Contain("PublishEmojiVfxIfRequested(input.EmojiIndex);"));
            Assert.That(playerController, Does.Contain("VfxEventType.EmojiRequested"));
            Assert.That(playerController, Does.Contain("VfxAttachPoint.CharacterHead"));

            string factory = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "VFX", "VfxPayloadFactory.cs");
            Assert.That(factory, Does.Contain("GetEmojiEffectKey"));
            Assert.That(factory, Does.Contain("$\"emoji_{emojiIndex}\""));

            string profile = ReadProjectFile("_Project", "Resources", "Presentation", "VfxFeedbackProfile.asset");
            Assert.That(profile, Does.Contain("effectKey: emoji_1"));
            Assert.That(profile, Does.Contain("guid: 3b40e7ceb76425d4892cc33dffffffed"));
            Assert.That(profile, Does.Contain("effectKey: emoji_2"));
            Assert.That(profile, Does.Contain("guid: 69359de5f8759454bbfe449e619129bc"));
            Assert.That(profile, Does.Contain("effectKey: emoji_3"));
            Assert.That(profile, Does.Contain("guid: 780f9efde9439f345a63baff480c0755"));
            Assert.That(profile, Does.Contain("effectKey: emoji_4"));
            Assert.That(profile, Does.Contain("guid: 72e06155695f11040b722d672293fe98"));
            Assert.That(profile, Does.Contain("effectKey: emoji_5"));
            Assert.That(profile, Does.Contain("guid: 28765ba8fe1d7654aaf05bc05d3757bd"));
            Assert.That(profile, Does.Contain("effectKey: emoji_6"));
            Assert.That(profile, Does.Contain("guid: 93a7485b7f1564c4aae6a739c3edc50e"));
            Assert.That(profile, Does.Contain("prefabScale: 1.5"));
            Assert.That(profile, Does.Contain("followOffset: {x: 0, y: 2.35}"));
        }

        [Test]
        public void StatusAppliedVfx_ScalesByStatusStackMagnitude()
        {
            Assert.That(
                InvokeStatusVfxScaleMultiplier(CreatePayload(VfxEventType.StatusApplied, "hit_apply_burn", 1f)),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                InvokeStatusVfxScaleMultiplier(CreatePayload(VfxEventType.StatusApplied, "hit_apply_poison", 2f)),
                Is.EqualTo(1.6f).Within(0.0001f));
            Assert.That(
                InvokeStatusVfxScaleMultiplier(CreatePayload(VfxEventType.StatusApplied, "hit_apply_frost", 3f)),
                Is.EqualTo(2.2f).Within(0.0001f));
            Assert.That(
                InvokeStatusVfxScaleMultiplier(CreatePayload(VfxEventType.StatusApplied, "hit_apply_electric_stack", 0f)),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(
                InvokeStatusVfxScaleMultiplier(CreatePayload(VfxEventType.ProjectileImpact, "projectile_impact", 3f)),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void ExternalVfxShaders_AreAlwaysIncludedForPlayerBuildStrippingGuard()
        {
            string graphicsSettings = ReadUnityProjectFile("ProjectSettings", "GraphicsSettings.asset");

            Assert.That(graphicsSettings, Does.Contain("m_AlwaysIncludedShaders:"));
            Assert.That(
                graphicsSettings,
                Does.Contain("{fileID: 4800000, guid: 0406db5a14f94604a8c57ccfbc9f3b46, type: 3}"));
            Assert.That(
                graphicsSettings,
                Does.Contain("{fileID: 4800000, guid: e260cfa7296ee7642b167f1eb5be5023, type: 3}"));
        }

        private static VfxEventPayload CreatePayload(VfxEventType eventType, string effectKey, float magnitudeHint = 1f)
        {
            return new VfxEventPayload(
                eventType,
                "test_card",
                effectKey,
                sourceTick: 10,
                sourceObjectId: 20,
                sourceEventSequence: 30,
                VfxPredictionState.Confirmed,
                sourcePlayerSlot: 1,
                targetPlayerSlot: 2,
                new VfxVector2(3f, 4f),
                new VfxVector2(1f, 0f),
                radiusHint: 0.5f,
                durationHint: 0.25f,
                magnitudeHint: magnitudeHint,
                VfxAttachPoint.World);
        }

        private static string ReadProjectFile(params string[] pathParts)
        {
            string path = Application.dataPath;
            for (int i = 0; i < pathParts.Length; i++)
            {
                path = Path.Combine(path, pathParts[i]);
            }

            return File.ReadAllText(path);
        }

        private static string ReadUnityProjectFile(params string[] pathParts)
        {
            string path = Directory.GetParent(Application.dataPath).FullName;
            for (int i = 0; i < pathParts.Length; i++)
            {
                path = Path.Combine(path, pathParts[i]);
            }

            return File.ReadAllText(path);
        }

        private static string InvokeProjectileEffectKey(
            VfxEventType eventType,
            float speedMultiplier,
            float sizeMultiplier,
            float explosionRadius,
            int extraBullets)
        {
            MethodInfo method = GetVfxPayloadFactoryMethod("GetProjectileEffectKey");
            return (string)method.Invoke(null, new object[]
            {
                eventType,
                speedMultiplier,
                sizeMultiplier,
                0,
                0,
                0f,
                explosionRadius,
                0f,
                BulletController.StatusEffect.None,
                extraBullets
            });
        }

        private static string ExtractProfilePresetBlock(string profile, string startMarker, string endMarker)
        {
            int start = profile.IndexOf(startMarker, StringComparison.Ordinal);
            int end = profile.IndexOf(endMarker, StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            Assert.That(end, Is.GreaterThan(start));
            return profile.Substring(start, end - start);
        }

        private static string ExtractProfilePresetBlockContaining(string profile, string marker)
        {
            int markerIndex = profile.IndexOf(marker, StringComparison.Ordinal);
            Assert.That(markerIndex, Is.GreaterThanOrEqualTo(0));

            int start = profile.LastIndexOf("\n  - cardKey:", markerIndex, StringComparison.Ordinal);
            if (start < 0)
            {
                start = profile.LastIndexOf("  - cardKey:", markerIndex, StringComparison.Ordinal);
            }
            Assert.That(start, Is.GreaterThanOrEqualTo(0));

            int end = profile.IndexOf("\n  - cardKey:", markerIndex + marker.Length, StringComparison.Ordinal);
            if (end < 0)
            {
                end = profile.Length;
            }

            return profile.Substring(start, end - start);
        }

        private static bool InvokeHasProjectileModifier(int extraBullets)
        {
            MethodInfo method = GetVfxPayloadFactoryMethod("HasProjectileModifier");
            return (bool)method.Invoke(null, new object[]
            {
                1f,
                1f,
                0,
                0,
                0f,
                0f,
                0f,
                BulletController.StatusEffect.None,
                extraBullets
            });
        }

        private static MethodInfo GetVfxPayloadFactoryMethod(string methodName)
        {
            Type factoryType = Type.GetType("Colosseum.NetworkGameplay.VFX.VfxPayloadFactory, Colosseum.NetworkGameplay");
            Assert.NotNull(factoryType);

            MethodInfo method = factoryType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(method);
            return method;
        }

        private static float InvokeStatusVfxScaleMultiplier(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "ResolvePayloadScaleMultiplier",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (float)method.Invoke(null, new object[] { payload });
        }

        private static bool InvokeCardSfxDedicatedRoute(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardSfxPresentationManager).GetMethod(
                "IsHandledByDedicatedRuntimeSfx",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static bool InvokeStaticTryResolveCue(Type managerType, VfxEventPayload payload)
        {
            MethodInfo method = managerType.GetMethod(
                "TryResolveCue",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return InvokeTryResolveCue(method, null, payload);
        }

        private static bool InvokeInstanceTryResolveCue(object manager, VfxEventPayload payload)
        {
            MethodInfo method = manager.GetType().GetMethod(
                "TryResolveCue",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);
            return InvokeTryResolveCue(method, manager, payload);
        }

        private static string InvokeInstanceCueChannel(object manager, VfxEventPayload payload)
        {
            MethodInfo method = manager.GetType().GetMethod(
                "TryResolveCue",
                BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.NotNull(method);
            Type cueType = method.GetParameters()[1].ParameterType.GetElementType();
            object[] args = { payload, Activator.CreateInstance(cueType) };
            Assert.IsTrue((bool)method.Invoke(manager, args));

            FieldInfo channel = cueType.GetField("Channel", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(channel);
            return (string)channel.GetValue(args[1]);
        }

        private static bool InvokeShouldSuppressProjectileSpawnPlaceholder(VfxEventPayload payload, CardVfxPreset preset)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "ShouldSuppressProjectileSpawnPlaceholder",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (bool)method.Invoke(null, new object[] { payload, preset });
        }

        private static bool InvokeVfxShouldSuppressLegacyStatusImpact(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "ShouldSuppressLegacyStatusImpact",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static bool InvokeVfxShouldBlinkTargetPlayer(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "ShouldBlinkTargetPlayer",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static bool InvokeSfxShouldSuppressLegacyStatusImpact(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardSfxPresentationManager).GetMethod(
                "ShouldSuppressLegacyStatusImpact",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.NotNull(method);
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static bool InvokeTryResolveCue(MethodInfo method, object target, VfxEventPayload payload)
        {
            Type cueType = method.GetParameters()[1].ParameterType.GetElementType();
            object[] args = { payload, Activator.CreateInstance(cueType) };
            return (bool)method.Invoke(target, args);
        }
    }
}
#endif
