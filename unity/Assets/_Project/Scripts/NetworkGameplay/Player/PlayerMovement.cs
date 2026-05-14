using Colosseum.Game;
using Colosseum.Network;
using UnityEngine;
using Fusion;

namespace Colosseum.Player
{
    public sealed class PlayerMovement
    {
        // Profile 미할당 fallback. 마이그레이션 안전망.
        // A303-317: coyote 제거 → 점프 횟수 기반. 절벽에서 떨어져도 jumpsUsed < MaxJumps면 점프 가능.
        private const float FallbackMoveSpeed = 8f;
        private const float FallbackJumpForce = 12f;
        private const float FallbackJumpBuffer = 0.1f;
        private const sbyte MaxJumps = 1;
        private const float FallbackVariableJumpCut = 0.5f;
        private const float FallbackFallGravityMultiplier = 2.0f;
        private const float FallbackApexHangThreshold = 1.5f;
        private const float FallbackApexGravityMultiplier = 0.5f;
        private const float FallbackAscendGravityMultiplier = 1.0f;

        // A303-292: 벽 타기 fallback. Profile 미할당 안전망.
        private const float FallbackWallSlideSpeed = 2.5f;
        private const float FallbackWallJumpVerticalForce = 11f;
        private const float FallbackWallJumpHorizontalForce = 7f;
        private const float FallbackWallJumpVariance = 0.1f;
        private const float FallbackWallReattachLockoutSeconds = 0.2f;
        private const float FallbackWallSlideStickInputThreshold = 0.5f;

        private GameplayProfile _profile;
        private Rigidbody2D _rb;

        // Fusion 결정론 보장 위해 base gravityScale 캐싱 — Profile 변경에도 baseline은 prefab 값 따름.
        private float _baseGravityScale = 1f;

        private float MoveSpeed => _profile != null ? _profile.combat.baseline.moveSpeed : FallbackMoveSpeed;
        private float JumpForce => _profile != null ? _profile.combat.baseline.jumpForce : FallbackJumpForce;
        private float JumpBufferSeconds => _profile != null ? _profile.combat.baseline.jumpBufferSeconds : FallbackJumpBuffer;
        private float VariableJumpCutFactor => _profile != null ? _profile.combat.baseline.variableJumpCutFactor : FallbackVariableJumpCut;
        private float FallGravityMultiplier => _profile != null ? _profile.combat.baseline.fallGravityMultiplier : FallbackFallGravityMultiplier;
        private float ApexHangThreshold => _profile != null ? _profile.combat.baseline.apexHangThreshold : FallbackApexHangThreshold;
        private float ApexGravityMultiplier => _profile != null ? _profile.combat.baseline.apexGravityMultiplier : FallbackApexGravityMultiplier;
        private float AscendGravityMultiplier => _profile != null ? _profile.combat.baseline.ascendGravityMultiplier : FallbackAscendGravityMultiplier;

        // A303-292
        private float WallSlideSpeed => _profile != null ? _profile.combat.baseline.wallSlideSpeed : FallbackWallSlideSpeed;
        private float WallJumpVerticalForce => _profile != null ? _profile.combat.baseline.wallJumpVerticalForce : FallbackWallJumpVerticalForce;
        private float WallJumpHorizontalForce => _profile != null ? _profile.combat.baseline.wallJumpHorizontalForce : FallbackWallJumpHorizontalForce;
        private float WallJumpVariance => _profile != null ? _profile.combat.baseline.wallJumpVariance : FallbackWallJumpVariance;
        private float WallReattachLockoutSeconds => _profile != null ? _profile.combat.baseline.wallReattachLockoutSeconds : FallbackWallReattachLockoutSeconds;
        private float WallSlideStickInputThreshold => _profile != null ? _profile.combat.baseline.wallSlideStickInputThreshold : FallbackWallSlideStickInputThreshold;

        public PlayerMovement(GameplayProfile profile)
        {
            _profile = profile;
        }

        public void BindRigidbody(Rigidbody2D rb)
        {
            _rb = rb;
            if (_rb != null)
            {
                _baseGravityScale = _rb.gravityScale;
            }
        }

        public void ApplyMovement(PlayerController self, in NetworkInputData input, bool isGroundedNow)
        {
            if (_rb == null)
            {
                return;
            }

            // A303-261: 상태이상 stun 체크. true면 수평 입력 무시 + 점프 입력 무시 (중력은 그대로).
            bool stunned = self.StatusController != null && self.StatusController.IsStunned();

            float speed = MoveSpeed;
            float jump = JumpForce;
            if (self.CardEffect != null)
            {
                speed *= self.CardEffect.MoveSpeedMultiplier;
                jump *= self.CardEffect.JumpForceMultiplier;
            }

            // A303-261: Frost 등 상태이상 modifier 곱연산 (CardEffect 위에 누적)
            if (self.StatusController != null)
            {
                speed *= self.StatusController.GetMoveSpeedMultiplier();
            }

            if (stunned)
            {
                // A303-292: stun 시 벽 슬라이드 즉시 해제 (이후 자유낙하)
                if (self.IsWallSliding)
                {
                    self.SetWallSlidingState(false, 0);
                }
                // 수평 0, 점프 무시. 중력은 ApplyVerticalGravityScale에서 그대로 작동.
                _rb.velocity = new Vector2(0f, _rb.velocity.y);
                ApplyVerticalGravityScale(isGroundedNow);
                return;
            }

            // A303-292: 지상 도달 시 벽 슬라이드 상태 해제
            if (isGroundedNow && self.IsWallSliding)
            {
                self.SetWallSlidingState(false, 0);
            }

            // A303-292: jumpDownThisTick은 벽 점프 분기에서도 필요해 일반 흐름보다 먼저 계산
            bool jumpDownThisTick = input.IsJumpPressed && !self.WasJumpHeldLastTick;
            bool jumpReleasedThisTick = !input.IsJumpPressed && self.WasJumpHeldLastTick;

            // A303-292: 벽 점프 — 벽 슬라이드 중 + (jump down 또는 jump buffer 만료 전).
            // bufferAlive를 조건에 포함시켜 직전 tick에 jump 입력을 받았으나 다음 tick에 키가
            // 떨어진 케이스에서도 buffer를 소비해 벽 점프가 누락되지 않도록 한다.
            // (코드 리뷰 반영, 김범승 — 일반 점프 흐름과 동일한 buffer 소비 패턴으로 정렬)
            bool wallJumpBufferAlive = !self.JumpBufferTimer.ExpiredOrNotRunning(self.Runner);
            if (self.IsWallSliding && (jumpDownThisTick || wallJumpBufferAlive))
            {
                int wallSide = self.WallSide;
                float varianceFactor = ComputeDeterministicJumpVariance(
                    self.Runner.Tick, self.Object.Id.Raw, WallJumpVariance);
                float vy = WallJumpVerticalForce * varianceFactor;
                float vx = -wallSide * WallJumpHorizontalForce; // 벽 반대 방향 추진
                _rb.velocity = new Vector2(vx, vy);

                self.WallReattachLockoutTimer =
                    TickTimer.CreateFromSeconds(self.Runner, WallReattachLockoutSeconds);
                self.WallReattachLockoutSide = (sbyte)wallSide;
                self.SetWallSlidingState(false, 0);
                self.JumpBufferTimer = TickTimer.None; // 명시적 buffer 소비
                ApplyVerticalGravityScale(isGroundedNow);
                return;
            }

            // A303-292: 벽 슬라이드 진입/유지 — 공중 + 입력 매칭 + 벽 접촉 + lockout 만료 시
            if (!isGroundedNow)
            {
                sbyte detectedSide = DetectWallStickSide(self, input);
                if (detectedSide != 0)
                {
                    self.SetWallSlidingState(true, detectedSide);

                    // 슬라이드: velocity.y를 하강 한도로 clamp, 수평은 0 (벽 부착)
                    float clampedY = Mathf.Max(_rb.velocity.y, -WallSlideSpeed);
                    _rb.velocity = new Vector2(0f, clampedY);

                    // 일정 속도 미끄러짐을 위해 fall/apex 가속 무시
                    _rb.gravityScale = _baseGravityScale;

                    // jump buffer는 벽 점프 입력 수용 위해 유지
                    if (jumpDownThisTick)
                    {
                        self.JumpBufferTimer =
                            TickTimer.CreateFromSeconds(self.Runner, JumpBufferSeconds);
                    }

                    return;
                }
                else if (self.IsWallSliding)
                {
                    // 입력 매칭 깨짐 또는 벽 끊김 → 자유낙하
                    self.SetWallSlidingState(false, 0);
                }
            }

            // 공중/지상 동일하게 입력 기반 즉시 velocity set. 공중 관성·감속 차이 없음.
            _rb.velocity = new Vector2(input.Direction.x * speed, _rb.velocity.y);

            if (isGroundedNow)
            {
                // A303-317: 점프 횟수 기반 — 지상 도달 시 사용 횟수 reset
                self.JumpsUsed = 0;
            }

            if (jumpDownThisTick)
            {
                self.JumpBufferTimer = TickTimer.CreateFromSeconds(self.Runner, JumpBufferSeconds);
            }

            if (jumpDownThisTick)
            {
                Collider2D gcHitDefault = null;
                Collider2D gcHitRunner = null;
                if (self.GroundCheck != null)
                {
                    gcHitDefault = Physics2D.OverlapBox(self.GroundCheck.position, self.GroundCheckSize, 0f, self.GroundLayer);
                    if (self.Runner != null)
                    {
                        gcHitRunner = self.Runner.GetPhysicsScene2D().OverlapBox(self.GroundCheck.position, self.GroundCheckSize, 0f, self.GroundLayer);
                    }
                }

                Debug.Log(
                    $"[Colosseum/DIAG] Jump pressed: IsGrounded={isGroundedNow}, " +
                    $"velocity.y={_rb.velocity.y:F2}, pos.y={self.transform.position.y:F2}, " +
                    $"jumpsUsed={self.JumpsUsed}/{MaxJumps}, " +
                    $"gcHitDefault={(gcHitDefault != null ? gcHitDefault.name : "NONE")}, " +
                    $"gcHitRunner={(gcHitRunner != null ? gcHitRunner.name : "NONE")}, " +
                    $"HasInputAuthority={self.Object.HasInputAuthority}");
            }

            bool bufferAlive = !self.JumpBufferTimer.ExpiredOrNotRunning(self.Runner);
            if (bufferAlive && (isGroundedNow || self.JumpsUsed < MaxJumps))
            {
                _rb.velocity = new Vector2(_rb.velocity.x, jump);
                self.JumpsUsed++;
                self.JumpBufferTimer = TickTimer.None;
            }

            if (jumpReleasedThisTick && _rb.velocity.y > 0f)
            {
                _rb.velocity = new Vector2(_rb.velocity.x, _rb.velocity.y * VariableJumpCutFactor);
            }

            ApplyVerticalGravityScale(isGroundedNow);
        }

        public void ApplyVerticalGravityScale(bool isGrounded)
        {
            if (_rb == null)
            {
                return;
            }

            if (isGrounded)
            {
                _rb.gravityScale = _baseGravityScale;
                return;
            }

            float vy = _rb.velocity.y;
            if (vy < 0f)
            {
                _rb.gravityScale = _baseGravityScale * FallGravityMultiplier;
            }
            else if (vy > 0f && vy < ApexHangThreshold)
            {
                _rb.gravityScale = _baseGravityScale * ApexGravityMultiplier;
            }
            else
            {
                // 상승 중(vy >= apex threshold) — ascend 배율 적용해 상승 시간 단축
                _rb.gravityScale = _baseGravityScale * AscendGravityMultiplier;
            }
        }

        /// <summary>
        /// A303-292: 벽 부착 측면 판정.
        /// 입력 dead zone 통과 + 같은 측 lockout 만료 + wallCheck OverlapBox 매칭이면 ±1 반환, 아니면 0.
        /// </summary>
        private sbyte DetectWallStickSide(PlayerController self, in NetworkInputData input)
        {
            float ix = input.Direction.x;
            if (Mathf.Abs(ix) < WallSlideStickInputThreshold)
            {
                return 0;
            }

            bool lockoutAlive = !self.WallReattachLockoutTimer.ExpiredOrNotRunning(self.Runner);

            if (ix > 0f)
            {
                if (lockoutAlive && self.WallReattachLockoutSide == 1) return 0;
                return self.IsTouchingWallOnSide(true) ? (sbyte)1 : (sbyte)0;
            }

            if (lockoutAlive && self.WallReattachLockoutSide == -1) return 0;
            return self.IsTouchingWallOnSide(false) ? (sbyte)-1 : (sbyte)0;
        }

        /// <summary>
        /// A303-292: ROUNDS 스타일 도움 닫기 — 벽 점프 점프력에 ±variance 폭의 변동을 적용.
        /// Fusion 결정론을 위해 UnityEngine.Random 대신 (tick, playerId) 기반 deterministic hash 사용.
        /// 모든 client/host가 동일 tick·동일 playerId에 대해 동일 결과를 산출한다.
        /// </summary>
        private static float ComputeDeterministicJumpVariance(int tick, uint playerIdRaw, float variance)
        {
            if (variance <= 0f) return 1f;
            unchecked
            {
                int hash = (tick * 73856093) ^ ((int)playerIdRaw * 19349663);
                float normalized = ((hash & 0xFFFF) / 65535f) * 2f - 1f; // [-1, +1]
                return 1f + normalized * variance;
            }
        }
    }
}
