using System;
using UnityEngine;
using Fusion;
using Fusion.Addons.Physics;
using System.Collections.Generic;
using Colosseum.Contracts;
using Colosseum.Player;
using Colosseum.Utils;
using Colosseum.CameraSystem;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;

namespace Colosseum.Game
{
    public class RoomManager : NetworkBehaviour
    {
        [Header("Room Setup")]
        [SerializeField] private List<RoomData> _rooms = new();
        [SerializeField] private float _roomGap = 0f;

        [Header("Debug (P1-3 deferred)")]
        [Tooltip("디버그/시연용: P1/P2 own-side clamp 비활성 + 양 방향 forward boundary 검사. MVPArena 1인 단독 시연 한정. SampleScene default false 유지(회귀 0). P1-6에서 hot-seat 2P + PlayerSpawnPoint adapter 도입 후 deprecate 예정.")]
        [SerializeField] private bool _debugAllowBothDirections = false;
        public bool DebugAllowBothDirections => _debugAllowBothDirections;

        [Header("Respawn")]
        [SerializeField] private float _maxRespawnDistance = 8f;
        [SerializeField] private float _outOfBoundsPadding = 3f;
        [SerializeField] private float _cameraOutOfViewDeathPadding = 0.25f;

        // ── 게임 상태 ──
        // 클라이언트도 PlayerRef 비교에 쓰므로 6개 모두 [Networked]로 자동 동기화.
        // (Player1/Player2 set은 서버 권한 측에서만 일어나니 일반 필드라도 정합성은 OK였지만,
        //  AttackDirectionHUD가 클라에서 LastKiller==Player1 비교를 해야 해서 Networked로 승격.)
        [Networked] public int CurrentRoomIndex { get; set; }
        [Networked] public NetworkBool IsVictoryRunActive { get; set; }
        [Networked] public PlayerRef VictoryRunner { get; set; }
        [Networked] public PlayerRef LastKiller { get; set; }
        [Networked] public PlayerRef Player1 { get; set; }
        [Networked] public PlayerRef Player2 { get; set; }
        [Networked] public PlayerRef LastRespawnedPlayer { get; set; }
        [Networked] public int RespawnCameraRevision { get; set; }
        [Networked] public NetworkBool HasMovementCameraTarget { get; set; }
        [Networked] public float MovementCameraTargetX { get; set; }
        [Networked] public PlayerRef MovementCameraAuthority { get; set; }
        [Networked] public int MovementCameraRoomIndex { get; set; }
        [Networked] public int MovementCameraStateRevision { get; set; }

        // 게임 종료 후 final room에서 승자가 벽 밖으로 계속 밀면 TryAdvanceRoom이
        // tick마다 호출되어 OnGameWin이 수십 번 발동, GAME OVER 로그/UI 이벤트가 스팸된다.
        // 서버에서만 쓰는 1회성 플래그이므로 [Networked] 아님.
        private bool _gameEnded;
        private bool _networkedStateReady;
        private PlayerRef _lastRenderedPlayer1;
        private PlayerRef _lastRenderedPlayer2;
        private PlayerRef _lastRoomAdvancePlayer;
        private int _lastRoomAdvanceIndex = int.MinValue;
        private PlayerRef _pendingRoomAdvanceDefenderRespawnPlayer;
        private int _pendingRoomAdvanceDefenderRespawnIndex = int.MinValue;
        private int _lastAppliedMovementCameraRespawnRevision;
        private readonly Dictionary<PlayerRef, DeathRespawnAnchor> _pendingDeathRespawnAnchors = new();
        private const int SegmentAdvanceVfxSequenceSaltBase = 360;
        private const int VictoryZoneConfettiVfxSequenceSaltBase = 390;
        private const float UseVfxProfileFeedbackHint = 0f;
        private const float InitialSpawnViewportOffsetRatio = 0.35f;
        private const float RespawnSeparationViewportRatio = 0.9f;
        private const float RespawnAuthorityMovementDirectionInset = 0.15f;
        private const float RespawnAuthorityAnchorOffsetRatio = 1f - RespawnAuthorityMovementDirectionInset * 2f;
        private const float RoomEdgePadding = 1f;
        private const float InitialSpawnYCameraOffset = 1f;
        private const float AdvanceEntryBoundaryClearance = 2f;
        private const int MinimumRoomAdvanceTickGap = 12;
        private const float SafePositionRaycastHeight = 18f;
        private const float SafePositionRaycastDistance = 48f;
        private const float SafePositionGroundClearance = 0.04f;
        private const float SafePositionHorizontalSearchStep = 0.5f;
        private const int SafePositionHorizontalSearchSteps = 48;
        private const float SafePositionMaxGroundDropCameraSizeRatio = 0.5f;
        private const float FallbackPlayerHalfHeight = 1f;
        private int _lastRoomAdvanceTick = int.MinValue;

        // [Networked] CurrentRoomIndex 변경을 tick 폴링이 아닌 명시적 delta로 감지한다.
        // FixedUpdateNetwork 내부에서 CameraController가 폴링하면 타이밍/참조 이슈로
        // 변경을 놓치는 경우가 있어, 서버→클라 동기화된 값 변동을 ChangeDetector로
        // 확정 후 CameraController에 직접 푸시한다 (이전 RPC 우회 방식의 안정성 복원).
        private ChangeDetector _changeDetector;

        private struct DeathRespawnAnchor
        {
            public PlayerRef Authority;
            public Vector2 AuthorityPosition;
            public int RoomIndex;
        }

        /// <summary>
        /// Fusion이 NetworkObject 스폰 완료 시 호출. RoomManager는 씬에 NetworkObject로
        /// 직렬화되어 있으므로 서버/클라 양쪽에서 반드시 이 콜백이 호출된다.
        /// </summary>
        public override void Spawned()
        {
            _networkedStateReady = true;

            // 방 경계 계산은 "결정적 로컬 연산" — 모든 클라이언트에서 실행한다.
            // _rooms는 [SerializeField] (네트워크 동기화 대상 아님). 씬 파일에
            // leftBound/rightBound/centerX는 전부 0으로 직렬화되어 있으므로,
            // 서버에서만 계산하면 클라 _rooms는 0으로 남아 CameraController /
            // CheckRoomBoundary / GetRespawnPosition 등이 잘못된 좌표로 동작한다.
            CalculateRoomBounds();

            // ChangeDetector는 SimulationState 소스로 잡아야 서버가 write한 [Networked] 값이
            // 클라이언트의 interpolated state와 동기화된 다음에 delta로 찍힌다.
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _lastRenderedPlayer1 = Player1;
            _lastRenderedPlayer2 = Player2;

            DiagLog.Log($"[Colosseum/DIAG] RoomManager.Spawned(): HasStateAuthority={Object.HasStateAuthority}, " +
                        $"Rooms={_rooms.Count}, CurrentRoomIndex={CurrentRoomIndex}, " +
                        $"Room4 bounds=[{(_rooms.Count>4?_rooms[4].leftBound:0)},{(_rooms.Count>4?_rooms[4].rightBound:0)}]");

            if (Object.HasStateAuthority)
            {
                IsVictoryRunActive = false;
                VictoryRunner = default;
                LastKiller = default;
                LastRespawnedPlayer = default;
                RespawnCameraRevision = 0;
                _pendingDeathRespawnAnchors.Clear();
                _pendingRoomAdvanceDefenderRespawnPlayer = default;
                _pendingRoomAdvanceDefenderRespawnIndex = int.MinValue;
                _lastRoomAdvanceTick = int.MinValue;
                CurrentRoomIndex = _rooms.Count / 2;
                ResetMovementCameraStateForCurrentRoom();
                RegisterExistingPlayerSlots();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority || !IsNetworkedStateReady)
            {
                return;
            }

            RefreshAuthoritativeMovementCameraState(forceContextReset: false);
        }

        public bool IsNetworkedStateReady => _networkedStateReady && Object != null && Object.IsValid;

        /// <summary>
        /// [Networked] 값 변경 감지 → 씬의 모든 CameraController에 명시 알림.
        /// Render에서 돌려야 실제 동기화된 값 대비 delta가 정확히 찍힌다
        /// (FixedUpdateNetwork에서 돌리면 predicted vs confirmed 간 타이밍이 꼬인다).
        /// </summary>
        public override void Render()
        {
            if (_changeDetector == null) return;

            bool playerSlotsChanged = false;
            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(CurrentRoomIndex))
                {
                    DiagLog.Log($"[Colosseum] RoomManager.CurrentRoomIndex changed → {CurrentRoomIndex}. Notifying cameras.");
                    NotifyCamerasRoomChanged();
                }
                else if (change == nameof(Player1) || change == nameof(Player2))
                {
                    playerSlotsChanged = true;
                }
            }

            if (playerSlotsChanged || _lastRenderedPlayer1 != Player1 || _lastRenderedPlayer2 != Player2)
            {
                _lastRenderedPlayer1 = Player1;
                _lastRenderedPlayer2 = Player2;
                OnPlayerSlotsChanged?.Invoke(Player1, Player2);
            }
        }

        private void NotifyCamerasRoomChanged()
        {
            var cameras = SceneSearch.FindAllInScene<CameraSystem.CameraController>(gameObject.scene);
            foreach (var cam in cameras)
            {
                if (cam != null) cam.OnRoomChanged();
            }
        }

        private void CalculateRoomBounds()
        {
            float currentX = 0f;
            float totalWidth = 0f;

            for (int i = 0; i < _rooms.Count; i++)
            {
                totalWidth += _rooms[i].width;
                if (i < _rooms.Count - 1) totalWidth += _roomGap;
            }

            currentX = -totalWidth / 2f;

            for (int i = 0; i < _rooms.Count; i++)
            {
                _rooms[i].leftBound = currentX;
                _rooms[i].rightBound = currentX + _rooms[i].width;
                _rooms[i].centerX = currentX + _rooms[i].width / 2f;
                currentX += _rooms[i].width + _roomGap;
            }
        }

        public RoomData GetCurrentRoom()
        {
            if (!IsNetworkedStateReady || _rooms.Count == 0) return null;
            return _rooms[Mathf.Clamp(CurrentRoomIndex, 0, _rooms.Count - 1)];
        }

        public bool TryRegisterPlayerSlot(PlayerRef player)
        {
            if (!IsNetworkReadyForSlotAccess() || !Object.HasStateAuthority || player == default)
                return false;

            if (Player1 == player || Player2 == player)
                return true;

            if (Player1 == default(PlayerRef))
            {
                Player1 = player;
                DiagLog.Log($"[Colosseum] Auto-registered Player1: {player}");
                return true;
            }

            if (Player2 == default(PlayerRef))
            {
                Player2 = player;
                DiagLog.Log($"[Colosseum] Auto-registered Player2: {player}");
                return true;
            }

            return false;
        }

        public bool TryIsVictoryRunLocked(PlayerRef player, out bool locked)
        {
            locked = false;
            if (!IsNetworkedStateReady)
                return false;

            locked = IsVictoryRunActive && VictoryRunner != player;
            return true;
        }

        public bool IsAnyOpponentDead(PlayerRef player)
        {
            if (!IsNetworkedStateReady || player == default)
            {
                return false;
            }

            var allHealth = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            foreach (var health in allHealth)
            {
                if (health == null) continue;

                var netObj = health.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority == player) continue;

                try
                {
                    if (health.IsDead)
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                catch (FieldAccessException)
                {
                    continue;
                }
            }

            return false;
        }

        private bool IsNetworkReadyForSlotAccess()
        {
            return IsNetworkedStateReady;
        }

        private void RegisterExistingPlayerSlots()
        {
            if (!Object.HasStateAuthority) return;

            var players = SceneSearch.FindAllInScene<PlayerController>(gameObject.scene);
            foreach (var player in players)
            {
                if (player == null || player.Object == null) continue;
                TryRegisterPlayerSlot(player.Object.InputAuthority);
            }
        }

        public RoomData GetRoom(int index)
        {
            if (index < 0 || index >= _rooms.Count) return null;
            return _rooms[index];
        }

        public int RoomCount => _rooms.Count;

        public bool IsFinalRoomIndex(int roomIndex)
        {
            if (_rooms.Count == 0) return false;
            return roomIndex == 0 || roomIndex == _rooms.Count - 1;
        }

        public bool IsFinalRoomActive()
        {
            return IsFinalRoomIndex(CurrentRoomIndex);
        }

        public Vector3 GetInitialSpawnPosition(bool isFirstPlayer)
        {
            RoomData room = GetCurrentRoom();
            if (room == null || room.cameraSlideMode != CameraSlideMode.Wide)
            {
                return CalculateFallbackInitialSpawnPosition(isFirstPlayer);
            }

            return ResolveSafeRoomPosition(room, CalculateInitialSpawnPosition(room, isFirstPlayer), null);
        }

        public static Vector3 CalculateInitialSpawnPosition(RoomData room, bool isFirstPlayer)
        {
            if (room == null) return Vector3.zero;

            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float offset = Mathf.Max(RoomEdgePadding, viewHalfWidth * InitialSpawnViewportOffsetRatio);
            float x = room.centerX + (isFirstPlayer ? -offset : offset);
            x = Mathf.Clamp(x, room.leftBound + RoomEdgePadding, room.rightBound - RoomEdgePadding);
            return new Vector3(x, CalculateInitialSpawnY(room), 0f);
        }

        public static Vector3 CalculateFallbackInitialSpawnPosition(bool isFirstPlayer)
        {
            float fallbackX = isFirstPlayer ? -7.8f : 7.8f;
            return new Vector3(fallbackX, 4f, 0f);
        }

        public static float CalculateDesignViewHalfWidth(RoomData room)
        {
            if (room == null) return 0f;
            return Mathf.Max(0.01f, room.cameraSize * CameraController.DesignViewportAspect);
        }

        public bool TryGetAuthoritativeMovementCameraX(out float cameraX)
        {
            cameraX = MovementCameraTargetX;
            return IsNetworkedStateReady &&
                   HasMovementCameraTarget &&
                   MovementCameraRoomIndex == CurrentRoomIndex;
        }

        private void ResetMovementCameraStateForCurrentRoom()
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            RoomData room = GetCurrentRoom();
            if (room == null || room.cameraSlideMode != CameraSlideMode.Wide || IsVictoryRunActive)
            {
                SetMovementCameraTarget(default, default, hasTarget: false, forceRevision: true);
                return;
            }

            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minCameraX = room.leftBound + viewHalfWidth;
            float maxCameraX = room.rightBound - viewHalfWidth;
            float roomCenterX = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                room.centerX,
                minCameraX,
                maxCameraX);

            SetMovementCameraTarget(roomCenterX, default, hasTarget: true, forceRevision: true);
            _lastAppliedMovementCameraRespawnRevision = RespawnCameraRevision;
        }

        private void RefreshAuthoritativeMovementCameraState(bool forceContextReset)
        {
            if (!Object.HasStateAuthority)
            {
                return;
            }

            RoomData room = GetCurrentRoom();
            if (room == null || room.cameraSlideMode != CameraSlideMode.Wide || IsVictoryRunActive)
            {
                SetMovementCameraTarget(default, default, hasTarget: false, forceRevision: forceContextReset);
                return;
            }

            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minCameraX = room.leftBound + viewHalfWidth;
            float maxCameraX = room.rightBound - viewHalfWidth;
            float roomCenterX = CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                room.centerX,
                minCameraX,
                maxCameraX);

            PlayerRef authority = LastKiller;
            int direction = ResolveAuthorityDirection(authority, Player1, Player2, default);
            if (authority == default || direction == 0)
            {
                SetMovementCameraTarget(roomCenterX, default, hasTarget: true, forceRevision: forceContextReset);
                _lastAppliedMovementCameraRespawnRevision = RespawnCameraRevision;
                return;
            }

            bool resetContext = forceContextReset ||
                !HasMovementCameraTarget ||
                MovementCameraAuthority != authority ||
                MovementCameraRoomIndex != CurrentRoomIndex;
            float currentCameraX = resetContext
                ? CameraController.CalculateMovementAuthorityRoomEntryCameraTargetX(
                    roomCenterX,
                    minCameraX,
                    maxCameraX,
                    direction,
                    preferEntrySide: IsCurrentRoomAdvanceAuthority(authority))
                : MovementCameraTargetX;

            if (!TryResolvePlayerCameraState(authority, out float authorityX, out bool authorityDead))
            {
                SetMovementCameraTarget(currentCameraX, authority, hasTarget: true, forceRevision: resetContext);
                _lastAppliedMovementCameraRespawnRevision = RespawnCameraRevision;
                return;
            }

            PlayerRef other = authority == Player1 ? Player2 : Player1;
            bool hasOther = TryResolvePlayerCameraState(other, out float otherX, out bool otherDead);
            if (!hasOther)
            {
                otherX = authorityX;
                otherDead = false;
            }

            bool playerRespawned = RespawnCameraRevision > 0 &&
                RespawnCameraRevision != _lastAppliedMovementCameraRespawnRevision &&
                LastRespawnedPlayer != default &&
                LastRespawnedPlayer != authority;
            bool shouldRecenterAfterRespawn = !authorityDead &&
                playerRespawned &&
                !otherDead &&
                CameraController.ShouldRecenterMovementAuthorityCameraAfterRespawn(
                    currentCameraX,
                    authorityX,
                    otherX,
                    viewHalfWidth);

            float targetCameraX;
            if (shouldRecenterAfterRespawn)
            {
                float midpointX = (authorityX + otherX) * 0.5f;
                targetCameraX = CameraController.CalculateMovementAuthorityCameraTargetX(
                    currentCameraX,
                    authorityX,
                    midpointX,
                    viewHalfWidth,
                    minCameraX,
                    maxCameraX,
                    direction,
                    recenterToMidpoint: true);
            }
            else if (!authorityDead)
            {
                targetCameraX = CameraController.CalculateMovementAuthorityCameraTargetX(
                    currentCameraX,
                    authorityX,
                    midpointX: currentCameraX,
                    viewHalfWidth,
                    minCameraX,
                    maxCameraX,
                    direction,
                    recenterToMidpoint: false);
            }
            else
            {
                targetCameraX = minCameraX > maxCameraX
                    ? (minCameraX + maxCameraX) * 0.5f
                    : Mathf.Clamp(currentCameraX, minCameraX, maxCameraX);
            }

            SetMovementCameraTarget(
                targetCameraX,
                authority,
                hasTarget: true,
                forceRevision: resetContext || shouldRecenterAfterRespawn);
            _lastAppliedMovementCameraRespawnRevision = RespawnCameraRevision;
        }

        private bool TryResolvePlayerCameraState(PlayerRef player, out float x, out bool isDead)
        {
            x = 0f;
            isDead = false;
            if (player == default)
            {
                return false;
            }

            foreach (var activePlayer in PlayerController.ActivePlayersInternal)
            {
                if (activePlayer == null ||
                    activePlayer.Object == null ||
                    !activePlayer.Object.IsValid ||
                    activePlayer.gameObject.scene != gameObject.scene ||
                    activePlayer.Object.InputAuthority != player)
                {
                    continue;
                }

                x = activePlayer.transform.position.x;
                var health = activePlayer.GetComponent<PlayerHealth>();
                isDead = health != null && health.IsDead;
                return true;
            }

            return false;
        }

        private void SetMovementCameraTarget(
            float targetX,
            PlayerRef authority,
            bool hasTarget,
            bool forceRevision)
        {
            PlayerRef targetAuthority = hasTarget ? authority : default;
            int targetRoomIndex = hasTarget ? CurrentRoomIndex : int.MinValue;
            bool contextChanged =
                HasMovementCameraTarget != hasTarget ||
                MovementCameraAuthority != targetAuthority ||
                MovementCameraRoomIndex != targetRoomIndex;

            HasMovementCameraTarget = hasTarget;
            MovementCameraTargetX = targetX;
            MovementCameraAuthority = targetAuthority;
            MovementCameraRoomIndex = targetRoomIndex;

            if (forceRevision || contextChanged)
            {
                MovementCameraStateRevision = MovementCameraStateRevision == int.MaxValue
                    ? 1
                    : MovementCameraStateRevision + 1;
            }
        }

        public bool TryAdvanceRoom(PlayerRef player, int direction)
        {
            if (!Object.HasStateAuthority)
            {
                return false;
            }

            // 게임이 이미 끝났으면 추가 TryAdvance 시도는 모두 무시.
            // 승자가 final room에서 계속 벽 밖으로 밀면 FixedUpdateNetwork마다 호출되어
            // OnGameWin이 수십 번 발동되는 버그 방지.
            if (_gameEnded) return false;

            bool hasLastKiller = LastKiller != default(PlayerRef);
            if (hasLastKiller && player != LastKiller)
            {
                return false;
            }

            if (ShouldBlockRoomAdvanceByTick(
                Runner != null ? Runner.Tick.Raw : int.MinValue,
                _lastRoomAdvanceTick,
                MinimumRoomAdvanceTickGap))
            {
                return false;
            }

            int targetRoom = CurrentRoomIndex + direction;
            DiagLog.Log($"[Colosseum/DIAG] TryAdvanceRoom: player={player}, direction={direction}, from={CurrentRoomIndex} to={targetRoom}, RoomCount={_rooms.Count}");
            if (targetRoom < 0 || targetRoom >= _rooms.Count)
            {
                if (_debugAllowBothDirections)
                {
                    // 1인 시연 hack: 인덱스 wrap-around. _gameEnded set 안 함, OnGameWin 안 부름.
                    // SampleScene default false → 본 분기 진입 0 → 회귀 0.
                    // post-demo closure 의무 (D74).
                    targetRoom = (targetRoom + _rooms.Count) % _rooms.Count;
                    DiagLog.Log($"[Colosseum/DBG] DebugBothDir wrap: targetRoom={targetRoom}");
                }
                else
                {
                    _gameEnded = true;
                    OnGameWin(player);
                    return false;
                }
            }

            CurrentRoomIndex = targetRoom;
            _lastRoomAdvanceTick = Runner != null ? Runner.Tick.Raw : int.MinValue;
            if (IsFinalRoomIndex(targetRoom))
            {
                IsVictoryRunActive = true;
                VictoryRunner = player;
                DiagLog.Log($"[Colosseum] Victory run started in room {CurrentRoomIndex} by {player}");
            }

            DiagLog.Log($"[Colosseum] Room advanced to {CurrentRoomIndex} by {player}");
            return true;
        }

        public void HandleRoomAdvance(PlayerRef advancingPlayer)
        {
            if (!Object.HasStateAuthority) return;

            LastKiller = advancingPlayer;
            _lastRoomAdvancePlayer = advancingPlayer;
            _lastRoomAdvanceIndex = CurrentRoomIndex;
            _pendingRoomAdvanceDefenderRespawnPlayer = default;
            _pendingRoomAdvanceDefenderRespawnIndex = int.MinValue;

            bool enteredFinalRoom = IsVictoryRunActive && VictoryRunner == advancingPlayer;
            var cardDeck = SceneSearch.FindInScene<Card.CardDeck>(gameObject.scene);
            var allHealth = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            Vector2 advancePosition = GetSegmentAdvanceVfxPosition(advancingPlayer, allHealth);
            MoveAdvancingPlayerIntoCurrentRoom(advancingPlayer, allHealth);
            PublishSegmentAdvanceVfx(advancingPlayer, advancePosition);
            if (enteredFinalRoom)
            {
                PublishVictoryZoneConfettiVfx(advancingPlayer);
            }

            bool foundOpponent = false;

            foreach (var health in allHealth)
            {
                var netObj = health.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority == advancingPlayer) continue;

                foundOpponent = true;
                if (!enteredFinalRoom)
                {
                    _pendingRoomAdvanceDefenderRespawnPlayer = netObj.InputAuthority;
                    _pendingRoomAdvanceDefenderRespawnIndex = CurrentRoomIndex;
                }

                if (health.IsDead)
                {
                    DiagLog.Log($"[Colosseum] Room advance: opponent {netObj.InputAuthority} already dead");
                    continue;
                }

                DiagLog.Log($"[Colosseum] Room advance: killing opponent {netObj.InputAuthority}");
                health.ForceDeath(advancingPlayer);
            }

            if (!foundOpponent)
            {
                Debug.LogWarning($"[Colosseum] Room advance: no opponent NetworkObject found! " +
                    $"advancingPlayer={advancingPlayer}, Player1={Player1}, Player2={Player2}");
            }

            RefreshAuthoritativeMovementCameraState(forceContextReset: true);

            if (cardDeck == null)
            {
                OnAdvanceTriggered?.Invoke();
                return;
            }

            if (enteredFinalRoom)
            {
                cardDeck.CancelDraw();
                OnAdvanceTriggered?.Invoke();
                return;
            }

            foreach (var health in allHealth)
            {
                var netObj = health.GetComponent<NetworkObject>();
                if (netObj == null) continue;
                if (netObj.InputAuthority == advancingPlayer) continue;

                cardDeck.StartDraw(netObj.InputAuthority);
            }

            OnAdvanceTriggered?.Invoke();
        }

        private void MoveAdvancingPlayerIntoCurrentRoom(PlayerRef advancingPlayer, IReadOnlyList<PlayerHealth> allHealth)
        {
            if (advancingPlayer == default || allHealth == null) return;

            RoomData room = GetCurrentRoom();
            if (room == null) return;

            foreach (var health in allHealth)
            {
                if (health == null) continue;

                var netObj = health.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority != advancingPlayer) continue;

                float horizontalExtent = CalculatePlayerHorizontalExtent(health);
                bool isPlayer1Advancing = advancingPlayer == Player1 || advancingPlayer != Player2;
                Vector3 targetPosition = GetAdvanceEntryPosition(health, isPlayer1Advancing, horizontalExtent);

                TeleportNetworkedRigidbody(health, targetPosition);

                DiagLog.Log($"[Colosseum] Room advance: positioned advancing player {advancingPlayer} at entry ({targetPosition.x:F2},{targetPosition.y:F2}) room={CurrentRoomIndex}");
                return;
            }
        }

        public static float CalculateAdvanceEntryX(
            bool isPlayer1Advancing,
            float roomLeftBound,
            float roomRightBound,
            float horizontalExtent)
        {
            float entryInset = Mathf.Max(RoomEdgePadding, Mathf.Max(0f, horizontalExtent) + AdvanceEntryBoundaryClearance);
            float leftEntry = roomLeftBound + entryInset;
            float rightEntry = roomRightBound - entryInset;
            if (leftEntry > rightEntry)
            {
                return (roomLeftBound + roomRightBound) * 0.5f;
            }

            return isPlayer1Advancing
                ? leftEntry
                : rightEntry;
        }

        public static bool ShouldBlockRoomAdvanceByTick(int currentTick, int lastAdvanceTick, int minimumTickGap)
        {
            if (currentTick == int.MinValue || lastAdvanceTick == int.MinValue)
            {
                return false;
            }

            int tickGap = currentTick - lastAdvanceTick;
            return tickGap >= 0 && tickGap < Mathf.Max(1, minimumTickGap);
        }

        public static Vector3 CalculateAdvanceEntryPosition(
            RoomData room,
            bool isPlayer1Advancing,
            float horizontalExtent)
        {
            if (room == null) return Vector3.zero;

            float targetX = CalculateAdvanceEntryX(
                isPlayer1Advancing,
                room.leftBound,
                room.rightBound,
                horizontalExtent);
            return new Vector3(targetX, CalculateAdvanceEntryY(room), 0f);
        }

        public Vector3 GetAdvanceEntryPosition(
            Component player,
            bool isPlayer1Advancing,
            float horizontalExtent)
        {
            RoomData room = GetCurrentRoom();
            if (room == null) return Vector3.zero;

            Vector3 candidate = CalculateAdvanceEntryPosition(
                room,
                isPlayer1Advancing,
                horizontalExtent);
            return ResolveSafeRoomPosition(room, candidate, player);
        }

        private static float CalculateInitialSpawnY(RoomData room)
        {
            return room != null ? room.cameraY + InitialSpawnYCameraOffset : 0f;
        }

        private static float CalculateAdvanceEntryY(RoomData room)
        {
            return CalculateInitialSpawnY(room);
        }

        private static float CalculatePlayerHorizontalExtent(PlayerHealth health)
        {
            float boundaryPadding = 0.25f;
            var player = health.GetComponent<PlayerController>();
            if (player != null)
            {
                boundaryPadding = player.BoundaryPadding;
            }

            float extent = boundaryPadding;
            var bodyCollider = health.GetComponent<Collider2D>();
            if (bodyCollider != null)
            {
                extent = Mathf.Max(extent, bodyCollider.bounds.extents.x);
            }

            var spriteRenderer = health.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                extent = Mathf.Max(extent, spriteRenderer.bounds.extents.x);
            }

            return extent;
        }

        private static void TeleportNetworkedRigidbody(Component target, Vector3 position)
        {
            if (target == null) return;

            var networkRigidbody = target.GetComponent<NetworkRigidbody2D>();
            if (networkRigidbody != null)
            {
                networkRigidbody.Teleport(position);
            }

            target.transform.position = position;

            var rb = target.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = position;
                rb.velocity = Vector2.zero;
            }

            Physics2D.SyncTransforms();
        }

        private Vector2 GetSegmentAdvanceVfxPosition(PlayerRef advancingPlayer, IReadOnlyList<PlayerHealth> allHealth)
        {
            foreach (var health in allHealth)
            {
                var netObj = health != null ? health.GetComponent<NetworkObject>() : null;
                if (netObj != null && netObj.InputAuthority == advancingPlayer)
                {
                    return health.transform.position;
                }
            }

            RoomData currentRoom = GetCurrentRoom();
            if (currentRoom != null)
            {
                return new Vector2(currentRoom.centerX, currentRoom.cameraY);
            }

            return transform.position;
        }

        private void PublishSegmentAdvanceVfx(PlayerRef advancingPlayer, Vector2 position)
        {
            string effectKey = VfxPayloadFactory.GetEnvironmentEffectKey(VfxEventType.SegmentBoundaryAdvanced);
            if (string.IsNullOrEmpty(effectKey)) return;

            Rpc_PublishSegmentAdvanceVfx(
                advancingPlayer,
                position.x,
                position.y,
                CurrentRoomIndex);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishSegmentAdvanceVfx(PlayerRef advancingPlayer, float x, float y, int roomIndex)
        {
            string effectKey = VfxPayloadFactory.GetEnvironmentEffectKey(VfxEventType.SegmentBoundaryAdvanced);
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.SegmentBoundaryAdvanced,
                string.Empty,
                effectKey,
                Runner,
                Object,
                advancingPlayer,
                default,
                new Vector2(x, y),
                Vector2.zero,
                UseVfxProfileFeedbackHint,
                UseVfxProfileFeedbackHint,
                roomIndex,
                VfxAttachPoint.World,
                SegmentAdvanceVfxSequenceSaltBase + roomIndex));
        }

        private void PublishVictoryZoneConfettiVfx(PlayerRef advancingPlayer)
        {
            string effectKey = VfxPayloadFactory.GetEnvironmentEffectKey(VfxEventType.VictoryZoneReached);
            if (string.IsNullOrEmpty(effectKey)) return;

            RoomData room = GetCurrentRoom();
            if (room == null) return;

            float width = Mathf.Max(1f, room.rightBound - room.leftBound);
            float height = Mathf.Max(1f, room.cameraSize * 2f);
            Rpc_PublishVictoryZoneConfettiVfx(
                advancingPlayer,
                room.centerX,
                room.cameraY,
                width,
                height,
                CurrentRoomIndex);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishVictoryZoneConfettiVfx(
            PlayerRef advancingPlayer,
            float x,
            float y,
            float width,
            float height,
            int roomIndex)
        {
            string effectKey = VfxPayloadFactory.GetEnvironmentEffectKey(VfxEventType.VictoryZoneReached);
            VfxEventBus.Publish(VfxPayloadFactory.Create(
                VfxEventType.VictoryZoneReached,
                string.Empty,
                effectKey,
                Runner,
                Object,
                advancingPlayer,
                default,
                new Vector2(x, y),
                Vector2.down,
                width,
                UseVfxProfileFeedbackHint,
                height,
                VfxAttachPoint.World,
                VictoryZoneConfettiVfxSequenceSaltBase + roomIndex));
        }

        public void RegisterKill(PlayerRef killer)
        {
            if (!Object.HasStateAuthority) return;

            if (killer == default(PlayerRef))
            {
                DiagLog.Log("[Colosseum] RegisterKill skipped: killer is None (suicide/out-of-bounds)");
                return;
            }

            LastKiller = killer;
            RefreshAuthoritativeMovementCameraState(forceContextReset: false);
            DiagLog.Log($"[Colosseum] Last killer set to: {killer}");
        }

        public void RegisterRespawn(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            if (player == default(PlayerRef)) return;

            LastRespawnedPlayer = player;
            _pendingDeathRespawnAnchors.Remove(player);
            RespawnCameraRevision = RespawnCameraRevision == int.MaxValue
                ? 1
                : RespawnCameraRevision + 1;
            if (_pendingRoomAdvanceDefenderRespawnPlayer == player &&
                _pendingRoomAdvanceDefenderRespawnIndex == CurrentRoomIndex)
            {
                _pendingRoomAdvanceDefenderRespawnPlayer = default;
                _pendingRoomAdvanceDefenderRespawnIndex = int.MinValue;
            }

            RefreshAuthoritativeMovementCameraState(forceContextReset: false);
            DiagLog.Log($"[Colosseum] Respawn camera revision {RespawnCameraRevision} by {player}");
        }

        public void RegisterDeathRespawnAnchor(
            PlayerRef deadPlayer,
            PlayerRef movementAuthority,
            Vector2 movementAuthorityPosition)
        {
            if (!Object.HasStateAuthority) return;
            if (deadPlayer == default(PlayerRef)) return;

            _pendingDeathRespawnAnchors[deadPlayer] = new DeathRespawnAnchor
            {
                Authority = movementAuthority,
                AuthorityPosition = movementAuthorityPosition,
                RoomIndex = CurrentRoomIndex
            };

            PrimeDeathRespawnMovementCamera(deadPlayer, movementAuthority, movementAuthorityPosition);
            DiagLog.Log($"[Colosseum] Death respawn anchor registered dead={deadPlayer} authority={movementAuthority} room={CurrentRoomIndex} position=({movementAuthorityPosition.x:F2},{movementAuthorityPosition.y:F2})");
        }

        private void PrimeDeathRespawnMovementCamera(
            PlayerRef deadPlayer,
            PlayerRef movementAuthority,
            Vector2 movementAuthorityPosition)
        {
            if (movementAuthority == default ||
                ShouldUseRoomAdvanceRespawnAnchor(deadPlayer))
            {
                return;
            }

            RoomData room = GetCurrentRoom();
            int direction = ResolveAuthorityDirection(movementAuthority, Player1, Player2, deadPlayer);
            if (room == null || direction == 0)
            {
                return;
            }

            Vector2 authorityAnchor = ResolveCurrentRespawnAuthorityAnchor(
                movementAuthority,
                movementAuthorityPosition);
            float cameraTargetX = CalculateRespawnCameraTargetForAuthority(
                room,
                authorityAnchor.x,
                direction);

            SetMovementCameraTarget(
                cameraTargetX,
                movementAuthority,
                hasTarget: true,
                forceRevision: true);
        }

        public Vector2 GetRespawnPosition(PlayerRef deadPlayer, Vector2 opponentPosition)
        {
            return GetRespawnPosition(deadPlayer, opponentPosition, null);
        }

        public Vector2 GetRespawnPosition(PlayerRef deadPlayer, Vector2 opponentPosition, Component respawningPlayer)
        {
            var room = GetCurrentRoom();
            if (room == null) return Vector2.zero;

            bool useRoomAdvanceAnchor = ShouldUseRoomAdvanceRespawnAnchor(deadPlayer);
            DeathRespawnAnchor deathAnchor = default;
            bool hasDeathAnchor = !useRoomAdvanceAnchor &&
                TryGetDeathRespawnAnchor(deadPlayer, out deathAnchor);
            PlayerRef respawnAuthority = useRoomAdvanceAnchor
                ? _lastRoomAdvancePlayer
                : hasDeathAnchor && deathAnchor.Authority != default
                    ? deathAnchor.Authority
                    : LastKiller;
            int direction = ResolveAuthorityDirection(respawnAuthority, Player1, Player2, deadPlayer);

            float horizontalExtent = CalculatePlayerHorizontalExtent(respawningPlayer);
            Vector2 authorityAnchor = useRoomAdvanceAnchor
                ? CalculateRoomAdvanceDefenderRespawnAnchor(
                    room,
                    direction,
                    horizontalExtent)
                : hasDeathAnchor
                    ? deathAnchor.AuthorityPosition
                    : opponentPosition;
            if (!useRoomAdvanceAnchor)
            {
                authorityAnchor = ResolveCurrentRespawnAuthorityAnchor(
                    respawnAuthority,
                    authorityAnchor);
            }

            float cameraTargetX = useRoomAdvanceAnchor
                ? ResolveRoomAdvanceRespawnCameraTargetX(room, authorityAnchor)
                : CalculateRespawnCameraTargetForAuthority(
                    room,
                    authorityAnchor.x,
                    direction);
            if (Object != null && Object.HasStateAuthority && respawnAuthority != default)
            {
                SetMovementCameraTarget(
                    cameraTargetX,
                    respawnAuthority,
                    hasTarget: true,
                    forceRevision: true);
            }

            Vector2 candidate = useRoomAdvanceAnchor
                ? CalculateRoomAdvanceDefenderRespawnPosition(
                    room,
                    direction,
                    horizontalExtent,
                    _maxRespawnDistance,
                    cameraTargetX)
                : CalculateDeathRespawnPosition(
                    room,
                    authorityAnchor,
                    direction,
                    _maxRespawnDistance,
                    cameraTargetX);
            Vector3 safe = ResolveSafeRespawnPosition(
                room,
                candidate,
                respawningPlayer,
                useRoomAdvanceAnchor);
            string anchorLabel = useRoomAdvanceAnchor
                ? "room-advance"
                : hasDeathAnchor ? "death-authority" : "opponent";
            DiagLog.Log($"[Colosseum] Respawn position resolved player={deadPlayer} room={CurrentRoomIndex} anchor={anchorLabel} anchorPos=({authorityAnchor.x:F2},{authorityAnchor.y:F2}) cameraTargetX={cameraTargetX:F2} candidate=({candidate.x:F2},{candidate.y:F2}) safe=({safe.x:F2},{safe.y:F2}) direction={direction}");
            return new Vector2(safe.x, safe.y);
        }

        private Vector2 ResolveCurrentRespawnAuthorityAnchor(
            PlayerRef respawnAuthority,
            Vector2 fallbackAnchor)
        {
            if (respawnAuthority != default &&
                TryResolvePlayerCameraState(respawnAuthority, out float authorityX, out bool authorityDead) &&
                !authorityDead)
            {
                return new Vector2(authorityX, fallbackAnchor.y);
            }

            return fallbackAnchor;
        }

        private bool TryGetDeathRespawnAnchor(PlayerRef deadPlayer, out DeathRespawnAnchor anchor)
        {
            if (_pendingDeathRespawnAnchors.TryGetValue(deadPlayer, out anchor) &&
                anchor.RoomIndex == CurrentRoomIndex)
            {
                return true;
            }

            anchor = default;
            return false;
        }

        private bool ShouldUseRoomAdvanceRespawnAnchor(PlayerRef deadPlayer)
        {
            return ShouldUseRoomAdvanceRespawnAnchorForPlayer(
                _lastRoomAdvancePlayer,
                _lastRoomAdvanceIndex,
                _pendingRoomAdvanceDefenderRespawnPlayer,
                _pendingRoomAdvanceDefenderRespawnIndex,
                CurrentRoomIndex,
                deadPlayer);
        }

        public static bool ShouldUseRoomAdvanceRespawnAnchorForPlayer(
            PlayerRef lastRoomAdvancePlayer,
            int lastRoomAdvanceIndex,
            PlayerRef pendingDefenderRespawnPlayer,
            int pendingDefenderRespawnIndex,
            int currentRoomIndex,
            PlayerRef deadPlayer)
        {
            return deadPlayer != default &&
                   lastRoomAdvancePlayer != default &&
                   pendingDefenderRespawnPlayer == deadPlayer &&
                   pendingDefenderRespawnIndex == currentRoomIndex &&
                   lastRoomAdvanceIndex == currentRoomIndex &&
                   deadPlayer != lastRoomAdvancePlayer;
        }

        public bool IsCurrentRoomAdvanceAuthority(PlayerRef player)
        {
            return player != default &&
                   LastKiller == player &&
                   _lastRoomAdvancePlayer == player &&
                   _lastRoomAdvanceIndex == CurrentRoomIndex;
        }

        private static float CalculatePlayerHorizontalExtent(Component player)
        {
            var health = player != null ? player.GetComponent<PlayerHealth>() : null;
            return health != null ? CalculatePlayerHorizontalExtent(health) : 0f;
        }

        public static Vector2 CalculateRoomAdvanceDefenderRespawnAnchor(
            RoomData room,
            int authorityDirection,
            float horizontalExtent)
        {
            bool authorityAdvancesRight = authorityDirection >= 0;
            Vector3 entry = CalculateAdvanceEntryPosition(
                room,
                authorityAdvancesRight,
                horizontalExtent);
            return new Vector2(entry.x, entry.y);
        }

        public static float CalculateRoomAdvanceDefenderRespawnDistance(float maxRespawnDistance)
        {
            return Mathf.Abs(maxRespawnDistance);
        }

        public static Vector2 CalculateRoomAdvanceDefenderRespawnPosition(
            RoomData room,
            int authorityDirection,
            float horizontalExtent,
            float maxRespawnDistance)
        {
            Vector2 anchor = CalculateRoomAdvanceDefenderRespawnAnchor(
                room,
                authorityDirection,
                horizontalExtent);
            return CalculateRoomAdvanceDefenderRespawnPosition(
                room,
                authorityDirection,
                horizontalExtent,
                maxRespawnDistance,
                CalculateRespawnCameraTargetX(room, anchor.x));
        }

        public static Vector2 CalculateRoomAdvanceDefenderRespawnPosition(
            RoomData room,
            int authorityDirection,
            float horizontalExtent,
            float maxRespawnDistance,
            float cameraTargetX)
        {
            Vector2 anchor = CalculateRoomAdvanceDefenderRespawnAnchor(
                room,
                authorityDirection,
                horizontalExtent);
            return CalculateRespawnPosition(
                room,
                anchor,
                authorityDirection,
                CalculateRoomAdvanceDefenderRespawnDistance(maxRespawnDistance),
                cameraTargetX);
        }

        public static Vector2 CalculateRespawnPosition(
            RoomData room,
            Vector2 authorityOrOpponentPosition,
            int authorityDirection,
            float maxRespawnDistance)
        {
            return CalculateRespawnPosition(
                room,
                authorityOrOpponentPosition,
                authorityDirection,
                maxRespawnDistance,
                CalculateRespawnCameraTargetX(room, authorityOrOpponentPosition.x));
        }

        public static Vector2 CalculateRespawnPosition(
            RoomData room,
            Vector2 authorityOrOpponentPosition,
            int authorityDirection,
            float maxRespawnDistance,
            float cameraTargetX)
        {
            if (room == null) return Vector2.zero;

            int direction = authorityDirection < 0 ? -1 : 1;
            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minimumSeparation = Mathf.Max(RoomEdgePadding, Mathf.Abs(maxRespawnDistance));
            float cameraReferenceX = CalculateRespawnCameraTargetX(room, cameraTargetX);
            float viewportEdgeX = cameraReferenceX + direction * viewHalfWidth * RespawnSeparationViewportRatio;
            float separatedAnchorX = authorityOrOpponentPosition.x + direction * minimumSeparation;
            float respawnX = direction > 0
                ? Mathf.Max(viewportEdgeX, separatedAnchorX)
                : Mathf.Min(viewportEdgeX, separatedAnchorX);
            respawnX = Mathf.Clamp(respawnX, room.leftBound + RoomEdgePadding, room.rightBound - RoomEdgePadding);

            return new Vector2(respawnX, CalculateInitialSpawnY(room));
        }

        public static Vector2 CalculateDeathRespawnPosition(
            RoomData room,
            Vector2 authorityPosition,
            int authorityDirection,
            float maxRespawnDistance,
            float cameraTargetX)
        {
            if (room == null) return Vector2.zero;

            int direction = authorityDirection < 0 ? -1 : 1;
            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minimumSeparation = Mathf.Max(RoomEdgePadding, Mathf.Abs(maxRespawnDistance));
            float cameraReferenceX = CalculateRespawnCameraTargetX(room, cameraTargetX);
            float forwardViewportAnchorX =
                cameraReferenceX + direction * viewHalfWidth * RespawnAuthorityAnchorOffsetRatio;
            float separatedAnchorX = authorityPosition.x + direction * minimumSeparation;
            float respawnX = direction > 0
                ? Mathf.Max(forwardViewportAnchorX, separatedAnchorX)
                : Mathf.Min(forwardViewportAnchorX, separatedAnchorX);
            respawnX = Mathf.Clamp(respawnX, room.leftBound + RoomEdgePadding, room.rightBound - RoomEdgePadding);

            return new Vector2(respawnX, CalculateInitialSpawnY(room));
        }

        public static float CalculateRespawnCameraTargetForAuthority(
            RoomData room,
            float authorityX,
            int authorityDirection)
        {
            if (room == null) return authorityX;
            if (authorityDirection == 0)
            {
                return CalculateRespawnCameraTargetX(room, authorityX);
            }

            int direction = authorityDirection < 0 ? -1 : 1;
            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float cameraTargetX =
                authorityX + direction * viewHalfWidth * RespawnAuthorityAnchorOffsetRatio;
            return CalculateRespawnCameraTargetX(room, cameraTargetX);
        }

        public static float CalculateRespawnCameraTargetX(RoomData room, float cameraOrAnchorX)
        {
            if (room == null) return cameraOrAnchorX;

            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minCameraX = room.leftBound + viewHalfWidth;
            float maxCameraX = room.rightBound - viewHalfWidth;
            if (minCameraX > maxCameraX)
            {
                return (minCameraX + maxCameraX) * 0.5f;
            }

            return Mathf.Clamp(cameraOrAnchorX, minCameraX, maxCameraX);
        }

        private float ResolveRoomAdvanceRespawnCameraTargetX(RoomData room, Vector2 authorityAnchor)
        {
            return TryGetAuthoritativeMovementCameraX(out float cameraTargetX)
                ? CalculateRespawnCameraTargetX(room, cameraTargetX)
                : CalculateRespawnCameraTargetX(room, authorityAnchor.x);
        }

        private Vector3 ResolveSafeRespawnPosition(
            RoomData room,
            Vector2 candidate,
            Component player,
            bool isRoomAdvanceDefenderRespawn)
        {
            Vector3 safe = ResolveSafeRoomPosition(
                room,
                new Vector3(candidate.x, candidate.y, 0f),
                player);
            if (isRoomAdvanceDefenderRespawn &&
                (!Mathf.Approximately(safe.x, candidate.x) || !Mathf.Approximately(safe.y, candidate.y)))
            {
                DiagLog.Log($"[Colosseum] Room advance defender respawn safe-adjusted candidate=({candidate.x:F2},{candidate.y:F2}) safe=({safe.x:F2},{safe.y:F2}) room={CurrentRoomIndex}");
            }

            return safe;
        }

        private Vector3 ResolveSafeRoomPosition(RoomData room, Vector3 candidate, Component player)
        {
            if (room == null)
            {
                return candidate;
            }

            float x = Mathf.Clamp(
                candidate.x,
                room.leftBound + RoomEdgePadding,
                room.rightBound - RoomEdgePadding);
            float fallbackY = candidate.y;

            bool HasSafeGroundAt(float probeX)
            {
                return TryFindGroundedY(room, probeX, fallbackY, player, out _);
            }

            if (TrySelectNearestSafeRoomX(room, x, HasSafeGroundAt, out float safeX) &&
                TryFindGroundedY(room, safeX, fallbackY, player, out float groundedY))
            {
                if (!Mathf.Approximately(safeX, x))
                {
                    DiagLog.Log($"[Colosseum] Safe room position shifted to nearest ground x={safeX:F2} from x={x:F2}");
                }

                return new Vector3(safeX, groundedY, 0f);
            }

            return new Vector3(x, fallbackY, 0f);
        }

        public static bool TrySelectNearestSafeRoomX(
            RoomData room,
            float preferredX,
            Func<float, bool> hasSafeGroundAtX,
            out float safeX)
        {
            return TrySelectNearestSafeRoomX(
                room,
                preferredX,
                hasSafeGroundAtX,
                SafePositionHorizontalSearchStep,
                SafePositionHorizontalSearchSteps,
                out safeX);
        }

        public static bool TrySelectNearestSafeRoomX(
            RoomData room,
            float preferredX,
            Func<float, bool> hasSafeGroundAtX,
            float searchStep,
            int searchSteps,
            out float safeX)
        {
            safeX = preferredX;
            if (room == null || hasSafeGroundAtX == null)
            {
                return false;
            }

            float minX = room.leftBound + RoomEdgePadding;
            float maxX = room.rightBound - RoomEdgePadding;
            float clampedPreferredX = Mathf.Clamp(preferredX, minX, maxX);
            safeX = clampedPreferredX;
            if (hasSafeGroundAtX(clampedPreferredX))
            {
                return true;
            }

            float stepSize = Mathf.Max(0.01f, Mathf.Abs(searchStep));
            int steps = Mathf.Max(0, searchSteps);
            for (int step = 1; step <= steps; step++)
            {
                float offset = step * stepSize;
                float leftX = clampedPreferredX - offset;
                float rightX = clampedPreferredX + offset;

                if (leftX >= minX && hasSafeGroundAtX(leftX))
                {
                    safeX = leftX;
                    return true;
                }

                if (rightX <= maxX && hasSafeGroundAtX(rightX))
                {
                    safeX = rightX;
                    return true;
                }
            }

            return false;
        }

        private bool TryFindGroundedY(
            RoomData room,
            float x,
            float fallbackY,
            Component player,
            out float groundedY)
        {
            groundedY = fallbackY;
            int layerMask = ResolveGroundLayerMask(player);
            float halfHeight = ResolvePlayerHalfHeight(player);
            float originY = Mathf.Max(fallbackY, room.cameraY) + SafePositionRaycastHeight;
            Vector2 origin = new Vector2(x, originY);

            RaycastHit2D hit = Runner != null
                ? Runner.GetPhysicsScene2D().Raycast(origin, Vector2.down, SafePositionRaycastDistance, layerMask)
                : Physics2D.Raycast(origin, Vector2.down, SafePositionRaycastDistance, layerMask);
            if (hit.collider == null || hit.collider.isTrigger)
            {
                return false;
            }

            groundedY = hit.point.y + halfHeight + SafePositionGroundClearance;
            if (!IsSafeRoomGroundedY(room, fallbackY, groundedY))
            {
                return false;
            }

            return true;
        }

        public static bool IsSafeRoomGroundedY(RoomData room, float fallbackY, float groundedY)
        {
            if (room == null) return true;

            float visibleFloorY = room.cameraY - room.cameraSize * SafePositionMaxGroundDropCameraSizeRatio;
            float fallbackDropFloorY = fallbackY - room.cameraSize * SafePositionMaxGroundDropCameraSizeRatio;
            float minSafeY = Mathf.Max(visibleFloorY, fallbackDropFloorY);
            return groundedY >= minSafeY;
        }

        private static int ResolveGroundLayerMask(Component player)
        {
            var controller = player != null
                ? player.GetComponent<PlayerController>()
                : null;
            int layerMask = controller != null ? controller.GroundLayer.value : 0;
            if (layerMask != 0)
            {
                return layerMask;
            }

            int namedGroundMask = LayerMask.GetMask("Ground");
            return namedGroundMask != 0 ? namedGroundMask : Physics2D.DefaultRaycastLayers;
        }

        private static float ResolvePlayerHalfHeight(Component player)
        {
            Collider2D bodyCollider = null;
            if (player != null)
            {
                bodyCollider = player.GetComponent<Collider2D>();
                if (bodyCollider == null)
                {
                    var controller = player.GetComponent<PlayerController>();
                    bodyCollider = controller != null ? controller.BodyCollider : null;
                }
            }

            if (bodyCollider == null)
            {
                return FallbackPlayerHalfHeight;
            }

            float halfHeight = bodyCollider.bounds.extents.y;
            return halfHeight > 0.01f ? halfHeight : FallbackPlayerHalfHeight;
        }

        public static int ResolveAuthorityDirection(
            PlayerRef authority,
            PlayerRef player1,
            PlayerRef player2,
            PlayerRef fallbackDeadPlayer)
        {
            if (authority != default(PlayerRef))
            {
                if (authority == player1) return 1;
                if (authority == player2) return -1;
            }

            if (fallbackDeadPlayer == player1) return -1;
            if (fallbackDeadPlayer == player2) return 1;
            return 0;
        }

        public bool IsOutOfBounds(Vector2 playerPos)
        {
            if (!IsNetworkedStateReady) return false;

            var room = GetCurrentRoom();
            if (room == null) return false;

            float left = room.leftBound - _outOfBoundsPadding;
            float right = room.rightBound + _outOfBoundsPadding;

            return playerPos.x < left || playerPos.x > right;
        }

        public bool IsOutsideVisibleCamera(Vector2 playerPos)
        {
            if (!IsNetworkedStateReady || IsVictoryRunActive) return false;

            RoomData room = GetCurrentRoom();
            if (room == null) return false;

            float cameraX = ResolveAuthoritativeCameraViewportX(room);
            return IsOutsideCameraViewport(
                playerPos,
                cameraX,
                room.cameraY,
                room.cameraSize,
                CameraController.DesignViewportAspect,
                _cameraOutOfViewDeathPadding);
        }

        public bool IsHorizontallyOutsideVisibleCamera(Vector2 playerPos)
        {
            if (!IsNetworkedStateReady || IsVictoryRunActive) return false;

            RoomData room = GetCurrentRoom();
            if (room == null) return false;

            float cameraX = ResolveAuthoritativeCameraViewportX(room);
            return IsHorizontallyOutsideCameraViewport(
                playerPos,
                cameraX,
                room.cameraSize,
                CameraController.DesignViewportAspect,
                _cameraOutOfViewDeathPadding);
        }

        public bool IsBelowVisibleCamera(Vector2 playerPos)
        {
            if (!IsNetworkedStateReady || IsVictoryRunActive) return false;

            RoomData room = GetCurrentRoom();
            if (room == null) return false;

            return IsBelowCameraViewport(
                playerPos,
                room.cameraY,
                room.cameraSize,
                _cameraOutOfViewDeathPadding);
        }

        private float ResolveAuthoritativeCameraViewportX(RoomData room)
        {
            if (TryGetAuthoritativeMovementCameraX(out float cameraX))
            {
                return cameraX;
            }

            float viewHalfWidth = CalculateDesignViewHalfWidth(room);
            float minCameraX = room.leftBound + viewHalfWidth;
            float maxCameraX = room.rightBound - viewHalfWidth;
            return CameraController.CalculateMovementAuthorityNeutralCameraTargetX(
                room.centerX,
                minCameraX,
                maxCameraX);
        }

        public static bool IsOutsideCameraViewport(
            Vector2 playerPos,
            float cameraX,
            float cameraY,
            float cameraSize,
            float aspect,
            float padding)
        {
            float halfHeight = Mathf.Max(0.01f, cameraSize);
            float safePadding = Mathf.Max(0f, padding);

            return IsHorizontallyOutsideCameraViewport(playerPos, cameraX, cameraSize, aspect, padding) ||
                   IsBelowCameraViewport(playerPos, cameraY, cameraSize, padding) ||
                   playerPos.y > cameraY + halfHeight + safePadding;
        }

        public static bool IsHorizontallyOutsideCameraViewport(
            Vector2 playerPos,
            float cameraX,
            float cameraSize,
            float aspect,
            float padding)
        {
            float halfHeight = Mathf.Max(0.01f, cameraSize);
            float halfWidth = halfHeight * Mathf.Max(0.01f, aspect);
            float safePadding = Mathf.Max(0f, padding);

            return playerPos.x < cameraX - halfWidth - safePadding ||
                   playerPos.x > cameraX + halfWidth + safePadding;
        }

        public static bool IsBelowCameraViewport(
            Vector2 playerPos,
            float cameraY,
            float cameraSize,
            float padding)
        {
            float halfHeight = Mathf.Max(0.01f, cameraSize);
            float safePadding = Mathf.Max(0f, padding);

            return playerPos.y < cameraY - halfHeight - safePadding;
        }

        /// <summary>
        /// 방 advance 이벤트. AdvanceCueUI 등 UI/Effect 측이 구독한다.
        /// 서버 권한 측(HandleRoomAdvance)에서만 invoke되므로, 클라이언트는
        /// Render()의 CurrentRoomIndex ChangeDetector delta로 별도 대응한다.
        /// </summary>
        public event System.Action OnAdvanceTriggered;
        public event Action<PlayerRef, PlayerRef> OnPlayerSlotsChanged;

        /// <summary>
        /// 게임 오버 이벤트. (NetworkRunner, winnerName) 으로 어떤 방에서 발생했는지 식별 가능.
        /// </summary>
        public static event Action<NetworkRunner, string> OnGameOverEvent;

        private void OnGameWin(PlayerRef winner)
        {
            DiagLog.Log($"[Colosseum] GAME OVER! Winner: {winner}");

            string winnerName = winner == Player1 ? "Player 1" : "Player 2";
            if (Runner != null && Object != null && Object.IsValid)
            {
                Rpc_PublishGameOver(winnerName);
                return;
            }

            OnGameOverEvent?.Invoke(Runner, winnerName);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishGameOver(string winnerName)
        {
            OnGameOverEvent?.Invoke(Runner, winnerName);

            // Presentation assembly observes OnGameOverEvent and owns UI display.
            // RoomManager does not depend on UI types, keeping the Fusion-weaved
            // NetworkGameplay assembly boundary smaller.
        }

        public void ResetGame()
        {
            if (!Object.HasStateAuthority) return;

            var cardDeck = SceneSearch.FindInScene<Card.CardDeck>(gameObject.scene);
            if (cardDeck != null)
            {
                cardDeck.ResetPlayerStacks();
            }

            CurrentRoomIndex = _rooms.Count / 2;
            LastKiller = default;
            Player1 = default;
            Player2 = default;
            LastRespawnedPlayer = default;
            RespawnCameraRevision = 0;
            _lastRoomAdvancePlayer = default;
            _lastRoomAdvanceIndex = int.MinValue;
            _pendingRoomAdvanceDefenderRespawnPlayer = default;
            _pendingRoomAdvanceDefenderRespawnIndex = int.MinValue;
            _lastRoomAdvanceTick = int.MinValue;
            IsVictoryRunActive = false;
            VictoryRunner = default;
            ResetMovementCameraStateForCurrentRoom();
            _gameEnded = false;
            DiagLog.Log($"[Colosseum] Game state reset for rematch. StartRoom: {CurrentRoomIndex}");
        }
    }
}
