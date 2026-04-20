---
last_updated: 2026-04-20
audience: ai-first
---

# BalanceProfile 스키마

이 문서는 Unity 프리셋 구현체가 어떤 묶음의 값을 가져야 하는지 정의한다.

이 문서는 필드 구조와 소유 경계를 다룬다. 실제 체력, 피해량, 속도, 쿨타임 같은 숫자는 이 문서에 쓰지 않는다.

## 목적

`BalanceProfile`은 전투 수치를 코드 상수에서 분리하고, 플레이테스트 목적에 따라 프리셋을 갈아끼울 수 있게 하는 데이터 계층이다.

| 목적 | 의미 |
|---|---|
| 코드 상수 제거 | 수치 실험 때문에 Runtime 코드를 계속 수정하지 않음 |
| 프리셋 교체 | 기본/플레이테스트/시연용 수치를 분리 |
| 변경 의도 기록 | 어떤 전투 양상을 검증하는지 추적 |
| 밸런스 회귀 방지 | 이전 프리셋으로 되돌릴 수 있음 |
| 에디터 생산성 | Odin/Inspector로 카드와 무기 수치를 검수 가능 |

## MVP 1차 도입 결정

MVP 1차에서는 `BalanceProfile`을 먼저 도입하고, `WeaponProfile`과 `CardEffectProfile`은 같은 구조 안에서 분리 가능한 하위 묶음으로 시작한다.

| 항목 | 기준 |
|---|---|
| 1차 목표 | 코드에 흩어진 전투 수치 상수를 `BalanceProfile` 기준으로 모음 |
| 즉시 분리하지 않는 것 | 무기별 파일, 카드별 수치 파일, 구간별 수치 파일의 과도한 세분화 |
| 분리 준비 | 필드명과 그룹은 나중에 `WeaponProfile`, `CardEffectProfile`, `SegmentProfile`로 떼어낼 수 있게 둠 |
| 금지 | 수치 실험을 위해 Runtime 코드 상수를 직접 수정 |

따라서 구현자는 처음부터 완벽한 프리셋 계층을 만들기보다, 코드 상수 제거와 프리셋 교체 가능성을 먼저 확보한다.

## 권장 필드 묶음

| 묶음 | 포함 대상 |
|---|---|
| profile metadata | 이름, 목적, 작성자, 상태, 관련 테스트 |
| player movement | 이동, 점프, 공중 제어, 대시 후보 |
| player survivability | 체력, 리스폰 자원, 보호 상태 후보 |
| weapon baseline | 기본 무기 발사, 장탄, 재장전, 투사체 기준 |
| projectile behavior | 탄속, 수명, 충돌, 반사, 폭발 후보 |
| knockback/stun | 넉백, 경직, 회복, 입력 잠금 후보 |
| card modifiers | 카드 효과가 참조할 배율/가산/토글 후보 |
| round flow | 선택 제한, 라운드 전환, 리스폰 재정렬 후보 |
| level interaction | 구조물 피해, 위험 지형, 이동 발판 후보 |

위 표는 값의 종류를 정의하는 것이지, 모든 필드를 MVP에 즉시 만들라는 뜻이 아니다. MVP 구현에 필요한 묶음부터 만들고 나머지는 확장 슬롯으로 둔다.

## MVP 1차 필드 스키마

아래 스키마는 필드의 역할만 정의한다. 실제 숫자는 프리셋 에셋에서 관리한다.

| 그룹 | 필드 역할 | 1차 포함 여부 |
|---|---|---|
| metadata | 프리셋 이름, 의도, 상태, 작성자, 관련 테스트 | 필수 |
| movement | 이동, 점프, 공중 제어, 입력 잠금 후보 | 필수 |
| survivability | 체력, 사망 후 보호, 리스폰 자원 초기화 후보 | 필수 |
| weapon baseline | 발사 간격, 장탄, 재장전, 기본 투사체 후보 | 필수 |
| projectile baseline | 탄속, 수명, 충돌 반응, 기본 넉백 후보 | 필수 |
| card limits | 티어 제한, maxed 판정, 후보 제외 기준 | 필수 |
| round flow | 선택 제한 시간, 자동 선택 정책 후보, 리스폰 보호 후보 | 필수 |
| structure interaction | 낙사, 추, 반사체, 위험 지형 반응 후보 | 선택 |
| feedback tuning | 화면 흔들림, 히트스톱, 사운드 강도 후보 | 별도 Presentation 프리셋 우선 |

`feedback tuning`은 전투 결과에 영향을 주지 않는 presentation 값이다. 전투 결과 수치와 같은 프리셋에 섞더라도 네트워크 truth에 사용하지 않는다.

## 분리 단계

| 단계 | 기준 |
|---|---|
| 1단계 | `BalanceProfile` 하나로 전투 수치 상수를 모은다 |
| 2단계 | 무기 후보가 늘어나면 `WeaponProfile` 또는 `WeaponDefinition`으로 분리한다 |
| 3단계 | 카드 효과가 조합 충돌을 만들기 시작하면 `CardEffectProfile`로 분리한다 |
| 4단계 | 구간별 위험 지형과 카메라 기준이 늘어나면 `SegmentProfile`로 분리한다 |

분리 기준은 파일 수가 아니라 책임 충돌이다. 한 사람이 한 프리팹이나 ScriptableObject를 계속 건드려 충돌이 생기면 분리한다.

## 데이터 소유권

| 데이터 | 권장 소유 위치 |
|---|---|
| 전역 전투 기준 | BalanceProfile |
| 무기별 고유 속성 | WeaponProfile 또는 WeaponDefinition |
| 카드 효과 파라미터 | CardDefinition 또는 CardEffectProfile |
| 구간별 환경 값 | Level/Segment Preset |
| 입력 키 매핑 | Input Actions 또는 별도 Input Preset |
| VFX/SFX 강도 | Presentation/Feedback Preset |

전역 기준과 개별 무기/카드/구간 값을 한 파일에 모두 몰아넣지 않는다. 단, MVP 초기에는 구현 단순화를 위해 하나의 ScriptableObject에서 시작하되, 필드 묶음과 분리 가능성을 유지한다.

## 상태값

| 상태 | 의미 |
|---|---|
| experimental | 검증되지 않은 실험용 |
| playtest | 특정 가설 검증용 |
| baseline | 현재 기본 구현 기준 |
| demo | 발표/시연 안정성 우선 |
| deprecated | 더 이상 사용하지 않지만 비교용으로 보존 |

상태값은 프리셋 파일의 의미를 나타낸다. `baseline`이라고 해서 기준문서의 `accepted`와 같은 뜻은 아니다.

## 구현 규칙

- Runtime 코드는 `BalanceProfile` 또는 하위 프리셋을 참조한다.
- 임시 실험값도 코드 상수로 박지 않고 실험 프리셋으로 만든다.
- 프리셋 필드는 역할별로 그룹화한다.
- 값 변경은 가능한 한 새 프리셋 생성 또는 버전 변경으로 남긴다.
- 플레이테스트 리포트는 어떤 프리셋을 사용했는지 기록한다.
- 네트워크 결과에 영향을 주는 값은 StateAuthority 기준으로 동일하게 읽혀야 한다.

## 검증 규칙

| 검증 | 기준 |
|---|---|
| 필수 참조 | 전투 Runtime이 null profile로 동작하지 않음 |
| 코드 상수 검사 | 전투 수치가 Runtime 코드에 흩어져 있지 않음 |
| 프리셋 기록 | 플레이테스트 결과가 사용 프리셋과 연결됨 |
| 네트워크 일관성 | Host/Client가 결과 수치를 다르게 해석하지 않음 |
| 되돌림 가능성 | 이전 baseline 또는 demo 프리셋으로 복귀 가능 |

## 금지

- 기준문서와 구현 코드에 같은 수치를 중복 관리하는 것.
- 플레이테스트용 값을 기본 프리셋에 바로 덮어쓰는 것.
- 카드/무기 설명 문서에 실제 수치를 최종값처럼 복사하는 것.
- 클라이언트마다 다른 로컬 프리셋으로 네트워크 결과를 확정하는 것.
- Inspector 편의를 이유로 전투 규칙과 VFX 연출 수치를 무분별하게 섞는 것.

## 미결정

- 프리셋 파일의 실제 Unity 폴더명.
- 플레이테스트 프리셋의 명명 규칙.
