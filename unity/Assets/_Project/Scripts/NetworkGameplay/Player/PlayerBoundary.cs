using Colosseum.Game;
using Fusion;
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
                        Debug.Log($"[Colosseum/DBG] DebugBothDir reached right bound (P1 forward).");
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
                        Debug.Log($"[Colosseum/DBG] DebugBothDir reached left bound (P2 forward).");
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
                float ownSideClampX = GetVisibleLeftBoundary(room) + horizontalExtent;
                bool pushingOwnSideOutward = moveX < 0f;
                if (currentX < ownSideClampX || (currentX <= ownSideClampX && pushingOwnSideOutward))
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
                        Debug.Log($"[Colosseum] Player1 reached right bound! LastKiller:{roomManager.LastKiller}");
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

            float ownSideRightClampX = GetVisibleRightBoundary(room) - horizontalExtent;
            bool pushingOwnSideOutwardRight = moveX > 0f;
            if (currentX > ownSideRightClampX || (currentX >= ownSideRightClampX && pushingOwnSideOutwardRight))
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
                    Debug.Log($"[Colosseum] Player2 reached left bound! LastKiller:{roomManager.LastKiller}");
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
            float targetX = isPlayer1
                ? advancedRoom.leftBound + horizontalExtent
                : advancedRoom.rightBound - horizontalExtent;

            // X1'-α (D77 D-15 시한부): advance 시 Y도 안전 floor 위로 강제 set (낙사 방지).
            // Y=2 = 1층(floor1Y=0) 위 2 단위. 자유낙하 후 1층 floor 안착. velocity 0 reset.
            // post-demo closure (D74-2): SpawnPoint adapter 도입 후 Y 좌표를 placeholder data 기반으로 결정.
            self.transform.position = new Vector3(targetX, 2f, 0f);
            if (self.Rigidbody != null) self.Rigidbody.velocity = Vector2.zero;
        }

        /// <summary>
        /// P1-3 디버그 (1인 단독 시연용): 양방향 advance 후 새 room의 반대 끝으로 player 이동.
        /// rightForward=true → +1 advance, 새 room 좌측 끝(leftBound+extent)으로 이동.
        /// rightForward=false → -1 advance, 새 room 우측 끝(rightBound-extent)으로 이동.
        /// internal: assembly 내부에서만 호출 (PlayerBoundary 자체 호출만 의도).
        /// 진입부 HasStateAuthority 가드로 proxy 클라이언트의 transform write 차단 (Fusion 안전).
        /// X1'-α: Y=2 강제 set (1층 floor 위 자유낙하 안전).
        /// </summary>
        internal void DebugMoveAfterAdvance(PlayerController self, RoomManager roomManager, bool rightForward)
        {
            if (self == null || self.Object == null || !self.Object.HasStateAuthority) return;
            // 50/04 Fusion 가드레일: resimulation tick에서 transform write 비결정성 차단 (PitTrigger.cs 동일 패턴).
            if (self.Runner == null || !self.Runner.IsForward) return;

            RoomData advancedRoom = roomManager.GetCurrentRoom();
            if (advancedRoom == null) return;

            float horizontalExtent = GetHorizontalBoundaryExtent(self.BodyCollider, self.SpriteRenderer, self.BoundaryPadding);
            float targetX = rightForward
                ? advancedRoom.leftBound + horizontalExtent
                : advancedRoom.rightBound - horizontalExtent;

            // X1'-α (D77): Y도 안전 floor 위로 set + velocity reset (advance 시 낙사 방지).
            self.transform.position = new Vector3(targetX, 2f, 0f);
            if (self.Rigidbody != null) self.Rigidbody.velocity = Vector2.zero;
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
            float aspect = 16f / 9f;
            float visibleLeft = room.centerX - room.cameraSize * aspect;
            return Mathf.Max(visibleLeft, room.leftBound);
        }

        public float GetVisibleRightBoundary(RoomData room)
        {
            float aspect = 16f / 9f;
            float visibleRight = room.centerX + room.cameraSize * aspect;
            return Mathf.Min(visibleRight, room.rightBound);
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
    }
}
