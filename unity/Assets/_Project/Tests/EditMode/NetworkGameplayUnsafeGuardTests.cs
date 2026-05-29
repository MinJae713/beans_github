using System;
using System.IO;
using Colosseum.Editor;
using Colosseum.Game;
using Colosseum.Network;
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

        [Test]
        public void GunWeaponInput_IsSuppressedDuringCardSelection()
        {
            Assert.That(Gun.ShouldSuppressWeaponForCardSelection(isCardSelectionPending: true), Is.True);
            Assert.That(Gun.ShouldSuppressWeaponForCardSelection(isCardSelectionPending: false), Is.False);
        }

        [Test]
        public void GunFixedUpdate_GatesCardSelectionBeforeNetworkInput()
        {
            string gunSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Weapon/Gun.cs"));

            int guardIndex = gunSource.IndexOf(
                "ShouldSuppressWeaponForCardSelection(IsCardSelectionPending())",
                StringComparison.Ordinal);
            int inputIndex = gunSource.IndexOf(
                "GetInput(out NetworkInputData input)",
                StringComparison.Ordinal);

            Assert.That(guardIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(inputIndex, Is.GreaterThan(guardIndex));
        }

        [Test]
        public void ReadySendMethods_ReturnAcceptedFlag()
        {
            Assert.That(typeof(NetworkManager).GetMethod(nameof(NetworkManager.SendReady)).ReturnType, Is.EqualTo(typeof(bool)));
            Assert.That(typeof(NetworkManager).GetMethod(nameof(NetworkManager.SendUnready)).ReturnType, Is.EqualTo(typeof(bool)));
        }

        [Test]
        public void ReadyProtocol_UsesSenderAckSeparateFromRoomCount()
        {
            string gameServerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Network/GameServerManager.cs"));
            string clientSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Network/NetworkManager.cs"));

            Assert.That(gameServerSource, Does.Contain("READY_STATE:1"));
            Assert.That(gameServerSource, Does.Contain("READY_STATE:0"));
            Assert.That(clientSource, Does.Contain("OnLocalReadyStateChanged"));
        }

        [Test]
        public void ReadyProtocol_DoesNotResetEarlyReadyOnPlayerJoined()
        {
            string gameServerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Network/GameServerManager.cs"));

            Assert.That(gameServerSource, Does.Contain("if (!_playerReady.ContainsKey(player))"));
            Assert.That(gameServerSource, Does.Contain("_playerReady[player] = false;"));
        }

        [Test]
        public void ReliableBroadcast_UsesAllKnownRoomParticipants()
        {
            string gameServerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Network/GameServerManager.cs"));

            Assert.That(gameServerSource, Does.Contain("foreach (var player in GetBroadcastRecipients())"));
            Assert.That(gameServerSource, Does.Contain("AddBroadcastRecipients(recipients, _playerReady.Keys)"));
            Assert.That(gameServerSource, Does.Contain("AddBroadcastRecipients(recipients, _nicknames.Keys)"));
            Assert.That(gameServerSource, Does.Contain("AddBroadcastRecipients(recipients, _spawnedPlayers.Keys)"));
        }

        private static string ToProjectPath(string assetPath)
        {
            string relativeToAssets = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativeToAssets);
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
