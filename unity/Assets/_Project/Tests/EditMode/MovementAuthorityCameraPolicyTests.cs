using System.IO;
using Colosseum.CameraSystem;
using Colosseum.Contracts;
using Colosseum.Game;
using Colosseum.Player;
using Colosseum.UI;
using Fusion;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Colosseum.Tests.EditMode
{
    public sealed class MovementAuthorityCameraPolicyTests
    {
        [Test]
        public void NeutralCamera_UsesSegmentCenterAsAuthorityBaseline()
        {
            float target = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                roomCenterX: 12f,
                minCameraX: -30f,
                maxCameraX: 30f);

            Assert.That(target, Is.EqualTo(12f).Within(0.001f));
        }

        [Test]
        public void NeutralCamera_ClampsSegmentCenterToCameraBounds()
        {
            float target = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                roomCenterX: 50f,
                minCameraX: -10f,
                maxCameraX: 10f);

            Assert.That(target, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void RoomEntryCamera_StartsAtEntrySideForRightAdvancer()
        {
            float target = CameraController.CalculateMovementAuthorityRoomEntryCameraTargetX(
                roomCenterX: 104f,
                minCameraX: 75.11f,
                maxCameraX: 132.89f,
                direction: 1,
                preferEntrySide: true);

            Assert.That(target, Is.EqualTo(75.11f).Within(0.001f));
        }

        [Test]
        public void RoomEntryCamera_StartsAtEntrySideForLeftAdvancer()
        {
            float target = CameraController.CalculateMovementAuthorityRoomEntryCameraTargetX(
                roomCenterX: 0f,
                minCameraX: -28.89f,
                maxCameraX: 28.89f,
                direction: -1,
                preferEntrySide: true);

            Assert.That(target, Is.EqualTo(28.89f).Within(0.001f));
        }

        [Test]
        public void RoomEntryCamera_KeepsCenterWhenEntrySideIsNotKnown()
        {
            float target = CameraController.CalculateMovementAuthorityRoomEntryCameraTargetX(
                roomCenterX: 104f,
                minCameraX: 75.11f,
                maxCameraX: 132.89f,
                direction: 0,
                preferEntrySide: true);

            Assert.That(target, Is.EqualTo(104f).Within(0.001f));
        }

        [Test]
        public void RightAuthority_StaysUntilForwardAnchorIsCrossed()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 0f,
                authorityX: 6.5f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: 1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void RightAuthority_AdvancesAtEightyFivePercentAnchor()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 0f,
                authorityX: 8f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: 1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void RightAuthority_DoesNotRetreat()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 4f,
                authorityX: 8f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: 1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void LeftAuthority_AdvancesAtFifteenPercentAnchor()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 0f,
                authorityX: -8f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: -1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void LeftAuthority_DoesNotRetreat()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: -4f,
                authorityX: -8f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: -1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(-4f).Within(0.001f));
        }

        [Test]
        public void RespawnRecenter_UsesMidpointEvenWhenItMovesBack()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 12f,
                authorityX: 20f,
                midpointX: 4f,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: 1,
                recenterToMidpoint: true);

            Assert.That(target, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void RespawnRecenter_ClampsMidpointToSegmentCameraBounds()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 12f,
                authorityX: 20f,
                midpointX: 40f,
                viewHalfWidth: 10f,
                minCameraX: -20f,
                maxCameraX: 20f,
                direction: 1,
                recenterToMidpoint: true);

            Assert.That(target, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void RespawnRecenter_IsNotNeededWhenBothPlayersRemainInCurrentViewport()
        {
            bool shouldRecenter = CameraController.ShouldRecenterMovementAuthorityCameraAfterRespawn(
                currentCameraX: 0f,
                authorityX: -1.75f,
                otherX: 19.05f,
                viewHalfWidth: 23.11f);

            Assert.That(shouldRecenter, Is.False);
        }

        [Test]
        public void RespawnRecenter_IsNeededWhenRespawnedPlayerIsOutsideCurrentViewport()
        {
            bool shouldRecenter = CameraController.ShouldRecenterMovementAuthorityCameraAfterRespawn(
                currentCameraX: 0f,
                authorityX: -1.75f,
                otherX: 26.5f,
                viewHalfWidth: 23.11f);

            Assert.That(shouldRecenter, Is.True);
        }

        [Test]
        public void CameraOutOfViewDeath_UsesDesignViewportBounds()
        {
            Assert.That(RoomManager.IsOutsideCameraViewport(
                playerPos: new Vector2(18.1f, 0f),
                cameraX: 0f,
                cameraY: 0f,
                cameraSize: 10f,
                aspect: CameraController.DesignViewportAspect,
                padding: 0.25f), Is.True);

            Assert.That(RoomManager.IsOutsideCameraViewport(
                playerPos: new Vector2(17.5f, 0f),
                cameraX: 0f,
                cameraY: 0f,
                cameraSize: 10f,
                aspect: CameraController.DesignViewportAspect,
                padding: 0.25f), Is.False);
        }

        [Test]
        public void CameraOutOfViewDeath_CanSeparateHorizontalAndBottomExit()
        {
            Assert.That(RoomManager.IsHorizontallyOutsideCameraViewport(
                playerPos: new Vector2(18.1f, 0f),
                cameraX: 0f,
                cameraSize: 10f,
                aspect: CameraController.DesignViewportAspect,
                padding: 0.25f), Is.True);

            Assert.That(RoomManager.IsBelowCameraViewport(
                playerPos: new Vector2(0f, -10.3f),
                cameraY: 0f,
                cameraSize: 10f,
                padding: 0.25f), Is.True);

            Assert.That(RoomManager.IsHorizontallyOutsideCameraViewport(
                playerPos: new Vector2(0f, -10.3f),
                cameraX: 0f,
                cameraSize: 10f,
                aspect: CameraController.DesignViewportAspect,
                padding: 0.25f), Is.False);
        }

        [Test]
        public void AuthorityChange_ReinitializesCameraContextAtSegmentCenter()
        {
            bool shouldReset = CameraController.ShouldResetMovementAuthorityCameraContext(
                cameraInitialized: true,
                lastAuthorityPlayerId: 1,
                currentAuthorityPlayerId: 2,
                lastRoomIndex: 3,
                currentRoomIndex: 3);

            float neutral = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                roomCenterX: 0f,
                minCameraX: -30f,
                maxCameraX: 30f);

            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: neutral,
                authorityX: -8f,
                midpointX: neutral,
                viewHalfWidth: 10f,
                minCameraX: -30f,
                maxCameraX: 30f,
                direction: -1,
                recenterToMidpoint: false);

            Assert.That(shouldReset, Is.True);
            Assert.That(target, Is.EqualTo(-1f).Within(0.001f));
        }

        [Test]
        public void SameAuthorityAndSameSegment_KeepsCameraContext()
        {
            bool shouldReset = CameraController.ShouldResetMovementAuthorityCameraContext(
                cameraInitialized: true,
                lastAuthorityPlayerId: 1,
                currentAuthorityPlayerId: 1,
                lastRoomIndex: 3,
                currentRoomIndex: 3);

            Assert.That(shouldReset, Is.False);
        }

        [Test]
        public void SegmentChange_ReinitializesCameraContextAtNewSegmentCenter()
        {
            bool shouldReset = CameraController.ShouldResetMovementAuthorityCameraContext(
                cameraInitialized: true,
                lastAuthorityPlayerId: 1,
                currentAuthorityPlayerId: 1,
                lastRoomIndex: 3,
                currentRoomIndex: 4);

            float neutral = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                roomCenterX: 25f,
                minCameraX: 10f,
                maxCameraX: 40f);

            Assert.That(shouldReset, Is.True);
            Assert.That(neutral, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void RightAuthorityAdvance_ClampsToSegmentCameraBounds()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 0f,
                authorityX: 100f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -20f,
                maxCameraX: 20f,
                direction: 1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void LeftAuthorityAdvance_ClampsToSegmentCameraBounds()
        {
            float target = CameraController.CalculateMovementAuthorityCameraTargetX(
                currentCameraX: 0f,
                authorityX: -100f,
                midpointX: 0f,
                viewHalfWidth: 10f,
                minCameraX: -20f,
                maxCameraX: 20f,
                direction: -1,
                recenterToMidpoint: false);

            Assert.That(target, Is.EqualTo(-20f).Within(0.001f));
        }

        [Test]
        public void OpeningViewportClamp_UsesDesignSixteenByNineCameraView()
        {
            var boundary = new PlayerBoundary();

            Assert.That(boundary.GetOpeningViewportLeftClampX(
                roomCenterX: 0f,
                roomLeftBound: -100f,
                cameraSize: 9f,
                horizontalExtent: 1f), Is.EqualTo(-15f).Within(0.001f));
            Assert.That(boundary.GetOpeningViewportRightClampX(
                roomCenterX: 0f,
                roomRightBound: 100f,
                cameraSize: 9f,
                horizontalExtent: 1f), Is.EqualTo(15f).Within(0.001f));
        }

        [Test]
        public void AdvanceEntryX_PlacesPlayerOneAtNewRoomLeftEntry()
        {
            float entryX = RoomManager.CalculateAdvanceEntryX(
                isPlayer1Advancing: true,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                horizontalExtent: 0.4f);

            Assert.That(entryX, Is.EqualTo(54.4f).Within(0.001f));
        }

        [Test]
        public void AdvanceEntryX_PlacesPlayerTwoAtNewRoomRightEntry()
        {
            float entryX = RoomManager.CalculateAdvanceEntryX(
                isPlayer1Advancing: false,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                horizontalExtent: 0.4f);

            Assert.That(entryX, Is.EqualTo(153.6f).Within(0.001f));
        }

        [Test]
        public void AdvanceCooldown_BlocksImmediateDuplicateRoomAdvance()
        {
            Assert.That(RoomManager.ShouldBlockRoomAdvanceByTick(
                currentTick: 105,
                lastAdvanceTick: 100,
                minimumTickGap: 12), Is.True);
            Assert.That(RoomManager.ShouldBlockRoomAdvanceByTick(
                currentTick: 112,
                lastAdvanceTick: 100,
                minimumTickGap: 12), Is.False);
        }

        [Test]
        public void CardSelectionPending_SuppressesPlayerCombatMovement()
        {
            Assert.That(PlayerController.ShouldSuppressMovementForCardSelection(isCardSelectionPending: true), Is.True);
            Assert.That(PlayerController.ShouldSuppressMovementForCardSelection(isCardSelectionPending: false), Is.False);
        }

        [Test]
        public void CardSelectionUi_ShowsForLocalDrawingPlayerEvenOnHostLocalRunner()
        {
            Assert.That(
                CardSelectionUI.ShouldShowForLocalDrawingPlayer(
                    PlayerRef.FromEncoded(1),
                    hasPendingDraw: true),
                Is.True);

            Assert.That(
                CardSelectionUI.ShouldShowForLocalDrawingPlayer(
                    default,
                    hasPendingDraw: true),
                Is.False);
        }

        [Test]
        public void RespawnRecovery_AllowsLivingPlayerMovementWhileOpponentIsDead()
        {
            Assert.That(PlayerController.ShouldSuppressMovementForRespawnRecovery(
                hasMovementAuthority: true,
                isOpponentDead: true), Is.False);

            Assert.That(PlayerController.ShouldSuppressMovementForRespawnRecovery(
                hasMovementAuthority: true,
                isOpponentDead: false), Is.False);

            Assert.That(PlayerController.ShouldSuppressMovementForRespawnRecovery(
                hasMovementAuthority: false,
                isOpponentDead: true), Is.False);
        }

        [Test]
        public void PlayerSimulationPolicy_LeavesRemoteProxyForNetworkInterpolation()
        {
            Assert.That(PlayerController.ShouldSimulatePlayerObject(
                hasInputAuthority: true,
                hasStateAuthority: false), Is.True);
            Assert.That(PlayerController.ShouldSimulatePlayerObject(
                hasInputAuthority: false,
                hasStateAuthority: true), Is.True);
            Assert.That(PlayerController.ShouldSimulatePlayerObject(
                hasInputAuthority: false,
                hasStateAuthority: false), Is.False);
        }

        [Test]
        public void LocalMovementAuthorityCamera_UsesPredictedLocalTransformBeforeServerSnapshot()
        {
            string cameraSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Camera/CameraController.cs"));
            int localOverrideIndex = cameraSource.IndexOf("TryResolveLocalMovementAuthorityCameraX(room, out cameraX)");
            int serverSnapshotIndex = cameraSource.IndexOf("_roomManager.TryGetAuthoritativeMovementCameraX(out cameraX)");

            Assert.That(localOverrideIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(serverSnapshotIndex, Is.GreaterThan(localOverrideIndex));
            Assert.That(cameraSource, Does.Contain("_roomManager.MovementCameraAuthority != localPlayer"));
            Assert.That(cameraSource, Does.Contain("localTransform.position.x"));
            Assert.That(cameraSource, Does.Contain("ResolvePlayerTransform(localPlayer)"));
        }

        [TestCase(false, true, true, true)]
        [TestCase(true, true, true, false)]
        [TestCase(false, false, true, false)]
        [TestCase(false, true, false, false)]
        public void RemoteStateAuthorityMissingInputHold_AppliesOnlyToRemoteStateAuthority(
            bool hasInputAuthority,
            bool hasStateAuthority,
            bool hasLastMovementInput,
            bool expected)
        {
            Assert.That(PlayerController.ShouldUseRemoteMissingMovementInputHold(
                hasInputAuthority,
                hasStateAuthority,
                hasLastMovementInput), Is.EqualTo(expected));
        }

        [TestCase(0, 3, false)]
        [TestCase(1, 3, true)]
        [TestCase(3, 3, true)]
        [TestCase(4, 3, false)]
        public void RemoteStateAuthorityMissingInputHold_IsShortLived(
            int missingInputAgeTicks,
            int maxHoldTicks,
            bool expected)
        {
            Assert.That(PlayerController.ShouldHoldRemoteMovementInput(
                missingInputAgeTicks,
                maxHoldTicks), Is.EqualTo(expected));
        }

        [Test]
        public void PlayerPrefab_Rigidbody2DInterpolationIsEnabled()
        {
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Resources/Player.prefab");
            Assert.NotNull(playerPrefab);

            Rigidbody2D rb = playerPrefab.GetComponent<Rigidbody2D>();
            Assert.NotNull(rb);
            Assert.That(rb.interpolation, Is.EqualTo(RigidbodyInterpolation2D.Interpolate));
        }

        [Test]
        public void OwnSideBoundary_UsesEntrySideViewportAfterRoomAdvance()
        {
            var boundary = new PlayerBoundary();
            float cameraX = PlayerBoundary.CalculateMovementAuthorityBoundaryCameraX(
                authorityX: 54.82f,
                roomCenterX: 104f,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                cameraSize: 13f,
                direction: 1,
                preferEntrySide: true);
            float ownSideClampX = boundary.GetVisibleLeftBoundary(
                cameraX,
                roomLeftBound: 52f,
                cameraSize: 13f) + 0.82f;

            Assert.That(cameraX, Is.EqualTo(75.111f).Within(0.001f));
            Assert.That(ownSideClampX, Is.EqualTo(52.82f).Within(0.001f));

            Assert.That(
                PlayerBoundary.ShouldClampOwnSideBoundary(
                    currentX: 54f,
                    ownSideClampX: ownSideClampX,
                    moveX: 0f,
                    ownSideIsLeft: true),
                Is.False);

            Assert.That(
                PlayerBoundary.ShouldClampOwnSideBoundary(
                    currentX: 54f,
                    ownSideClampX: ownSideClampX,
                    moveX: 1f,
                    ownSideIsLeft: true),
                Is.False);

            Assert.That(
                PlayerBoundary.ShouldClampOwnSideBoundary(
                    currentX: 52.7f,
                    ownSideClampX: ownSideClampX,
                    moveX: -1f,
                    ownSideIsLeft: true),
                Is.True);
        }

        [Test]
        public void OwnSideBoundary_KeepsCenteredViewportWhenNotRoomAdvanceEntry()
        {
            var boundary = new PlayerBoundary();
            float cameraX = PlayerBoundary.CalculateMovementAuthorityBoundaryCameraX(
                authorityX: 54.82f,
                roomCenterX: 104f,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                cameraSize: 13f,
                direction: 1,
                preferEntrySide: false);
            float ownSideClampX = boundary.GetVisibleLeftBoundary(
                cameraX,
                roomLeftBound: 52f,
                cameraSize: 13f) + 0.82f;

            Assert.That(cameraX, Is.EqualTo(104f).Within(0.001f));
            Assert.That(ownSideClampX, Is.EqualTo(81.709f).Within(0.001f));
        }

        [Test]
        public void OwnSideBoundary_SlidesOnlyAfterAuthorityCrossesForwardAnchor()
        {
            var boundary = new PlayerBoundary();
            float entryCameraX = PlayerBoundary.CalculateMovementAuthorityBoundaryCameraX(
                authorityX: 54.82f,
                roomCenterX: 104f,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                cameraSize: 13f,
                direction: 1,
                preferEntrySide: true);
            float advancedCameraX = PlayerBoundary.CalculateMovementAuthorityBoundaryCameraX(
                authorityX: 96f,
                roomCenterX: 104f,
                roomLeftBound: 52f,
                roomRightBound: 156f,
                cameraSize: 13f,
                direction: 1,
                preferEntrySide: true);
            float entryClampX = boundary.GetVisibleLeftBoundary(
                entryCameraX,
                roomLeftBound: 52f,
                cameraSize: 13f) + 0.82f;
            float advancedClampX = boundary.GetVisibleLeftBoundary(
                advancedCameraX,
                roomLeftBound: 52f,
                cameraSize: 13f) + 0.82f;

            Assert.That(advancedCameraX, Is.GreaterThan(entryCameraX));
            Assert.That(advancedClampX, Is.GreaterThan(entryClampX));
            Assert.That(advancedClampX, Is.LessThan(81.709f));
        }

        [Test]
        public void InitialSpawnPosition_UsesSegmentCenterViewportOffsets()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 6f,
                cameraSize = 13f,
                leftBound = -52f,
                rightBound = 52f,
                centerX = 0f
            };

            var p1 = RoomManager.CalculateInitialSpawnPosition(room, isFirstPlayer: true);
            var p2 = RoomManager.CalculateInitialSpawnPosition(room, isFirstPlayer: false);

            Assert.That(p1.x, Is.EqualTo(-8.088889f).Within(0.001f));
            Assert.That(p2.x, Is.EqualTo(8.088889f).Within(0.001f));
            Assert.That(p1.y, Is.EqualTo(7f).Within(0.001f));
            Assert.That(p2.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void FallbackInitialSpawnPosition_KeepsLegacyDefaultCombatCoordinates()
        {
            var p1 = RoomManager.CalculateFallbackInitialSpawnPosition(isFirstPlayer: true);
            var p2 = RoomManager.CalculateFallbackInitialSpawnPosition(isFirstPlayer: false);

            Assert.That(p1.x, Is.EqualTo(-7.8f).Within(0.001f));
            Assert.That(p2.x, Is.EqualTo(7.8f).Within(0.001f));
            Assert.That(p1.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(p2.y, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void RespawnPosition_UsesAuthorityDirectionAndViewportBoundedSeparation()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 6f,
                cameraSize = 13f,
                leftBound = -52f,
                rightBound = 52f,
                centerX = 0f
            };

            var respawn = RoomManager.CalculateRespawnPosition(
                room,
                authorityOrOpponentPosition: new UnityEngine.Vector2(0f, 1.5f),
                authorityDirection: 1,
                maxRespawnDistance: 8f,
                cameraTargetX: 0f);

            Assert.That(respawn.x, Is.EqualTo(20.8f).Within(0.001f));
            Assert.That(respawn.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void RespawnPosition_KeepsMinimumSeparationWhenAuthorityIsPastViewportEdge()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 6f,
                cameraSize = 13f,
                leftBound = -52f,
                rightBound = 52f,
                centerX = 0f
            };

            var respawn = RoomManager.CalculateRespawnPosition(
                room,
                authorityOrOpponentPosition: new UnityEngine.Vector2(30f, 1.5f),
                authorityDirection: 1,
                maxRespawnDistance: 8f,
                cameraTargetX: 0f);

            Assert.That(respawn.x, Is.EqualTo(38f).Within(0.001f));
            Assert.That(respawn.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void DeathRespawnCameraTarget_PlacesRightAuthorityAtTrailingFifteenPercent()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 6f,
                cameraSize = 13f,
                leftBound = -52f,
                rightBound = 52f,
                centerX = 0f
            };

            float cameraTarget = RoomManager.CalculateRespawnCameraTargetForAuthority(
                room,
                authorityX: 0f,
                authorityDirection: 1);
            var respawn = RoomManager.CalculateDeathRespawnPosition(
                room,
                authorityPosition: new UnityEngine.Vector2(0f, 1.5f),
                authorityDirection: 1,
                maxRespawnDistance: 8f,
                cameraTargetX: cameraTarget);

            Assert.That(cameraTarget, Is.EqualTo(16.17778f).Within(0.001f));
            Assert.That(respawn.x, Is.EqualTo(32.35556f).Within(0.001f));
            Assert.That(respawn.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void DeathRespawnCameraTarget_PlacesLeftAuthorityAtTrailingFifteenPercent()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 6f,
                cameraSize = 13f,
                leftBound = -52f,
                rightBound = 52f,
                centerX = 0f
            };

            float cameraTarget = RoomManager.CalculateRespawnCameraTargetForAuthority(
                room,
                authorityX: 0f,
                authorityDirection: -1);
            var respawn = RoomManager.CalculateDeathRespawnPosition(
                room,
                authorityPosition: new UnityEngine.Vector2(0f, 1.5f),
                authorityDirection: -1,
                maxRespawnDistance: 8f,
                cameraTargetX: cameraTarget);

            Assert.That(cameraTarget, Is.EqualTo(-16.17778f).Within(0.001f));
            Assert.That(respawn.x, Is.EqualTo(-32.35556f).Within(0.001f));
            Assert.That(respawn.y, Is.EqualTo(7f).Within(0.001f));
        }

        [Test]
        public void DeathRespawnRuntimeFlow_SetsCameraTargetBeforeCalculatingRespawnCandidate()
        {
            string roomManagerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Game/RoomManager.cs"));
            int currentAnchorIndex = roomManagerSource.IndexOf("ResolveCurrentRespawnAuthorityAnchor(");
            int cameraIndex = roomManagerSource.IndexOf("CalculateRespawnCameraTargetForAuthority(");
            int setCameraIndex = roomManagerSource.IndexOf("SetMovementCameraTarget(", cameraIndex);
            int candidateIndex = roomManagerSource.IndexOf("CalculateDeathRespawnPosition(", setCameraIndex);

            Assert.That(currentAnchorIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(cameraIndex, Is.GreaterThan(currentAnchorIndex));
            Assert.That(setCameraIndex, Is.GreaterThan(cameraIndex));
            Assert.That(candidateIndex, Is.GreaterThan(setCameraIndex));
        }

        [Test]
        public void DeathRespawnRuntimeFlow_PrimesCameraBeforeRegisterKillRefresh()
        {
            string roomManagerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Game/RoomManager.cs"));
            int registerAnchorIndex = roomManagerSource.IndexOf("public void RegisterDeathRespawnAnchor(");
            int primeCallIndex = roomManagerSource.IndexOf("PrimeDeathRespawnMovementCamera(", registerAnchorIndex);
            int primeMethodIndex = roomManagerSource.IndexOf("private void PrimeDeathRespawnMovementCamera(", primeCallIndex);
            int cameraIndex = roomManagerSource.IndexOf("CalculateRespawnCameraTargetForAuthority(", primeMethodIndex);
            int setCameraIndex = roomManagerSource.IndexOf("SetMovementCameraTarget(", cameraIndex);

            string playerHealthSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Player/PlayerHealth.cs"));
            int anchorCallIndex = playerHealthSource.IndexOf("_roomManager.RegisterDeathRespawnAnchor(");
            int killCallIndex = playerHealthSource.IndexOf("_roomManager.RegisterKill(movementAuthority);", anchorCallIndex);

            Assert.That(registerAnchorIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(primeCallIndex, Is.GreaterThan(registerAnchorIndex));
            Assert.That(primeMethodIndex, Is.GreaterThan(primeCallIndex));
            Assert.That(cameraIndex, Is.GreaterThan(primeMethodIndex));
            Assert.That(setCameraIndex, Is.GreaterThan(cameraIndex));
            Assert.That(anchorCallIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(killCallIndex, Is.GreaterThan(anchorCallIndex));
        }

        [Test]
        public void GameOverEvent_UsesRoomManagerRpcSoAllClientsReceivePresentationSignal()
        {
            string roomManagerSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/NetworkGameplay/Game/RoomManager.cs"));

            Assert.That(roomManagerSource, Does.Contain("Rpc_PublishGameOver(winnerName);"));
            Assert.That(roomManagerSource, Does.Contain("[Rpc(RpcSources.StateAuthority, RpcTargets.All)]"));
            Assert.That(roomManagerSource, Does.Contain("private void Rpc_PublishGameOver(string winnerName)"));
            Assert.That(roomManagerSource, Does.Contain("OnGameOverEvent?.Invoke(Runner, winnerName);"));

            string gameOverUiSource = File.ReadAllText(ToProjectPath("Assets/_Project/Scripts/Presentation/UI/GameOverUI.cs"));
            Assert.That(gameOverUiSource, Does.Contain("RoomManager.OnGameOverEvent += OnRoomManagerGameOver;"));
            Assert.That(gameOverUiSource, Does.Contain("NetworkManager.OnGameOver += OnNetworkGameOver;"));
            Assert.That(gameOverUiSource, Does.Contain("IsDuplicateGameOverSignal(winnerName)"));
            Assert.That(gameOverUiSource, Does.Contain("ResetGameOverDedupe();"));
        }

        [Test]
        public void RoomAdvanceDefenderRespawnAnchor_UsesNewSegmentEntrySide()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 3f,
                cameraSize = 13f,
                leftBound = 52f,
                rightBound = 156f,
                centerX = 104f
            };

            var rightAdvanceAnchor = RoomManager.CalculateRoomAdvanceDefenderRespawnAnchor(
                room,
                authorityDirection: 1,
                horizontalExtent: 0.5f);
            var leftAdvanceAnchor = RoomManager.CalculateRoomAdvanceDefenderRespawnAnchor(
                room,
                authorityDirection: -1,
                horizontalExtent: 0.5f);

            Assert.That(rightAdvanceAnchor.x, Is.EqualTo(54.5f).Within(0.001f));
            Assert.That(leftAdvanceAnchor.x, Is.EqualTo(153.5f).Within(0.001f));
            Assert.That(rightAdvanceAnchor.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(leftAdvanceAnchor.y, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void RoomAdvanceDefenderRespawnPosition_DoesNotFollowDriftedOpponentPosition()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 3f,
                cameraSize = 13f,
                leftBound = 52f,
                rightBound = 156f,
                centerX = 104f
            };

            var respawn = RoomManager.CalculateRoomAdvanceDefenderRespawnPosition(
                room,
                authorityDirection: 1,
                horizontalExtent: 0.5f,
                maxRespawnDistance: 8f);

            Assert.That(respawn.x, Is.EqualTo(95.91111f).Within(0.001f));
            Assert.That(respawn.y, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void RoomAdvanceDefenderRespawnDistance_UsesConfiguredSeparationFloor()
        {
            Assert.That(
                RoomManager.CalculateRoomAdvanceDefenderRespawnDistance(20.8f),
                Is.EqualTo(20.8f).Within(0.001f));
        }

        [Test]
        public void RoomAdvanceDefenderRespawnAnchor_IsOneShotPendingForDefender()
        {
            var advancer = PlayerRef.FromEncoded(1);
            var defender = PlayerRef.FromEncoded(2);

            Assert.That(
                RoomManager.ShouldUseRoomAdvanceRespawnAnchorForPlayer(
                    lastRoomAdvancePlayer: advancer,
                    lastRoomAdvanceIndex: 4,
                    pendingDefenderRespawnPlayer: defender,
                    pendingDefenderRespawnIndex: 4,
                    currentRoomIndex: 4,
                    deadPlayer: defender),
                Is.True);

            Assert.That(
                RoomManager.ShouldUseRoomAdvanceRespawnAnchorForPlayer(
                    lastRoomAdvancePlayer: advancer,
                    lastRoomAdvanceIndex: 4,
                    pendingDefenderRespawnPlayer: default,
                    pendingDefenderRespawnIndex: int.MinValue,
                    currentRoomIndex: 4,
                    deadPlayer: defender),
                Is.False);
        }

        [Test]
        public void SafeRoomXSearch_ChoosesNearestGroundedPlatform()
        {
            var room = new RoomData
            {
                leftBound = -10f,
                rightBound = 10f
            };

            bool found = RoomManager.TrySelectNearestSafeRoomX(
                room,
                0f,
                x => x <= -3f || x >= 1.5f,
                0.5f,
                20,
                out float safeX);

            Assert.That(found, Is.True);
            Assert.That(safeX, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void RoomAdvanceDefenderRespawnSafeSearch_ShiftsDefenderCandidateWithoutChangingAdvancerEntry()
        {
            var room = new RoomData
            {
                width = 104f,
                cameraY = 3f,
                cameraSize = 13f,
                leftBound = 52f,
                rightBound = 156f,
                centerX = 104f
            };

            var advancerEntry = RoomManager.CalculateAdvanceEntryPosition(
                room,
                isPlayer1Advancing: true,
                horizontalExtent: 0.5f);
            var defenderCandidate = RoomManager.CalculateRoomAdvanceDefenderRespawnPosition(
                room,
                authorityDirection: 1,
                horizontalExtent: 0.5f,
                maxRespawnDistance: 20.8f);

            bool found = RoomManager.TrySelectNearestSafeRoomX(
                room,
                defenderCandidate.x,
                x => x >= 96.5f,
                0.5f,
                20,
                out float safeDefenderX);

            Assert.That(advancerEntry.x, Is.EqualTo(54.5f).Within(0.001f));
            Assert.That(defenderCandidate.x, Is.EqualTo(95.91111f).Within(0.001f));
            Assert.That(found, Is.True);
            Assert.That(safeDefenderX, Is.EqualTo(96.91111f).Within(0.001f));
        }

        [Test]
        public void SafeRoomGroundedY_RejectsPitFloorBelowPlayableBand()
        {
            var room = new RoomData
            {
                cameraY = 3f,
                cameraSize = 13f
            };

            Assert.That(
                RoomManager.IsSafeRoomGroundedY(
                    room,
                    fallbackY: 4f,
                    groundedY: -7.46f),
                Is.False);
        }

        [Test]
        public void SafeRoomGroundedY_AllowsEntryPlatformBelowCameraCenter()
        {
            var room = new RoomData
            {
                cameraY = 3f,
                cameraSize = 13f
            };

            Assert.That(
                RoomManager.IsSafeRoomGroundedY(
                    room,
                    fallbackY: 4f,
                    groundedY: 0.24f),
                Is.True);
        }

        [Test]
        public void ResolveAuthorityDirection_FallsBackToDeadPlayersOpponent()
        {
            var player1 = PlayerRef.FromEncoded(1);
            var player2 = PlayerRef.FromEncoded(2);

            Assert.That(
                RoomManager.ResolveAuthorityDirection(
                    default,
                    player1,
                    player2,
                    fallbackDeadPlayer: player1),
                Is.EqualTo(-1));

            Assert.That(
                RoomManager.ResolveAuthorityDirection(
                    default,
                    player1,
                    player2,
                    fallbackDeadPlayer: player2),
                Is.EqualTo(1));
        }

        [Test]
        public void DeathMovementAuthority_FallsBackToOpponentEvenWhenOpponentIsRespawning()
        {
            var deadPlayer = PlayerRef.FromEncoded(1);
            var opponent = PlayerRef.FromEncoded(2);

            var authority = PlayerHealth.ResolveMovementAuthorityAfterDeath(
                creditedKiller: default,
                deadPlayer: deadPlayer,
                opponentPlayer: opponent);

            Assert.That(authority, Is.EqualTo(opponent));
        }

        [Test]
        public void DeathMovementAuthority_UsesCreditedKillerForDirectKill()
        {
            var deadPlayer = PlayerRef.FromEncoded(1);
            var opponent = PlayerRef.FromEncoded(2);

            var authority = PlayerHealth.ResolveMovementAuthorityAfterDeath(
                creditedKiller: opponent,
                deadPlayer: deadPlayer,
                opponentPlayer: opponent);

            Assert.That(authority, Is.EqualTo(opponent));
        }

        [Test]
        public void LocalAutoAnimatorAim_UsesNetworkedAimForVisuals()
        {
            Assert.That(
                PlayerAnimatorController.ShouldUseNetworkedAimForLocalAuthority(
                    localAutoInputMode: true,
                    hasGunController: true),
                Is.True);
        }

        [Test]
        public void LocalAutoAnimatorAim_KeepsMouseAimOutsideLocalAutoMode()
        {
            Assert.That(
                PlayerAnimatorController.ShouldUseNetworkedAimForLocalAuthority(
                    localAutoInputMode: false,
                    hasGunController: true),
                Is.False);

            Assert.That(
                PlayerAnimatorController.ShouldUseNetworkedAimForLocalAuthority(
                    localAutoInputMode: true,
                    hasGunController: false),
                Is.False);
        }

        [Test]
        public void MVPArena_IsDefaultSelectableSceneAndRegisteredInBuildSettings()
        {
            string mvpaPath = SelectableScene.MVPArena.ToScenePath();

            Assert.That(SelectableScene.Unspecified.ToSceneName(), Is.EqualTo("MVPArena"));
            Assert.That(mvpaPath, Is.EqualTo("Assets/_Project/Scenes/Greybox/MVPArena.unity"));
            Assert.That(HasEnabledEditorBuildScene(mvpaPath), Is.True);
        }

        private static bool HasEnabledEditorBuildScene(string scenePath)
        {
            foreach (var scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && scene.path == scenePath)
                    return true;
            }

            return false;
        }

        private static string ToProjectPath(string assetPath)
        {
            string relativeToAssets = assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(Application.dataPath, relativeToAssets);
        }
    }
}
