---
last_updated: 2026-04-28
audience: ai-first
---

# MVP 전투 수치 프리셋

> 상태: candidate. 이 문서는 MVP 전투 수치와 전역 조합 규칙의 1차 후보를 기록한다. 수치와 정책은 플레이테스트 전까지 `baseline` 또는 `demo` 프리셋으로 확정하지 않는다.

이 문서는 MVP 기본 전투 수치와 전역 전투 스탯 상한/하한의 1차 후보를 기록한다.

여기의 숫자는 구현과 플레이테스트를 시작하기 위한 후보값이다. 플레이테스트 없이 `baseline` 또는 `demo` 프리셋으로 승격하지 않는다.

## 문서 범위

이 문서는 `S14P31A303-209` 범위의 기본 전투 프리셋 필드만 다룬다.

| 포함 | 제외 |
|---|---|
| 카드가 없는 기본 전투 수치 | 카드별 효과 수치 |
| 전투 스탯 상한/하한 | 카드 제안 가중치 |
| 직격/폭발/상태이상/반사/관통 조합 규칙 | 구간 길이, 카메라, 리스폰 위치 |
| 구현 안전을 위한 물리 중복 이벤트 방지값 | VFX, SFX, 아이콘, HUD 배치 |

카드별 효과 수치는 `CardDefinition.mechanics` 또는 추후 `CardEffectProfile`에서 관리한다. 이 문서는 카드 수치를 확정하는 문서가 아니라, 카드 수치를 계산할 때 기준이 되는 기본 전투값과 전역 안전장치를 관리한다.

## 상태 기준

| 상태 | 의미 |
|---|---|
| `field_candidate` | 필드명과 책임 위치만 제안한 상태 |
| `value_candidate` | 초기 수치 후보를 제안한 상태 |
| `playtest_needed` | 수치는 있으나 구현 후 체감 검증이 필요한 상태 |
| `baseline_candidate` | 플레이테스트 후 기본 프리셋 승격 후보 |
| `demo_candidate` | 발표/시연용 프리셋 승격 후보 |

## 기본 전투 baseline 후보

`GameplayProfile.combat.baseline`은 카드가 하나도 없을 때의 전투 기준값이다. 이 값이 흔들리면 카드 수치의 의미도 같이 흔들리므로, 카드별 mechanics 수치와 분리해 관리한다.

필드명은 `baseline` 아래에 들어간다는 전제를 둔다. 따라서 `baseMaxHealth`, `baseDirectDamage`처럼 `base`를 반복하지 않고 `maxHealth`, `directHitDamage`처럼 적는다.

| 필드 | 타입 | 단위 | 초기 후보 | 상태 | 의도 |
|---|---|---|---:|---|---|
| `baseline.maxHealth` | float | hp | 100 | value_candidate | 피해 비율과 상태이상 총 피해를 읽기 쉽게 한다. |
| `baseline.directHitDamage` | float | hp | 20 | value_candidate | 기본 교전이 늘어진다는 피드백 반영. |
| `baseline.projectileSpeed` | float | world unit/sec | 32.2 | value_candidate | 플레이테스트에서 탄속 압박감을 보강한 현재 후보값. |
| `baseline.projectileGravityScale` | float | multiplier | 1.0 | value_candidate | 기본 탄환은 중력 영향을 받는 곡사탄으로 둔다. |
| `baseline.projectileLifetimeSeconds` | float | seconds | 3.0 | value_candidate | 발사 후 남아 있을 수 있는 기본 탄환 수명. |
| `baseline.projectileBaseScale` | float | multiplier | 2.0 | value_candidate | 카드 크기 배율을 적용하기 전 탄환 기준 크기. |
| `baseline.fireInterval` | float | seconds | 0.5 | value_candidate | 기본 사격 리듬. |
| `baseline.reloadTime` | float | seconds | 1.5 | value_candidate | 장전 리듬 카드의 체감을 만들 수 있는 기준값. |
| `baseline.ammoCapacity` | int | shots | 6 | value_candidate | 기본 피해 상향 후 한 탄창 전탄 명중 시 풀피 대상을 처치할 수 있다. |
| `baseline.directHitKnockbackImpulse` | float | physics impulse | TBD | field_candidate | 명중 순간 충격량. 이동 물리와 경계 돌파 판정 단위가 나온 뒤 정한다. |
| `baseline.respawnInvulnerableSeconds` | float | seconds | 1.0 | value_candidate | 리스폰 직후 억울한 피격 방지. |
| `baseline.moveSpeed` | float | world unit/sec | 7.0 | value_candidate | 플레이어 좌우 이동 속도(지상·공중 동일). 카드 `moveSpeedMultiplier` 적용 전 값. |
| `baseline.jumpForce` | float | world unit/sec | 21.0 | value_candidate | 점프 발사 순간 부여하는 수직 초기 속도. 카드 `jumpForceMultiplier` 적용 전 값. |
| `baseline.jumpBufferSeconds` | float | seconds | 0.10 | value_candidate | 공중에서 미리 누른 점프 입력을 착지 시 자동으로 사용하는 선입력 시간. 0이면 비활성. |
| `baseline.variableJumpCutFactor` | float (0~1) | multiplier | 0.65 | value_candidate | 점프 키를 떼는 순간 상승 속도에 곱하는 비율. 1이면 풀 점프, 0이면 즉시 정지. |
| `baseline.fallGravityMultiplier` | float | multiplier | 2.0 | value_candidate | 떨어지는 중(y속도 음수)에 적용하는 중력 배율. 평소(1.0) 대비 떨어지는 답답함을 줄인다. |
| `baseline.apexHangThreshold` | float | world unit/sec | 0.8 | value_candidate | 점프 정점 부근으로 간주하는 |y속도| 임계값. 이 값 미만 상승 중일 때 정점 중력 배율을 적용한다. |
| `baseline.apexGravityMultiplier` | float (0~1) | multiplier | 0.40 | value_candidate | 정점 부근에서 적용하는 중력 배율. 1.0 미만이면 정점에서 잠깐 떠 있는 느낌이 생긴다. |
| `baseline.ascendGravityMultiplier` | float | multiplier | 2.5 | value_candidate | 상승 중(y속도가 정점 임계값 이상)에 적용하는 중력 배율. 1.0보다 크면 상승 시간이 짧아진다. |

## baseline 해석

`maxHealth: 100`은 독, 화상, 폭발 피해를 검토하기 위한 기준점이다. 실제 UI에서 체력 숫자를 그대로 노출한다는 뜻은 아니다.

`directHitDamage: 20`이면 기본탄만으로는 5회 직접 명중이 필요하다.

`ammoCapacity: 6` 기준 한 탄창 전탄 명중 피해는 120이다. 플레이테스트 피드백상 기본 피해가 낮아 교전이 늘어지는 문제가 있어, 현재 후보는 한 탄창을 모두 맞히면 풀피 대상을 처치할 수 있게 둔다.

이는 배포 직전 플레이테스트 후보값이며, 장전/조준/이동 실패가 섞이는 실제 TTK를 기준으로 재평가한다. 카드 누적 성장 이후 TTK 단축은 허용된다.

`projectileSpeed: 32.2`는 기본탄 회피 가능성을 남기면서도 교전 지연을 줄이기 위한 현재 플레이테스트 후보값이다.

`projectileGravityScale`은 기본 탄환이 중력 영향을 받는다는 뜻이다. 현재 런타임에서는 `CardEffect.GravityModifier`를 baseline에 더한 뒤 0 이상으로 clamp한다. 안티-그래비티처럼 직선탄을 만드는 카드는 현재 MVP profile의 baseline을 상쇄하는 음수 modifier를 사용하며, `Anti Gravity.asset`은 직선탄 체감 강화를 위해 `speedMultiplier: 1.3`을 함께 사용한다.

상태이상 DoT는 첫 적용 즉시 피해를 주지 않고 `tickInterval` 경과 후부터 피해를 준다. 만료와 같은 simulation tick에 마지막 interval이 도달하면 피해를 먼저 처리한 뒤 만료한다. 현재 MVP16 SO 기준 Burn은 3초 동안 0.5초마다 6회 tick, Poison은 3초 동안 1초마다 3회 tick이다. tick 피해는 상태탄 카드 장수와 피격 대상의 상태이상 runtime stack을 함께 본다.

| 상태 | 카드 장수 | 1스택 tick 피해 | 2스택 tick 피해 | 3스택 tick 피해 |
|---|---:|---:|---:|---:|
| 화염탄 | 1장 | 3 | 6 | 9 |
| 화염탄 | 2장 | 6 | 9 | 12 |
| 화염탄 | 3장 | 9 | 12 | 15 |
| 중독탄 | 1장 | 6 | 12 | 18 |
| 중독탄 | 2장 | 12 | 18 | 24 |
| 중독탄 | 3장 | 18 | 24 | 30 |

`projectileLifetimeSeconds: 3.0`은 발사된 기본 탄환이 충돌 없이 남아 있을 수 있는 최대 시간이다. 반사, 관통, 지연 폭발처럼 수명을 바꾸는 카드가 붙기 전 기준값이며, 네트워크 오브젝트와 물리 이벤트가 무한히 남지 않게 하는 안전 기준이기도 하다.

`projectileBaseScale: 2.0`은 카드의 `sizeMultiplier`나 후속 크기 보정이 적용되기 전 탄환 프리팹 기준 크기다. 이 값은 표현 리소스의 원본 크기를 바꾸는 값이 아니라, 전투 판정에 쓰이는 탄환 크기 계산의 기준 배율이다. 대형탄은 `sizeMultiplier: 2.0`을 카드 1장당 `+1.0` 선형 step으로 해석하므로 1장 2배, 2장 3배, 3장 4배가 된다. 중량탄은 `sizeMultiplier: 1.3`을 카드 1장당 `+0.3` 선형 step으로 해석하므로 1장 1.3배, 2장 1.6배, 3장 1.9배가 된다. 코드에는 별도 hard cap을 두지 않고 카드 제안/획득 정책의 maxStack을 따른다.

`fireInterval: 0.5`는 기본탄 기준 이론상 6발 전탄 명중까지 최소 2.5초가 걸리는 리듬이다. `directHitDamage: 20` 기준 풀피 처치에는 5발 직접 명중이 필요하므로 한 탄창 안에서 처치가 가능하다. 실제 TTK는 조준, 이동, 장전, 넉백 때문에 더 길어진다.

`directHitKnockbackImpulse`는 숫자를 지금 확정하지 않는다. 경계 돌파 판정과 캐릭터 이동 물리가 먼저 나와야 의미 있는 단위로 정할 수 있다.

`respawnInvulnerableSeconds: 1.0`은 리스폰 직후 피격을 막기 위한 시간값이다. 공격 시 무적 해제 같은 별도 cancel policy는 두지 않는다.

## 플레이어 이동·점프 baseline 해석

`moveSpeed: 7.0`은 카드 효과가 없는 상태의 플레이어 좌우 이동 속도다. A303-317에서 공중 관성 로직을 폐기해 지상과 공중 모두 같은 속도로 즉시 반응한다. 카드 `moveSpeedMultiplier`가 곱해지기 전 값이며, `resolvedCaps.statusSlowMax: 0.30`은 이 값에 대한 감소율 상한이다.

`jumpForce: 21.0`은 점프 발사 순간 수직 초기 속도다. 점프 높이 H는 대략 `H = jumpForce² / (2 × g × ascendGravityMultiplier)`로 계산되므로, 상승 중력 배율(아래 항목)과 함께 점프 곡선을 결정한다. 점프력 카드는 이 초기 속도에만 곱해지며, 버퍼/가변 컷/상승·정점·낙하 가속 같은 점프 감각 파라미터는 카드 영향을 받지 않는다.

점프 가능 여부는 횟수 기반으로 관리한다. `PlayerController.JumpsUsed`(networked sbyte)가 0이면 지상이든 공중이든 점프 가능, `PlayerMovement.MaxJumps`(상수 1) 도달 시 추가 점프 차단. 지상 도달 시 자동으로 0으로 되돌린다. 절벽에서 그냥 떨어진 직후(JumpsUsed=0)에도 첫 점프는 항상 허용된다. 기존 `coyoteTimeSeconds` 필드는 A303-317에서 폐기.

`jumpBufferSeconds: 0.10`은 공중에서 미리 누른 점프 입력을 착지 직후 자동으로 사용하는 선입력 허용 시간이다. 착지 타이밍을 정확히 맞추지 않아도 점프가 이어지게 해 입력 손실을 줄인다. 0초면 비활성. 의도상 "공중 선입력만 비활성"이며 지상 직접 점프(같은 tick에 키 누름)는 항상 허용된다.

`variableJumpCutFactor: 0.65`는 점프 키를 떼는 순간 상승 속도에 곱하는 비율이다. 1이면 풀 점프(키 길이 무관), 0.65면 65% 지점에서 끊김, 0이면 즉시 정지. 짧은 점프와 긴 점프를 의도적으로 구분할 수 있는 핵심 파라미터다. A303-317 튜닝에서 0.50 → 0.65로 올려 짧은 점프 높이를 약 70% 키워 도약감을 보강했다.

`fallGravityMultiplier: 2.0`은 떨어지는 중(y속도 음수)에 추가로 곱하는 중력 배율이다. 1.0(평소)보다 크게 두면 점프가 가볍게 통통 튀는 느낌이 나고, 떨어지면서 답답함이 줄어든다. 1.5~2.5가 일반적인 플랫포머 권장 범위다.

`apexHangThreshold: 0.8`와 `apexGravityMultiplier: 0.40`은 점프 정점 hang time 처리를 위한 한 쌍이다. |y속도|가 임계값(0.8 m/s) 미만으로 상승 중일 때 중력을 정점 배율(0.4)로 약화해, 정점 부근에서 잠깐 떠 있는 느낌을 만든다.

`ascendGravityMultiplier: 2.5`는 상승 중(y속도가 정점 임계값 이상)에 적용하는 중력 배율이다. 1.0보다 크게 두면 정점까지 도달하는 시간이 짧아진다. A303-317에서 `jumpForce`를 21로 키운 만큼 상승 시간이 늘어나는 부작용을 상쇄하기 위해 신규 도입했다. 결과적으로 "최고 점프 높이는 보존하면서 정점 도달 시간만 약 31% 단축"을 달성한다.

## 전투 스탯 상한/하한 후보

전투 스탯 상한/하한은 카드별 수치가 아니다. 카드 조합과 무관하게 전투 스탯별 상/하한 값을 전역적으로 제한한다.

아래 숫자는 1차 후보이며, 플레이 화면과 맵 크기 확정 전까지 최종값으로 보지 않는다.

| 필드 | 타입 | 단위 | 초기 후보 | 상태 | 필요한 이유 |
|---|---|---|---:|---|---|
| `resolvedCaps.projectileSizeMin` | float | multiplier | 0.70 | value_candidate | 너무 작은 탄환 방지 |
| `resolvedCaps.projectileSizeMax` | float | multiplier | 5.00 | value_candidate | 화면을 덮는 탄환 방지. 재미 조합을 먼저 허용하고 극단값만 막는다. |
| `resolvedCaps.projectileSpeedMin` | float | multiplier | 0.40 | value_candidate | 너무 느린 탄환 방지 |
| `resolvedCaps.projectileSpeedMax` | float | multiplier | 3.00 | value_candidate | 회피 불가능 탄속 방지. 추후 스나이핑 계열 확장 여지를 남긴다. |
| `resolvedCaps.reloadTimeMin` | float | seconds | 0.50 | value_candidate | 장전 시간이 0에 가까워지는 상황 방지 |
| `resolvedCaps.fireIntervalMin` | float | seconds | 0.05 | value_candidate | 연사 간격이 과하게 짧아지는 상황 방지 |
| `resolvedCaps.hitKnockbackImpulseMax` | float | multiplier | 4.00 | playtest_needed | 경계 돌파 넉백 과대화 방지 |
| `resolvedCaps.explosionRadiusMax` | float | world unit | 30.00 | value_candidate | 시스템 폭주 방지용 하드가드 |
| `resolvedCaps.statusStunMax` | float | seconds | 0.80 | value_candidate | 감전 경직 과대화 방지 |
| `resolvedCaps.statusSlowMax` | float | reduction fraction | 0.30 | value_candidate | 도망 선택지 상실 방지 |

## 전투 스탯 상한/하한 해석

개별 카드에는 `sizeMultiplierMax`, `reloadTimeMin` 같은 필드를 두지 않는다.

전역 cap은 카드 컨셉을 약화시키기 위한 장치가 아니라, 조합 폭주를 막는 마지막 안전장치다.

`projectileSizeMax: 5.00`은 일반적인 조합을 제한하려는 값이 아니라 극단적인 화면 점유만 막기 위한 최후 상한이다. 대형탄, 폭발탄, 오버차지 조합처럼 큰 탄환을 만드는 재미는 우선 허용한다.

`projectileSpeedMin: 0.40`은 기본 탄속 10.0 기준 최종 탄속 4.0까지 허용한다. 느린 중량/폭발 계열의 체감은 살리되, 탄환이 과도하게 오래 남는 상황은 막는다.

`projectileSpeedMax: 3.00`은 현재 MVP 조합보다 추후 스나이핑 계열 확장을 고려한 상한이다. 기본 탄속 10.0 기준 최종 탄속 30.0까지 허용한다.

`fireIntervalMin: 0.05`는 추후 미니건 계열처럼 극단적으로 빠른 발사 컨셉을 열어두기 위한 최후 상한이다. 초당 20발까지 허용하는 값이므로 실제 적용 전에는 성능, 네트워크, 피격 판정 검증이 필요하다.

`statusSlowMax: 0.30`은 이동 속도 감소율 상한이다. 즉 최종 이동 속도는 최소 70%를 보장한다.

`explosionRadiusMax: 30.00`은 밸런스 제한이 아니라 시스템 폭주 방지용 하드가드다. 폭발 크기 조합의 재미는 전역 cap으로 과하게 제한하지 않는다.

현 MVP 카드 데이터에서 폭발탄은 주 폭발 카드로 `explosionRadius: 1.1333334`를 사용한다. 이는 시각 검수에서 실제 피격 범위가 폭발 VFX보다 작게 느껴진 문제를 보정하기 위해 기존 `0.6666667` 대비 1.7배로 키운 gameplay 반경이다. 폭발탄은 스택마다 반경과 splash damage ratio가 누적된다. 중량탄은 단독으로 폭발을 만들지 않으며 `explosionRadius: 0`, `explosionRadiusMultiplier: 1.4`로 폭발탄 보유 시에만 기존 폭발 반경을 곱연산으로 키운다. presentation runtime key는 `projectile_heavy`를 유지해 무거운 탄환 cue와 폭발 cue가 섞이지 않게 한다. 폭발 VFX는 기존 시각 크기를 유지하기 위해 gameplay `ExplosionRadius`에 `ExplosionVfxRadiusScale=0.5882353`을 곱한 radius hint를 사용한다.

`hitKnockbackImpulseMax: 4.00`은 `baseline.directHitKnockbackImpulse` 기준 배율이다. 기준 충격량 자체가 아직 TBD이므로 반드시 플레이테스트가 필요하다.

## 전역 조합 규칙 후보

전역 조합 규칙은 카드별 효과가 겹칠 때 같은 상황을 서로 다르게 해석하지 않도록 하는 계산 정책이다.

| 필드 | 타입 | 초기 후보 | 상태 | 메모 |
|---|---|---|---|---|
| `combinationRules.sameProjectileSameTargetHitPolicy` | enum | `RequiresWorldCollisionReset` | value_candidate | 같은 탄환의 같은 대상 재타격은 벽/바닥 충돌 이후에만 허용 |
| `combinationRules.physicsSafetyCooldown` | float | `0.02` | value_candidate | 같은 물리 프레임 근처의 중복 충돌 이벤트 방지용 구현 안전값 |
| `combinationRules.statusApplicationFromMultiProjectilePolicy` | enum | `ApplyPerProjectileHit` | value_candidate | 벅샷 다중 상태 적용 허용 |
| `combinationRules.statusApplicationFromExplosionPolicy` | enum | `ApplyOncePerProjectilePerTarget` | value_candidate | 폭발 범위에도 상태이상 적용, 같은 탄환-같은 대상은 1회 |
| `combinationRules.knockbackAggregationMode` | enum | `VectorSumWithMagnitudeCap` | value_candidate | 넉백 방향 벡터를 합산하고 최종 크기는 전역 상한으로 제한 |
| `combinationRules.damageAggregationMode` | enum | `SeparateChannelsAdditive` | value_candidate | 서로 다른 직격, 폭발, 틱 피해는 별도 채널로 계산하고 합산하되, 같은 impact의 직격 대상에게 splash 피해를 중복 적용하지 않음 |
| `combinationRules.collisionPriorityPolicy` | enum | `TargetExplodeWallReflectFirst` | value_candidate | 대상 명중은 폭발, 반사가 남은 벽 충돌은 반사 우선 |

## 전역 조합 규칙 해석

벅샷으로 같은 대상에게 여러 발이 맞으면 상태 스택도 여러 번 적용한다.

폭발 범위 안의 대상에게도 상태이상을 적용한다.

같은 탄환이 같은 대상에게 직격과 폭발을 모두 일으켜도 상태이상은 1회만 적용한다. 같은 impact의 직격 대상에게는 splash 피해도 중복 적용하지 않는다.

같은 탄환은 같은 캐릭터 대상에게 직격 상태이상을 중복 적용하지 않는다.

반사탄과 관통탄은 공간 활용 재미를 남긴다. 반사탄은 벽/바닥 반사 후 새 경로에서 다시 피격 판정을 만든다. 관통탄은 벽/지형 충돌을 관통하고, 캐릭터 직격은 관통하지 않고 직격 피해/상태이상/폭발 후 소멸로 해결한다.

다중 탄환, 직격, 폭발 넉백이 겹치면 방향 벡터를 합산하고 최종 크기는 `resolvedCaps.hitKnockbackImpulseMax`로 제한한다.

직격 피해, 폭발 피해, 상태이상 틱 피해는 별도 피해 채널로 계산한 뒤 합산한다. 단, 폭발 splash는 범위 안의 원 발사자에게도 적용하지만, 같은 impact에서 이미 직격 피해를 받은 대상에게는 추가 splash 피해를 주지 않는다.

피해 합산과 상태이상 적용 횟수는 별도 규칙이다. 상태이상 적용 횟수는 `statusApplicationFromMultiProjectilePolicy`, `statusApplicationFromExplosionPolicy`, `sameProjectileSameTargetHitPolicy`를 따른다.

관통탄과 폭발탄이 함께 있으면 벽/지형 관통 횟수가 남은 충돌에서는 탄환이 계속 진행한다. 캐릭터 대상 명중이나 관통 횟수를 모두 소모한 벽/지형 충돌에서는 폭발이 발생할 수 있다.

반사 횟수가 남아 있는 벽 충돌에서는 반사가 폭발보다 우선한다. 반사 횟수를 모두 소모한 뒤 벽에 충돌하면 폭발이 발생할 수 있다.

`physicsSafetyCooldown: 0.02`는 밸런스 쿨다운이 아니라 물리 중복 이벤트 방지용 안전값이다.

## 구현 전달 기준

이 문서는 Unity 프리셋 파일 자체가 아니라, 구현자가 `GameplayProfile.combat`을 만들 때 참고할 설계 기준이다.

구현자가 이 문서에서 가져갈 수 있는 내용은 다음과 같다.

- `GameplayProfile.combat.baseline`에 들어갈 필드명과 1차 후보값
- `GameplayProfile.combat.resolvedCaps`에 들어갈 필드명과 1차 후보값
- `GameplayProfile.combat.combinationRules`에 들어갈 필드명과 1차 후보 정책
- 기본 피해 상향 후 한 탄창 전탄 명중 시 풀피 대상을 처치할 수 있다는 기준
- 기본 탄환은 중력 영향을 받는 곡사탄이라는 기준
- 기본 탄환 수명과 크기 배율은 카드 효과 적용 전 기준값으로 둔다는 기준
- 같은 탄환이 같은 대상을 다시 타격하려면 월드 충돌 이후 재타격 가능 상태가 되어야 한다는 기준

구현자가 직접 검증한 뒤 값을 확정해야 하는 내용은 다음과 같다.

- `directHitKnockbackImpulse`의 실제 단위와 값
- 극단 연사, 극단 탄속, 극단 폭발 반경의 성능/네트워크 영향
- 탄환 수명과 크기 기준값이 실제 맵 크기, 카메라, 네트워크 오브젝트 정리 주기와 맞는지 여부
- 경계 돌파 판정과 넉백 상한의 실제 체감
- 같은 탄환 재타격 권한 리셋과 `physicsSafetyCooldown: 0.02`의 실제 충돌 안정성

이 문서만 보고 확정하면 안 되는 내용은 다음과 같다.

- 공식 baseline 프리셋 최종값
- 플레이테스트 없이 고정된 TTK
- 카드별 mechanics 수치
- 카드 제안 정책

## 작성 규칙

- 수치는 확정값이 아니라 테스트 후보로 적는다.
- 수치 변경 이유는 `85_밸런스_프리셋/30_변경로그/01_플레이테스트_변경로그.md`에 남긴다.
- 기준문서의 TTK 철학, 기본 무기 금지선, 전투 조합 원칙을 깨는 값은 사용하지 않는다.
- 코드 상수로만 남기지 않고 `GameplayProfile` 계열 데이터와 연결한다.
- 네트워크 결과에 영향을 주는 값은 StateAuthority 기준으로 동일하게 읽혀야 한다.

## A303-245 코드 정합 기준값 (동작 동결 스냅샷)

아래 값은 Gun/Bullet의 baseline 9필드를 `GameplayProfile.combat.baseline` 단일 출처로 정렬한 뒤, legacy flat 6필드 제거까지 반영한 현재 기준값이다.

| 필드 | 타입 | 단위 | 기준값 | 상태 | 의도 |
|---|---|---|---:|---|---|
| `baseline.fireInterval` | float | seconds | 0.5 | value_candidate | 발사 간 딜레이를 기존 1.0초 대비 절반으로 축소 |
| `baseline.ammoCapacity` | int | shots | 6 | value_candidate | 기본 탄환 수를 3발에서 6발로 확대 |
| `baseline.reloadTime` | float | seconds | 1.5 | value_candidate | 기존 장전 리듬 유지 |
| `baseline.directHitDamage` | float | hp | 20 | value_candidate | 기본 교전이 늘어진다는 피드백 반영 |
| `baseline.projectileSpeed` | float | world unit/sec | 32.2 | value_candidate | 기존 23 대비 40% 증속 |
| `baseline.projectileGravityScale` | float | multiplier | 2.7 | value_candidate | A303-305 총알 무게감 1차 튜닝 — 가파른 포물선 |
| `baseline.projectileLifetimeSeconds` | float | seconds | 5.0 | value_candidate | 기존 프리팹 수명 유지 |
| `baseline.projectileBaseScale` | float | multiplier | 2.0 | value_candidate | 현재 GameplayProfile 기준 스케일 유지 |
| `baseline.directHitKnockbackImpulse` | float | physics impulse | 0.0 | value_candidate | baseline 적용 후에도 기존 무카드 넉백 체감 동결 |

메모:
- 본 스냅샷 값은 `unity/Assets/_Project/Data/GameplayProfile.asset`의 `combat.baseline`을 기준으로 한다.
- 2026-05-18 기준 현재 튜닝: 장전 시간은 1.5초로 유지하고, 기본 탄환 수 6발, 발사 간격 0.5초, 기본 탄속 32.2로 조정한다.
- A303-245에서 사격·탄환 legacy flat 6필드가 제거되어, Inspector `combat.baseline` 값이 즉시 런타임 기준값이 된다.
- 프리셋 후보값과 차이가 있는 항목은 `A303-245-ballistics-mapping.md`에 충돌로 기록한다.
