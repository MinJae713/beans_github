using UnityEngine;
using Colosseum.Player;
using Colosseum.NetworkGameplay.Diagnostics;

namespace Colosseum.Game
{
    /// <summary>
    /// 낙사 trigger. BoxCollider2D + isTrigger=true와 함께 부착해 Player가 진입 시 사망 판정.
    ///
    /// docs/30/10/02 §4 낙사 (S6 단일) 패턴 1차 구현.
    ///   - StateAuthority가 사망 판정 ([30/02 §허용 기준]).
    ///   - D45 (넉백 경계 이탈 무효화)와 분리: 낙사는 정상 사망 판정 (§4.4).
    ///   - killer=default(PlayerRef) → 자살/낙사. 킬 보상은 없지만 PlayerHealth가 생존 상대에게 이동 주도권을 이관한다.
    ///   - **카드 드로우 X**: ForceDeath(default) → Die(killer, triggerCardDraw=false) 경로. 보상 카드 미발생 (D72, D-15 한정).
    ///   - **무적 보호 O**: InvulnerabilityTimer.ExpiredOrNotRunning 통과 시에만 사망 판정 (Status 정책 일관성, D72).
    ///   - **Fusion 안전**: Runner.IsForward 가드로 resimulation 중복 fire 차단 (50/04 가드레일).
    ///
    /// D-15 데모 한정 1차 구현. Q47 (사전 경고음 위치) / Q48 (낙사 trigger ↔ SegmentBoundary 좌표 분리 정량) 미해소.
    /// </summary>
    public sealed class PitTrigger : MonoBehaviour
    {
        private const string CombatHitboxName = "CombatHitbox";

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (IsCombatHitbox(other)) return;

            // PlayerHealth는 NetworkBehaviour. 자식 collider 가능 → GetComponentInParent 사용.
            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) return;

            if (playerHealth.Object == null) return;
            if (!playerHealth.Object.HasStateAuthority) return;
            if (playerHealth.Runner == null || !playerHealth.Runner.IsForward) return;
            if (playerHealth.IsDead) return;

            // 정책 D72 (D-15 데모 1인 시연 한정 명문화):
            //   - 낙사 = 자살: killer=default → triggerCardDraw 경로 미사용 → 카드 드로우 X, 이동 주도권은 생존 상대에게 이관.
            //   - 낙사 = 무적 보호: D48 리스폰 1초 무적 중에는 무시 (Status 정책 일관성).
            // post-demo closure 의무 (D74): D-day 후 정식 사망 판정 정책 결정 필요.
            if (ShouldIgnorePitDeath(playerHealth, out string ignoreReason))
            {
                DiagLog.Log($"[Colosseum] PitTrigger: player {playerHealth.Object.InputAuthority} fell into pit during {ignoreReason} — ignored.");
                return;
            }

            DiagLog.Log($"[Colosseum] PitTrigger: player {playerHealth.Object.InputAuthority} fell into pit at {transform.position}.");
            ApplyPitDeath(playerHealth);
        }

        /// <summary>
        /// Sticky 진입자 처리 (이슈 2 fix, 2026-05-07):
        /// 무적 중 OnTriggerEnter2D는 ignored로 silent skip되지만, 무적 만료 후엔
        /// Enter event가 다시 발생하지 않아(이미 trigger 안) IsDead=false로 영원히 stuck.
        /// OnTriggerStay2D로 매 tick polling — 무적 만료 시점에 1회만 발화하고 사망 처리.
        /// IsDead=true set 후 후속 Stay tick은 IsDead 가드로 즉시 차단(부담 0).
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            if (IsCombatHitbox(other)) return;

            PlayerHealth playerHealth = other.GetComponentInParent<PlayerHealth>();
            if (playerHealth == null) return;

            if (playerHealth.Object == null) return;
            if (!playerHealth.Object.HasStateAuthority) return;
            if (playerHealth.Runner == null || !playerHealth.Runner.IsForward) return;
            if (playerHealth.IsDead) return;

            // 무적/빅토리런 보호 중에는 silent skip (Enter에서 이미 ignored 로그 1회 발생).
            if (ShouldIgnorePitDeath(playerHealth, out _)) return;

            DiagLog.Log($"[Colosseum] PitTrigger Stay: player {playerHealth.Object.InputAuthority} stuck in pit without active protection — applying death.");
            ApplyPitDeath(playerHealth);
        }

        private static bool ShouldIgnorePitDeath(PlayerHealth playerHealth, out string reason)
        {
            bool victoryRunProtected = playerHealth.IsVictoryRunDamageProtectionActive();
            bool respawnInvulnerable = !playerHealth.InvulnerabilityTimer.ExpiredOrNotRunning(playerHealth.Runner);

            if (victoryRunProtected && respawnInvulnerable)
            {
                reason = "victory run and respawn invulnerability";
            }
            else if (victoryRunProtected)
            {
                reason = "victory run protection";
            }
            else
            {
                reason = "respawn invulnerability";
            }

            return PlayerHealth.ShouldIgnoreEnvironmentDeath(victoryRunProtected, respawnInvulnerable);
        }

        private void ApplyPitDeath(PlayerHealth playerHealth)
        {
            playerHealth.PublishPitFallDeathVfx(transform.position);
            playerHealth.ForceDeath(default);
        }

        private static bool IsCombatHitbox(Collider2D other)
        {
            return other != null && other.isTrigger && other.name == CombatHitboxName;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.1f, 0.1f, 0.4f);
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size * (Vector2)transform.localScale);
            }
        }
#endif
    }
}
