using System.Reflection;
using Colosseum.Contracts;
using Colosseum.Presentation.VFX;
using NUnit.Framework;

namespace Colosseum.Tests.EditMode
{
    public sealed class StatusVfxLifetimePolicyTests
    {
        [Test]
        public void StatusAppliedVfx_IsStatefulPerTargetAndEffect()
        {
            VfxEventPayload burn = CreatePayload(VfxEventType.StatusApplied, "hit_apply_burn", targetPlayerSlot: 2);
            VfxEventPayload impact = CreatePayload(VfxEventType.ProjectileImpact, "hit_apply_burn", targetPlayerSlot: 2);

            Assert.That(InvokeIsStatefulStatusVfx(burn), Is.True);
            Assert.That(InvokeStatusVfxKey(burn), Is.EqualTo("2:hit_apply_burn"));
            Assert.That(InvokeIsStatefulStatusVfx(impact), Is.False);
        }

        [Test]
        public void StatusAppliedVfx_OnlyRecognizesRuntimeStatusKeys()
        {
            Assert.That(
                InvokeIsStatefulStatusVfx(CreatePayload(VfxEventType.StatusApplied, "hit_apply_poison", targetPlayerSlot: 1)),
                Is.True);
            Assert.That(
                InvokeIsStatefulStatusVfx(CreatePayload(VfxEventType.StatusApplied, "hit_apply_frost", targetPlayerSlot: 1)),
                Is.True);
            Assert.That(
                InvokeIsStatefulStatusVfx(CreatePayload(VfxEventType.StatusApplied, "hit_apply_electric_stack", targetPlayerSlot: 1)),
                Is.True);
            Assert.That(
                InvokeIsStatefulStatusVfx(CreatePayload(VfxEventType.StatusApplied, "projectile_impact", targetPlayerSlot: 1)),
                Is.False);
        }

        [Test]
        public void StatusAppliedVfx_UsesPerEffectLoopCutoff()
        {
            VfxEventPayload burn = CreatePayload(VfxEventType.StatusApplied, "hit_apply_burn", targetPlayerSlot: 1);
            VfxEventPayload poison = CreatePayload(VfxEventType.StatusApplied, "hit_apply_poison", targetPlayerSlot: 1);
            VfxEventPayload frost = CreatePayload(VfxEventType.StatusApplied, "hit_apply_frost", targetPlayerSlot: 1);
            VfxEventPayload electric = CreatePayload(VfxEventType.StatusApplied, "hit_apply_electric_stack", targetPlayerSlot: 1);

            Assert.That(InvokeStatusVfxLoopCutoffSeconds(burn), Is.EqualTo(2.0f).Within(0.0001f));
            Assert.That(InvokeStatusVfxLoopCutoffSeconds(poison), Is.EqualTo(2.0f).Within(0.0001f));
            Assert.That(InvokeStatusVfxLoopCutoffSeconds(frost), Is.EqualTo(1.0f).Within(0.0001f));
            Assert.That(InvokeStatusVfxLoopCutoffSeconds(electric), Is.EqualTo(2.0f).Within(0.0001f));
        }

        [Test]
        public void NonStatusVfx_DoesNotUseLoopCutoff()
        {
            VfxEventPayload impact = CreatePayload(VfxEventType.ProjectileImpact, "projectile_impact", targetPlayerSlot: 1);

            Assert.That(InvokeStatusVfxLoopCutoffSeconds(impact), Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void StatusAppliedVfx_ClampsLoopCutoffToStatusDuration()
        {
            VfxEventPayload burn = CreatePayload(
                VfxEventType.StatusApplied,
                "hit_apply_burn",
                targetPlayerSlot: 1,
                durationHint: 0.75f);

            Assert.That(InvokeStatusVfxLoopCutoffSeconds(burn), Is.EqualTo(0.75f).Within(0.0001f));
        }

        private static VfxEventPayload CreatePayload(
            VfxEventType eventType,
            string effectKey,
            int targetPlayerSlot,
            float durationHint = 3f)
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
                targetPlayerSlot: targetPlayerSlot,
                new VfxVector2(3f, 4f),
                new VfxVector2(1f, 0f),
                radiusHint: 0.5f,
                durationHint: durationHint,
                magnitudeHint: 1f,
                VfxAttachPoint.CharacterBody);
        }

        private static bool InvokeIsStatefulStatusVfx(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "IsStatefulStatusVfx",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static string InvokeStatusVfxKey(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "GetStatusVfxKey",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { payload });
        }

        private static float InvokeStatusVfxLoopCutoffSeconds(VfxEventPayload payload)
        {
            MethodInfo method = typeof(CardVfxPlaceholderManager).GetMethod(
                "ResolveStatusVfxLoopCutoffSeconds",
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.That(method, Is.Not.Null);
            return (float)method.Invoke(null, new object[] { payload });
        }
    }
}
