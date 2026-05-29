using System.Collections.Generic;
using Colosseum.Card;
using Colosseum.Contracts;
using Colosseum.Game;
using Colosseum.Network;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;
using Colosseum.Utils;
using Fusion;
using UnityEngine;

namespace Colosseum.Player
{
    public class PlayerController : NetworkBehaviour
    {
        internal const int PlayerJumpStartedVfxSequenceSalt = 310;
        private const int PlayerLandedVfxSequenceSalt = 311;
        private const int PlayerEmojiVfxSequenceSaltBase = 410;
        private const int RemoteStateAuthorityMissingInputHoldTicks = 3;
        private static readonly List<PlayerController> ActivePlayers = new();

        [Header("Gameplay Profile")]
        [Tooltip("Assets/_Project/Data/GameplayProfile.asset 드래그. 미할당 시 fallback 값 사용 + LogError.")]
        [SerializeField] private GameplayProfile _profile;

        [Header("Movement (Profile 외부화 대상 아님)")]
        [SerializeField] private float _boundaryPadding = 0.25f;

        [Header("Ground Check")]
        [SerializeField] private Transform _groundCheck;
        [SerializeField] private Vector2 _groundCheckSize = new Vector2(0.8f, 0.1f);
        [SerializeField] private LayerMask _groundLayer;

        [Header("Player Contact")]
        [SerializeField] private float _playerGroundSnapDistance = 0.08f;
        [SerializeField] private float _playerGroundPenetrationTolerance = 0.08f;
        [SerializeField] private float _playerGroundHorizontalInset = 0.05f;
        [SerializeField] private float _maxGroundedSupportRiseSpeed = 0.2f;
        [SerializeField] private float _playerOverlapPadding = 0.01f;

        [Header("Wall Climb (A303-292)")]
        [Tooltip("좌측 벽 접촉 판정용 자식 Transform. Player.prefab에 wallCheckLeft 하위 오브젝트 배치 후 슬롯 배선.")]
        [SerializeField] private Transform _wallCheckLeft;
        [Tooltip("우측 벽 접촉 판정용 자식 Transform.")]
        [SerializeField] private Transform _wallCheckRight;
        [Tooltip("벽 접촉 판정 OverlapBox 크기. 너무 크면 천장/바닥과 오작동, 너무 작으면 누락.")]
        [SerializeField] private Vector2 _wallCheckSize = new Vector2(0.15f, 0.6f);
        [Tooltip("벽으로 인정할 LayerMask. 일반적으로 GroundLayer와 동일.")]
        [SerializeField] private LayerMask _wallLayer;

        private Rigidbody2D _rb;
        private Collider2D _bodyCollider;
        private SpriteRenderer _spriteRenderer;
        private PlayerHealth _health;
        private RoomManager _roomManager;
        private CardDeck _cardDeck;
        private CardEffect _cardEffect;
        private Colosseum.Status.PlayerStatusController _statusController;
        private float _currentMoveInputX;
        private NetworkInputData _lastMovementInput;
        private int _lastMovementInputTick = int.MinValue;
        private bool _hasLastMovementInput;

        private PlayerMovement _movement;
        private PlayerGroundProbe _groundProbe;
        private PlayerBoundary _boundary;
        private Transform _visualRoot;
        private Transform _visualOffsetRoot;

        [Networked] private NetworkBool _isGrounded { get; set; }
        [Networked] private NetworkBool _groundVfxInitialized { get; set; }

        /// <summary>PlayerAnimatorController 등 외부에서 착지 여부를 읽기 위한 속성.</summary>
        public bool IsGrounded => _isGrounded;

        [Networked] private sbyte _jumpsUsed { get; set; }
        [Networked] private TickTimer _jumpBufferTimer { get; set; }
        [Networked] private NetworkBool _wasJumpHeldLastTick { get; set; }

        // A303-292: 벽 타기 동기화 상태
        [Networked] private NetworkBool _isWallSliding { get; set; }
        [Networked] private sbyte _wallSide { get; set; } // -1=left, 0=none, +1=right
        [Networked] private TickTimer _wallReattachLockoutTimer { get; set; }
        [Networked] private sbyte _wallReattachLockoutSide { get; set; } // lockout 적용 대상 측면

        // A303-306: 슬라이드 해제·벽 점프 직후 짧은 시간 벽 반대 방향으로 부드럽게 밀어내는 detach velocity 상태
        [Networked] private TickTimer _wallDetachVelocityTimer { get; set; }
        [Networked] private sbyte _wallDetachSide { get; set; } // detach 시작 시점의 벽 측면 (벽 반대 방향으로 추진)

        internal static IReadOnlyList<PlayerController> ActivePlayersInternal => ActivePlayers;

        internal Rigidbody2D Rigidbody => _rb;
        internal Collider2D BodyCollider => _bodyCollider;
        internal SpriteRenderer SpriteRenderer => _spriteRenderer;
        internal RoomManager RoomManager => _roomManager;
        internal CardEffect CardEffect => _cardEffect;

        /// <summary>
        /// 상태이상 컨트롤러. PlayerMovement가 매 tick stat 계산 시 modifier·stun 조회용으로 사용.
        /// 같은 GameObject에 PlayerStatusController가 부착되어 있어야 정상 동작 (없어도 null-safe).
        /// </summary>
        internal Colosseum.Status.PlayerStatusController StatusController => _statusController;
        internal float CurrentMoveInputX => _currentMoveInputX;

        internal Transform GroundCheck => _groundCheck;
        internal Vector2 GroundCheckSize => _groundCheckSize;
        internal LayerMask GroundLayer => _groundLayer;

        internal float PlayerGroundSnapDistance => _playerGroundSnapDistance;
        internal float PlayerGroundPenetrationTolerance => _playerGroundPenetrationTolerance;
        internal float PlayerGroundHorizontalInset => _playerGroundHorizontalInset;
        internal float MaxGroundedSupportRiseSpeed => _maxGroundedSupportRiseSpeed;
        internal float PlayerOverlapPadding => _playerOverlapPadding;
        internal float BoundaryPadding => _boundaryPadding;

        internal sbyte JumpsUsed
        {
            get => _jumpsUsed;
            set => _jumpsUsed = value;
        }

        internal TickTimer JumpBufferTimer
        {
            get => _jumpBufferTimer;
            set => _jumpBufferTimer = value;
        }

        internal bool WasJumpHeldLastTick
        {
            get => _wasJumpHeldLastTick;
            set => _wasJumpHeldLastTick = value;
        }

        // A303-292: 벽 타기 외부 노출
        /// <summary>외부 (Animator, UI, VFX) 가 벽 슬라이드 상태를 읽기 위한 속성.</summary>
        public bool IsWallSliding => _isWallSliding;

        /// <summary>벽 슬라이드 중인 측면. -1=왼쪽 벽, 0=벽 없음, +1=오른쪽 벽.</summary>
        public int WallSide => _wallSide;

        // A303-292: 벽 타기 PlayerMovement 접근용 (internal)
        internal Transform WallCheckLeft => _wallCheckLeft;
        internal Transform WallCheckRight => _wallCheckRight;
        internal Vector2 WallCheckSize => _wallCheckSize;
        internal LayerMask WallLayer => _wallLayer;

        internal void SetWallSlidingState(bool active, sbyte side)
        {
            _isWallSliding = active;
            _wallSide = side;
        }

        internal TickTimer WallReattachLockoutTimer
        {
            get => _wallReattachLockoutTimer;
            set => _wallReattachLockoutTimer = value;
        }

        internal sbyte WallReattachLockoutSide
        {
            get => _wallReattachLockoutSide;
            set => _wallReattachLockoutSide = value;
        }

        // A303-306: detach velocity 상태 PlayerMovement 접근용
        internal TickTimer WallDetachVelocityTimer
        {
            get => _wallDetachVelocityTimer;
            set => _wallDetachVelocityTimer = value;
        }

        internal sbyte WallDetachSide
        {
            get => _wallDetachSide;
            set => _wallDetachSide = value;
        }

        /// <summary>
        /// A303-292: 좌/우 별 벽 접촉 판정. PlayerMovement에서 호출.
        /// PlayerGroundProbe를 직접 노출하지 않고 wallCheck Transform/Size/Layer를 캡슐화.
        /// </summary>
        internal bool IsTouchingWallOnSide(bool right)
        {
            Transform check = right ? _wallCheckRight : _wallCheckLeft;
            if (check == null || _groundProbe == null) return false;
            return _groundProbe.IsTouchingWallOnSide(check, _wallCheckSize, _wallLayer, Runner);
        }

        private void OnEnable()
        {
            if (!ActivePlayers.Contains(this))
            {
                ActivePlayers.Add(this);
            }
        }

        private void OnDisable()
        {
            ActivePlayers.Remove(this);
        }

        private const float LocalCorrectionSmoothTime = 0.03f;
        private const float LocalCorrectionSnapDistance = 0.2f;
        private const float LocalCorrectionMaxVisualOffset = 0.07f;
        private const float LocalCorrectionSettleEpsilon = 0.01f;

        private Vector3 _localCorrectionVelocity;

        public override void Render()
        {
            if (_visualRoot == null || _visualOffsetRoot == null) return;

            if (Object.HasInputAuthority)
            {
                Vector3 correctionError = transform.position - _visualRoot.position;
                if (correctionError.sqrMagnitude <
                    LocalCorrectionSettleEpsilon * LocalCorrectionSettleEpsilon)
                {
                    _visualOffsetRoot.localPosition = Vector3.zero;
                    _localCorrectionVelocity = Vector3.zero;
                }
                else if (correctionError.sqrMagnitude > LocalCorrectionSnapDistance * LocalCorrectionSnapDistance)
                {
                    _visualOffsetRoot.localPosition = Vector3.zero;
                    _localCorrectionVelocity = Vector3.zero;
                }
                else
                {
                    Vector3 targetLocalOffset = Vector3.ClampMagnitude(
                        -correctionError,
                        LocalCorrectionMaxVisualOffset);

                    _visualOffsetRoot.localPosition = Vector3.SmoothDamp(
                        _visualOffsetRoot.localPosition,
                        targetLocalOffset,
                        ref _localCorrectionVelocity,
                        LocalCorrectionSmoothTime
                    );
                }
                return;
            }

            _visualOffsetRoot.localPosition = Vector3.zero;
            _localCorrectionVelocity = Vector3.zero;
        }

        public override void Spawned()
        {
            if (_profile == null)
            {
                Debug.LogError($"[Colosseum] PlayerController on {gameObject.name}: GameplayProfile 미할당. Fallback 값 사용 중. " +
                    "Player.prefab → PlayerController → Profile 슬롯에 GameplayProfile.asset 드래그 필요.");
            }

            _rb = GetComponent<Rigidbody2D>();
            _bodyCollider = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _health = GetComponent<PlayerHealth>();
            _roomManager = SceneSearch.FindInScene<RoomManager>(gameObject.scene);
            _cardDeck = SceneSearch.FindInScene<CardDeck>(gameObject.scene);
            _cardEffect = GetComponent<CardEffect>();
            _statusController = GetComponent<Colosseum.Status.PlayerStatusController>();

            _movement = new PlayerMovement(_profile);
            _groundProbe = new PlayerGroundProbe();
            _boundary = new PlayerBoundary();

            _movement.BindRigidbody(_rb);
            _groundProbe.EnsurePlayerContactMaterial(_bodyCollider);
            _visualRoot = transform.Find("PlayerVisualRoot");
            if (_visualRoot == null)
            {
                Debug.LogWarning("[Colosseum] PlayerVisualRoot not found. Local correction smoothing disabled.");
            }
            else
            {
                _visualOffsetRoot = _visualRoot.Find("PlayerVisualOffsetRoot");
                if (_visualOffsetRoot == null)
                {
                    Debug.LogWarning("[Colosseum] PlayerVisualOffsetRoot not found. Local correction smoothing disabled.");
                }
                else
                {
                    _visualOffsetRoot.localPosition = Vector3.zero;
                }
            }
            _localCorrectionVelocity = Vector3.zero;

            Runner.SetIsSimulated(Object, ShouldSimulatePlayerObject(
                Object.HasInputAuthority,
                Object.HasStateAuthority));

            if (Object.HasInputAuthority)
            {
                _spriteRenderer.color = Color.green;
            }
            else
            {
                _spriteRenderer.color = Color.red;
            }

            if (Object.HasStateAuthority && _roomManager != null)
            {
                _roomManager.TryRegisterPlayerSlot(Object.InputAuthority);
            }

            DiagLog.Log($"[Colosseum/DIAG] PlayerController.Spawned() END: InputAuthority={Object.InputAuthority}, " +
                        $"HasStateAuthority={Object.HasStateAuthority}, HasInputAuthority={Object.HasInputAuthority}, " +
                        $"Rb.isKinematic={_rb.isKinematic}, Rb.gravityScale={_rb.gravityScale}, " +
                        $"pos={transform.position}, roomManager={(_roomManager != null ? "OK" : "NULL")}");
        }

        public override void FixedUpdateNetwork()
        {
            if (_health != null && _health.IsDead)
            {
                return;
            }

            if (_roomManager != null &&
                _roomManager.TryIsVictoryRunLocked(Object.InputAuthority, out bool victoryLocked) &&
                victoryLocked)
            {
                if (_rb != null)
                {
                    _rb.velocity = new Vector2(0f, _rb.velocity.y);
                }

                return;
            }

            if (ShouldSuppressMovementForCardSelection(IsCardSelectionPending()))
            {
                SuppressCombatMovementForCardSelection();
                return;
            }

            if (!TryGetMovementInput(out NetworkInputData input))
            {
                return;
            }

            if (Object.HasStateAuthority)
            {
                PublishEmojiVfxIfRequested(input.EmojiIndex);
            }

            _currentMoveInputX = input.Direction.x;

            _groundProbe.ResolvePlayerOverlap(this, _bodyCollider);

            bool isGroundedNow =
                _groundProbe.IsGroundedOnLevel(_groundCheck, _groundCheckSize, _groundLayer, Runner) ||
                _groundProbe.IsGroundedOnOtherPlayer(
                    this,
                    _bodyCollider,
                    _rb,
                    _playerGroundSnapDistance,
                    _playerGroundPenetrationTolerance,
                    _playerGroundHorizontalInset,
                    _maxGroundedSupportRiseSpeed);

            bool wasGrounded = _isGrounded;
            _isGrounded = isGroundedNow;

            if (Object.HasStateAuthority)
            {
                if (_groundVfxInitialized && !wasGrounded && isGroundedNow)
                {
                    PublishMovementVfx(
                        VfxEventType.PlayerLanded,
                        VfxPayloadFactory.GetPlayerMovementEffectKey(VfxEventType.PlayerLanded),
                        VfxAttachPoint.CharacterFeet,
                        Vector2.down,
                        0.65f,
                        _rb != null ? Mathf.Abs(_rb.velocity.y) : 1f,
                        PlayerLandedVfxSequenceSalt);
                }

                _groundVfxInitialized = true;
            }

            _movement.ApplyMovement(this, input, isGroundedNow);

            _wasJumpHeldLastTick = input.IsJumpPressed;

            if (input.Direction.x != 0f)
            {
                _spriteRenderer.flipX = input.Direction.x < 0f;
            }

            _boundary.CheckRoomBoundary(this, input.Direction.x);
        }

        public static bool ShouldSuppressMovementForCardSelection(bool isCardSelectionPending)
        {
            return isCardSelectionPending;
        }

        public static bool ShouldSuppressMovementForRespawnRecovery(bool hasMovementAuthority, bool isOpponentDead)
        {
            // Respawn recovery must not behave like card-selection pause. The dead player is already
            // stopped by IsDead; the living player keeps movement authority during the respawn window.
            return false;
        }

        public static bool ShouldSimulatePlayerObject(bool hasInputAuthority, bool hasStateAuthority)
        {
            return hasInputAuthority || hasStateAuthority;
        }

        public static bool ShouldUseRemoteMissingMovementInputHold(
            bool hasInputAuthority,
            bool hasStateAuthority,
            bool hasLastMovementInput)
        {
            return hasStateAuthority && !hasInputAuthority && hasLastMovementInput;
        }

        public static bool ShouldHoldRemoteMovementInput(int missingInputAgeTicks, int maxHoldTicks)
        {
            return missingInputAgeTicks > 0 && missingInputAgeTicks <= maxHoldTicks;
        }

        private bool TryGetMovementInput(out NetworkInputData input)
        {
            if (GetInput(out input))
            {
                CacheRemoteStateAuthorityMovementInput(input);
                return true;
            }

            return TryGetHeldRemoteMovementInput(out input);
        }

        private void CacheRemoteStateAuthorityMovementInput(in NetworkInputData input)
        {
            if (Object == null || Runner == null)
            {
                return;
            }

            if (!Object.HasStateAuthority || Object.HasInputAuthority)
            {
                return;
            }

            _lastMovementInput = input;
            _lastMovementInputTick = Runner.Tick.Raw;
            _hasLastMovementInput = true;
        }

        private bool TryGetHeldRemoteMovementInput(out NetworkInputData input)
        {
            input = default;
            if (Object == null || Runner == null)
            {
                return false;
            }

            if (!ShouldUseRemoteMissingMovementInputHold(
                    Object.HasInputAuthority,
                    Object.HasStateAuthority,
                    _hasLastMovementInput))
            {
                return false;
            }

            int missingInputAgeTicks = Runner.Tick.Raw - _lastMovementInputTick;
            if (!ShouldHoldRemoteMovementInput(
                    missingInputAgeTicks,
                    RemoteStateAuthorityMissingInputHoldTicks))
            {
                return false;
            }

            input = _lastMovementInput;
            input.IsJumpPressed = false;
            input.IsFirePressed = false;
            input.EmojiIndex = 0;
            return true;
        }

        private bool IsCardSelectionPending()
        {
            if (_cardDeck == null)
            {
                _cardDeck = SceneSearch.FindInScene<CardDeck>(gameObject.scene);
            }

            try
            {
                return _cardDeck != null &&
                       _cardDeck.Object != null &&
                       _cardDeck.Object.IsValid &&
                       _cardDeck.IsDrawing;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.FieldAccessException)
            {
                return false;
            }
        }

        private void SuppressCombatMovementForCardSelection()
        {
            _currentMoveInputX = 0f;
            _wasJumpHeldLastTick = false;
            _isWallSliding = false;
            _wallSide = 0;

            if (_rb != null)
            {
                _rb.velocity = Vector2.zero;
            }
        }

        internal void PublishMovementVfx(
            VfxEventType eventType,
            string effectKey,
            VfxAttachPoint attachPoint,
            Vector2 direction,
            float radius,
            float magnitude,
            int sequenceSalt)
        {
            if (Object == null || !Object.HasStateAuthority)
            {
                return;
            }

            int sourceTick = VfxPayloadFactory.GetSourceTick(Runner);
            int sourceEventSequence = VfxPayloadFactory.BuildSourceEventSequence(sourceTick, sequenceSalt);
            Vector2 position = transform.position;
            Rpc_PublishMovementVfx(
                (int)eventType,
                effectKey,
                position.x,
                position.y,
                direction.x,
                direction.y,
                radius,
                magnitude,
                (int)attachPoint,
                sourceTick,
                sourceEventSequence);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void Rpc_PublishMovementVfx(
            int eventType,
            string effectKey,
            float x,
            float y,
            float directionX,
            float directionY,
            float radius,
            float magnitude,
            int attachPoint,
            int sourceTick,
            int sourceEventSequence)
        {
            VfxEventBus.Publish(VfxPayloadFactory.CreateWithIdentity(
                (VfxEventType)eventType,
                effectKey,
                effectKey,
                Object,
                Object.InputAuthority,
                Object.InputAuthority,
                new Vector2(x, y),
                new Vector2(directionX, directionY),
                radius,
                0.45f,
                magnitude,
                (VfxAttachPoint)attachPoint,
                sourceTick,
                sourceEventSequence));
        }

        private void PublishEmojiVfxIfRequested(byte emojiIndex)
        {
            if (emojiIndex < 1 || emojiIndex > 6)
            {
                return;
            }

            string effectKey = VfxPayloadFactory.GetEmojiEffectKey(emojiIndex);
            if (string.IsNullOrEmpty(effectKey))
            {
                return;
            }

            PublishMovementVfx(
                VfxEventType.EmojiRequested,
                effectKey,
                VfxAttachPoint.CharacterHead,
                Vector2.up,
                1f,
                emojiIndex,
                PlayerEmojiVfxSequenceSaltBase + emojiIndex);
        }

        // 방 상태(CurrentRoomIndex/IsVictoryRunActive/VictoryRunner)는
        // RoomManager의 [Networked] 프로퍼티로 Fusion이 tick마다 자동 전파한다.
        // CameraController의 _lastRoomIndex 비교가 값 변경을 감지해 OnRoomChanged()를 호출.
    }
}
