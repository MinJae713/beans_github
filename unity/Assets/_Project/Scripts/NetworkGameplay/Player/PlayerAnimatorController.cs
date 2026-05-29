using System;
using UnityEngine;
using Fusion;
using MoreMountains.Feedbacks;
using Colosseum.Weapon;

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
        private static bool? _localAutoInputModeCache;
        private enum AnimationStateKind
        {
            Idle,
            Run,
            JumpUp,
            Fall,
            Hurt,
        }

        [Header("Character Animators")]
        [SerializeField] private RuntimeAnimatorController _beanyController;
        [SerializeField] private RuntimeAnimatorController _peaController;

        [Header("Player Colors")]
        [SerializeField] private Color _player1BodyColor = new Color(0xd7/255f, 0x73/255f, 0x89/255f, 1f); // #D77389
        [SerializeField] private Color _player1GunColor  = new Color(0xd7/255f, 0x73/255f, 0x89/255f, 1f); // #D77389
        [SerializeField] private Color _player2BodyColor = new Color(0x8f/255f, 0xbf/255f, 0x3f/255f, 1f); // #8FBF3F
        [SerializeField] private Color _player2GunColor  = new Color(0x8f/255f, 0xbf/255f, 0x3f/255f, 1f); // #8FBF3F

        [Header("Animation Smoothing")]
        [SerializeField] [Range(5f, 30f)] private float _speedSmoothRate = 15f;

        // A303-168: 마우스 추적 팔 + 총 부착 + 발사 위치 정합.
        // - LeftArm/RightArm 두 자식 GameObject. facing 따라 main(aim 회전) / off(idle 자세) 분리.
        // - Gun은 매 LateUpdate에 main arm의 Tip 위치/회전 추종 — 손에 들고 있는 연출.
        // - facing 결정은 마우스 aim 방향 우선. PlayerController의 input 기반 flip은 LateUpdate에서 덮어쓴다.
        [Header("Arm System (A303-168)")]
        [Tooltip("좌측 팔 Transform. 캐릭터가 좌측을 조준할 때 main hand로 활성, aim 방향 회전.")]
        [SerializeField] private Transform _leftArm;
        [Tooltip("우측 팔 Transform. 캐릭터가 우측을 조준할 때 main hand.")]
        [SerializeField] private Transform _rightArm;
        [Tooltip("좌측 팔의 끝(손) 지점. Gun이 이 위치로 추종됨.")]
        [SerializeField] private Transform _leftArmTip;
        [Tooltip("우측 팔의 끝(손) 지점.")]
        [SerializeField] private Transform _rightArmTip;
        [Tooltip("Gun GameObject. 매 LateUpdate에 main arm Tip 위치 + main arm 회전 추종.")]
        [SerializeField] private Transform _gunTransform;
        [Tooltip("off-hand 팔(반대 방향) 자세 각도. 자연스럽게 늘어진 자세로 두면 좋음.")]
        [SerializeField] private float _offHandIdleAngle = -10f;
        [Tooltip("팔 SpriteRenderer sortingOrder 기준. main 측 +1, off 측 -1.")]
        [SerializeField] private int _armSortingOrderBase = 5;

        private Animator         _animator;
        private Rigidbody2D      _rb;
        private SpriteRenderer   _spriteRenderer;
        private SpriteRenderer   _gunRenderer;
        private SpriteRenderer   _leftArmRenderer;  // A303-168: sortingOrder 제어
        private SpriteRenderer   _rightArmRenderer;
        private Camera           _cachedCam;        // A303-168: 마우스 → 월드 변환용
        private Gun              _gunController;    // A303-168: AimDirection 단일 source 조회용
        private PlayerController _playerController;
        private PlayerHealth     _health;

        [SerializeField] private MMFeedbacks _hurtFeedbacks;
        [SerializeField] private MMFeedbacks _deathFeedbacks;
        [SerializeField] private float _deathHideDelay = 1.1f;

        private float     _smoothedSpeed         = 0f;
        private float     _prevProxyPosX         = 0f;  // [ANIM] proxy Speed 추정용 직전 frame position.x
        private float     _prevHp                = float.MaxValue;
        private bool      _isPlayer1             = true;
        private bool      _initialized           = false;
        private bool      _wasDead               = false;
        private bool      _hurtFacingLocked      = false;
        private bool      _lockedFlipX           = false;
        private float     _postHurtFacingTimer   = 0f;
        private float     _lastGunAngle          = 0f;   // A303-168: UpdateArmAim 끝에서 매 frame 갱신
        private float     _lockedGunAngle        = 0f;   // A303-168: hurt lock 시작 시점의 gunAngle 캡처
        private TickTimer _deathHideTimer;
        private const float PostHurtFacingHold = 0.35f;

        public override void Spawned()
        {
            _animator         = GetComponent<Animator>();
            _rb               = GetComponent<Rigidbody2D>();
            _spriteRenderer   = GetComponent<SpriteRenderer>();
            _playerController = GetComponent<PlayerController>();
            _health           = GetComponent<PlayerHealth>();

            // Gun 자식 오브젝트의 SpriteRenderer + GunController
            // A303-168: _gunTransform이 Inspector에서 배선되어 있으면 그걸 우선 사용, 없으면 fallback.
            if (_gunTransform == null)
                _gunTransform = transform.Find("Gun");
            if (_gunTransform != null)
            {
                _gunRenderer   = _gunTransform.GetComponent<SpriteRenderer>();
                _gunController = _gunTransform.GetComponent<Gun>();
            }

            // A303-168: 팔 SpriteRenderer 캐싱 (sortingOrder 제어용)
            if (_leftArm  != null) _leftArmRenderer  = _leftArm.GetComponent<SpriteRenderer>();
            if (_rightArm != null) _rightArmRenderer = _rightArm.GetComponent<SpriteRenderer>();

            // 스폰 시점의 실제 HP로 초기화 — 스폰 직후 Hurt 피드백 오동작 방지
            _prevHp = _health != null ? _health.CurrentHealth : float.MaxValue;
            // [ANIM] 스폰 시점 position.x 캐시 — 첫 frame deltaX 가 spawn 위치 부터의 폭주 방지.
            _prevProxyPosX = transform.position.x;

            TryInitializePlayerSide();

            _animator.runtimeAnimatorController = _isPlayer1 ? _beanyController : _peaController;
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
            // [ANIM] 본인(InputAuthority)은 자기 velocity 기반 — 즉시성 보장.
            // 상대(proxy)는 server snapshot 의 position.x 변화량으로 추정 — _rb.velocity.x 는
            // NetworkRigidbody2D position-only 보간 영향으로 proxy 측 신뢰성 낮음.
            // input 동기화 방식은 TryGetMovementInput 의 stale hold 메커니즘 영향으로 정지 시
            // 잔여값이 유지되어 발 동동 구르는 회귀 발생 → position delta 로 회피.
            float rawSpeedX;
            if (Object != null && Object.HasInputAuthority)
            {
                rawSpeedX = Mathf.Abs(_rb.velocity.x);
            }
            else
            {
                // proxy: position.x delta / dt 로 m/s 단위 속도 환산.
                // 정지 시 position 안 변하므로 정확히 0 → Idle.
                float dt = Mathf.Max(Time.deltaTime, 0.0001f);
                float deltaX = transform.position.x - _prevProxyPosX;
                rawSpeedX = Mathf.Abs(deltaX) / dt;
            }
            _prevProxyPosX = transform.position.x;
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
                ResetVisualStateAfterRespawn();
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
                    // A303-168: hurt lock 동안 arm/Gun도 같이 고정 — 현재 aim 각도 캡처해서 lock 해제 전까지 유지.
                    // body/arm/Gun 방향 불일치(몸은 좌측, 팔/총은 우측 등) 방지.
                    _lockedGunAngle = _lastGunAngle;

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
            bool isInHurt   = IsCurrentState(stateInfo, AnimationStateKind.Hurt);

            if (!isDead && isInHurt && stateInfo.normalizedTime >= 0.6f)
            {
                _animator.ResetTrigger(ParamIsHurt);
                if (!grounded)
                    TryPlayState(_rb.velocity.y >= 0f ? AnimationStateKind.JumpUp : AnimationStateKind.Fall);
                else if (_smoothedSpeed > 0.1f)
                    TryPlayState(AnimationStateKind.Run);
                else
                    TryPlayState(AnimationStateKind.Idle);
            }

            if (_spriteRenderer == null) return;

            // 방향(flipX) 전환
            // 로컬: PlayerController(input 기반)가 처리
            // 리모트: velocity 기반 추론
            //   - Hurt 중에는 피격 시 velocity가 역방향으로 전환되어 방향이 뒤집히는 문제 방지
            // velocity 기반 방향 전환 — 리모트 클라이언트 전용 (로컬은 PlayerController input 기반)
            // A303-168: UpdateArmAim이 LateUpdate에서 aim 기반 flipX로 덮어쓰지만, Update 시점 시각
            // 일관성을 위해 body flipX는 여기서 set. Gun side는 UpdateArmAim 단독 제어.
            if (!Object.HasInputAuthority && !isInHurt && !justGotHurt && Mathf.Abs(_rb.velocity.x) > 1f)
            {
                _spriteRenderer.flipX = _rb.velocity.x < 0;
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
                // Gun/arm은 UpdateArmAim의 _hurtFacingLocked 분기에서 _lockedGunAngle 기반 고정 처리됨.
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
                _animator.runtimeAnimatorController = _isPlayer1 ? _beanyController : _peaController;
                // Gun side(flipX/localPosition)는 UpdateArmAim이 매 frame 처리 — 별도 호출 불필요.
                _initialized = true;
            }
        }

            // A303-168: UpdateGunSide 제거됨. Gun localPosition/rotation/flip은 UpdateArmAim이 단독 제어.
            // 본 메서드의 localPosition.x 설정은 LateUpdate가 매 frame 덮어쓰는 dead path였고,
            // flipX 설정도 새 시스템(flipY 기반 좌우 대칭)에서 의미 없어짐.

        public void ResetVisualStateAfterRespawn()
        {
            if (_animator == null)
            {
                _animator = GetComponent<Animator>();
            }

            if (_spriteRenderer == null)
            {
                _spriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (_health == null)
            {
                _health = GetComponent<PlayerHealth>();
            }

            if (!_initialized)
            {
                TryInitializePlayerSide();
            }

            _deathHideTimer = default;
            _prevHp = _health != null ? _health.CurrentHealth : float.MaxValue;
            _hurtFacingLocked = false;
            _postHurtFacingTimer = 0f;
            _wasDead = false;

            // [A303-170 MR 리뷰 보강] respawn/teleport 직후 spawn 위치와 직전 frame position 의 deltaX 가
            // 큰 spike 가 되어 proxy 다리 애니가 한 frame 폭주하지 않도록 동시에 reset.
            _prevProxyPosX = transform.position.x;
            _smoothedSpeed = 0f;

            if (_animator != null)
            {
                _animator.ResetTrigger(ParamIsHurt);
                _animator.SetBool(ParamIsDead, false);
                TryPlayState(AnimationStateKind.Idle);
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.flipX = !_isPlayer1;
            }

            ShowAllRenderers();
        }

        private void ShowAllRenderers()
        {
            foreach (var r in GetComponentsInChildren<SpriteRenderer>(true))
                r.enabled = true;
        }

        private bool IsCurrentState(AnimatorStateInfo stateInfo, AnimationStateKind kind)
        {
            return stateInfo.IsName(GetPreferredStateName(kind)) ||
                   stateInfo.IsName(GetStickManStateName(kind));
        }

        private bool TryPlayState(AnimationStateKind kind)
        {
            return TryPlayStateName(GetPreferredStateName(kind)) ||
                   TryPlayStateName(GetStickManStateName(kind));
        }

        private bool TryPlayStateName(string stateName)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName))
            {
                return false;
            }

            int shortHash = Animator.StringToHash(stateName);
            if (_animator.HasState(0, shortHash))
            {
                _animator.Play(shortHash, 0, 0f);
                return true;
            }

            int fullPathHash = Animator.StringToHash($"Base Layer.{stateName}");
            if (_animator.HasState(0, fullPathHash))
            {
                _animator.Play(fullPathHash, 0, 0f);
                return true;
            }

            return false;
        }

        private string GetPreferredStateName(AnimationStateKind kind)
        {
            string prefix = ResolveBeanyStatePrefix();
            switch (kind)
            {
                case AnimationStateKind.Idle: return $"{prefix}_idle";
                case AnimationStateKind.Run: return $"{prefix}_run";
                case AnimationStateKind.JumpUp: return $"{prefix}_jumpup";
                case AnimationStateKind.Fall: return $"{prefix}_fall";
                case AnimationStateKind.Hurt: return $"{prefix}_hurt";
                default: return $"{prefix}_idle";
            }
        }

        private string ResolveBeanyStatePrefix()
        {
            string controllerName = _animator != null && _animator.runtimeAnimatorController != null
                ? _animator.runtimeAnimatorController.name
                : string.Empty;

            if (controllerName.IndexOf("Pea", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "beany_pea";
            }

            if (controllerName.IndexOf("Normal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "beany_normal";
            }

            return _isPlayer1 ? "beany_normal" : "beany_pea";
        }

        private static string GetStickManStateName(AnimationStateKind kind)
        {
            switch (kind)
            {
                case AnimationStateKind.Idle: return "StickMan_Idle";
                case AnimationStateKind.Run: return "StickMan_Run";
                case AnimationStateKind.JumpUp: return "StickMan_JumpUp";
                case AnimationStateKind.Fall: return "StickMan_Fall";
                case AnimationStateKind.Hurt: return "StickMan_Hurt";
                default: return "StickMan_Idle";
            }
        }

        // A303-168: 매 LateUpdate에 팔 / 총 visual을 정합한다.
        // - Update()에서 Gun.cs Render()가 _networkedAimDir 기반으로 Gun.localRotation을 set 한 직후 실행.
        // - main arm(facing 쪽)을 aim 방향으로 회전, off arm은 idle 자세.
        // - Gun은 main arm Tip 위치/회전으로 덮어씀 — 손에 들고 있는 연출.
        private void LateUpdate()
        {
            UpdateArmAim();
        }

        private void UpdateArmAim()
        {
            if (_leftArm == null || _rightArm == null) return;
            if (_gunTransform == null) return;

            // A303-168: 사망 또는 body sprite가 hide 된 상태에서는 arm/gun도 같이 숨김.
            // (그렇지 않으면 LateUpdate가 매 frame arm renderer를 다시 enable해서 사망 hide가 무효화됨)
            bool isDead       = _health != null && _health.IsDead;
            bool bodyVisible  = _spriteRenderer == null || _spriteRenderer.enabled;
            if (isDead || !bodyVisible)
            {
                if (_leftArmRenderer  != null) _leftArmRenderer.enabled  = false;
                if (_rightArmRenderer != null) _rightArmRenderer.enabled = false;
                if (_gunRenderer      != null) _gunRenderer.enabled      = false;
                return;
            }

            // aim 방향 결정.
            // - hurt lock 중에는 lock 시점에 캡처한 _lockedGunAngle 사용 → body/arm/Gun 방향 일관성 보장.
            // - 그 외에는 GetAimDirection으로 로컬 마우스 / 리모트 sync 분기.
            float gunAngle;
            if (_hurtFacingLocked)
            {
                gunAngle = _lockedGunAngle;
            }
            else
            {
                Vector2 aimDir = GetAimDirection();
                if (aimDir.sqrMagnitude < 0.0001f)
                {
                    // aim 입력이 없어도 (mouse가 player 위치에 가까울 때) renderer 활성/비활성 상태는 유지.
                    if (_gunRenderer != null) _gunRenderer.enabled = true;
                    return;
                }
                gunAngle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            }

            // Atan2 반환 범위 -180~180 기준 우측 hemisphere = facing right
            bool facingRight = gunAngle > -90f && gunAngle < 90f;

            // body flipX: aim 방향 우선. hurt 잠금 중에는 침범 금지.
            if (_spriteRenderer != null && !_hurtFacingLocked)
            {
                _spriteRenderer.flipX = !facingRight;
            }

            // main(facing 쪽) / off(반대) 분리
            Transform      mainArm    = facingRight ? _rightArm        : _leftArm;
            Transform      offArm     = facingRight ? _leftArm         : _rightArm;
            Transform      mainTip    = facingRight ? _rightArmTip     : _leftArmTip;
            SpriteRenderer mainRender = facingRight ? _rightArmRenderer : _leftArmRenderer;
            SpriteRenderer offRender  = facingRight ? _leftArmRenderer  : _rightArmRenderer;

            // LeftArm은 Inspector에서 Transform.localScale.x = -1로 mirror된 상태.
            // 그래서 rotation = gunAngle을 그대로 주면 mirror 효과와 합쳐서 반대 방향을 가리킨다.
            // 보정: LeftArm이 main일 때 rotation = gunAngle + 180°.
            bool mainIsLeftArm = !facingRight;
            float mainArmRotation = mainIsLeftArm ? (gunAngle + 180f) : gunAngle;
            mainArm.localRotation = Quaternion.Euler(0f, 0f, mainArmRotation);

            // off arm idle 자세도 mirror 보정 (LeftArm이 off일 때 -_offHandIdleAngle).
            bool offIsLeftArm = facingRight;
            float offArmRotation = offIsLeftArm ? (-_offHandIdleAngle) : _offHandIdleAngle;
            offArm.localRotation = Quaternion.Euler(0f, 0f, offArmRotation);

            // off arm은 완전히 숨김 — facing 안 보이는 쪽 팔.
            if (mainRender != null)
            {
                mainRender.enabled = true;
                mainRender.sortingOrder = _armSortingOrderBase + 1;
            }
            if (offRender != null)
            {
                offRender.enabled = false;
            }

            // Gun: main arm Tip 위치 + aim 방향 회전 + sprite 보정.
            // - sprite는 pivot이 muzzle(우측 edge)에 있어, rotation = gunAngle 그대로 적용하면
            //   muzzle이 aim 방향을 향하게 됨 (별도 오프셋 불필요).
            // - 단, 좌측 hemisphere(rotation ≈ 180°)에서는 sprite 상하가 뒤집혀 보임 → flipY = true로
            //   상하 복원. 우측 hemisphere(rotation ≈ 0°)에서는 flipY = false 유지.
            // - 결과: 좌우 어느 쪽 조준이든 gun이 top-up 자세 + muzzle은 aim 방향.
            if (mainTip != null)
            {
                _gunTransform.position = mainTip.position;
                _gunTransform.rotation = Quaternion.Euler(0f, 0f, gunAngle);
            }
            if (_gunRenderer != null)
            {
                _gunRenderer.enabled = true;
                _gunRenderer.flipY   = !facingRight;
            }

            // A303-168: hurt lock 시작 시점 캡처용으로 매 frame 마지막 gunAngle 저장.
            _lastGunAngle = gunAngle;

            // Gun의 sortingOrder는 Inspector에서 직접 set — Order in Layer 슬라이더.
            // 코드에서 매 frame 덮어쓰지 않음. (팔과 독립적으로 z-order 조정 가능)
        }

        public static bool ShouldUseNetworkedAimForLocalAuthority(bool localAutoInputMode, bool hasGunController)
        {
            return localAutoInputMode && hasGunController;
        }

        private static bool IsLocalAutoInputMode()
        {
            if (_localAutoInputModeCache.HasValue)
                return _localAutoInputModeCache.Value;

            bool enabled = false;
            foreach (string arg in Environment.GetCommandLineArgs())
            {
                if (string.Equals(arg, "--local-auto-role", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--local-auto-devmode", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(arg, "--local-auto-verify-gameplay", StringComparison.OrdinalIgnoreCase))
                {
                    enabled = true;
                    break;
                }
            }

            _localAutoInputModeCache = enabled;
            return enabled;
        }

        // A303-168: aim 방향 캡처.
        // - 일반 로컬 입력 권한 보유 / 비네트워크 환경: 마우스 위치를 직접 읽어 (cursor - player) 벡터 반환.
        // - local-auto 검증 / 리모트 플레이어: Gun.AimDirection (networked, StateAuthority 확정 발사 방향) 직접 조회.
        private Vector2 GetAimDirection()
        {
            if (ShouldUseNetworkedAimForLocalAuthority(IsLocalAutoInputMode(), _gunController != null))
            {
                return _gunController.AimDirection;
            }

            bool useMouse = (Object == null) || Object.HasInputAuthority;

            if (useMouse)
            {
                if (_cachedCam == null) _cachedCam = Camera.main;
                if (_cachedCam == null) return Vector2.zero;

                Vector3 mouseScreen = Input.mousePosition;
                mouseScreen.z       = -_cachedCam.transform.position.z;
                Vector3 mouseWorld  = _cachedCam.ScreenToWorldPoint(mouseScreen);

                Vector3 origin = transform.position;
                Vector2 dir    = (Vector2)(mouseWorld - origin);
                return dir;
            }

            // 리모트: Gun의 networked aim direction을 직접 읽음 (단일 source).
            return _gunController != null ? _gunController.AimDirection : Vector2.zero;
        }

        // A303-168: Scene view에서 어깨/팔길이/Gun 위치를 시각화해 Inspector 튜닝 보조.
        // Player GameObject 선택 시에만 표시.
        private void OnDrawGizmosSelected()
        {
            // 어깨 위치 (노랑 wire sphere)
            if (_leftArm != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_leftArm.position, 0.08f);
            }
            if (_rightArm != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(_rightArm.position, 0.08f);
            }

            // 팔길이 (빨강 라인 + 끝점 wire sphere)
            if (_leftArm != null && _leftArmTip != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_leftArm.position, _leftArmTip.position);
                Gizmos.DrawWireSphere(_leftArmTip.position, 0.06f);
            }
            if (_rightArm != null && _rightArmTip != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(_rightArm.position, _rightArmTip.position);
                Gizmos.DrawWireSphere(_rightArmTip.position, 0.06f);
            }

            // Gun 현재 위치 (시안 wire sphere)
            if (_gunTransform != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(_gunTransform.position, 0.05f);
            }
        }
    }
}
