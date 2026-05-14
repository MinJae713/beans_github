---
last_updated: 2026-04-28
audience: ai-first
---

# GameplayProfile 스키마

> 상태: candidate. 이 문서는 `GameplayProfile`의 필드 구조와 책임 경계를 제안하는 후보 기준이다. 실제 Unity 클래스명, 에셋 경로, baseline 승격 여부는 별도 구현과 플레이테스트 결과로 확정한다.

이 문서는 Unity에서 사용할 `GameplayProfile` 프리셋이 어떤 게임플레이 수치와 규칙 묶음을 가져야 하는지 정의한다.

`GameplayProfile`은 게임플레이에 영향을 주는 수치와 규칙을 담는 프리셋 에셋 또는 데이터 파일의 상위 이름이다. `combat`은 그 안에서 전투 결과에 직접 영향을 주는 수치와 규칙 묶음이다.

`GameplayProfile`은 전투 엔티티를 대체하지 않는다. `Gun`, `BulletController`, `PlayerHealth`, `CardDeck`, `RoomManager` 같은 런타임 객체는 각자의 책임을 유지하고, `GameplayProfile`은 이 객체들이 참조할 수치와 정책의 출처가 된다.

이 문서는 필드 구조와 소유 경계를 다룬다. 실제 체력, 피해량, 속도, 쿨타임 같은 숫자의 후보는 `20_수치프리셋` 문서에 기록하고, 구현 시에는 그 후보를 바탕으로 Unity `GameplayProfile` 에셋 또는 동등한 데이터 파일에 값을 둔다.

## 목적

`GameplayProfile`은 게임플레이 수치와 규칙을 코드 상수에서 분리하고, 플레이테스트 목적에 따라 프리셋을 갈아끼울 수 있게 하는 데이터 계층이다.

| 목적 | 의미 |
|---|---|
| 코드 상수 제거 | 수치 실험 때문에 Runtime 코드를 계속 수정하지 않음 |
| 프리셋 교체 | 기본/플레이테스트/시연용 수치를 분리 |
| 변경 의도 기록 | 어떤 전투 양상을 검증하는지 추적 |
| 수치 회귀 방지 | 이전 프리셋으로 되돌릴 수 있음 |
| 에디터 생산성 | Inspector에서 전투 수치 후보를 검수 가능 |

## 책임 경계 용어

아래 용어는 `GameplayProfile`에 모든 데이터를 넣기 위한 목록이 아니다. 어떤 데이터가 `GameplayProfile`에 속하고, 어떤 데이터가 다른 계층으로 빠져야 하는지 구분하기 위한 책임 경계다.

| 용어 | 의미 |
|---|---|
| `GameplayProfile` | Unity에서 교체 가능한 게임플레이 수치/규칙 프리셋 에셋 또는 데이터 파일 |
| `combat` | 체력, 기본탄, 장전, 전역 상한/하한, 충돌 조합 규칙 같은 전투 수치 묶음 |
| `CardDefinition.mechanics` | 카드별 효과 수치와 규칙 |
| `CardEffectProfile` | 카드 효과 데이터가 커졌을 때 분리할 수 있는 후보 |
| `CardOfferProfile` | 카드 제안 수, 희귀도 가중치, eligible pool 정책이 커졌을 때 분리할 수 있는 후보 |
| `PresentationResolver` | 아이콘, VFX, SFX, HUD 표시 같은 표현 리소스를 결정하는 계층 |

`GameplayProfile`이라는 이름은 파일이나 에셋 계층을 부르는 이름으로 사용한다. 기본 전투 수치 자체를 설명할 때는 `combat.baseline`처럼 더 좁은 하위 그룹 이름을 쓴다.

## MVP 1차 도입 결정

MVP 1차에서는 `GameplayProfile` 하나를 먼저 도입하되, 내부 필드 묶음은 나중에 분리할 수 있게 둔다.

| 항목 | 기준 |
|---|---|
| 1차 목표 | 코드에 흩어진 전투 수치 상수를 프리셋 기준으로 모음 |
| 즉시 분리하지 않는 것 | 무기별 파일, 카드별 수치 파일, 구간별 수치 파일의 과도한 세분화 |
| 분리 준비 | 필드명과 그룹은 나중에 `WeaponProfile`, `CardEffectProfile`, `SegmentProfile`로 떼어낼 수 있게 둠 |
| 금지 | 수치 실험을 위해 Runtime 코드 상수를 직접 수정 |

따라서 구현자는 처음부터 완벽한 프리셋 계층을 만들기보다, 코드 상수 제거와 프리셋 교체 가능성을 먼저 확보한다.

MVP 1차 구현에서는 `metadata`와 `combat`을 필수로 둔다. 단, 런타임 전투 수치 적용은 `combat`부터 시작한다. `cardOffer`와 `segment`는 `GameplayProfile` 안에 구조를 예약하되, 별도 이슈에서 확정되기 전까지는 미적용 가능 필드로 둔다.

## 권장 최상위 구조

아래 구조는 역할 구분을 위한 스키마 후보이며, 모든 하위 그룹을 MVP에서 즉시 구현하라는 뜻은 아니다.

```text
GameplayProfile
  metadata
  combat
    baseline
    resolvedCaps
    combinationRules
  cardOffer
  segment
```

| 그룹 | 역할 | MVP 1차 포함 |
|---|---|---|
| `metadata` | 프리셋 이름, 목적, 상태, 작성자, 관련 테스트 | 필수 |
| `combat.baseline` | 카드가 없는 기본 전투 기준값 | 필수 |
| `combat.resolvedCaps` | 모든 카드 조합 계산 후 적용하는 전역 전투 스탯 상한/하한 | 필수 |
| `combat.combinationRules` | 직격, 폭발, 상태이상, 반사, 관통이 겹칠 때의 전역 계산 규칙 | 필수 |
| `cardOffer` | 카드 제시 수, 희귀도 가중치, maxStack 도달 카드 제외 정책 | 별도 이슈 |
| `segment` | 구간 길이, 경계, 리스폰, 카메라 압박 등 레벨 수치 | 별도 이슈 |

## combat 필드 경계

`combat`은 전투 결과가 달라지는 값을 다룬다. 표현 리소스, UI 배치, 사운드 강도는 여기에 넣지 않는다.

| 하위 그룹 | 포함 대상 |
|---|---|
| `baseline` | 기본 체력, 기본 직접 피해, 기본 탄속, 기본 중력, 기본 탄환 수명, 기본 탄환 크기, 기본 발사 간격, 기본 장탄수, 기본 재장전 시간, 리스폰 피격 무적, 플레이어 이동 속도, 플레이어 점프력, 코요테 시간, 점프 입력 버퍼 시간, 가변 점프 컷 비율, 낙하 중력 배율, 정점 hang 임계 속도, 정점 중력 배율, 공중 좌우 제어 배율 |
| `resolvedCaps` | 최종 탄환 크기, 최종 탄속, 최종 장전 시간, 최종 발사 간격, 최종 넉백, 최종 폭발 반경, 최종 경직/둔화 상한 |
| `combinationRules` | 같은 탄환의 같은 대상 재타격 조건, 물리 중복 이벤트 안전값, 다중 탄환 상태이상 적용, 폭발 상태이상 적용, 피해/넉백 합산 방식 |

## 소유권

| 데이터 | 권장 소유 위치 |
|---|---|
| 카드가 없는 기본 전투 수치 | `GameplayProfile.combat.baseline` |
| 카드 조합 후 최종 전투 스탯 상한/하한 | `GameplayProfile.combat.resolvedCaps` |
| 직격/폭발/상태이상/반사/관통 조합 규칙 | `GameplayProfile.combat.combinationRules` |
| 카드별 효과 파라미터 | `CardDefinition.mechanics` 또는 `CardEffectProfile` |
| 카드 제안 정책 | `cardOffer` 또는 `CardOfferProfile` |
| 구간별 환경 값 | `GameplayProfile.segment`, `SegmentProfile` 또는 Level Preset |
| 입력 키 매핑 | Input Actions 또는 별도 Input Preset |
| VFX/SFX/아이콘/HUD 표시 | `PresentationResolver` 또는 Feedback/Presentation Profile |

전역 기준과 개별 무기/카드/구간 값을 한 파일에 모두 몰아넣지 않는다. 단, MVP 초기에는 구현 단순화를 위해 하나의 ScriptableObject에서 시작하되, 필드 묶음과 분리 가능성을 유지한다.

## 상태값

| 상태 | 의미 |
|---|---|
| `field_candidate` | 필드명과 책임 위치만 제안한 상태 |
| `value_candidate` | 초기 수치 후보를 제안한 상태 |
| `playtest_needed` | 수치는 있으나 구현 후 플레이테스트 검증이 필요한 상태 |
| `baseline_candidate` | 기본 플레이 기준으로 승격 가능한 후보 |
| `demo_candidate` | 발표/시연용 고정값 후보 |
| `deprecated` | 더 이상 사용하지 않지만 비교용으로 보존 |

`baseline_candidate`라고 해서 기준문서의 `accepted`와 같은 뜻은 아니다. 수치 후보는 플레이테스트와 변경로그를 거쳐 프리셋 상태가 올라간다.

## 분리 단계

| 단계 | 기준 |
|---|---|
| 1단계 | `GameplayProfile` 하나로 전투 수치 상수를 모은다 |
| 2단계 | 무기 후보가 늘어나면 `WeaponProfile` 또는 `WeaponDefinition`으로 분리한다 |
| 3단계 | 카드 효과가 조합 충돌을 만들기 시작하면 `CardEffectProfile`로 분리한다 |
| 4단계 | 카드 제안 정책이 모드별로 갈라지면 `CardOfferProfile`로 분리한다 |
| 5단계 | 구간별 위험 지형과 카메라 기준이 늘어나면 `SegmentProfile`로 분리한다 |

분리 기준은 파일 수가 아니라 책임 충돌이다. 한 사람이 한 프리팹이나 ScriptableObject를 계속 건드려 충돌이 생기면 분리한다.

## 구현 규칙

- Runtime 코드는 `GameplayProfile` 또는 하위 프리셋을 참조한다.
- 임시 실험값도 코드 상수로 박지 않고 실험 프리셋으로 만든다.
- 프리셋 필드는 역할별로 그룹화한다.
- 값 변경은 가능한 한 새 프리셋 생성 또는 버전 변경으로 남긴다.
- 플레이테스트 리포트는 어떤 프리셋을 사용했는지 기록한다.
- 네트워크 결과에 영향을 주는 값은 StateAuthority 기준으로 동일하게 읽혀야 한다.
- 표현 리소스는 전투 결과를 바꾸지 않는 한 `GameplayProfile.combat`에서 관리하지 않는다.

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
- 클라이언트별 개별 프리셋으로 네트워크 결과를 확정하는 것.
- Inspector 편의를 이유로 전투 규칙과 VFX 연출 수치를 무분별하게 섞는 것.

## 미결정

- 프리셋 파일의 실제 Unity 폴더명.
- 플레이테스트 프리셋의 명명 규칙.
- `GameplayProfile`의 실제 Unity 클래스명과 에셋 생성 메뉴명.

## A303-245 정합 스냅샷 (Gun/Bullet baseline 9필드)

아래 표는 `GameplayProfile.combat.baseline`의 사격/탄환 9필드를 코드 소비 지점과 1:1로 맞춘 현재 스냅샷이다.

| 필드 | 타입 | 단위 | 현재 기본값 | 코드 소비 지점 |
|---|---|---|---:|---|
| `baseline.fireInterval` | float | seconds | 1.0 | `Gun.FireInterval` |
| `baseline.ammoCapacity` | int | shots | 3 | `Gun.MagazineCapacity` |
| `baseline.reloadTime` | float | seconds | 1.5 | `Gun.ReloadDurationBase` |
| `baseline.directHitDamage` | float | hp | 10 | `BulletController.DirectHitDamage` |
| `baseline.projectileSpeed` | float | world unit/sec | 23 | `BulletController.ProjectileSpeed` |
| `baseline.projectileGravityScale` | float | multiplier | 2.7 | `BulletController.ProjectileGravityScale` |
| `baseline.projectileLifetimeSeconds` | float | seconds | 5.0 | `BulletController.ProjectileLifetimeSeconds` |
| `baseline.projectileBaseScale` | float | multiplier | 1.0 | `BulletController.ProjectileBaseScale` |
| `baseline.directHitKnockbackImpulse` | float | physics impulse | 0.0 | `BulletController.DirectHitKnockbackImpulse` |

메모:
- 카드 배율은 baseline 값을 기준으로 계산한다. 예: 탄속은 `projectileSpeed * BulletSpeedMultiplier`.
- 넉백은 `directHitKnockbackImpulse + KnockbackForce`를 사용한다.
- 값 출처는 `unity/Assets/_Project/Data/GameplayProfile.asset`의 `combat.baseline`이다.

## A303-317 정합 스냅샷 (PlayerMovement baseline 8필드)

아래 표는 `GameplayProfile.combat.baseline`의 이동/점프 8필드를 코드 소비 지점과 1:1로 맞춘 현재 스냅샷이다. A303-317에서 `coyoteTimeSeconds`와 `airControlMultiplier`를 폐기하고 `ascendGravityMultiplier`를 신규 추가했다. 점프 가능 여부는 `PlayerController.JumpsUsed`(networked sbyte)와 `PlayerMovement.MaxJumps`(상수)로 관리한다.

| 필드 | 타입 | 단위 | 현재 기본값 | 코드 소비 지점 |
|---|---|---|---:|---|
| `baseline.moveSpeed` | float | world unit/sec | 7.0 | `PlayerMovement.MoveSpeed` |
| `baseline.jumpForce` | float | world unit/sec | 21.0 | `PlayerMovement.JumpForce` |
| `baseline.jumpBufferSeconds` | float | seconds | 0.1 | `PlayerMovement.JumpBufferSeconds` |
| `baseline.variableJumpCutFactor` | float (0~1) | multiplier | 0.65 | `PlayerMovement.VariableJumpCutFactor` |
| `baseline.fallGravityMultiplier` | float | multiplier | 2.0 | `PlayerMovement.FallGravityMultiplier` |
| `baseline.apexHangThreshold` | float | world unit/sec | 0.8 | `PlayerMovement.ApexHangThreshold` |
| `baseline.apexGravityMultiplier` | float (0~1) | multiplier | 0.4 | `PlayerMovement.ApexGravityMultiplier` |
| `baseline.ascendGravityMultiplier` | float | multiplier | 2.5 | `PlayerMovement.AscendGravityMultiplier` |

## Legacy Flat 필드 현황 (A303-245)

- 제거 완료: `baseFireInterval`, `baseMagazineCapacity`, `baseReloadDuration`, `baseDamage`, `baseBulletSpeed`, `baseBulletGravityScale`
- 임시 유지: `maxHealth`, `respawnDelaySeconds`
- 유지 사유: `PlayerHealth`가 아직 `_profile.maxHealth` / `_profile.respawnDelaySeconds`를 직접 참조한다.
- 제거 조건: `PlayerHealth`가 `combat.baseline.maxHealth` / `combat.baseline.respawnDelaySeconds` 직접 참조로 전환되는 후속 Story에서 제거.
