// [A303-171 단계 7c 연장] BulletController stub 화로 인한 리플렉션 기반 테스트 일시 비활성화.
// 본 테스트들이 검증하던 BulletController 의 server-authoritative projectile 로직은 BulletServerSimulation
// + BulletVisual 로 이전됨. 후속 Story 에서 새 패턴 (Visual-Only Projectile) 의 검증 테스트로 교체 예정.
#if false
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Colosseum.Player;
using Colosseum.Weapon;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class BulletControllerCollisionPolicyTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void ProjectileCollisionPolicy_TreatsChildColliderAsProjectile()
        {
            var projectileRoot = new GameObject("Other projectile");
            var projectileChild = new GameObject("Other projectile child");
            var nonProjectile = new GameObject("Arena wall");

            try
            {
                projectileRoot.AddComponent<BulletController>();
                projectileChild.transform.SetParent(projectileRoot.transform, false);

                Assert.True(InvokeIsProjectileCollision(projectileRoot));
                Assert.True(InvokeIsProjectileCollision(projectileChild));
                Assert.False(InvokeIsProjectileCollision(nonProjectile));
                Assert.False(InvokeIsProjectileCollision(null));
            }
            finally
            {
                Object.DestroyImmediate(projectileChild);
                Object.DestroyImmediate(projectileRoot);
                Object.DestroyImmediate(nonProjectile);
            }
        }

        [TestCase(true, false, 0, true)]
        [TestCase(true, true, 0, false)]
        [TestCase(true, false, 1, false)]
        [TestCase(false, false, 0, false)]
        public void OwnerIgnorePolicy_AllowsOwnerHitAfterReflect(
            bool isOwnerHit,
            bool ownerIgnoreExpired,
            int reflectCount,
            bool expectedIgnored)
        {
            Assert.AreEqual(
                expectedIgnored,
                InvokeShouldIgnoreOwnerHit(isOwnerHit, ownerIgnoreExpired, (byte)reflectCount));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void PiercePolicy_ConsumesOnlyCharacterHits(int remainingPierces, bool expectedPierce)
        {
            Assert.AreEqual(expectedPierce, InvokeShouldPierceCharacterHit(remainingPierces));
        }

        [TestCase(0, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        public void CameraEdgePolicy_ReflectsOnlyWhenBounceRemains(int remainingBounces, bool expectedReflect)
        {
            Assert.AreEqual(expectedReflect, InvokeShouldReflectCameraEdge(remainingBounces));
        }

        [Test]
        public void CameraEdgePolicy_AllowsNormalProjectilePassThrough()
        {
            string source = ReadBulletControllerSource();
            string cameraEdgeMethod = ExtractBetween(
                source,
                "private void HandleCameraEdgeImpact(",
                "private void OnCollisionEnter2D(");

            Assert.That(cameraEdgeMethod, Does.Contain("ShouldReflectCameraEdge(_remainingBounces)"));
            Assert.That(cameraEdgeMethod, Does.Contain("Bullet passed through camera edge"));
            Assert.That(cameraEdgeMethod, Does.Not.Contain("DespawnIfAlive();"));
            Assert.That(cameraEdgeMethod, Does.Not.Contain("Bullet despawned at camera edge"));
        }

        [Test]
        public void PiercePolicy_DoesNotPierceWorldCollision()
        {
            string source = ReadBulletControllerSource();

            Assert.That(source, Does.Contain("if (ShouldPierceCharacterHit(_remainingPierces))"));
            Assert.That(source, Does.Contain("Bullet pierced player! Remaining"));
            Assert.That(source, Does.Contain("Pierce is character-only"));
            Assert.That(source, Does.Not.Contain("Bullet pierced wall"));
            Assert.That(source, Does.Not.Contain("Physics2D.IgnoreCollision(_physicsCollider, collision.collider, true)"));
        }

        [Test]
        public void ExplosionSplashPolicy_SkipsDirectHitDuplicateAndAllowsOtherTargets()
        {
            var directObject = new GameObject("Direct hit target");
            var splashObject = new GameObject("Splash target");

            try
            {
                var directHealth = directObject.AddComponent<PlayerHealth>();
                var splashHealth = splashObject.AddComponent<PlayerHealth>();
                var damagedTargets = new HashSet<PlayerHealth>();
                int skippedDuplicate = 0;

                InvokePreRegisterDirectHitForSplashSkip(directHealth, damagedTargets, ref skippedDuplicate);

                Assert.True(damagedTargets.Contains(directHealth));
                Assert.AreEqual(1, skippedDuplicate);

                Assert.False(InvokeTryRegisterSplashTarget(directHealth, damagedTargets, ref skippedDuplicate));
                Assert.AreEqual(2, skippedDuplicate);

                Assert.True(InvokeTryRegisterSplashTarget(splashHealth, damagedTargets, ref skippedDuplicate));
                Assert.True(damagedTargets.Contains(splashHealth));
                Assert.AreEqual(2, skippedDuplicate);
            }
            finally
            {
                Object.DestroyImmediate(directObject);
                Object.DestroyImmediate(splashObject);
            }
        }

        [Test]
        public void GunAimPolicy_UsesSubmittedWorldTargetWithoutImplicitVerticalOffset()
        {
            var origin = new Vector2(-2.39f, 6.18f);
            var target = new Vector2(2.24f, 4.71f);

            Vector2 actual = InvokeCalculateAimDirection(origin, target);
            Vector2 expected = (target - origin).normalized;

            Assert.That(actual.x, Is.EqualTo(expected.x).Within(0.0001f));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(0.0001f));
            Assert.That(actual.y, Is.LessThan(0f));
        }

        [Test]
        public void GunAimPolicy_FallsBackRightWhenTargetEqualsOrigin()
        {
            Vector2 actual = InvokeCalculateAimDirection(Vector2.one, Vector2.one);

            Assert.AreEqual(Vector2.right, actual);
        }

        [Test]
        public void ProjectileSimulationPolicy_LeavesRemoteProxyForNetworkInterpolation()
        {
            Assert.That(BulletController.ShouldSimulateProjectileObject(
                hasInputAuthority: true,
                hasStateAuthority: false), Is.True);
            Assert.That(BulletController.ShouldSimulateProjectileObject(
                hasInputAuthority: false,
                hasStateAuthority: true), Is.True);
            Assert.That(BulletController.ShouldSimulateProjectileObject(
                hasInputAuthority: false,
                hasStateAuthority: false), Is.False);
        }

        [TestCase(BulletController.StatusEffect.None, 0, true)]
        [TestCase(BulletController.StatusEffect.Burn, 0, false)]
        [TestCase(BulletController.StatusEffect.None, 1, false)]
        [TestCase(BulletController.StatusEffect.Poison, 0, false)]
        public void DirectHitImpactFeedbackPolicy_SuppressesCommonImpactForStatusProjectiles(
            BulletController.StatusEffect appliedStatus,
            int appliedStatusMask,
            bool expected)
        {
            Assert.That(
                BulletController.ShouldPublishDirectHitImpactFeedback(
                    appliedStatus,
                    appliedStatusMask),
                Is.EqualTo(expected));
        }

        [Test]
        public void BulletPrefab_Rigidbody2DInterpolationIsEnabled()
        {
            GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Bullet.prefab");
            Assert.NotNull(bulletPrefab);

            Rigidbody2D rb = bulletPrefab.GetComponent<Rigidbody2D>();
            Assert.NotNull(rb);
            Assert.That(rb.interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
        }

        [Test]
        public void ProjectileSpawnVisualCatchUp_IsNotAppliedToProjectileRootOrVisual()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "NetworkGameplay",
                "Weapon",
                "BulletController.cs"));

            Assert.That(source, Does.Contain("ProjectileVisual"));
            Assert.That(source, Does.Contain("ResetProjectileVisualTransform();"));
            Assert.That(source, Does.Not.Contain("SpawnVisualCatchUp"));
            Assert.That(source, Does.Not.Contain("CalculateSpawnVisualStartPosition"));
            Assert.That(source, Does.Not.Contain("_visualRoot.position = Vector3.Lerp"));
        }

        [Test]
        public void PlayerPrefab_CombatHitboxExtendsVisualHitAreaWithoutChangingBodyCollider()
        {
            var prefab = Resources.Load<GameObject>("Player");
            Assert.NotNull(prefab);

            var bodyCollider = prefab.GetComponent<BoxCollider2D>();
            Assert.NotNull(bodyCollider);
            Assert.False(bodyCollider.isTrigger);

            Transform combatHitboxTransform = prefab.transform.Find("CombatHitbox");
            Assert.NotNull(combatHitboxTransform);

            var combatHitbox = combatHitboxTransform.GetComponent<BoxCollider2D>();
            Assert.NotNull(combatHitbox);
            Assert.True(combatHitbox.isTrigger);
            Assert.That(combatHitbox.size.x, Is.GreaterThan(bodyCollider.size.x));
            Assert.That(combatHitbox.size.y, Is.GreaterThan(bodyCollider.size.y));
            Assert.That(combatHitboxTransform.localPosition.y, Is.GreaterThan(bodyCollider.offset.y));
        }

        [Test]
        public void HitRigidbodyPolicy_ResolvesParentRigidbodyForChildCombatHitbox()
        {
            var playerRoot = new GameObject("Player root");
            var combatHitbox = new GameObject("CombatHitbox");

            try
            {
                var playerBody = playerRoot.AddComponent<Rigidbody2D>();
                combatHitbox.transform.SetParent(playerRoot.transform, false);
                var combatCollider = combatHitbox.AddComponent<BoxCollider2D>();
                combatCollider.isTrigger = true;

                Assert.AreSame(playerBody, InvokeResolveHitRigidbody(combatCollider));
            }
            finally
            {
                Object.DestroyImmediate(combatHitbox);
                Object.DestroyImmediate(playerRoot);
            }
        }

        private static bool InvokeIsProjectileCollision(GameObject collisionObject)
        {
            MethodInfo method = GetPrivateStaticMethod("IsProjectileCollision");
            return (bool)method.Invoke(null, new object[] { collisionObject });
        }

        private static bool InvokeShouldIgnoreOwnerHit(bool isOwnerHit, bool ownerIgnoreExpired, byte reflectCount)
        {
            MethodInfo method = GetPrivateStaticMethod("ShouldIgnoreOwnerHit");
            return (bool)method.Invoke(null, new object[] { isOwnerHit, ownerIgnoreExpired, reflectCount });
        }

        private static bool InvokeShouldPierceCharacterHit(int remainingPierces)
        {
            MethodInfo method = GetPrivateStaticMethod("ShouldPierceCharacterHit");
            return (bool)method.Invoke(null, new object[] { remainingPierces });
        }

        private static bool InvokeShouldReflectCameraEdge(int remainingBounces)
        {
            MethodInfo method = GetPrivateStaticMethod("ShouldReflectCameraEdge");
            return (bool)method.Invoke(null, new object[] { remainingBounces });
        }

        private static void InvokePreRegisterDirectHitForSplashSkip(
            PlayerHealth directHealth,
            HashSet<PlayerHealth> damagedTargets,
            ref int skippedDuplicate)
        {
            MethodInfo method = GetPrivateStaticMethod("PreRegisterDirectHitForSplashSkip");
            object[] args = { directHealth, damagedTargets, skippedDuplicate };
            method.Invoke(null, args);
            skippedDuplicate = (int)args[2];
        }

        private static bool InvokeTryRegisterSplashTarget(
            PlayerHealth hitHealth,
            HashSet<PlayerHealth> damagedTargets,
            ref int skippedDuplicate)
        {
            MethodInfo method = GetPrivateStaticMethod("TryRegisterSplashTarget");
            object[] args = { hitHealth, damagedTargets, skippedDuplicate };
            bool result = (bool)method.Invoke(null, args);
            skippedDuplicate = (int)args[2];
            return result;
        }

        private static Vector2 InvokeCalculateAimDirection(Vector2 origin, Vector2 target)
        {
            MethodInfo method = typeof(Gun).GetMethod("CalculateAimDirection", PrivateStatic);
            Assert.NotNull(method);
            return (Vector2)method.Invoke(null, new object[] { origin, target });
        }

        private static Rigidbody2D InvokeResolveHitRigidbody(Collider2D hitCollider)
        {
            MethodInfo method = GetPrivateStaticMethod("ResolveHitRigidbody");
            return (Rigidbody2D)method.Invoke(null, new object[] { hitCollider });
        }

        private static MethodInfo GetPrivateStaticMethod(string methodName)
        {
            MethodInfo method = typeof(BulletController).GetMethod(methodName, PrivateStatic);
            Assert.NotNull(method);
            return method;
        }

        private static string ReadBulletControllerSource()
        {
            return File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "NetworkGameplay",
                "Weapon",
                "BulletController.cs"));
        }

        private static string ExtractBetween(string source, string startMarker, string endMarker)
        {
            int start = source.IndexOf(startMarker, System.StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), startMarker);
            int end = source.IndexOf(endMarker, start + startMarker.Length, System.StringComparison.Ordinal);
            Assert.That(end, Is.GreaterThan(start), endMarker);
            return source.Substring(start, end - start);
        }
    }
}
#endif
