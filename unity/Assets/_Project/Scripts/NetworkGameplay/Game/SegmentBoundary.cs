using UnityEngine;
using Colosseum.Player;
using Colosseum.Utils;

namespace Colosseum.Game
{
    /// <summary>
    /// MVPArena 7 segments 사이 advance trigger.
    ///
    /// 구현 형태 (D-15 데모 한정 임시 패턴):
    ///   - GameObject + BoxCollider2D(isTrigger=true) + 본 MonoBehaviour
    ///   - StateAuthority 분기 없음 (RoomManager 측이 [Networked] 권위 보유)
    ///   - OnTriggerEnter2D 1회 발화 후 _consumed 가드로 멱등 (F1 중복 방지)
    ///
    /// D44 (authoritative trigger 권위) 위배:
    ///   - docs/30_전장_레벨디자인/01_전장_구간_기준.md:71 "SegmentBoundary 콜라이더의 authoritative
    ///     trigger 진입 이벤트로 판정" → 정식 구현은 StateAuthority 분기 + Networked 트리거여야 함
    ///   - docs/50_기술_구조_기준/02_네트워크_truth_표.md:40 "구간 돌파 trigger / StateAuthority"
    ///   - D-day(2026-05-20) 후 정식 closure 의무 (post-demo backlog)
    ///   - 4-way SOT row 1건으로 위반 표면화 (memory/pr-fact-matrix.md)
    ///
    /// 회귀 0건 보장:
    ///   - SampleScene 9방은 PlayerBoundary.cs가 x좌표 기반 advance를 처리하므로 본 컴포넌트 미사용.
    ///   - 본 컴포넌트는 MVPArena.unity 7 segments에만 P2-3에서 GameObject 배치 시 부착.
    /// </summary>
    public class SegmentBoundary : MonoBehaviour
    {
        [Tooltip("이 boundary가 진입 신호로 보낼 다음 segment index. RoomManager.CurrentRoomIndex가 이 값보다 작으면 advance 트리거.")]
        [SerializeField] private int _nextSegmentIndex;

        [Tooltip("PlayerHealth 컴포넌트로 진입 식별 (자식 collider 가능). 끄면 모든 collider가 트리거 (위험).")]
        [SerializeField] private bool _requirePlayerComponent = true;

        // F1 (Pre-mortem 5.1): OnTriggerEnter2D 중복 발화 방지 가드.
        // 한 boundary가 동일 player에게 두 번 advance 트리거하지 않도록 멱등 보장.
        private bool _consumed;

        // Scene-local 캐시. FindObjectOfType hot path 신규 호출 0건 (50/04 가드레일).
        // RoomManager는 씬당 1개 NetworkBehaviour로 직렬화되어 있다 (RoomManager.cs:11).
        private RoomManager _roomManager;

        private void Awake()
        {
            _roomManager = SceneSearch.FindInScene<RoomManager>(gameObject.scene);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_consumed) return;
            if (_roomManager == null) return;

            PlayerHealth playerHealth = null;
            if (_requirePlayerComponent)
            {
                playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth == null) return;
            }

            // 다른 boundary가 먼저 발화해 RoomManager.CurrentRoomIndex가 이미 진행한 경우 무시.
            if (_roomManager.CurrentRoomIndex >= _nextSegmentIndex) return;

            _consumed = true;

            // RoomManager.HandleRoomAdvance(PlayerRef advancingPlayer) 시그니처와 정합.
            // PlayerHealth는 NetworkBehaviour이므로 Object.InputAuthority로 PlayerRef 식별
            // (PlayerBoundary.cs:75 / :109 패턴 답습 — RoomManager 자체도 동일 PlayerRef 식별 사용).
            var netObj = playerHealth != null ? playerHealth.Object : null;
            if (netObj == null)
            {
                // _requirePlayerComponent=false 시 PlayerRef 식별 불가 → advance 호출 스킵.
                // 데모 시나리오에서 이 경로는 사용하지 않음.
                Debug.LogWarning("[Colosseum] SegmentBoundary: PlayerHealth NetworkObject not found, advance skipped.");
                return;
            }

            // PlayerBoundary 정공 패턴 답습 (PlayerBoundary.cs:72-76 / :106-110) —
            // TryAdvanceRoom으로 [Networked] CurrentRoomIndex 권위 변경 (StateAuthority 분기 내부 처리)
            // 후 성공 시에만 HandleRoomAdvance(opponent kill / cardDeck draw / OnAdvanceTriggered).
            // _nextSegmentIndex는 direction 부호 결정에 사용하지 않는다 (1-based 우측 segment id로
            // 직렬화돼 좌향 진행 표현 불가). Player Rigidbody2D.velocity.x 부호로 ±1 산출 (PlayerBoundary가
            // moveX input을 쓰는 자리에 SegmentBoundary는 velocity로 대치 — trigger 진입 순간 부호 정합).
            // velocity가 미세할 경우 trigger 위치-Player 위치 차분으로 fallback.
            int direction;
            var prb = other.attachedRigidbody;
            if (prb != null && Mathf.Abs(prb.velocity.x) > 0.01f)
            {
                direction = prb.velocity.x > 0f ? 1 : -1;
            }
            else
            {
                direction = (other.transform.position.x >= transform.position.x) ? 1 : -1;
            }

            if (_roomManager.TryAdvanceRoom(netObj.InputAuthority, direction))
            {
                _roomManager.HandleRoomAdvance(netObj.InputAuthority);
            }
        }

#if UNITY_EDITOR
        // Scene Gizmo — segment index 시각화 (Editor 전용).
        private void OnDrawGizmos()
        {
            Gizmos.color = _consumed ? Color.gray : Color.cyan;
            var col = GetComponent<BoxCollider2D>();
            if (col != null)
            {
                Gizmos.DrawWireCube(transform.position + (Vector3)col.offset, col.size);
            }
        }
#endif
    }
}
