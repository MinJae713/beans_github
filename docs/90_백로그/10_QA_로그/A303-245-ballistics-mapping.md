# A303-245 Gun/Bullet baseline 9필드 매핑

## 범위
- 대상 코드: `Gun.cs`, `BulletController.cs`, `CardEffect.cs`
- 대상 필드: `GameplayProfile.combat.baseline` 9필드
- 기준: 런타임 참조값 단일 출처를 `GameplayProfile.combat.baseline`으로 정렬

| # | 필드 | 현재 출처 | 목표 출처 | 변경 필요 |
|---|---|---|---|---|
| 1 | `fireInterval` | `Gun.cs` -> `_profile.baseFireInterval` (fallback `0.3`) | `Gun.cs` -> `_profile.combat.baseline.fireInterval` | Y |
| 2 | `ammoCapacity` | `Gun.cs` -> `_profile.baseMagazineCapacity` (fallback `3`) | `Gun.cs` -> `_profile.combat.baseline.ammoCapacity` | Y |
| 3 | `reloadTime` | `Gun.cs` -> `_profile.baseReloadDuration` (fallback `1.5`) | `Gun.cs` -> `_profile.combat.baseline.reloadTime` | Y |
| 4 | `directHitDamage` | `BulletController.cs` -> `_profile.baseDamage` (fallback `10`) | `BulletController.cs` -> `_profile.combat.baseline.directHitDamage` | Y |
| 5 | `projectileSpeed` | `BulletController.cs` -> `_profile.baseBulletSpeed` (fallback `25`) + `CardEffect.BulletSpeedMultiplier` | `BulletController.cs` -> `_profile.combat.baseline.projectileSpeed` + `CardEffect.BulletSpeedMultiplier` | Y |
| 6 | `projectileGravityScale` | `BulletController.cs` -> `_profile.baseBulletGravityScale` (fallback `0.5`) | `BulletController.cs` -> `_profile.combat.baseline.projectileGravityScale` | Y |
| 7 | `projectileLifetimeSeconds` | `BulletController.cs` SerializeField `_lifetime` (prefab 기본 `5`) | `BulletController.cs` -> `_profile.combat.baseline.projectileLifetimeSeconds` | Y |
| 8 | `projectileBaseScale` | `BulletController.cs` 코드 상수 `0.08f` + `SizeMultiplier` | `BulletController.cs` -> `_profile.combat.baseline.projectileBaseScale` (+ 기존 `0.08f` 기준 스케일 보정) | Y |
| 9 | `directHitKnockbackImpulse` | baseline 미사용. 실제 적용은 `CardEffect.KnockbackForce` 누산값만 사용 (기본 0) | `BulletController.cs` -> `_profile.combat.baseline.directHitKnockbackImpulse` + `CardEffect.KnockbackForce` | Y |

## 최종 상태 (legacy 제거 반영)

- A303-245 후속 커밋에서 사격/탄환 legacy flat 6필드(`baseFireInterval`, `baseMagazineCapacity`, `baseReloadDuration`, `baseDamage`, `baseBulletSpeed`, `baseBulletGravityScale`) 제거 완료.
- 현재 Gun/Bullet 런타임 기준값은 `GameplayProfile.combat.baseline`만을 참조한다.

## 충돌/주의 메모
- 문서 프리셋(`01_MVP_전투_수치_프리셋.md`) 후보값과 현재 런타임 값이 일부 다름.
  - 예: `fireInterval`(0.75 vs 현재 1.0), `ammoCapacity`(6 vs 현재 3), `reloadTime`(1.40 vs 현재 1.5), `directHitDamage`(12 vs 현재 10), `projectileSpeed`(10 vs 현재 15), `projectileLifetimeSeconds`(3 vs 현재 5).
- 이번 스토리의 핵심 제약(행동 변화 0)을 우선해, 코드 정렬 이후 `.asset` 값은 현재 런타임 동작 기준으로 동결한다.

## Fusion 결정론/구현 가드레일 후속 메모(본 스토리 범위 외)
- `BulletController`에서 `Physics2D.IgnoreCollision`, `Physics2D.OverlapCircleAll` 글로벌 API 사용 중.
- `SceneSearch.FindAllInScene` 기반 전역 검색이 초기화 경로에 존재.
- 본 스토리에서는 외부화 9필드 정렬만 수행하고, 위 항목은 후속 Story에서 구조 정리 필요.
