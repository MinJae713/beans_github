using UnityEngine;
using Fusion;
using MoreMountains.Feedbacks;

namespace Colosseum.Player
{
    [RequireComponent(typeof(Animator))]
    public class PlayerAnimatorController : NetworkBehaviour
    {
        private static readonly int ParamSpeed      = Animator.StringToHash("Speed");
        private static readonly int ParamIsGrounded = Animator.StringToHash("IsGrounded");
        private static readonly int ParamVelocityY  = Animator.StringToHash("VelocityY");
        private static readonly int ParamIsDead     = Animator.StringToHash("IsDead");
        private static readonly int ParamIsHurt     = Animator.StringToHash("IsHurt");

        [Header("Player Colors")]
        [SerializeField] private Color _player1BodyColor = new Color(1.000f, 0.702f, 0.278f, 1f); // #FFB347
        [SerializeField] private Color _player1GunColor  = new Color(1.000f, 0.353f, 0.122f, 1f); // #FF5A1F
        [SerializeField] private Color _player2BodyColor = new Color(0.333f, 0.667f, 1.000f, 1f); // #55AAFF
        [SerializeField] private Color _player2GunColor  = new Color(0.000f, 0.333f, 1.000f, 1f); // #0055FF

        [Header("Gun Position")]
        [SerializeField] private float _gunXOffset = 0.45f;

        [Header("Animation Smoothing")]
        [SerializeField] [Range(5f, 30f)] private float _speedSmoothRate = 15f;

        private Animator         _animator;
        private Rigidbody2D      _rb;
        private SpriteRenderer   _spriteRenderer;
        private SpriteRenderer   _gunRenderer;
        private PlayerController _playerController;
        private PlayerHealth     _health;

        [SerializeField] private MMFeedbacks _hurtFeedbacks;
        [SerializeField] private MMFeedbacks _deathFeedbacks;
        [SerializeField] private float _deathHideDelay = 1.1f;

        private float     _smoothedSpeed         = 0f;
        private float     _prevHp                = float.MaxValue;
        private bool      _isPlayer1             = true;
        private bool      _initialized           = false;
        private bool      _wasDead               = false;
        private bool      _hurtFacingLocked      = false;
        private bool      _lockedFlipX           = false;
        private float     _postHurtFacingTimer   = 0f;
        private TickTimer _deathHideTimer;
        private const float PostHurtFacingHold = 0.35f;

        public override void Spawned()
        {
            _animator         = GetComponent<Animator>();
            _rb               = GetComponent<Rigidbody2D>();
            _spriteRenderer   = GetComponent<SpriteRenderer>();
            _playerController = GetComponent<PlayerController>();
            _health           = GetComponent<PlayerHealth>();

            // Gun 자식 오브젝트의 SpriteRenderer
            var gunTransform = transform.Find("Gun");
            if (gunTransform != null)
                _gunRenderer = gunTransform.GetComponent<SpriteRenderer>();

            // 스폰 시점의 실제 HP로 초기화 — 스폰 직후 Hurt 피드백 오동작 방지
            _prevHp = _health != null ? _health.CurrentHealth : float.MaxValue;

            TryInitializePlayerSide();
        }

        private void OnDisable()
        {
            // disconnect/씬 reload 등으로 컴포넌트가 꺼질 때 사망 상태면 즉시 숨김 처리
            // — TickTimer 만료 전에 Update가 중단되어 visual이 잔존하는 케이스 방지.
            //
            // A303-267: NetworkObject가 이미 invalid(Despawn 진행 중)인 경우 IsDead 접근이
            // InvalidOperationException을 던진다. 이 예외가 NetworkManager.LeaveToMainMenu의
            // SceneManager.LoadScene("MainMenu") 호출까지 차단해 게임 종료 후 SampleScene이
            // 잔존하는 부작용까지 일으킨다. Despawn 진행 중에는 GameObject 자체가 곧 삭제되므로
            // 시각 처리(HideVisualsAfterDeath) 자체가 의미 없음 → 가드로 스킵.
            if (_health == null) return;
            if (_health.Object == null || !_health.Object.IsValid) return;
            if (_health.IsDead)
                _health.HideVisualsAfterDeath();
        }

        private void Update()
        {
            if (_animator == null || _rb == null) return;

            // Player1/2 판별 (Lazy 재시도)
            if (!_initialized)
                TryInitializePlayerSide();

            // 플레이어 색상 적용
            if (_initialized)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.color = _isPlayer1 ? _player1BodyColor : _player2BodyColor;
                if (_gunRenderer != null)
                    _gunRenderer.color = _isPlayer1 ? _player1GunColor : _player2GunColor;
            }
            else
            {
                if (_spriteRenderer != null) _spriteRenderer.color = Color.white;
            }

            // 애니메이션 속도 스무딩
            float rawSpeedX = Mathf.Abs(_rb.velocity.x);
            // 멈출 때는 빠르게 0으로 감소, 움직일 때는 부드럽게 증가
            if (rawSpeedX < 0.5f)
                _smoothedSpeed = Mathf.MoveTowards(_smoothedSpeed, 0f, Time.deltaTime * 40f);
            else
                _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, rawSpeedX, Time.deltaTime * _speedSmoothRate);

            _animator.SetFloat(ParamSpeed, _smoothedSpeed);
            _animator.SetFloat(ParamVelocityY, _rb.velocity.y);

            bool grounded = _playerController != null && _playerController.IsGrounded;
            _animator.SetBool(ParamIsGrounded, grounded);

            bool isDead = _health != null && _health.IsDead;
            _animator.SetBool(ParamIsDead, isDead);

            // 사망 후 지연 숨김 타이머 체크 (TickTimer 기반 — Host/Client 타이밍 일치)
            if (Runner != null && _deathHideTimer.Expired(Runner))
            {
                _deathHideTimer = default;
                if (_health != null && _health.IsDead)
                    _health.HideVisualsAfterDeath();
            }

            // 사망 감지
            // IsDead가 true→false로 바뀐 순간 = 리스폰
            // _prevHp 리셋 + Hurt 피드백 방지 + Die 상태 강제 탈출 + 초기 방향 복원
            if (_wasDead && !isDead)
            {
                _deathHideTimer = default; // 리스폰 시 대기 중인 타이머 취소
                _prevHp = _health != null ? _health.CurrentHealth : float.MaxValue;
                _animator.ResetTrigger(ParamIsHurt);
                _animator.Play("StickMan_Idle");
                // Player1=오른쪽(flipX false), Player2=왼쪽(flipX true) 고정
                if (_spriteRenderer != null)
                    _spriteRenderer.flipX = !_isPlayer1;
                ShowAllRenderers();
            }
            if (!_wasDead && isDead)
            {
                _deathFeedbacks?.PlayFeedbacks();
                if (Runner != null)
                    _deathHideTimer = TickTimer.CreateFromSeconds(Runner, _deathHideDelay);
            }
            _wasDead = isDead;

            // 피격 감지
            bool justGotHurt = false;
            if (_health != null)
            {
                float currentHp = _health.CurrentHealth;
                if (currentHp < _prevHp && !isDead && currentHp > 0f)
                {
                    if (_spriteRenderer != null)
                    {
                        _lockedFlipX      = _spriteRenderer.flipX;
                        _hurtFacingLocked = true;
                    }
                    _animator.SetTrigger(ParamIsHurt);
                    _hurtFeedbacks?.PlayFeedbacks();
                    justGotHurt = true;
                }
                _prevHp = currentHp;
            }

            // Hurt 애니메이션 완료 + 상태 전환
            // AnimatorController에 Hurt 상태의 exit transition이 없으므로
            // 애니메이션이 끝난 뒤 현재 상태에 맞는 스테이트로 강제 전환
            var stateInfo   = _animator.GetCurrentAnimatorStateInfo(0);
            bool isInHurt   = stateInfo.IsName("StickMan_Hurt");

            if (!isDead && isInHurt && stateInfo.normalizedTime >= 0.6f)
            {
                _animator.ResetTrigger(ParamIsHurt);
                if (!grounded)
                    _animator.Play(_rb.velocity.y >= 0f ? "StickMan_JumpUp" : "StickMan_Fall");
                else if (_smoothedSpeed > 0.1f)
                    _animator.Play("StickMan_Run");
                else
                    _animator.Play("StickMan_Idle");
            }

            if (_spriteRenderer == null) return;

            // 방향(flipX) 전환
            // 로컬: PlayerController(input 기반)가 처리
            // 리모트: velocity 기반 추론
            //   - Hurt 중에는 피격 시 velocity가 역방향으로 전환되어 방향이 뒤집히는 문제 방지
            // velocity 기반 방향 전환 — 리모트 클라이언트 전용 (로컬은 PlayerController input 기반)
            if (!Object.HasInputAuthority && !isInHurt && !justGotHurt && Mathf.Abs(_rb.velocity.x) > 1f)
            {
                _spriteRenderer.flipX = _rb.velocity.x < 0;
                UpdateGunSide(_spriteRenderer.flipX);
            }

            // Hurt 중 방향 잠금: 피격 중 및 피격 후 PostHurtFacingHold 동안 velocity 기반 flip 차단
            // — hurt 종료 직후 넉백 velocity가 남아있어 방향이 뒤집히는 것을 방지
            if (isInHurt || justGotHurt)
            {
                _postHurtFacingTimer = 0f;
            }
            else if (_hurtFacingLocked)
            {
                _postHurtFacingTimer += Time.deltaTime;
                if (_postHurtFacingTimer >= PostHurtFacingHold)
                {
                    _hurtFacingLocked    = false;
                    _postHurtFacingTimer = 0f;
                }
            }
            if (_hurtFacingLocked)
            {
                _spriteRenderer.flipX = _lockedFlipX;
                UpdateGunSide(_lockedFlipX);
            }
        }

        private void TryInitializePlayerSide()
        {
            if (_initialized || Runner == null || Object == null) return;

            int  count       = 0;
            bool hasLowerRef = false;
            foreach (var p in Runner.ActivePlayers)
            {
                count++;
                if (p.RawEncoded < Object.InputAuthority.RawEncoded)
                    hasLowerRef = true;
            }

            if (count > 0)
            {
                _isPlayer1 = !hasLowerRef;
                if (_spriteRenderer != null)
                    _spriteRenderer.flipX = !_isPlayer1;
                UpdateGunSide(!_isPlayer1);
                _initialized = true;
            }
        }

        private void UpdateGunSide(bool facingLeft)
        {
            if (_gunRenderer == null) return;
            float side = facingLeft ? -1f : 1f;
            var t = _gunRenderer.transform;
            t.localPosition = new Vector3(side * _gunXOffset, t.localPosition.y, t.localPosition.z);
            _gunRenderer.flipX = facingLeft;
        }

        private void ShowAllRenderers()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = true;
        }
    }
}
