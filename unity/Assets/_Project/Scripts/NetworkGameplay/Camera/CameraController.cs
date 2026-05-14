using UnityEngine;
using Fusion;
using Colosseum.Player;
using Colosseum.Utils;

namespace Colosseum.CameraSystem
{
    public class CameraController : NetworkBehaviour
    {
        [Header("References")]
        [SerializeField] private Game.RoomManager _roomManager;

        [Header("Follow Settings")]
        [SerializeField] private float _followSpeed = 5f;

        [Header("Smooth Transition (A303-324)")]
        [Tooltip("SmoothDamp smoothTime — 카메라 위치/줌 전환 부드러움. Unity 공식 camera follow 예제 0.3f.")]
        [SerializeField] private float _transitionDuration = 0.3f;

        // SmoothDamp ref velocity. class-level 보존 의무 — local 변수면 매 호출 리셋되어 SmoothDamp 무력화.
        private Vector3 _cameraVelocity;
        private float _sizeVelocity;

        // P1-3 (A212): hot-seat 2P 양플레이어 중간점 추적용.
        // OnPlayerSlotsChanged 이벤트로 런타임 spawn 시 자동 등록 (이벤트 기반, alloc 0).
        // null 허용 (비활성 시 winner fallback).
        [Header("Hot-Seat 2P (P1-3)")]
        [Tooltip("hot-seat 2P 양플레이어 중간점 추적 활성 플래그. OnPlayerSlotsChanged로 자동 wire.")]
        [SerializeField] private bool _hotSeatActive = false;
        [Tooltip("hot-seat 2P 활성 시 중간점 계산 대상 Transform. 비활성/null 시 _winnerTransform fallback.")]
        [SerializeField] private Transform[] _playerTransforms;

        [Tooltip("디버그/시연용: IsVictoryRunActive=false + winnerTransform=null 시 씬의 첫 PlayerController follow. MVPArena 1인 단독 시연 한정. SampleScene은 default false 유지(회귀 0). P1-6에서 PlayerSpawnPoint adapter 도입 후 deprecate 예정. post-demo closure 의무 (D74).")]
        [SerializeField] private bool _fallbackFollowFirstPlayer = false;

        // _fallbackFollowFirstPlayer hot path 가드 (50/04 §FindObjectOfType 의존 제한):
        // SceneSearch는 매 tick alloc → 1회만 시도하고 sentinel로 잠금.
        private bool _fallbackSearchAttempted = false;

        private UnityEngine.Camera _camera;
        private bool _cameraInitialized = false;
        private bool _hasNetworkObject = false;

        // 카메라 목표 위치 (NetworkObject 있으면 [Networked]로 동기화, 없으면 로컬)
        private Vector2 _targetPosition;
        private float _targetSize;

        private int _lastRoomIndex = -1;
        private Transform _winnerTransform;

        public override void Spawned()
        {
            _hasNetworkObject = true;
            InitializeCamera();
        }

        /// <summary>
        /// Dedicated Server 모드: NetworkObject 없이 씬에 배치된 경우
        /// Spawned()가 호출되지 않으므로 Start()에서 초기화.
        /// </summary>
        private void Start()
        {
            if (!_cameraInitialized)
            {
                InitializeCamera();
            }
            else
            {
                // 이미 초기화됐더라도 Start 시점에 RoomManager가 늦게 스폰될 수 있음
                EnsureRoomManager();
            }
        }

        private void OnDestroy()
        {
            if (_roomManager != null)
                _roomManager.OnPlayerSlotsChanged -= OnPlayerSlotsWired;
        }

        private void InitializeCamera()
        {
            _camera = UnityEngine.Camera.main;
            if (_camera == null)
            {
                // 서버(headless)에서는 카메라가 없을 수 있음
                Debug.LogWarning("[Colosseum] Main Camera not found (expected on headless server)");
                return;
            }
            _cameraInitialized = true;

            EnsureRoomManager();
            EnsureBulletWalls();

            // 초기 카메라 위치를 현재 방에 맞춤.
            // Start()가 RoomManager.Spawned()보다 먼저 실행되면 [Networked] CurrentRoomIndex
            // 접근 시 Fusion이 InvalidOperationException을 던진다. Object.IsValid 로 가드.
            // RoomManager가 아직 준비 안 됐으면 초기 위치 세팅은 스킵 — 나중에
            // RoomManager.Render()의 ChangeDetector가 OnRoomChanged를 호출해 맞춘다.
            if (IsRoomManagerReady())
            {
                var room = _roomManager.GetCurrentRoom();
                if (room != null)
                {
                    Vector3 pos = _camera.transform.position;
                    pos.x = room.centerX;
                    pos.y = room.cameraY;
                    _camera.transform.position = pos;
                    _camera.orthographicSize = room.cameraSize;
                    _targetPosition = new Vector2(room.centerX, room.cameraY);
                    _targetSize = room.cameraSize;
                    _lastRoomIndex = _roomManager.CurrentRoomIndex;
                }
            }

            Debug.Log("[Colosseum] CameraController initialized");
        }

        // ──────────────────────────────────────────────
        //  Fusion 콜백 (NetworkObject가 있을 때만 호출됨)
        // ──────────────────────────────────────────────

        public override void FixedUpdateNetwork()
        {
            // 카메라는 각 클라이언트가 "동기화된 입력"(VictoryRunner, CurrentRoomIndex, [Networked] 위치)으로
            // 결정적으로 계산하므로 State Authority 체크 없이 모든 클라이언트에서 실행해야 한다.
            // (Dedicated Server는 ApplyCameraPosition 내부에서 _camera == null 가드로 무시됨)
            UpdateTargetPosition();
        }

        public override void Render()
        {
            // Late joiner 보완: P2에서 RoomManager.Spawned()가 _lastRenderedPlayer1/2를
            // snapshot 값으로 초기화 → ChangeDetector가 fire 안 함 → OnPlayerSlotsWired 미호출.
            // _hotSeatActive가 false이고 양 슬롯이 채워져 있으면 재시도 (성공 시 자동 중단).
            if (!_hotSeatActive && _roomManager != null && IsRoomManagerReady())
            {
                var p1 = _roomManager.Player1;
                var p2 = _roomManager.Player2;
                if (p1 != default && p2 != default)
                    OnPlayerSlotsWired(p1, p2);
            }

            UpdateTargetPosition();
            ApplyCameraPosition();
        }

        // ──────────────────────────────────────────────
        //  MonoBehaviour 콜백 (NetworkObject 없을 때 대체)
        // ──────────────────────────────────────────────

        private void LateUpdate()
        {
            // NetworkObject가 있으면 Fusion의 FixedUpdateNetwork/Render가 처리
            if (_hasNetworkObject) return;
            if (!_cameraInitialized) return;

            UpdateTargetPosition();
            ApplyCameraPosition();
        }

        // ──────────────────────────────────────────────
        //  카메라 로직
        // ──────────────────────────────────────────────

        private void UpdateTargetPosition()
        {
            // 씬 복제/씬 전환 시 [SerializeField] _roomManager 참조가 끊어져
            // room change 감지가 멈추는 경우를 방지. null/파괴 상태이면 재획득.
            if (_roomManager == null)
            {
                EnsureRoomManager();
                if (_roomManager == null) return;
            }

            // RoomManager가 아직 Spawned되지 않았으면 [Networked] 프로퍼티 접근 시 예외.
            // Spawned 완료 후에는 RoomManager.Render()의 ChangeDetector가 OnRoomChanged를
            // 명시 호출하므로, 이 폴링은 보조 역할로만 동작.
            if (!IsRoomManagerReady()) return;

            if (_roomManager.CurrentRoomIndex != _lastRoomIndex)
            {
                OnRoomChanged();
            }

            var room = _roomManager.GetCurrentRoom();
            if (room == null) return;
            UpdateWinnerTarget();

            bool isWide = room.cameraSlideMode == Game.CameraSlideMode.Wide;
            if (isWide)
            {
                // 넓은 방: hot-seat 2P 활성 시 양플레이어 중간점, 비활성 시 winner 추적
                Vector2? trackingPoint = ResolveTrackingPoint();
                if (trackingPoint.HasValue)
                {
                    float aspect = _camera != null ? _camera.aspect : 16f / 9f;
                    float orthoSize = _camera != null ? _camera.orthographicSize : room.cameraSize;

                    float clampedX = Mathf.Clamp(
                        trackingPoint.Value.x,
                        room.leftBound + orthoSize * aspect,
                        room.rightBound - orthoSize * aspect);

                    _targetPosition = new Vector2(clampedX, room.cameraY);
                }
                else
                {
                    // 추적 대상 없음 — 방 중앙 fallback
                    _targetPosition = new Vector2(room.centerX, room.cameraY);
                }
            }
            else
            {
                // 좁은 방: 카메라 고정
                _targetPosition = new Vector2(room.centerX, room.cameraY);
            }

            _targetSize = room.cameraSize;
        }

        /// <summary>
        /// P1-3 (A212): Wide 방에서 카메라 추적 X좌표 계산.
        /// hot-seat 2P 활성 + 활성 플레이어 transform 2개 이상 → 중간점 (centroid).
        /// VictoryRun 중에는 centroid 대신 _winnerTransform follow (기존 동작 보존).
        /// 비활성 또는 wire 미완 → 기존 _winnerTransform fallback (회귀 0건).
        /// hot path (FixedUpdateNetwork/Render/LateUpdate)에서 호출되므로 alloc/FindObjectOfType 금지.
        /// </summary>
        private Vector2? ResolveTrackingPoint()
        {
            bool isVictoryRun = _roomManager != null && _roomManager.IsVictoryRunActive;
            if (_hotSeatActive && !isVictoryRun && _playerTransforms != null && _playerTransforms.Length > 0)
            {
                float sumX = 0f;
                float sumY = 0f;
                int count = 0;
                for (int i = 0; i < _playerTransforms.Length; i++)
                {
                    var t = _playerTransforms[i];
                    if (t == null) continue;
                    sumX += t.position.x;
                    sumY += t.position.y;
                    count++;
                }
                if (count > 0)
                {
                    return new Vector2(sumX / count, sumY / count);
                }
            }

            if (_winnerTransform != null)
            {
                return new Vector2(_winnerTransform.position.x, _winnerTransform.position.y);
            }

            return null;
        }

        private void ApplyCameraPosition()
        {
            if (_camera == null) return;
            if (_roomManager == null) return;
            // RoomManager.Spawned() 이전에 [Networked] 프로퍼티 접근 시 예외.
            // UpdateTargetPosition은 가드되어 있지만 ApplyCameraPosition에는 누락되어 있어
            // DevMode 진입처럼 Spawned 타이밍이 미세하게 늦은 경우 LateUpdate에서 스팸 발생.
            if (!IsRoomManagerReady()) return;

            var room = _roomManager.GetCurrentRoom();
            if (room == null) return;

            if (room.cameraSlideMode == Game.CameraSlideMode.Wide)
            {
                // 넓은 방: Vector3.SmoothDamp로 frame-rate independent 부드러운 추적 (A303-324)
                Vector3 currentPos = _camera.transform.position;
                Vector3 targetPos = new Vector3(_targetPosition.x, _targetPosition.y, currentPos.z);
                _camera.transform.position = Vector3.SmoothDamp(currentPos, targetPos, ref _cameraVelocity, _transitionDuration);
            }
            else
            {
                // 좁은 방: 즉시 전환 (컷). 다음 Wide 진입 시 안정화 위해 velocity 리셋 (A303-324 HIGH 3).
                Vector3 pos = _camera.transform.position;
                pos.x = _targetPosition.x;
                pos.y = _targetPosition.y;
                _camera.transform.position = pos;
                _cameraVelocity = Vector3.zero;
                _sizeVelocity = 0f;
            }

            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize, _targetSize, ref _sizeVelocity, _transitionDuration);
        }

        private void UpdateWinnerTarget()
        {
            if (_roomManager == null || !_roomManager.IsVictoryRunActive)
            {
                // _fallbackFollowFirstPlayer (디버그/시연 한정): IsVictoryRunActive=false 시 첫 PlayerController follow.
                // SampleScene 등 default false에서는 기존 동작(stationary at room.centerX) 유지 → 회귀 0.
                if (_fallbackFollowFirstPlayer)
                {
                    // 기존 _winnerTransform이 살아있고 PlayerController scene 안에 있다면 유지 (재할당 불필요).
                    if (_winnerTransform != null) return;

                    // 50/04 §FindObjectOfType 의존 제한: SceneSearch hot path 회피 — 1회만 시도하고 lock.
                    // PlayerController spawn 직후 1회 hit하면 그 후 _winnerTransform 캐시로 매 tick alloc 0.
                    if (_fallbackSearchAttempted) return;

                    foreach (var player in SceneSearch.FindAllInScene<PlayerController>(gameObject.scene))
                    {
                        if (player != null)
                        {
                            _winnerTransform = player.transform;
                            _fallbackSearchAttempted = true;
                            return;
                        }
                    }
                    _fallbackSearchAttempted = true;
                    return;
                }

                _winnerTransform = null;
                return;
            }

            if (_winnerTransform != null)
            {
                var winnerNetworkObject = _winnerTransform.GetComponent<NetworkObject>();
                if (winnerNetworkObject != null && winnerNetworkObject.InputAuthority == _roomManager.VictoryRunner)
                {
                    return;
                }
            }

            foreach (var player in SceneSearch.FindAllInScene<PlayerController>(gameObject.scene))
            {
                if (player.Object != null && player.Object.InputAuthority == _roomManager.VictoryRunner)
                {
                    _winnerTransform = player.transform;
                    return;
                }
            }

            _winnerTransform = null;
        }

        /// <summary>
        /// 승자의 Transform을 세팅 (넓은 방에서 카메라 추적용)
        /// </summary>
        public void SetWinnerTarget(Transform winner)
        {
            _winnerTransform = winner;
        }

        /// <summary>
        /// 방 전환 시 호출 — 타겟만 업데이트, ApplyCameraPosition의 SmoothDamp가 자연 보간 (A303-324)
        /// </summary>
        public void OnRoomChanged()
        {
            var room = _roomManager?.GetCurrentRoom();
            if (room == null || _camera == null) return;

            // 타겟만 업데이트 — SmoothDamp가 ApplyCameraPosition에서 자연 보간 (REVISE 2: direct write 제거).
            _targetPosition = new Vector2(room.centerX, room.cameraY);
            _targetSize = room.cameraSize;
            _lastRoomIndex = _roomManager.CurrentRoomIndex;
        }

        /// <summary>
        /// [SerializeField] _roomManager가 null이거나 파괴된 경우 씬에서 재획득.
        /// 씬 복제/NetworkObject 재스폰 과정에서 serialize 레퍼런스가 끊어질 수 있고,
        /// 이 참조가 끊어지면 room change 감지가 완전히 정지하므로 방어적으로 처리.
        /// </summary>
        private void EnsureRoomManager()
        {
            if (_roomManager != null) return;

            _roomManager = SceneSearch.FindInScene<Game.RoomManager>(gameObject.scene);
            if (_roomManager != null)
            {
                Debug.Log("[Colosseum] CameraController re-acquired RoomManager via SceneSearch");
                // 재획득 시점에 _lastRoomIndex를 맞추지 않으면 다음 프레임에 OnRoomChanged가
                // 트리거되어 카메라가 한 번 점프한다. 최초 매칭이면 의도한 동작이지만,
                // 중간에 재획득됐을 때는 현재 방으로 스냅시키는 게 안전.
                _lastRoomIndex = -1;

                _roomManager.OnPlayerSlotsChanged -= OnPlayerSlotsWired;
                _roomManager.OnPlayerSlotsChanged += OnPlayerSlotsWired;
                if (IsRoomManagerReady())
                    OnPlayerSlotsWired(_roomManager.Player1, _roomManager.Player2);
            }
            else
            {
                Debug.LogWarning("[Colosseum] CameraController could not find RoomManager in scene");
            }
        }

        private void OnPlayerSlotsWired(PlayerRef p1, PlayerRef p2)
        {
            var players = SceneSearch.FindAllInScene<PlayerController>(gameObject.scene);
            Transform t1 = null, t2 = null;

            foreach (var player in players)
            {
                if (player.Object == null || !player.Object.IsValid) continue;

                if (p1 != default && player.Object.InputAuthority == p1)
                    t1 = player.transform;
                else if (p2 != default && player.Object.InputAuthority == p2)
                    t2 = player.transform;
            }

            if (t1 != null && t2 != null)
            {
                _playerTransforms = new Transform[] { t1, t2 };
                _hotSeatActive = true;
            }
            else
            {
                _playerTransforms = System.Array.Empty<Transform>();
                _hotSeatActive = false;
            }
        }

        /// <summary>
        /// A303-263: Main Camera GameObject에 CameraEdgeBulletWalls 컴포넌트 자동 부착.
        /// 카메라 ortho 영역 4변에 invisible Trigger collider를 자식으로 두어 총알이 화면 밖으로 못 나가게 막는다.
        /// 이미 부착되어 있으면 no-op (idempotent).
        /// SampleScene 변경 0 — 런타임 동적 부착이라 SCENE-LOCK 무관.
        /// </summary>
        private void EnsureBulletWalls()
        {
            if (_camera == null) return;
            if (_camera.GetComponent<CameraEdgeBulletWalls>() != null) return;

            _camera.gameObject.AddComponent<CameraEdgeBulletWalls>();
            Debug.Log("[Colosseum] CameraEdgeBulletWalls 자동 부착됨 (총알 카메라 경계 벽 활성화)");
        }

        /// <summary>
        /// RoomManager의 NetworkObject가 Spawned() 완료 후 상태인지 확인.
        /// 이 체크 없이 [Networked] CurrentRoomIndex에 접근하면 Fusion이
        /// "Networked properties can only be accessed when Spawned() has been called" 예외를 던진다.
        /// </summary>
        private bool IsRoomManagerReady()
        {
            if (_roomManager == null) return false;
            var obj = _roomManager.Object;
            return obj != null && obj.IsValid;
        }
    }
}
