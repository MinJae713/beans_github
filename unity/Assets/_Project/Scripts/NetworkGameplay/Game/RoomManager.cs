using System;
using UnityEngine;
using Fusion;
using System.Collections.Generic;
using Colosseum.Contracts;
using Colosseum.Player;
using Colosseum.Utils;
using Colosseum.CameraSystem;
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

        // 게임 종료 후 final room에서 승자가 벽 밖으로 계속 밀면 TryAdvanceRoom이
        // tick마다 호출되어 OnGameWin이 수십 번 발동, GAME OVER 로그/UI 이벤트가 스팸된다.
        // 서버에서만 쓰는 1회성 플래그이므로 [Networked] 아님.
        private bool _gameEnded;
        private PlayerRef _lastRenderedPlayer1;
        private PlayerRef _lastRenderedPlayer2;
        private const int SegmentAdvanceVfxSequenceSaltBase = 360;
        private const float UseVfxProfileFeedbackHint = 0f;

        // [Networked] CurrentRoomIndex 변경을 tick 폴링이 아닌 명시적 delta로 감지한다.
        // FixedUpdateNetwork 내부에서 CameraController가 폴링하면 타이밍/참조 이슈로
        // 변경을 놓치는 경우가 있어, 서버→클라 동기화된 값 변동을 ChangeDetector로
        // 확정 후 CameraController에 직접 푸시한다 (이전 RPC 우회 방식의 안정성 복원).
        private ChangeDetector _changeDetector;

        /// <summary>
        /// Fusion이 NetworkObject 스폰 완료 시 호출. RoomManager는 씬에 NetworkObject로
        /// 직렬화되어 있으므로 서버/클라 양쪽에서 반드시 이 콜백이 호출된다.
        /// </summary>
        public override void Spawned()
        {
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

            Debug.Log($"[Colosseum/DIAG] RoomManager.Spawned(): HasStateAuthority={Object.HasStateAuthority}, " +
                      $"Rooms={_rooms.Count}, CurrentRoomIndex={CurrentRoomIndex}, " +
                      $"Room4 bounds=[{(_rooms.Count>4?_rooms[4].leftBound:0)},{(_rooms.Count>4?_rooms[4].rightBound:0)}]");

            if (Object.HasStateAuthority)
            {
                IsVictoryRunActive = false;
                VictoryRunner = default;
                CurrentRoomIndex = _rooms.Count / 2;
            }
        }

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
                    Debug.Log($"[Colosseum] RoomManager.CurrentRoomIndex changed → {CurrentRoomIndex}. Notifying cameras.");
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
            if (_rooms.Count == 0) return null;
            return _rooms[Mathf.Clamp(CurrentRoomIndex, 0, _rooms.Count - 1)];
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

        public bool TryAdvanceRoom(PlayerRef player, int direction)
        {
            if (!Object.HasStateAuthority)
            {
                Debug.Log("[Colosseum] TryAdvance BLOCKED: no state authority");
                return false;
            }

            // 게임이 이미 끝났으면 추가 TryAdvance 시도는 모두 무시.
            // 승자가 final room에서 계속 벽 밖으로 밀면 FixedUpdateNetwork마다 호출되어
            // OnGameWin이 수십 번 발동되는 버그 방지.
            if (_gameEnded) return false;

            bool hasLastKiller = LastKiller != default(PlayerRef);
            if (hasLastKiller && player != LastKiller)
            {
                Debug.Log($"[Colosseum] TryAdvance BLOCKED: player {player} != LastKiller {LastKiller}");
                return false;
            }

            int targetRoom = CurrentRoomIndex + direction;
            Debug.Log($"[Colosseum/DIAG] TryAdvanceRoom: player={player}, direction={direction}, from={CurrentRoomIndex} to={targetRoom}, RoomCount={_rooms.Count}");
            if (targetRoom < 0 || targetRoom >= _rooms.Count)
            {
                if (_debugAllowBothDirections)
                {
                    // 1인 시연 hack: 인덱스 wrap-around. _gameEnded set 안 함, OnGameWin 안 부름.
                    // SampleScene default false → 본 분기 진입 0 → 회귀 0.
                    // post-demo closure 의무 (D74).
                    targetRoom = (targetRoom + _rooms.Count) % _rooms.Count;
                    Debug.Log($"[Colosseum/DBG] DebugBothDir wrap: targetRoom={targetRoom}");
                }
                else
                {
                    _gameEnded = true;
                    OnGameWin(player);
                    return false;
                }
            }

            CurrentRoomIndex = targetRoom;
            if (IsFinalRoomIndex(targetRoom))
            {
                IsVictoryRunActive = true;
                VictoryRunner = player;
                Debug.Log($"[Colosseum] Victory run started in room {CurrentRoomIndex} by {player}");
            }

            Debug.Log($"[Colosseum] Room advanced to {CurrentRoomIndex} by {player}");
            return true;
        }

        public void HandleRoomAdvance(PlayerRef advancingPlayer)
        {
            if (!Object.HasStateAuthority) return;

            LastKiller = advancingPlayer;

            bool enteredFinalRoom = IsVictoryRunActive && VictoryRunner == advancingPlayer;
            var cardDeck = SceneSearch.FindInScene<Card.CardDeck>(gameObject.scene);
            var allHealth = SceneSearch.FindAllInScene<PlayerHealth>(gameObject.scene);
            Vector2 advancePosition = GetSegmentAdvanceVfxPosition(advancingPlayer, allHealth);
            PublishSegmentAdvanceVfx(advancingPlayer, advancePosition);
            bool foundOpponent = false;

            foreach (var health in allHealth)
            {
                var netObj = health.GetComponent<NetworkObject>();
                if (netObj == null || netObj.InputAuthority == advancingPlayer) continue;

                foundOpponent = true;

                if (health.IsDead)
                {
                    Debug.Log($"[Colosseum] Room advance: opponent {netObj.InputAuthority} already dead");
                    continue;
                }

                Debug.Log($"[Colosseum] Room advance: killing opponent {netObj.InputAuthority}");
                health.ForceDeath(advancingPlayer);
            }

            if (!foundOpponent)
            {
                Debug.LogWarning($"[Colosseum] Room advance: no opponent NetworkObject found! " +
                    $"advancingPlayer={advancingPlayer}, Player1={Player1}, Player2={Player2}");
            }

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

        public void RegisterKill(PlayerRef killer)
        {
            if (!Object.HasStateAuthority) return;

            if (killer == default(PlayerRef))
            {
                Debug.Log("[Colosseum] RegisterKill skipped: killer is None (suicide/out-of-bounds)");
                return;
            }

            LastKiller = killer;
            Debug.Log($"[Colosseum] Last killer set to: {killer}");
        }

        public Vector2 GetRespawnPosition(PlayerRef deadPlayer, Vector2 opponentPosition)
        {
            var room = GetCurrentRoom();
            if (room == null) return Vector2.zero;

            bool isPlayer1 = deadPlayer == Player1;
            float respawnX;

            if (isPlayer1)
            {
                respawnX = opponentPosition.x - _maxRespawnDistance;
                respawnX = Mathf.Max(respawnX, room.leftBound + 1f);
            }
            else
            {
                respawnX = opponentPosition.x + _maxRespawnDistance;
                respawnX = Mathf.Min(respawnX, room.rightBound - 1f);
            }

            return new Vector2(respawnX, room.cameraY + 2f);
        }

        public bool IsOutOfBounds(Vector2 playerPos)
        {
            var room = GetCurrentRoom();
            if (room == null) return false;

            float left = room.leftBound - _outOfBoundsPadding;
            float right = room.rightBound + _outOfBoundsPadding;

            return playerPos.x < left || playerPos.x > right;
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
            Debug.Log($"[Colosseum] GAME OVER! Winner: {winner}");

            string winnerName = winner == Player1 ? "Player 1" : "Player 2";
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
            IsVictoryRunActive = false;
            VictoryRunner = default;
            _gameEnded = false;
            Debug.Log($"[Colosseum] Game state reset for rematch. StartRoom: {CurrentRoomIndex}");
        }
    }
}
