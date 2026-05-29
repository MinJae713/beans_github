---
last_updated: 2026-05-16
audience: ai-first
---

# MVP 구간 수치 프리셋

이 문서는 전장과 구간 관련 수치 후보를 기록하기 위한 자리다.

7구간 구조는 기준문서에서 확정하지만, 구간 길이, 카메라 속도, 리스폰 좌표 같은 값은 프리셋으로 관리한다.

## 스키마-only 커밋 TODO (T9/D56, 2026-04-27 마감)

> W2 Exit ⑥ (SCHEDULE.md) 스키마-only 커밋 예정. 담당 김범승. 아래 "관리 대상" 항목의 필드 시그니처(이름·타입·단위)만 잠금, 구체 수치는 W3~W4 플레이테스트 후 채운다. 본 문서는 현재 "작성 대기" 상태이며, 구현자는 이 섹션을 binding으로 읽지 않는다.

## 관리 대상

| 항목 | 설명 |
|---|---|
| 전투 구간 길이 | 가운데 5개 전투 구간(S2~S6)의 길이 후보 |
| 최종 구간 길이 | 양끝 승리 구간(S1·S7)의 길이 후보 |
| 카메라 추적 속도 | 이동 주도권 보유자를 따라가는 속도 |
| 카메라 경계 | 후방 이탈, 화면 밖 압박, 경계 패배 판정 기준 |
| 리스폰 앵커 | 구간 돌파 후 공격자/방어자 재배치 기준 |
| GO UI 기준 | 이동 주도권 방향 표시와 전환 조건 |

## A303-331 카메라 후보 값 (2026-05-15)

> 상태: candidate. 아래 값은 A303-331 이동 주도권 카메라 후보 구현과 문서 정합을 맞추기 위한 현재값이다. MR 확정 전에는 full physical route 검증 근거로 과장하지 않고, 승인 후 `GameplayProfile` 또는 카메라 프리셋으로 승격할지 결정한다.

| 항목 | 후보 값 | 의미 |
|---|---|---|
| 설계 viewport aspect | 16:9 | 카메라 target과 gameplay 경계 계산은 픽셀 해상도 자체가 아니라 16:9 설계 aspect를 기준으로 한다. 로컬 2클라 runner의 960×540 창은 이 비율의 검수용 표시 크기다. |
| 중립/라운드 시작 이동 clamp | 구간 중심 기준 16:9 viewport | `LastKiller`가 없을 때는 양 플레이어가 구간 중심 카메라 viewport 밖으로 전진 이탈하지 못한다. |
| 구간 전환 진입 위치 | P1=새 구간 leftBound+extent+2u clearance, P2=새 구간 rightBound-extent-2u clearance, Y=실제 지형 착지점 | room advance가 제품 경로나 fixture 경로에서 호출되더라도 돌파 주체를 새 구간 진입 edge 안쪽과 지형 위로 정렬해 이전 구간 좌표 기반 즉시 out-of-bounds/낙사 사망과 다음 경계 overlap을 막는다. |
| 구간 돌파 중복 guard | 12 authoritative ticks | 돌파 직후 같은 trigger overlap이나 teleport settling으로 다음 구간이 연속 자동 돌파되지 않게 막는다. |
| 이동 주도권 전방 anchor | 0.85 / 0.15 viewport ratio | 오른쪽 진행 시 주도권 보유자가 85% 지점을 넘거나, 왼쪽 진행 시 15% 지점을 넘으면 해당 방향으로 계속 sliding하는 edge guard 기준. |
| anchor offset | `(0.85 * 2 - 1) * viewHalfWidth` | 카메라 중심에서 edge anchor까지의 횡축 offset. 현재 후보 구현은 0.7 * viewHalfWidth를 사용한다. |
| 이동 주도권자 카메라 후퇴 | 금지 | 주도권 보유자가 뒤로 밀리거나 방향 반대로 움직여도 카메라가 후방으로 되감기지 않는다. |
| 초기 스폰 위치 | Wide 전투 구간 중심 16:9 viewport 안쪽 좌/우 offset | MVPArena wide 전투 구간의 최초 진입 좌표는 고정 월드좌표가 아니라 현재 구간 중심과 설계 viewport half-width에서 계산한다. Static/legacy 기본 전투 경로는 기존 좌표를 유지한다. |
| 리스폰 위치 | 일반 사망: 주도권자 이동방향 15% inset 선정렬 + 반대편 85%, 구간 돌파 방어자: entry anchor 기반 viewport-forward, Y=실제 지형 착지점 | 일반 사망 리스폰 X는 presentation 카메라 transform이 아니라 StateAuthority가 확정한 이동 주도권 카메라 target, 현재 구간, 이동 주도권 방향, 리스폰 시점의 주도권자 위치, 16:9 설계 viewport 기준으로 계산한다. 먼저 생존 주도권자가 이동방향 기준 15% inset 지점에 놓이도록 camera target을 확정하고, 사망자는 같은 camera 기준 반대편 85% 지점에 둔다. 화면 좌표 기준 오른쪽 진행은 주도권자 15%/사망자 85%, 왼쪽 진행은 주도권자 85%/사망자 15%다. 구간 돌파 직후 방어자 리스폰은 첫 1회에 한해 돌파자의 현재 이동/낙하 좌표가 아니라 새 구간 진입 앵커를 seed로 사용하고 기존 viewport-forward 정책으로 배치한다. 돌파자 entry 위치는 기존 구간 전환 진입 위치를 유지한다. Y는 `roomCameraY + offset` 공식이 아니라 지형 착지점으로 보정한다. |
| 안전 Y/X 산정 | Fusion runner physics scene 우선 ground raycast + playable band guard | 네트워크 runner가 있으면 해당 runner의 2D physics scene에서 Ground raycast로 착지 Y를 확정한다. 일반 사망 리스폰과 구간 돌파 직후 방어자 첫 리스폰 모두 후보 X 아래에 안전 발판이 없으면 같은 구간 안에서 좌우 0.5u 단위로 가장 가까운 안전 X를 탐색하고, 실패 시 seed Y를 fallback으로 사용한다. 단, `cameraY - cameraSize * 0.5` 또는 seed Y 대비 과도하게 낮은 지형은 pit/하부 바닥으로 보고 리스폰·구간진입 착지점으로 쓰지 않는다. |
| 리스폰 완료 직후 | networked respawn revision + midpoint recenter | 리스폰 완료는 서버가 revision으로 확정하고, 각 클라이언트 카메라는 같은 revision을 관측해 두 플레이어 midpoint로 재정렬한다. |
| 구간 경계 clamp | 구간 camera bounds | 모든 target X는 구간 좌단·우단에서 viewport half-width를 뺀 camera bounds 안으로 clamp한다. |

## 작성 규칙

- 구간 수는 기준문서의 7구간 구조를 따른다.
- 수치는 확정값이 아니라 맵 프리팹과 플레이테스트를 연결하는 후보로 적는다.
- 카메라/경계 수치는 전투 결과를 과도하게 왜곡하지 않는지 함께 검수한다.

## 남은 작성 대기

기본 전투 구간 프리팹과 카메라 추적 로직이 연결된 뒤 작성한다.
