using System;
using System.IO;
using Colosseum.Editor;
using Colosseum.Game;
using Colosseum.Weapon;
using NUnit.Framework;
using UnityEditor.Build;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class NetworkGameplayUnsafeGuardTests
    {
        [Test]
        public void NetworkGameplaySources_DoNotContainHandWrittenUnsafeCode()
        {
            Assert.DoesNotThrow(
                () => NetworkGameplayUnsafeBuildGuard.ValidateOrThrow(),
                "Build Both must fail if hand-written unsafe code is added to Colosseum.NetworkGameplay.");
        }

        [Test]
        public void NetworkGameplayUnsafeGuard_RejectsForbiddenSource()
        {
            string root = Path.Combine(Path.GetTempPath(), $"NetworkGameplayUnsafeGuard_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);

            try
            {
                File.WriteAllText(
                    Path.Combine(root, "UnsafeExample.cs"),
                    "public unsafe class UnsafeExample { private int* _ptr; }");

                Assert.Throws<BuildFailedException>(
                    () => NetworkGameplayUnsafeBuildGuard.ValidateOrThrow(root),
                    "The Build Both guard must reject representative hand-written unsafe code.");
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
        }

        [Test]
        public void NetworkedAccessors_DoNotThrowFieldAccessException()
        {
            var gunObject = new GameObject("Gun accessor smoke");
            var roomObject = new GameObject("RoomManager accessor smoke");

            try
            {
                var gun = gunObject.AddComponent<Gun>();
                var roomManager = roomObject.AddComponent<RoomManager>();

                AssertNoFieldAccessException("Gun.IsReloading", () => _ = gun.IsReloading);
                AssertNoFieldAccessException("RoomManager.CurrentRoomIndex", () => _ = roomManager.CurrentRoomIndex);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gunObject);
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        private static void AssertNoFieldAccessException(string accessorName, TestDelegate action)
        {
            try
            {
                action();
            }
            catch (FieldAccessException ex)
            {
                Assert.Fail($"{accessorName} threw FieldAccessException. Fusion weave/asmdef unsafe boundary is invalid. {ex}");
            }
            catch (Exception)
            {
                // Unspawned Fusion behaviours can throw before full runtime setup.
                // This smoke is specifically guarding the documented Ptr FieldAccessException regression.
            }
        }
    }
}
