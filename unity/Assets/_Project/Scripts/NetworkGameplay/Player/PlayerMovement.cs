using Colosseum.Game;
using Colosseum.Network;
using Colosseum.Contracts;
using Colosseum.NetworkGameplay.Diagnostics;
using Colosseum.NetworkGameplay.VFX;
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
        private const float FallbackWallSlideSpeed = 0.5f;
        private const float FallbackWallJumpVerticalForce = 11f;
        private const float FallbackWallJumpHorizontalForce = 7f;
        private const float FallbackWallJumpVariance = 0.1f;
        private const float FallbackWallReattachLockoutSeconds = 0.2f;
        private const float FallbackWallSlideStickInputThreshold = 0.5f;

        // A303-306: detach velocity. Profile 노출 전 단계라 fallback 상수만 사용.
        private const float FallbackWallDetachVelocityX = 2.5f;        // 벽 반대 방향 횡속 (m/s)
        private const float FallbackWallDetachDurationSeconds = 0.15f; // detach 지속 시간 (s)

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

        // A303-306: detach velocity는 아직 profile에 노출 안 함. 체감 확정 후 baseline에 추가 예정.
        private float WallDetachVelocityX => FallbackWallDetachVelocityX;
        private float WallDetachDurationSeconds => FallbackWallDetachDurationSeconds;

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
                // A303-306: 벽 점프 vy를 일반 JumpForce와 동일하게.
                // 의도: "벽에 매달린 채 점프 → 기본 점프 높이만큼 올라가면서 벽에서 부드럽게 detach".
                // 수평 추진은 즉발 ±WallJumpHorizontalForce 대신 detach velocity 시스템에 위임.
                float vy = JumpForce * varianceFactor;
                _rb.velocity = new Vector2(_rb.velocity.x, vy);

                // A303-306: 벽 반대 방향으로 짧은 시간 부드럽게 밀어내는 detach 트리거.
                self.WallDetachVelocityTimer =
                    TickTimer.CreateFromSeconds(self.Runner, WallDetachDurationSeconds);
                self.WallDetachSide = (sbyte)wallSide;

                // A303-306: 벽 점프도 점프 횟수 1회 소모. 부착 시 리필했더라도 벽 점프로 공중에 나오면 추가 공중 점프 차단.
                // (키 떼서 그냥 떨어진 경우엔 JumpsUsed가 0으로 남아 공중 점프 가능 — 의도된 비대칭)
                self.JumpsUsed++;

                // A303-306 진단: 벽 점프 발동 시점
                DiagLog.Log($"[Wall/DIAG] WALL JUMP fired: wallSide={wallSide}, vy={vy:F2}, jumpDownThisTick={jumpDownThisTick}, bufferAlive={wallJumpBufferAlive}, input.x={input.Direction.x:F2}");

                self.WallReattachLockoutTimer =
                    TickTimer.CreateFromSeconds(self.Runner, WallReattachLockoutSeconds);
                self.WallReattachLockoutSide = (sbyte)wallSide;
                self.SetWallSlidingState(false, 0);
                self.JumpBufferTimer = TickTimer.None; // 명시적 buffer 소비
                self.PublishMovementVfx(
                    VfxEventType.PlayerJumpStarted,
                    VfxPayloadFactory.GetPlayerMovementEffectKey(VfxEventType.PlayerJumpStarted),
                    VfxAttachPoint.CharacterFeet,
                    Vector2.down,
                    0.55f,
                    vy,
                    PlayerController.PlayerJumpStartedVfxSequenceSalt);
                ApplyVerticalGravityScale(isGroundedNow);
                return;
            }

            // A303-292: 벽 슬라이드 진입/유지 — 공중 + 입력 매칭 + 벽 접촉 + lockout 만료 시
            if (!isGroundedNow)
            {
                sbyte detectedSide = DetectWallStickSide(self, input);
                if (detectedSide != 0)
                {
                    bool wasAlreadySliding = self.IsWallSliding;
                    self.SetWallSlidingState(true, detectedSide);

                    // 슬라이드: velocity.y를 [-WallSlideSpeed, 0] 범위로 clamp, 수평은 0 (벽 부착).
                    // A303-306: 상한 0 추가 — 상승 중 진입 시 상승 관성 보존되면 base gravity 영향으로
                    // 점프 높이가 폭증하던 회귀를 막는다.
                    float vyBefore = _rb.velocity.y;
                    float clampedY = Mathf.Clamp(vyBefore, -WallSlideSpeed, 0f);
                    _rb.velocity = new Vector2(0f, clampedY);

                    // 일정 속도 미끄러짐을 위해 fall/apex 가속 무시
                    _rb.gravityScale = _baseGravityScale;

                    // A303-306 진단: 진입 시점에만 한 번 (유지 중 30 tick 폴링 로그는 A303-372에서 제거)
                    if (!wasAlreadySliding)
                    {
                        // A303-306: 벽 부착 시 점프 횟수 리필.
                        // 의도: 벽 점프든 슬라이드 해제 후 자유낙하 중 일반 점프든, 둘 다 항상 사용 가능.
                        self.JumpsUsed = 0;
                        DiagLog.Log($"[Wall/DIAG] WALL SLIDE entered: side={detectedSide}, input.x={input.Direction.x:F2}, vyBefore={vyBefore:F2}, clampedY={clampedY:F2}, WallSlideSpeed={WallSlideSpeed:F2}, jumpsUsed refilled");
                    }

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
                    // 입력 매칭 깨짐 또는 벽 끊김 → 자유낙하 + A303-306: 부드러운 detach 발동
                    sbyte previousSide = (sbyte)self.WallSide;
                    self.SetWallSlidingState(false, 0);
                    self.WallDetachVelocityTimer =
                        TickTimer.CreateFromSeconds(self.Runner, WallDetachDurationSeconds);
                    self.WallDetachSide = previousSide;
                    DiagLog.Log($"[Wall/DIAG] WALL SLIDE released: input.x={input.Direction.x:F2}, vy={_rb.velocity.y:F2}, detachSide={previousSide}");
                }
            }

            // A303-306: detach velocity 활성 동안에는 벽 반대 방향 추진을 우선 적용한다.
            // 사용자 입력이 detach 방향과 같으면 더 큰 절댓값을 따라가고, 반대거나 0이면 detach 적용.
            // (재부착 lockout과 별개 — lockout은 슬라이드 진입 차단, detach는 vx 횡속 보장.)
            bool wallDetachAlive = !self.WallDetachVelocityTimer.ExpiredOrNotRunning(self.Runner);
            if (wallDetachAlive)
            {
                float detachVx = -self.WallDetachSide * WallDetachVelocityX;
                float inputVx = input.Direction.x * speed;
                float resolvedVx =
                    Mathf.Sign(inputVx) == Mathf.Sign(detachVx) && Mathf.Abs(inputVx) > Mathf.Abs(detachVx)
                        ? inputVx
                        : detachVx;
                _rb.velocity = new Vector2(resolvedVx, _rb.velocity.y);
            }
            else
            {
                // 공중/지상 동일하게 입력 기반 즉시 velocity set. 공중 관성·감속 차이 없음.
                _rb.velocity = new Vector2(input.Direction.x * speed, _rb.velocity.y);
            }

            if (isGroundedNow)
            {
                // A303-317: 점프 횟수 기반 — 지상 도달 시 사용 횟수 reset
                self.JumpsUsed = 0;
            }

            if (jumpDownThisTick)
            {
                self.JumpBufferTimer = TickTimer.CreateFromSeconds(self.Runner, JumpBufferSeconds);
            }

            bool bufferAlive = !self.JumpBufferTimer.ExpiredOrNotRunning(self.Runner);
            if (bufferAlive && (isGroundedNow || self.JumpsUsed < MaxJumps))
            {
                _rb.velocity = new Vector2(_rb.velocity.x, jump);
                self.JumpsUsed++;
                self.JumpBufferTimer = TickTimer.None;
                self.PublishMovementVfx(
                    VfxEventType.PlayerJumpStarted,
                    VfxPayloadFactory.GetPlayerMovementEffectKey(VfxEventType.PlayerJumpStarted),
                    VfxAttachPoint.CharacterFeet,
                    Vector2.down,
                    0.55f,
                    jump,
                    PlayerController.PlayerJumpStartedVfxSequenceSalt);

                // A303-306 진단: 일반 점프 발동 시점 — 벽 점프와 구분
                DiagLog.Log($"[Jump/DIAG] NORMAL JUMP fired: vy={jump:F2}, isGrounded={isGroundedNow}, jumpsUsed={self.JumpsUsed}/{MaxJumps}, input.x={input.Direction.x:F2}");
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
