using System.Reflection;
using Colosseum.Card;
using Colosseum.Game;
using Colosseum.Status;
using Fusion;
using NUnit.Framework;
using UnityEditor;

namespace Colosseum.Tests.EditMode
{
    public sealed class CombatBalancePolicyTests
    {
        [Test]
        public void GameplayProfile_DirectHitDamage_IsTwenty()
        {
            var profile = AssetDatabase.LoadAssetAtPath<GameplayProfile>("Assets/_Project/Data/GameplayProfile.asset");
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.combat.baseline.directHitDamage, Is.EqualTo(20f).Within(0.0001f));

            var defaults = new GameplayProfile.CombatBaseline();
            Assert.That(defaults.directHitDamage, Is.EqualTo(20f).Within(0.0001f));
            Assert.That(defaults.projectileBaseScale, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void BigBulletSizeMultiplier_StacksLinearlyByOneBaseScalePerCard()
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Big Bullet.asset");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.damageMultiplier, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(card.sizeMultiplier, Is.EqualTo(2f).Within(0.0001f));

            float scale = 1f;
            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(2f).Within(0.0001f));

            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(3f).Within(0.0001f));

            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(4f).Within(0.0001f));
        }

        [Test]
        public void HeavyBulletSizeMultiplier_StacksLinearlyByPointThreeWithoutExtraCap()
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Heavy Bullet.asset");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.cardKey, Is.EqualTo("projectile_heavy"));
            Assert.That(card.sizeMultiplier, Is.EqualTo(1.3f).Within(0.0001f));

            float scale = 1f;
            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(1.3f).Within(0.0001f));

            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(1.6f).Within(0.0001f));

            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(1.9f).Within(0.0001f));

            scale = CardEffect.CalculateBulletSizeMultiplier(scale, card.cardKey, card.sizeMultiplier);
            Assert.That(scale, Is.EqualTo(2.2f).Within(0.0001f));
        }

        [Test]
        public void BurnAndPoisonAssets_MatchRequestedDotTimingAndBaseTickDamage()
        {
            var burn = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/StatusEffects/BurnEffect.asset");
            var poison = AssetDatabase.LoadAssetAtPath<StatusEffectDefinition>("Assets/_Project/Data/StatusEffects/PoisonEffect.asset");
            Assert.That(burn, Is.Not.Null);
            Assert.That(poison, Is.Not.Null);

            Assert.That(burn.baseDuration, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(burn.tickInterval, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(PlayerStatusController.CalculatePeriodicTickCount(burn.baseDuration, burn.tickInterval), Is.EqualTo(6));
            Assert.That(ReadDamagePerTick(burn), Is.EqualTo(3f).Within(0.0001f));

            Assert.That(poison.baseDuration, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(poison.tickInterval, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(PlayerStatusController.CalculatePeriodicTickCount(poison.baseDuration, poison.tickInterval), Is.EqualTo(3));
            Assert.That(ReadDamagePerTick(poison), Is.EqualTo(6f).Within(0.0001f));
        }

        [Test]
        public void StatusCards_CarryThreeSecondDurationOverrides()
        {
            var burn = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Burn Shot.asset");
            var poison = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Poison Shot.asset");
            Assert.That(burn, Is.Not.Null);
            Assert.That(poison, Is.Not.Null);

            Assert.That(burn.statusDuration, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(poison.statusDuration, Is.EqualTo(3f).Within(0.0001f));
        }

        [Test]
        public void PiercingRoundCard_DescribesWorldPierceOnly()
        {
            var card = AssetDatabase.LoadAssetAtPath<CardData>("Assets/_Project/Data/Cards/Piercing Round.asset");
            Assert.That(card, Is.Not.Null);
            Assert.That(card.extraPierce, Is.EqualTo(1));
            Assert.That(card.description, Does.Contain("\uBCBD/\uC9C0\uD615"));
            Assert.That(card.description, Does.Not.Contain("\uB300\uC0C1\uC744 \uB9DE\uD78C \uB4A4"));
        }

        [Test]
        public void StatusCardMagnitude_UsesCardCountButDoesNotChangeRuntimeStackDelta()
        {
            Assert.That(CardEffect.CalculateStatusCardMagnitude(1), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(CardEffect.CalculateStatusCardMagnitude(2), Is.EqualTo(2f).Within(0.0001f));
            Assert.That(CardEffect.CalculateStatusCardMagnitude(3), Is.EqualTo(3f).Within(0.0001f));

            Assert.That(PlayerStatusController.CalculateStatusStackDelta(1f), Is.EqualTo(1));
            Assert.That(PlayerStatusController.CalculateStatusStackDelta(3f), Is.EqualTo(1));
        }

        [TestCase(3f, 1, 3f)]
        [TestCase(3f, 2, 6f)]
        [TestCase(3f, 3, 9f)]
        [TestCase(6f, 1, 6f)]
        [TestCase(6f, 2, 12f)]
        [TestCase(6f, 3, 18f)]
        public void DotDamage_UsesStatusStackForSingleCard(float baseDamage, int statusStacks, float expectedDamage)
        {
            Assert.That(
                StatusEffectDefinition.CalculateStackedTickDamage(baseDamage, 1f, statusStacks),
                Is.EqualTo(expectedDamage).Within(0.0001f));
        }

        [TestCase(3f, 2f, 1, 6f)]
        [TestCase(3f, 2f, 2, 9f)]
        [TestCase(3f, 3f, 3, 15f)]
        [TestCase(6f, 2f, 1, 12f)]
        [TestCase(6f, 2f, 2, 18f)]
        [TestCase(6f, 3f, 3, 30f)]
        public void DotDamage_UsesCardCountMagnitudeAndRuntimeStatusStack(
            float baseDamage,
            float cardCountMagnitude,
            int statusStacks,
            float expectedDamage)
        {
            Assert.That(
                StatusEffectDefinition.CalculateStackedTickDamage(baseDamage, cardCountMagnitude, statusStacks),
                Is.EqualTo(expectedDamage).Within(0.0001f));
        }

        [Test]
        public void StatusRefresh_AddsOneRuntimeStackEvenWhenCardMagnitudeIsHigher()
        {
            MethodInfo method = typeof(PlayerStatusController).GetMethod(
                "RefreshExistingStatusInstance",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            var existing = new StatusEffectInstance
            {
                Source = PlayerRef.FromEncoded(1),
                Stacks = 1,
                Magnitude = 1f
            };

            var refreshed = (StatusEffectInstance)method.Invoke(null, new object[]
            {
                existing,
                true,
                3,
                PlayerRef.FromEncoded(2),
                3f
            });

            Assert.That(refreshed.Stacks, Is.EqualTo(2));
            Assert.That(refreshed.Magnitude, Is.EqualTo(3f).Within(0.0001f));
        }

        private static float ReadDamagePerTick(StatusEffectDefinition effect)
        {
            var serialized = new SerializedObject(effect);
            return serialized.FindProperty("_damagePerTick").floatValue;
        }
    }
}
