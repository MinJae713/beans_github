---
last_updated: 2026-04-21
audience: ai-first
---

# MVP 카드 가중치 프리셋

이 문서는 카드 3장 제시 정책에 필요한 가중치와 제한 후보를 기록하기 위한 자리다.

카드 제시 원칙은 확정되어 있지만, 실제 등장 가중치와 티어별 세부 수치는 플레이테스트 전 확정하지 않는다.

## 스키마-only 커밋 TODO (T9/D56, 2026-04-27 마감)

> W2 Exit ⑥ (SCHEDULE.md) 스키마-only 커밋 예정. 담당 김범승. 아래 필드 시그니처(이름·타입·단위)만 잠금, 구체 수치는 W3~W4 플레이테스트 후 채운다. 본 문서는 현재 "작성 대기" 상태이며, 구현자는 이 섹션을 binding으로 읽지 않는다.

## 확정된 상위 원칙

| 항목 | 기준 |
|---|---|
| 제시 수 | 3장 |
| 같은 제시 내 중복 | 제외 |
| maxed 카드 | 제외 |
| 티어 제한 초과 카드 | 제외 |
| 등급 해금 | 모든 등급은 처음부터 등장 가능 |
| 시간 만료 | 제시 후보 중 StateAuthority가 랜덤 자동 선택 |

## 관리 대상

| 항목 | 설명 |
|---|---|
| rarity weight | 일반, 희귀, 에픽, 레전더리 등장 후보 가중치 |
| tier cap | 티어별 보유 제한 후보 |
| card max stack | 카드별 최대 누적 후보 |
| exclusive tag | 같이 나오거나 같이 보유하면 안 되는 카드 태그 |
| offer fallback | 후보 부족 시 대체 제시 규칙 |

## D46 카드 선택 deadline 스키마 (T9 스키마-only 대상)

> 정본 정의: [`40_카드_무기_설계/30_구현가이드/01_카드_선택_네트워크_핸드셰이크.md §선택 deadline 정책`](../../40_카드_무기_설계/30_구현가이드/01_카드_선택_네트워크_핸드셰이크.md). 본 섹션은 프리셋 스키마 매핑만 잠그고, 구체 수치는 W3~W4 실측 후 확정.

| 필드 | 타입 | 단위 | 초기 후보값 | 설명 |
|---|---|---|---|---|
| `visibleChoiceDuration` | float | 초 | 10.0 | 플레이어 카운트다운 시간 |
| `serverAcceptPadding` | float | 초 | 0.3 | RTT 흡수 버퍼 (Photon KR p95 300ms 기준) |
| `hardDeadlineTickCalculation` | 수식 | tick | — | `visibleDeadlineTick + ceil(serverAcceptPadding / tickInterval)` |
| `choiceSeedSource` | enum | — | (미정) | `matchSeed+roundCounter` / `networkedRandom` 중 선택. 라운드 ChoicePending 진입 시 `[Networked]`로 브로드캐스트 |
| `sameTickRacePolicy` | enum | — | `player_choice_wins` | D46 확정. 타임아웃 이벤트는 폐기 |

## 작성 대기

카드 후보 데이터가 Unity 구현 데이터로 옮겨진 뒤, 첫 드래프트 테스트 결과를 바탕으로 작성한다. 위 스키마 필드의 구체 수치는 W3 원격 RTT 실측과 `70/07` 관찰 결과를 반영해 확정한다.
