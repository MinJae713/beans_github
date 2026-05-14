using Colosseum.Network;
using UnityEngine;

namespace Colosseum.Bot
{
    /// <summary>
    /// DevMode 더미 봇 FSM.
    /// PlayerController의 FixedUpdateNetwork에서 GetInput() 대신
    /// BotInputBridge.GetBotInput()으로 교체하여 사용한다.
    ///
    /// 상태 전이:
    ///   Move(2-4s) → Aim → Fire(3-5발) → Reload(고정 1s) → Move
    ///   Move 중 IsGrounded==false 이면 Jump 입력 추가
    /// </summary>
    public class DummyBotFSM : MonoBehaviour
    {
        // ── FSM 상태 ──────────────────────────────────────────────
        public enum BotState { Idle, Move, Aim, Fire, Reload }

        // ── Inspector ─────────────────────────────────────────────
        [Header("Target")]
        [SerializeField] private Transform _aimTarget;

        [Header("Move Timing")]
        [SerializeField] private float _moveMinDuration = 2f;
        [SerializeField] private float _moveMaxDuration = 4f;

        [Header("Fire")]
        [SerializeField] private int _burstMinShots = 3;
        [SerializeField] private int _burstMaxShots = 5;
        [SerializeField] private float _fireInterval   = 0.15f;  // 발사 간격(초)

        [Header("Reload")]
        [SerializeField] private float _reloadDuration = 1f;

        [Header("Aim")]
        [SerializeField] private float _aimDuration = 0.3f;     // Fire 전 조준 준비 시간

        // ── 내부 상태 ─────────────────────────────────────────────
        private BotState  _currentState   = BotState.Idle;
        private float     _stateTimer     = 0f;
        private Vector2   _targetDirection = Vector2.right;
        private Vector2   _aimDirection   = Vector2.right;

        private int   _burstShotsRemaining = 0;
        private float _fireTimer           = 0f;
        private bool  _fireThisTick        = false;

        // ── 공개 인터페이스 ────────────────────────────────────────

        /// <summary>현재 FSM 상태 (디버그·외부 감시용)</summary>
        public BotState CurrentState => _currentState;

        /// <summary>
        /// 매 네트워크 틱(FixedUpdateNetwork 대응)에서 호출.
        /// Photon Fusion 입력 구조체를 생성해 반환한다.
        /// </summary>
        public NetworkInputData GetNextInput()
        {
            _stateTimer -= Time.fixedDeltaTime;
            _fireTimer  -= Time.fixedDeltaTime;
            _fireThisTick = false;

            UpdateFSM();
            return BuildInput();
        }

        // ── FSM 업데이트 ───────────────────────────────────────────

        private void UpdateFSM()
        {
            switch (_currentState)
            {
                case BotState.Idle:
                    EnterMove();
                    break;

                case BotState.Move:
                    if (_stateTimer <= 0f)
                        EnterAim();
                    break;

                case BotState.Aim:
                    UpdateAimDirection();
                    if (_stateTimer <= 0f)
                        EnterFire();
                    break;

                case BotState.Fire:
                    if (_fireTimer <= 0f && _burstShotsRemaining > 0)
                    {
                        _fireThisTick = true;
                        _burstShotsRemaining--;
                        _fireTimer = _fireInterval;
                    }
                    if (_burstShotsRemaining <= 0)
                        EnterReload();
                    break;

                case BotState.Reload:
                    if (_stateTimer <= 0f)
                        EnterMove();
                    break;
            }
        }

        // ── 상태 진입 ─────────────────────────────────────────────

        private void EnterMove()
        {
            _currentState    = BotState.Move;
            _stateTimer      = Random.Range(_moveMinDuration, _moveMaxDuration);
            // 매번 랜덤 방향(-1 또는 +1)
            _targetDirection = Random.value > 0.5f ? Vector2.right : Vector2.left;
        }

        private void EnterAim()
        {
            _currentState = BotState.Aim;
            _stateTimer   = _aimDuration;
            UpdateAimDirection();
        }

        private void EnterFire()
        {
            _currentState        = BotState.Fire;
            _burstShotsRemaining = Random.Range(_burstMinShots, _burstMaxShots + 1);
            _fireTimer           = 0f;   // 첫 발은 즉시
        }

        private void EnterReload()
        {
            _currentState = BotState.Reload;
            _stateTimer   = _reloadDuration;
        }

        // ── 보조 ──────────────────────────────────────────────────

        private void UpdateAimDirection()
        {
            if (_aimTarget != null)
            {
                _aimDirection = (Vector2)_aimTarget.position;
            }
            else
            {
                // 타깃 없으면 현재 이동 방향 + 약간 위를 조준
                _aimDirection = (Vector2)transform.position + _targetDirection + Vector2.up * 0.5f;
            }
        }

        private NetworkInputData BuildInput()
        {
            var input = new NetworkInputData();

            switch (_currentState)
            {
                case BotState.Move:
                    input.Direction    = _targetDirection;
                    input.AimDirection = _aimDirection;
                    break;

                case BotState.Aim:
                    // Aim 중에는 이동 멈춤, 조준만
                    input.Direction    = Vector2.zero;
                    input.AimDirection = _aimDirection;
                    break;

                case BotState.Fire:
                    input.Direction      = Vector2.zero;
                    input.AimDirection   = _aimDirection;
                    input.IsFirePressed  = _fireThisTick;
                    break;

                case BotState.Reload:
                    input.Direction    = Vector2.zero;
                    input.AimDirection = _aimDirection;
                    break;

                default:
                    input.Direction    = Vector2.zero;
                    input.AimDirection = (Vector2)transform.position + Vector2.right;
                    break;
            }

            return input;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            if (_aimTarget != null)
                Gizmos.DrawLine(transform.position, _aimTarget.position);

            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 0.8f,
                $"BotFSM: {_currentState} ({_stateTimer:F1}s)");
        }
#endif
    }
}
