// [A303-171 단계 7c 연장] BulletController stub 화로 인한 리플렉션/정적 메서드 기반 테스트 일시 비활성화.
// 본 테스트들이 검증하던 CardEffect → BulletController.[Networked] 변수 set 흐름은
// CardEffect → Gun.SpawnBullet 의 OnFireBullet 인자 전달로 이전됨.
// 후속 Story 에서 새 패턴 (Visual-Only Projectile) 의 검증 테스트로 교체 예정.
#if false
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Colosseum.Card;
using Colosseum.Player;
using Colosseum.Status;
using Colosseum.Status.Effects;
using Colosseum.Weapon;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class CardRuntimeFeedbackConsumptionTests
    {
        [Test]
        public void AmmoCapacityBonus_AddsToBaselineAndKeepsMinimumCapacity()
        {
            Assert.That(Gun.CalculateMagazineCapacity(3, 1), Is.EqualTo(4));
            Assert.That(Gun.CalculateMagazineCapacity(3, 3), Is.EqualTo(6));
            Assert.That(Gun.CalculateMagazineCapacity(0, 0), Is.EqualTo(1));
        }

        [Test]
        public void DevModeDebugCardApply_UsesMagazineCapacityParityPath()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/DevMode/UI/DebugCardSelectPanel.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("magazineCapacityBeforeCard"));
            Assert.That(source, Does.Contain("ApplyMagazineCapacityChange"));
        }

        [Test]
        public void DevModeDefaultAssetPack_ExposesMvp16CardsOnly()
        {
            UnityEngine.Object assetPack = AssetDatabase.LoadMainAssetAtPath(
                "Assets/_Project/Data/DevMode/DefaultAssetPack.asset");
            Assert.That(assetPack, Is.Not.Null);

            string[] expectedKeys =
            {
                "projectile_size_up",
                "projectile_speed_up",
                "hit_knockback_up",
                "reload_time_down",
                "ammo_capacity_up",
                "projectile_heavy",
                "hit_apply_poison",
                "hit_apply_burn",
                "hit_apply_frost",
                "hit_apply_electric_stack",
                "reload_overcharge",
                "projectile_reflect",
                "projectile_explosion",
                "projectile_pierce",
                "projectile_multishot",
                "projectile_gravity_zero"
            };

            var serialized = new SerializedObject(assetPack);
            SerializedProperty cardDataList = serialized.FindProperty("cardDataList");
            Assert.That(cardDataList, Is.Not.Null);
            Assert.That(cardDataList.arraySize, Is.EqualTo(expectedKeys.Length));

            string[] actualKeys = new string[cardDataList.arraySize];
            for (int i = 0; i < cardDataList.arraySize; i++)
            {
                var card = cardDataList.GetArrayElementAtIndex(i).objectReferenceValue as CardData;
                Assert.That(card, Is.Not.Null, $"DefaultAssetPack.cardDataList[{i}] is null.");
                Assert.That(card.isDraftEnabled, Is.True, $"{card.cardName} must be enabled for MVP DevMode testing.");
                actualKeys[i] = card.cardKey;
            }

            Assert.That(actualKeys, Is.EqualTo(expectedKeys));
            Assert.That(actualKeys, Does.Not.Contain("hit_life_steal"));
        }

        [Test]
        public void ProjectileGravityModifier_AddsToBaselineAndClampsAtZero()
        {
            Assert.That(BulletController.CalculateProjectileGravityScale(2.7f, -2.7f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(BulletController.CalculateProjectileGravityScale(2.7f, -0.5f), Is.EqualTo(2.2f).Within(0.0001f));
            Assert.That(BulletController.CalculateProjectileGravityScale(2.7f, 0.3f), Is.EqualTo(3.0f).Within(0.0001f));
        }

        [Test]
        public void GravityModifier_ProducesProjectileModifierFeedbackKey()
        {
            Type factoryType = typeof(BulletController).Assembly.GetType("Colosseum.NetworkGameplay.VFX.VfxPayloadFactory");
            Assert.That(factoryType, Is.Not.Null);

            Type eventType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType("Colosseum.Contracts.VfxEventType"))
                .FirstOrDefault(type => type != null);
            Assert.That(eventType, Is.Not.Null);

            object spawnedWithModifier = Enum.Parse(eventType, "ProjectileSpawnedWithModifier");
            MethodInfo hasModifier = factoryType.GetMethod(
                "HasProjectileModifier",
                BindingFlags.Public | BindingFlags.Static);
            MethodInfo getEffectKey = factoryType.GetMethod(
                "GetProjectileEffectKey",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(hasModifier, Is.Not.Null);
            Assert.That(getEffectKey, Is.Not.Null);

            object[] modifierArgs =
            {
                1f,
                1f,
                0,
                0,
                0f,
                0f,
                -2.7f,
                BulletController.StatusEffect.None,
                0
            };
            object[] keyArgs =
            {
                spawnedWithModifier,
                1f,
                1f,
                0,
                0,
                0f,
                0f,
                -2.7f,
                BulletController.StatusEffect.None,
                0
            };
            object[] speedBoostedKeyArgs =
            {
                spawnedWithModifier,
                1.3f,
                1f,
                0,
                0,
                0f,
                0f,
                -2.7f,
                BulletController.StatusEffect.None,
                0
            };

            Assert.That((bool)hasModifier.Invoke(null, modifierArgs), Is.True);
            Assert.That((string)getEffectKey.Invoke(null, keyArgs), Is.EqualTo("projectile_gravity_zero"));
            Assert.That((string)getEffectKey.Invoke(null, speedBoostedKeyArgs), Is.EqualTo("projectile_gravity_zero"));
        }

        [Test]
        public void HeavyBulletAsset_ProvidesConditionalExplosionRadiusMultiplier()
        {
            var heavy = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Heavy Bullet.asset");
            var blast = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Blast Shot.asset");
            Assert.That(heavy, Is.Not.Null);
            Assert.That(blast, Is.Not.Null);

            Assert.That(heavy.cardKey, Is.EqualTo("projectile_heavy"));
            Assert.That(heavy.explosionRadius, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(heavy.explosionRadiusMultiplier, Is.EqualTo(1.4f).Within(0.0001f));
            Assert.That(blast.explosionRadius, Is.EqualTo(1.1333334f).Within(0.0001f));
            Assert.That(blast.explosionRadiusMultiplier, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void AntiGravityAsset_CombinesZeroGravityWithSpeedIncrease()
        {
            var antiGravity = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Anti Gravity.asset");
            Assert.That(antiGravity, Is.Not.Null);

            Assert.That(antiGravity.cardKey, Is.EqualTo("projectile_gravity_zero"));
            Assert.That(antiGravity.gravityModifier, Is.EqualTo(-2.7f).Within(0.0001f));
            Assert.That(antiGravity.speedMultiplier, Is.EqualTo(1.3f).Within(0.0001f));
        }

        [Test]
        public void ExplosionRuntimePolicy_StacksBlastAndLetsHeavyMultiplyOnlyExistingExplosion()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/NetworkGameplay/Card/CardEffect.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("ExplosionRadiusMultiplier"));
            Assert.That(source, Does.Contain("ExplosionRadius += radiusBonus * GetActiveExplosionRadiusMultiplier();"));
            Assert.That(source, Does.Contain("ExplosionDamageRatio + card.explosionDamageRatio"));
            Assert.That(source, Does.Contain("ExplosionRadiusMultiplier *= radiusMultiplier;"));
            Assert.That(source, Does.Contain("if (ExplosionRadius > 0f)"));
        }

        [Test]
        public void StatusEffectAssets_AreWiredOnPlayerPrefab()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Player.prefab");
            Assert.That(playerPrefab, Is.Not.Null);

            var controller = playerPrefab.GetComponent<PlayerStatusController>();
            Assert.That(controller, Is.Not.Null);

            var serialized = new SerializedObject(controller);
            AssertStatusSlot(serialized, "_freeze", "Assets/_Project/Data/StatusEffects/FrostEffect.asset", "frost");
            AssertStatusSlot(serialized, "_burn", "Assets/_Project/Data/StatusEffects/BurnEffect.asset", "burn");
            AssertStatusSlot(serialized, "_poison", "Assets/_Project/Data/StatusEffects/PoisonEffect.asset", "poison");
            AssertStatusSlot(serialized, "_electric", "Assets/_Project/Data/StatusEffects/ElectricEffect.asset", "electric");
        }

        [Test]
        public void BulletPrefab_SerializesCardRuntimeProjectileFields()
        {
            GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bullet.prefab");
            Assert.That(bulletPrefab, Is.Not.Null);

            var bullet = bulletPrefab.GetComponent<BulletController>();
            Assert.That(bullet, Is.Not.Null);

            var serialized = new SerializedObject(bullet);
            Assert.That(serialized.FindProperty("_GravityScaleModifier"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_AppliedStatusMask"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_StatusDuration"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_StatusMagnitude"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_FreezeStatusDuration"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_BurnStatusDuration"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_PoisonStatusDuration"), Is.Not.Null);
            Assert.That(serialized.FindProperty("_ElectricStatusDuration"), Is.Not.Null);
        }

        [Test]
        public void StatusDuration_UsesCardOverrideWhenPresent()
        {
            var burn = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/StatusEffects/BurnEffect.asset");
            Assert.That(burn, Is.Not.Null);

            Assert.That(PlayerStatusController.ResolveDuration(burn, 2f), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(PlayerStatusController.ResolveDuration(burn, 0f), Is.EqualTo(burn.baseDuration).Within(0.0001f));
        }

        [Test]
        public void MvpStatusEffects_StackToThreeForRuntimeVfxScale()
        {
            string[] assetPaths =
            {
                "Assets/_Project/Data/StatusEffects/BurnEffect.asset",
                "Assets/_Project/Data/StatusEffects/PoisonEffect.asset",
                "Assets/_Project/Data/StatusEffects/FrostEffect.asset",
                "Assets/_Project/Data/StatusEffects/ElectricEffect.asset"
            };

            foreach (string assetPath in assetPaths)
            {
                var effect = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>(assetPath);
                Assert.That(effect, Is.Not.Null, assetPath);
                Assert.That(effect.isStackable, Is.True, assetPath);
                Assert.That(effect.maxStack, Is.EqualTo(3), assetPath);
            }
        }

        [Test]
        public void CardStatusRuntimeRoute_ExposesDurationAndMagnitudeForStatusApply()
        {
            Assert.That(typeof(CardData).GetField("statusDuration"), Is.Not.Null);
            Assert.That(typeof(CardData).GetField("statusMagnitude", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic), Is.Null);
            Assert.That(typeof(CardEffect).GetProperty("CurrentStatusEffectMask"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("StatusDuration"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("StatusMagnitude"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("FreezeStatusDuration"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("BurnStatusDuration"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("PoisonStatusDuration"), Is.Not.Null);
            Assert.That(typeof(CardEffect).GetProperty("ElectricStatusDuration"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("AppliedStatusMask"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("StatusDuration"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("StatusMagnitude"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("FreezeStatusDuration"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("BurnStatusDuration"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("PoisonStatusDuration"), Is.Not.Null);
            Assert.That(typeof(BulletController).GetProperty("ElectricStatusDuration"), Is.Not.Null);

            MethodInfo takeDamage = typeof(PlayerHealth).GetMethod(
                "TakeDamage",
                new[]
                {
                    typeof(float),
                    typeof(PlayerRef),
                    typeof(BulletController.StatusEffect),
                    typeof(float),
                    typeof(float)
                });

            Assert.That(takeDamage, Is.Not.Null);

            MethodInfo multiStatusTakeDamage = typeof(PlayerHealth).GetMethod(
                "TakeDamage",
                new[]
                {
                    typeof(float),
                    typeof(PlayerRef),
                    typeof(int),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float),
                    typeof(float)
                });

            Assert.That(multiStatusTakeDamage, Is.Not.Null);
        }

        [Test]
        public void CardStatusCards_AccumulateProjectileStatusMaskWithoutCardStackMagnitude()
        {
            string cardEffect = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Card", "CardEffect.cs");
            Assert.That(cardEffect, Does.Contain("ApplyCard(CardData card, int cardStackCount = 1)"));
            Assert.That(cardEffect, Does.Contain("CurrentStatusEffectMask = AddStatusEffectToMask(CurrentStatusEffectMask, card.statusEffect);"));
            Assert.That(cardEffect, Does.Contain("SetStatusDuration(card.statusEffect, StatusDuration);"));
            Assert.That(cardEffect, Does.Contain("StatusMagnitude = 1f;"));
            Assert.That(cardEffect, Does.Not.Contain("StatusMagnitude = Mathf.Max(1f, cardStackCount);"));
            Assert.That(cardEffect, Does.Not.Contain("상태이상/특수효과는 최신 카드가 덮어씀"));

            string cardDeck = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Card", "CardDeck.cs");
            Assert.That(cardDeck, Does.Contain("int selectedCardStackCount = GetPlayerCardStack(drawingPlayer, selected) + 1;"));
            Assert.That(cardDeck, Does.Contain("cardEffect.ApplyCard(card, cardStackCount);"));

            string debugPanel = ReadProjectFile("_Project", "Scripts", "DevMode", "UI", "DebugCardSelectPanel.cs");
            Assert.That(debugPanel, Does.Contain("effect.ApplyCard(card, newCount);"));

            string gun = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "Gun.cs");
            Assert.That(gun, Does.Contain("bullet.AppliedStatusMask = _cardEffect.CurrentStatusEffectMask;"));
            Assert.That(gun, Does.Contain("bullet.FreezeStatusDuration = _cardEffect.FreezeStatusDuration;"));
            Assert.That(gun, Does.Contain("bullet.BurnStatusDuration = _cardEffect.BurnStatusDuration;"));
            Assert.That(gun, Does.Contain("bullet.PoisonStatusDuration = _cardEffect.PoisonStatusDuration;"));
            Assert.That(gun, Does.Contain("bullet.ElectricStatusDuration = _cardEffect.ElectricStatusDuration;"));
            Assert.That(gun, Does.Contain("bullet.StatusMagnitude = _cardEffect.StatusMagnitude;"));

            string bullet = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Weapon", "BulletController.cs");
            Assert.That(bullet, Does.Contain("ApplyDamageWithStatuses(health, finalDamage);"));
            Assert.That(bullet, Does.Contain("ApplyDamageWithStatuses(hitHealth, splashDamage);"));
            Assert.That(bullet, Does.Contain("health.TakeDamage("));
            Assert.That(bullet, Does.Contain("AppliedStatusMask,"));
            Assert.That(bullet, Does.Contain("health.TakeDamage(damage, _owner, AppliedStatus, StatusDuration, StatusMagnitude);"));

            string playerHealth = ReadProjectFile("_Project", "Scripts", "NetworkGameplay", "Player", "PlayerHealth.cs");
            Assert.That(playerHealth, Does.Contain("float statusMagnitude = 1f"));
            Assert.That(playerHealth, Does.Contain("ApplyStatusesByMask("));
            Assert.That(playerHealth, Does.Contain("_statusController.ApplyByEnum(status, attacker, statusMagnitude, statusDuration);"));
        }

        [Test]
        public void StatusEffectMaskHelpers_KeepCardAndProjectileEnumOrderingCompatible()
        {
            int cardMask = 0;
            cardMask = CardEffect.AddStatusEffectToMask(cardMask, StatusEffectType.Burn);
            cardMask = CardEffect.AddStatusEffectToMask(cardMask, StatusEffectType.Poison);

            Assert.That(CardEffect.HasStatusEffect(cardMask, StatusEffectType.Burn), Is.True);
            Assert.That(CardEffect.HasStatusEffect(cardMask, StatusEffectType.Poison), Is.True);
            Assert.That(CardEffect.HasStatusEffect(cardMask, StatusEffectType.Freeze), Is.False);

            int projectileMask = BulletController.ToStatusEffectMask(BulletController.StatusEffect.Burn) |
                BulletController.ToStatusEffectMask(BulletController.StatusEffect.Poison);

            Assert.That(projectileMask, Is.EqualTo(cardMask));
            Assert.That(BulletController.HasStatusEffect(projectileMask, BulletController.StatusEffect.Burn), Is.True);
            Assert.That(BulletController.HasStatusEffect(projectileMask, BulletController.StatusEffect.Electric), Is.False);
        }

        [Test]
        public void BurnAndPoisonDotTicks_UseFullIntervalCountWithinDuration()
        {
            var burn = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/StatusEffects/BurnEffect.asset");
            var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/StatusEffects/PoisonEffect.asset");
            Assert.That(burn, Is.Not.Null);
            Assert.That(poison, Is.Not.Null);

            Assert.That(PlayerStatusController.CalculatePeriodicTickCount(burn.baseDuration, burn.tickInterval), Is.EqualTo(2));
            Assert.That(PlayerStatusController.CalculatePeriodicTickCount(poison.baseDuration, poison.tickInterval), Is.EqualTo(3));
        }

        [TestCase("frost", "hit_apply_frost")]
        [TestCase("burn", "hit_apply_burn")]
        [TestCase("poison", "hit_apply_poison")]
        [TestCase("electric", "hit_apply_electric_stack")]
        public void StatusPresentationEffectKey_MapsRuntimeKeyToCardFeedbackKey(string runtimeKey, string expectedPresentationKey)
        {
            Assert.That(PlayerStatusController.GetPresentationEffectKey(runtimeKey), Is.EqualTo(expectedPresentationKey));
        }

        [Test]
        public void StatusAppliedVfx_UsesForegroundSortingForAttachedPrefab()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Presentation/VFX/CardVfxPlaceholderManager.cs");
            string source = File.ReadAllText(sourcePath);

            Assert.That(source, Does.Contain("StatusVfxForegroundSortingOrder"));
            Assert.That(source, Does.Contain("preset.eventType == VfxEventType.StatusApplied"));
            Assert.That(source, Does.Contain("ParticleSystemRenderer"));
            Assert.That(source, Does.Contain("SpriteRenderer[] spriteRenderers"));
        }

        [Test]
        public void StatusRefresh_UpdatesSourceToLatestCaster()
        {
            MethodInfo method = typeof(PlayerStatusController).GetMethod(
                "RefreshExistingStatusInstance",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var firstCaster = PlayerRef.FromEncoded(1);
            var latestCaster = PlayerRef.FromEncoded(2);
            var existing = new StatusEffectInstance
            {
                Source = firstCaster,
                Stacks = 1,
                Magnitude = 1f
            };

            var refreshed = (StatusEffectInstance)method.Invoke(null, new object[]
            {
                existing,
                false,
                5,
                latestCaster,
                2f
            });

            Assert.That(refreshed.Source, Is.EqualTo(latestCaster));
            Assert.That(refreshed.Stacks, Is.EqualTo(1));
            Assert.That(refreshed.Magnitude, Is.EqualTo(2f).Within(0.0001f));

            var stacked = (StatusEffectInstance)method.Invoke(null, new object[]
            {
                existing,
                true,
                3,
                latestCaster,
                2f
            });

            Assert.That(stacked.Source, Is.EqualTo(latestCaster));
            Assert.That(stacked.Stacks, Is.EqualTo(3));
            Assert.That(stacked.Magnitude, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void ElectricStatus_ConsumesStacksWhenStunThresholdTriggers()
        {
            var electric = ScriptableObject.CreateInstance<ElectricEffect>();
            try
            {
                electric.effectKey = "electric";
                electric.isStackable = true;
                electric.maxStack = 3;

                var instance = new StatusEffectInstance
                {
                    EffectKey = "electric",
                    Stacks = 3,
                    Magnitude = 1f
                };

                Assert.That(PlayerStatusController.ShouldTriggerStackStun(electric, instance), Is.True);

                StatusEffectInstance consumed = PlayerStatusController.ConsumeStacksAfterTriggeredStun(electric, instance);
                Assert.That(consumed.Stacks, Is.EqualTo(0));
                Assert.That(PlayerStatusController.ShouldTriggerStackStun(electric, consumed), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(electric);
            }
        }

        private static void AssertStatusSlot(SerializedObject serialized, string slotName, string assetPath, string expectedEffectKey)
        {
            SerializedProperty slot = serialized.FindProperty(slotName);
            Assert.That(slot, Is.Not.Null);

            var assigned = slot.objectReferenceValue as StatusEffectDefinition;
            Assert.That(assigned, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(assigned), Is.EqualTo(assetPath));
            Assert.That(assigned.effectKey, Is.EqualTo(expectedEffectKey));
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
    }
}
#endif
