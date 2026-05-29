using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Colosseum.CameraSystem;
using Colosseum.Player;
using Colosseum.Weapon;
using NUnit.Framework;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class VictoryRunCameraFallbackTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

        [Test]
        public void VictoryRunFallback_TracksSoleSimulatedPlayerWhenWinnerTransformIsMissing()
        {
            var deadPlayer = new GameObject("Dead victory-run player");
            var survivingPlayer = new GameObject("Surviving victory-run player");

            try
            {
                deadPlayer.transform.position = new Vector3(-40f, 2f, 0f);
                survivingPlayer.transform.position = new Vector3(72f, 2f, 0f);
                deadPlayer.AddComponent<Rigidbody2D>().simulated = false;
                survivingPlayer.AddComponent<Rigidbody2D>().simulated = true;

                Vector2? trackingPoint = InvokeResolveTrackingPointForCamera(
                    hotSeatActive: true,
                    isVictoryRun: true,
                    winnerTransform: null,
                    playerTransforms: new[] { deadPlayer.transform, survivingPlayer.transform });

                Assert.That(trackingPoint.HasValue, Is.True);
                Assert.That(trackingPoint.Value.x, Is.EqualTo(72f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(deadPlayer);
                Object.DestroyImmediate(survivingPlayer);
            }
        }

        [Test]
        public void VictoryRunFallback_TracksSoleActivePlayerWhenSceneSearchMissesRuntimeSpawn()
        {
            var deadPlayerObject = new GameObject("Dead active victory-run player");
            var survivingPlayerObject = new GameObject("Surviving active victory-run player");

            try
            {
                deadPlayerObject.transform.position = new Vector3(312f, 2f, 0f);
                survivingPlayerObject.transform.position = new Vector3(332f, 2f, 0f);
                deadPlayerObject.AddComponent<Rigidbody2D>().simulated = false;
                survivingPlayerObject.AddComponent<Rigidbody2D>().simulated = true;

                PlayerController deadPlayer = deadPlayerObject.AddComponent<PlayerController>();
                PlayerController survivingPlayer = survivingPlayerObject.AddComponent<PlayerController>();

                bool resolved = InvokeTryGetSoleCameraFocusablePlayer(
                    new[] { deadPlayer, survivingPlayer },
                    out Transform solePlayer);

                Assert.That(resolved, Is.True);
                Assert.That(solePlayer, Is.EqualTo(survivingPlayerObject.transform));
            }
            finally
            {
                Object.DestroyImmediate(deadPlayerObject);
                Object.DestroyImmediate(survivingPlayerObject);
            }
        }

        [Test]
        public void VictoryRunFallback_UsesTrackingEvenWhenFinalRoomIsStatic()
        {
            Assert.That(
                InvokeShouldTrackRoomTarget(cameraSlideModeValue: 0, isVictoryRun: true),
                Is.True);
        }

        [Test]
        public void StaticRoomWithoutVictoryRun_RemainsCenterLocked()
        {
            Assert.That(
                InvokeShouldTrackRoomTarget(cameraSlideModeValue: 0, isVictoryRun: false),
                Is.False);
        }

        [Test]
        public void VictoryRunOutOfBoundsDeath_IsDisabledDuringFinalRun()
        {
            Assert.That(
                InvokeShouldApplyOutOfBoundsDeath(isVictoryRun: true),
                Is.False);
        }

        [Test]
        public void CombatOutOfBoundsDeath_RemainsEnabled()
        {
            Assert.That(
                InvokeShouldApplyOutOfBoundsDeath(isVictoryRun: false),
                Is.True);
        }

        [Test]
        public void RespawnInvulnerability_DoesNotDisableCombatOutOfBoundsDeath()
        {
            Assert.That(
                InvokeShouldApplyOutOfBoundsDeath(isVictoryRun: false, isRespawnInvulnerabilityActive: true),
                Is.True);
        }

        [Test]
        public void RespawnGrace_DisablesOnlyHorizontalCameraOutOfBoundsDeath()
        {
            Assert.That(
                InvokeShouldApplyHorizontalOutOfBoundsDeath(isVictoryRun: false, isRespawnCameraGraceActive: true),
                Is.False);
            Assert.That(
                InvokeShouldApplyHorizontalOutOfBoundsDeath(isVictoryRun: false, isRespawnCameraGraceActive: false),
                Is.True);
            Assert.That(
                InvokeShouldApplyOutOfBoundsDeath(isVictoryRun: false, isRespawnInvulnerabilityActive: true),
                Is.True);
        }

        [Test]
        public void VictoryRunDamagePolicy_IgnoresCombatDamageUntilRoundReset()
        {
            Assert.That(
                InvokeShouldIgnoreDamageDuringVictoryRun(isVictoryRun: true),
                Is.True);
            Assert.That(
                InvokeShouldIgnoreDamageDuringVictoryRun(isVictoryRun: false),
                Is.False);
        }

        [Test]
        public void VictoryRunDamagePolicy_IgnoresEnvironmentDeathButKeepsCombatPitPolicy()
        {
            Assert.That(
                PlayerHealth.ShouldIgnoreEnvironmentDeath(
                    isVictoryRunDamageProtected: true,
                    isRespawnInvulnerabilityActive: false),
                Is.True);
            Assert.That(
                PlayerHealth.ShouldIgnoreEnvironmentDeath(
                    isVictoryRunDamageProtected: true,
                    isRespawnInvulnerabilityActive: true),
                Is.True);
            Assert.That(
                PlayerHealth.ShouldIgnoreEnvironmentDeath(
                    isVictoryRunDamageProtected: false,
                    isRespawnInvulnerabilityActive: true),
                Is.True);
            Assert.That(
                PlayerHealth.ShouldIgnoreEnvironmentDeath(
                    isVictoryRunDamageProtected: false,
                    isRespawnInvulnerabilityActive: false),
                Is.False);
        }

        [Test]
        public void InvulnerabilityPolicy_IgnoresCombatKnockback()
        {
            Assert.That(
                PlayerHealth.ShouldIgnoreCombatKnockback(
                    isVictoryRunDamageProtected: true,
                    isRespawnInvulnerabilityActive: false),
                Is.True);
            Assert.That(
                PlayerHealth.ShouldIgnoreCombatKnockback(
                    isVictoryRunDamageProtected: false,
                    isRespawnInvulnerabilityActive: true),
                Is.True);
            Assert.That(
                PlayerHealth.ShouldIgnoreCombatKnockback(
                    isVictoryRunDamageProtected: false,
                    isRespawnInvulnerabilityActive: false),
                Is.False);
        }

        [Test]
        public void VictoryRunDamagePolicy_DoesNotSuppressFireInput()
        {
            Assert.That(
                Gun.ShouldSuppressWeaponForCardSelection(isCardSelectionPending: false),
                Is.False);

            string gunSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Weapon/Gun.cs"));

            Assert.That(gunSource, Does.Not.Contain("VictoryRun"));
        }

        [Test]
        public void VictoryRunFallback_DoesNotUseCentroidWhenWinnerAndSoleSurvivorAreUnknown()
        {
            var leftPlayer = new GameObject("Left focusable player");
            var rightPlayer = new GameObject("Right focusable player");

            try
            {
                leftPlayer.transform.position = new Vector3(-20f, 2f, 0f);
                rightPlayer.transform.position = new Vector3(40f, 2f, 0f);
                leftPlayer.AddComponent<Rigidbody2D>().simulated = true;
                rightPlayer.AddComponent<Rigidbody2D>().simulated = true;

                Vector2? trackingPoint = InvokeResolveTrackingPointForCamera(
                    hotSeatActive: true,
                    isVictoryRun: true,
                    winnerTransform: null,
                    playerTransforms: new[] { leftPlayer.transform, rightPlayer.transform });

                Assert.That(trackingPoint.HasValue, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(leftPlayer);
                Object.DestroyImmediate(rightPlayer);
            }
        }

        [Test]
        public void CombatHotSeatTracking_StillUsesPlayerCentroid()
        {
            var leftPlayer = new GameObject("Left combat player");
            var rightPlayer = new GameObject("Right combat player");

            try
            {
                leftPlayer.transform.position = new Vector3(-10f, 2f, 0f);
                rightPlayer.transform.position = new Vector3(30f, 2f, 0f);

                Vector2? trackingPoint = InvokeResolveTrackingPointForCamera(
                    hotSeatActive: true,
                    isVictoryRun: false,
                    winnerTransform: null,
                    playerTransforms: new[] { leftPlayer.transform, rightPlayer.transform });

                Assert.That(trackingPoint.HasValue, Is.True);
                Assert.That(trackingPoint.Value.x, Is.EqualTo(10f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(leftPlayer);
                Object.DestroyImmediate(rightPlayer);
            }
        }

        private static Vector2? InvokeResolveTrackingPointForCamera(
            bool hotSeatActive,
            bool isVictoryRun,
            Transform winnerTransform,
            IReadOnlyList<Transform> playerTransforms)
        {
            MethodInfo method = typeof(CameraController).GetMethod(
                "ResolveTrackingPointForCamera",
                PrivateStatic);
            Assert.NotNull(method);

            object result = method.Invoke(
                null,
                new object[] { hotSeatActive, isVictoryRun, winnerTransform, playerTransforms });

            return result == null ? null : (Vector2)result;
        }

        private static bool InvokeTryGetSoleCameraFocusablePlayer(
            IReadOnlyList<PlayerController> players,
            out Transform solePlayer)
        {
            MethodInfo method = typeof(CameraController).GetMethod(
                "TryGetSoleCameraFocusablePlayer",
                PrivateStatic);
            Assert.NotNull(method);

            object[] args = { players, null };
            bool result = (bool)method.Invoke(null, args);
            solePlayer = (Transform)args[1];
            return result;
        }

        private static bool InvokeShouldTrackRoomTarget(int cameraSlideModeValue, bool isVictoryRun)
        {
            MethodInfo method = typeof(CameraController).GetMethod(
                "ShouldTrackRoomTarget",
                PrivateStatic);
            Assert.NotNull(method);

            System.Type cameraSlideModeType = method.GetParameters()[0].ParameterType;
            object cameraSlideMode = System.Enum.ToObject(cameraSlideModeType, cameraSlideModeValue);
            return (bool)method.Invoke(null, new object[] { cameraSlideMode, isVictoryRun });
        }

        private static bool InvokeShouldApplyOutOfBoundsDeath(bool isVictoryRun)
        {
            MethodInfo method = typeof(PlayerHealth).GetMethod(
                "ShouldApplyOutOfBoundsDeath",
                PrivateStatic,
                null,
                new[] { typeof(bool) },
                null);
            Assert.NotNull(method);

            return (bool)method.Invoke(null, new object[] { isVictoryRun });
        }

        private static bool InvokeShouldApplyOutOfBoundsDeath(bool isVictoryRun, bool isRespawnInvulnerabilityActive)
        {
            MethodInfo method = typeof(PlayerHealth).GetMethod(
                "ShouldApplyOutOfBoundsDeath",
                PrivateStatic,
                null,
                new[] { typeof(bool), typeof(bool) },
                null);
            Assert.NotNull(method);

            return (bool)method.Invoke(null, new object[] { isVictoryRun, isRespawnInvulnerabilityActive });
        }

        private static bool InvokeShouldApplyHorizontalOutOfBoundsDeath(bool isVictoryRun, bool isRespawnCameraGraceActive)
        {
            MethodInfo method = typeof(PlayerHealth).GetMethod(
                "ShouldApplyHorizontalOutOfBoundsDeath",
                PrivateStatic,
                null,
                new[] { typeof(bool), typeof(bool) },
                null);
            Assert.NotNull(method);

            return (bool)method.Invoke(null, new object[] { isVictoryRun, isRespawnCameraGraceActive });
        }

        private static bool InvokeShouldIgnoreDamageDuringVictoryRun(bool isVictoryRun)
        {
            MethodInfo method = typeof(PlayerHealth).GetMethod(
                "ShouldIgnoreDamageDuringVictoryRun",
                PrivateStatic,
                null,
                new[] { typeof(bool) },
                null);
            Assert.NotNull(method);

            return (bool)method.Invoke(null, new object[] { isVictoryRun });
        }

        private static string ToProjectPath(string assetPath)
        {
            string relativeToAssets = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativeToAssets);
        }
    }
}
