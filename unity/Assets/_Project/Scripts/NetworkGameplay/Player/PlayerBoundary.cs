using Colosseum.Game;
using Colosseum.CameraSystem;
using Colosseum.NetworkGameplay.Diagnostics;
using Fusion;
using Fusion.Addons.Physics;
using UnityEngine;

namespace Colosseum.Player
{
    public sealed class PlayerBoundary
    {
        // A303-268: forward boundary 도달 로그 dedupe.
        // 캐릭터가 우측 끝(P1)/좌측 끝(P2)에 닿아있는 동안 매 FixedUpdateNetwork tick 마다
        // Debug.Log가 발화해 Console이 동일 줄로 도배되는 문제(초당 30~60줄) 차단.
        // transition (false→true) 순간에만 로그하고, 해제 후 재진입 시 다시 활성화.
        // PlayerBoundary 인스턴스는 PlayerController 1:1 (per-player) 이므로 단일 필드로 충분.
        private bool _wasAtForwardBound;

        public void CheckRoomBoundary(PlayerController self, float moveX)
        {
            RoomManager roomManager = self.RoomManager;
            if (roomManager == null)
            {
                return;
            }

            if (!self.Object.HasStateAuthority)
            {
                return;
            }

            RoomData room = roomManager.GetCurrentRoom();
            if (room == null)
            {
                return;
            }

            float currentX = self.transform.position.x;
            float horizontalExtent = GetHorizontalBoundaryExtent(self.BodyCollider, self.SpriteRenderer, self.BoundaryPadding);
            bool hasMovementAuthority = roomManager.LastKiller != default(PlayerRef);

            if (!hasMovementAuthority)
            {
                float openingLeftClampX = GetOpeningViewportLeftClampX(room, horizontalExtent);
                float openingRightClampX = GetOpeningViewportRightClampX(room, horizontalExtent);

                if (openingLeftClampX > openingRightClampX)
                {
                    float midpointX = (openingLeftClampX + openingRightClampX) * 0.5f;
                    ClampToX(self.transform, self.Rigidbody, midpointX);
                    _wasAtForwardBound = false;
                    return;
                }

                bool pushingLeftOut = moveX < 0f;
                if (currentX < openingLeftClampX || (currentX <= openingLeftClampX && pushingLeftOut))
                {
                    ClampToX(self.transform, self.Rigidbody, openingLeftClampX);
                    _wasAtForwardBound = false;
                    return;
                }

                bool pushingRightOut = moveX > 0f;
                if (currentX > openingRightClampX || (currentX >= openingRightClampX && pushingRightOut))
                {
                    ClampToX(self.transform, self.Rigidbody, openingRightClampX);
                    _wasAtForwardBound = false;
                    return;
                }
            }

            // P1-3 디버그 (1인 단독 시연용): RoomManager._debugAllowBothDirections=true 시
            // P1/P2 비대칭 own-side clamp 비활성. 좌/우 양 boundary 모두 forward 가능.
            // SampleScene default false → 본 분기 진입 0 → 회귀 0.
            if (roomManager.DebugAllowBothDirections)
            {
                float leftBoundaryX_dbg = room.leftBound + horizontalExtent;
                float rightBoundaryX_dbg = room.rightBound - horizontalExtent;

                // narrow room (horizontalExtent×2 > room width): 양방향 동시 진입 race 방지.
                // right 검사를 먼저 return하므로 좌측 영구 누락 케이스를 차단.
                if (leftBoundaryX_dbg > rightBoundaryX_dbg)
                {
                    Debug.LogWarning($"[Colosseum/DBG] DebugBothDir narrow room: width={room.width:F2} < extent×2={horizontalExtent * 2f:F2}. Skipping advance check.");
                    _wasAtForwardBound = false;
                    return;
                }

                bool atLeftBound = currentX < leftBoundaryX_dbg || (currentX <= leftBoundaryX_dbg && moveX < 0f);
                bool atRightBound = currentX > rightBoundaryX_dbg || (currentX >= rightBoundaryX_dbg && moveX > 0f);

                if (atRightBound)
                {
                    if (!_wasAtForwardBound)
                    {
                        DiagLog.Log($"[Colosseum/DBG] DebugBothDir reached right bound (P1 forward).");
                    }
                    if (roomManager.TryAdvanceRoom(self.Object.InputAuthority, 1))
                    {
                        // direction +1 → 새 room 좌측 끝으로
                        DebugMoveAfterAdvance(self, roomManager, true);
                        roomManager.HandleRoomAdvance(self.Object.InputAuthority);
                    }
                    else
                    {
                        ClampToX(self.transform, self.Rigidbody, rightBoundaryX_dbg);
                    }
                    _wasAtForwardBound = atRightBound;
                    return;
                }

                if (atLeftBound)
                {
                    if (!_wasAtForwardBound)
                    {
                        DiagLog.Log($"[Colosseum/DBG] DebugBothDir reached left bound (P2 forward).");
                    }
                    if (roomManager.TryAdvanceRoom(self.Object.InputAuthority, -1))
                    {
                        // direction -1 → 새 room 우측 끝으로
                        DebugMoveAfterAdvance(self, roomManager, false);
                        roomManager.HandleRoomAdvance(self.Object.InputAuthority);
                    }
                    else
                    {
                        ClampToX(self.transform, self.Rigidbody, leftBoundaryX_dbg);
                    }
                    _wasAtForwardBound = atLeftBound;
                    return;
                }

                _wasAtForwardBound = false;
                return;
            }

            bool isPlayer1;
            if (roomManager.Player1 != default || roomManager.Player2 != default)
            {
                isPlayer1 = self.Object.InputAuthority == roomManager.Player1;
            }
            else
            {
                isPlayer1 = self.transform.position.x < room.centerX;
                Debug.LogWarning($"[Colosseum] Player1/Player2 not set! Using position fallback: isPlayer1={isPlayer1}");
            }

            if (isPlayer1)
            {
                float ownSideClampX = ResolveOwnSideClampX(
                    self,
                    roomManager,
                    room,
                    horizontalExtent,
                    ownSideIsLeft: true);
                if (ShouldClampOwnSideBoundary(
                    currentX,
                    ownSideClampX,
                    moveX,
                    ownSideIsLeft: true))
                {
                    ClampToX(self.transform, self.Rigidbody, ownSideClampX);
                    // own-side clamp 영역(자기 진영 끝)은 logically forward bound 반대.
                    // early return 직전에 깃발을 false로 reset해야 advance 후 자기 진영 시작점에서
                    // 깃발이 stuck 되어 다음 forward 진입 로그가 누락되는 결손 차단. (리뷰 반영)
                    _wasAtForwardBound = false;
                    return;
                }

                float forwardBoundaryX = room.rightBound - horizontalExtent;
                bool pushingForward = moveX > 0f;
                bool atForwardBoundP1 = currentX > forwardBoundaryX || (currentX >= forwardBoundaryX && pushingForward);
                if (atForwardBoundP1)
                {
                    if (!_wasAtForwardBound)
                    {
                        DiagLog.Log($"[Colosseum] Player1 reached right bound! LastKiller:{roomManager.LastKiller}");
                    }
                    if (roomManager.TryAdvanceRoom(self.Object.InputAuthority, 1))
                    {
                        MoveIntoAdvancedRoom(isPlayer1, self, roomManager);
                        roomManager.HandleRoomAdvance(self.Object.InputAuthority);
                    }
                    else
                    {
                        ClampToX(self.transform, self.Rigidbody, forwardBoundaryX);
                    }
                }
                _wasAtForwardBound = atForwardBoundP1;

                return;
            }

            float ownSideRightClampX = ResolveOwnSideClampX(
                self,
                roomManager,
                room,
                horizontalExtent,
                ownSideIsLeft: false);
            if (ShouldClampOwnSideBoundary(
                currentX,
                ownSideRightClampX,
                moveX,
                ownSideIsLeft: false))
            {
                ClampToX(self.transform, self.Rigidbody, ownSideRightClampX);
                // P2 own-side clamp early return — P1 분기와 동일 사유로 깃발 reset. (리뷰 반영)
                _wasAtForwardBound = false;
                return;
            }

            float forwardLeftBoundaryX = room.leftBound + horizontalExtent;
            bool pushingForwardLeft = moveX < 0f;
            bool atForwardBoundP2 = currentX < forwardLeftBoundaryX || (currentX <= forwardLeftBoundaryX && pushingForwardLeft);
            if (atForwardBoundP2)
            {
                if (!_wasAtForwardBound)
                {
                    DiagLog.Log($"[Colosseum] Player2 reached left bound! LastKiller:{roomManager.LastKiller}");
                }
                if (roomManager.TryAdvanceRoom(self.Object.InputAuthority, -1))
                {
                    MoveIntoAdvancedRoom(isPlayer1, self, roomManager);
                    roomManager.HandleRoomAdvance(self.Object.InputAuthority);
                }
                else
                {
                    ClampToX(self.transform, self.Rigidbody, forwardLeftBoundaryX);
                }
            }
            _wasAtForwardBound = atForwardBoundP2;
        }

        private float ResolveOwnSideClampX(
            PlayerController self,
            RoomManager roomManager,
            RoomData room,
            float horizontalExtent,
            bool ownSideIsLeft)
        {
            if (TryResolveMovementAuthorityBoundaryCameraX(self, roomManager, room, out float cameraX))
            {
                return ownSideIsLeft
                    ? GetVisibleLeftBoundary(cameraX, room.leftBound, room.cameraSize) + horizontalExtent
                    : GetVisibleRightBoundary(cameraX, room.rightBound, room.cameraSize) - horizontalExtent;
            }

            return ownSideIsLeft
                ? GetVisibleLeftBoundary(room) + horizontalExtent
                : GetVisibleRightBoundary(room) - horizontalExtent;
        }

        private bool TryResolveMovementAuthorityBoundaryCameraX(
            PlayerController self,
            RoomManager roomManager,
            RoomData room,
            out float cameraX)
        {
            cameraX = room != null ? room.centerX : 0f;
            if (self == null || roomManager == null || room == null)
            {
                return false;
            }

            PlayerRef authority = roomManager.LastKiller;
            if (authority == default(PlayerRef))
            {
                return false;
            }

            if (roomManager.TryGetAuthoritativeMovementCameraX(out cameraX))
            {
                return true;
            }

            int direction = RoomManager.ResolveAuthorityDirection(
                authority,
                roomManager.Player1,
                roomManager.Player2,
                default);
            if (direction == 0)
            {
                return false;
            }

            if (!TryResolvePlayerX(self, authority, out float authorityX))
            {
                return false;
            }

            bool preferEntrySide = roomManager.IsCurrentRoomAdvanceAuthority(authority);
            cameraX = CalculateMovementAuthorityBoundaryCameraX(
                authorityX,
                room.centerX,
                room.leftBound,
                room.rightBound,
                room.cameraSize,
                direction,
                preferEntrySide);
            return true;
        }

        private static bool TryResolvePlayerX(PlayerController self, PlayerRef player, out float x)
        {
            x = 0f;
            if (self != null &&
                self.Object != null &&
                self.Object.IsValid &&
                self.Object.InputAuthority == player)
            {
                x = self.transform.position.x;
                return true;
            }

            foreach (var activePlayer in PlayerController.ActivePlayersInternal)
            {
                if (activePlayer == null || activePlayer.Object == null || !activePlayer.Object.IsValid)
                {
                    continue;
                }

                if (self != null && activePlayer.gameObject.scene != self.gameObject.scene)
                {
                    continue;
                }

                if (activePlayer.Object.InputAuthority == player)
                {
                    x = activePlayer.transform.position.x;
                    return true;
                }
            }

            return false;
        }

        public static float CalculateMovementAuthorityBoundaryCameraX(
            float authorityX,
            float roomCenterX,
            float roomLeftBound,
            float roomRightBound,
            float cameraSize,
            int direction,
            bool preferEntrySide)
        {
            float viewHalfWidth = Mathf.Max(0.01f, cameraSize * CameraController.DesignViewportAspect);
            float minCameraX = roomLeftBound + viewHalfWidth;
            float maxCameraX = roomRightBound - viewHalfWidth;
            float neutralCameraX = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                roomCenterX,
                minCameraX,
                maxCameraX);
            float baseCameraX = preferEntrySide
                ? CameraController.CalculateMovementAuthorityRoomEntryCameraTargetX(
                    neutralCameraX,
                    minCameraX,
                    maxCameraX,
                    direction,
                    preferEntrySide: true)
                : neutralCameraX;

            return CameraController.CalculateMovementAuthorityCameraTargetX(
                baseCameraX,
                authorityX,
                midpointX: baseCameraX,
                viewHalfWidth,
                minCameraX,
                maxCameraX,
                direction,
                recenterToMidpoint: false);
        }

        public static bool ShouldClampOwnSideBoundary(
            float currentX,
            float ownSideClampX,
            float moveX,
            bool ownSideIsLeft)
        {
            return ownSideIsLeft
                ? currentX <= ownSideClampX && moveX < 0f
                : currentX >= ownSideClampX && moveX > 0f;
        }

        public void MoveIntoAdvancedRoom(bool isPlayer1, PlayerController self, RoomManager roomManager)
        {
            // 50/04 Fusion 가드레일: resimulation tick에서 transform write 비결정성 차단 (PitTrigger.cs 동일 패턴).
            if (self.Runner == null || !self.Runner.IsForward) return;

            RoomData advancedRoom = roomManager.GetCurrentRoom();
            if (advancedRoom == null)
            {
                return;
            }

            float horizontalExtent = GetHorizontalBoundaryExtent(self.BodyCollider, self.SpriteRenderer, self.BoundaryPadding);
            Vector3 targetPosition = roomManager.GetAdvanceEntryPosition(
                self,
                isPlayer1,
                horizontalExtent);
            TeleportNetworkedRigidbody(self, targetPosition);
        }

        /// <summary>
        /// P1-3 디버그 (1인 단독 시연용): 양방향 advance 후 새 room의 반대 끝으로 player 이동.
        /// rightForward=true → +1 advance, 새 room 좌측 edge 안쪽 clearance로 이동.
        /// rightForward=false → -1 advance, 새 room 우측 edge 안쪽 clearance로 이동.
        /// internal: assembly 내부에서만 호출 (PlayerBoundary 자체 호출만 의도).
        /// 진입부 HasStateAuthority 가드로 proxy 클라이언트의 transform write 차단 (Fusion 안전).
        /// X1'-α: entry X는 구간 경계 기준, Y는 현재 room의 실제 지형 raycast로 정렬.
        /// </summary>
        internal void DebugMoveAfterAdvance(PlayerController self, RoomManager roomManager, bool rightForward)
        {
            if (self == null || self.Object == null || !self.Object.HasStateAuthority) return;
            // 50/04 Fusion 가드레일: resimulation tick에서 transform write 비결정성 차단 (PitTrigger.cs 동일 패턴).
            if (self.Runner == null || !self.Runner.IsForward) return;

            RoomData advancedRoom = roomManager.GetCurrentRoom();
            if (advancedRoom == null) return;

            float horizontalExtent = GetHorizontalBoundaryExtent(self.BodyCollider, self.SpriteRenderer, self.BoundaryPadding);
            Vector3 targetPosition = roomManager.GetAdvanceEntryPosition(
                self,
                rightForward,
                horizontalExtent);
            TeleportNetworkedRigidbody(self, targetPosition);
        }

        public float GetHorizontalBoundaryExtent(Collider2D bodyCollider, SpriteRenderer spriteRenderer, float boundaryPadding)
        {
            float extent = boundaryPadding;

            if (bodyCollider != null)
            {
                extent = Mathf.Max(extent, bodyCollider.bounds.extents.x);
            }

            if (spriteRenderer != null)
            {
                extent = Mathf.Max(extent, spriteRenderer.bounds.extents.x);
            }

            return extent;
        }

        public float GetVisibleLeftBoundary(RoomData room)
        {
            return GetVisibleLeftBoundary(room.centerX, room.leftBound, room.cameraSize);
        }

        public float GetVisibleLeftBoundary(float roomCenterX, float roomLeftBound, float cameraSize)
        {
            float aspect = CameraController.DesignViewportAspect;
            float visibleLeft = roomCenterX - cameraSize * aspect;
            return Mathf.Max(visibleLeft, roomLeftBound);
        }

        public float GetVisibleRightBoundary(RoomData room)
        {
            return GetVisibleRightBoundary(room.centerX, room.rightBound, room.cameraSize);
        }

        public float GetVisibleRightBoundary(float roomCenterX, float roomRightBound, float cameraSize)
        {
            float aspect = CameraController.DesignViewportAspect;
            float visibleRight = roomCenterX + cameraSize * aspect;
            return Mathf.Min(visibleRight, roomRightBound);
        }

        public float GetOpeningViewportLeftClampX(RoomData room, float horizontalExtent)
        {
            return GetOpeningViewportLeftClampX(room.centerX, room.leftBound, room.cameraSize, horizontalExtent);
        }

        public float GetOpeningViewportLeftClampX(float roomCenterX, float roomLeftBound, float cameraSize, float horizontalExtent)
        {
            return GetVisibleLeftBoundary(roomCenterX, roomLeftBound, cameraSize) + horizontalExtent;
        }

        public float GetOpeningViewportRightClampX(RoomData room, float horizontalExtent)
        {
            return GetOpeningViewportRightClampX(room.centerX, room.rightBound, room.cameraSize, horizontalExtent);
        }

        public float GetOpeningViewportRightClampX(float roomCenterX, float roomRightBound, float cameraSize, float horizontalExtent)
        {
            return GetVisibleRightBoundary(roomCenterX, roomRightBound, cameraSize) - horizontalExtent;
        }

        public void ClampToX(Transform tr, Rigidbody2D rb, float targetX)
        {
            Vector3 pos = tr.position;
            pos.x = targetX;
            tr.position = pos;

            if (rb != null)
            {
                rb.velocity = new Vector2(0f, rb.velocity.y);
            }
        }

        private static void TeleportNetworkedRigidbody(PlayerController player, Vector3 position)
        {
            if (player == null) return;

            var networkRigidbody = player.GetComponent<NetworkRigidbody2D>();
            if (networkRigidbody != null)
            {
                networkRigidbody.Teleport(position);
            }

            player.transform.position = position;

            if (player.Rigidbody != null)
            {
                player.Rigidbody.position = position;
                player.Rigidbody.velocity = Vector2.zero;
            }

            Physics2D.SyncTransforms();
        }
    }
}
