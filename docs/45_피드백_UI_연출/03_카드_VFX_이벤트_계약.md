---
last_updated: 2026-05-08
audience: ai-first
---

# 카드 VFX 이벤트 계약

> 상태: candidate
> 범위: MVP16 카드 효과를 presentation이 읽을 수 있는 VFX 사건 계약 후보, VFX 한정 UI 후보, placeholder 구현 기준 후보.
> 중단 조건: 이 문서가 network truth, 카드 선택 StateAuthority 확정, 기존 HUD 정보구조, 캐릭터 최종 아트 구조를 덮어써야 하면 중단하고 각 DRI 확인을 받는다.

이 문서는 카드 효과가 전투에서 발동했을 때 어떤 사건을 presentation 레이어로 내보낼지 정리하는 후보 문서다. 목적은 VFX prefab을 먼저 확정하는 것이 아니라, 전투 truth와 시각 표현 사이의 계약 후보를 먼저 좁히는 것이다.

이 문서의 상태는 plain `candidate`다. 구현 이슈에서 사용하려면 작업 브리핑에 임시 가정, 검증 기준, 영향 DRI 확인을 남긴다.

## 핵심 결론

- 전투 결과는 `NetworkGameplay`과 StateAuthority가 결정한다.
- VFX는 전투 결과를 읽어 재생하는 presentation이며 게임 결과를 바꾸지 않는다.
- 카드 효과 VFX는 `Contracts`의 사건 payload 또는 동등한 읽기 전용 DTO를 통해 presentation이 소비한다.
- 실제 고품질 캐릭터 부착형 VFX는 캐릭터 sprite/material/animation/attach point가 확정된 뒤 후속 이슈로 분리한다.
- MVP 1차 구현은 placeholder 품질이어도 되며, 중복 재생 방지와 전투 복귀 안정성을 우선 검증한다.

## 역할 경계 후보

아래 표는 A303-298 문서 작업에서 정리한 임시 라우팅 후보이며, `50_기술_구조_기준/05_파일경로별_DRI.md`의 accepted DRI 매트릭스를 대체하지 않는다. 실제 Jira assignee, MR reviewer, CODEOWNERS 변경은 별도 결정 또는 MR에서 확정한다.

| 영역 | 주 책임 | 기준 |
|---|---|---|
| 광범위 UI, HUD, 캐릭터, 공통 비주얼 톤 | 우수연 | 화면 정보구조, 캐릭터 최종 sprite/material/animation, 공통 아트 톤, 카드 UI의 시각 완성도 |
| 카드 효과 VFX, VFX 한정 UI, VFX 이벤트 계약 | 김범승 | 카드 효과 사건을 어떤 preset/VFX/toast/indicator로 표시할지, payload와 dedupe 기준, placeholder VFX manager 범위 |
| map 디자인, 전장 구조, 카메라 기준 | 박병준 | 전장 구조, map/greybox/카메라 기준, 씬 락과 전장 문서 정합성 |
| 전투/네트워크 판정 사건 생성 | 최진우 | 발사, 피격, 반사, 폭발, 상태이상, 라운드 전환의 authoritative 결과 생성 |

경계 문장 후보: 기본 HUD와 캐릭터 최종 룩은 우수연 범위, 카드 효과가 발동했다는 사실을 짧게 보여주는 VFX 전용 toast/marker/indicator는 김범승 범위로 라우팅한다. map 디자인과 전장 구조는 박병준 범위로 라우팅한다.

## 계층 원칙

```
NetworkGameplay
  - 전투 truth 생성
  - 카드 적용, 탄환 충돌, 상태이상 적용
  - presentation event emit point 제공

Contracts
  - VFX 사건 payload 또는 읽기 전용 event DTO
  - Unity prefab, ParticleSystem, MMFeedbacks, ScriptableObject 참조 금지

Presentation
  - VFX event listener
  - preset lookup
  - pooled placeholder prefab, particle, MMFeedbacks, toast 재생
```

## 금지선

- VFX manager가 데미지, 넉백, 상태이상 지속 시간, 카드 선택 결과를 계산하지 않는다.
- `NetworkGameplay`이 presentation prefab, particle prefab, toast UI prefab을 직접 참조하지 않는다.
- `GameplayProfile`에 particle prefab, VFX 색상, toast duration, camera shake profile 같은 presentation 값을 섞지 않는다.
- 상태이상 tick마다 과도한 toast나 particle을 새로 생성하지 않는다.
- 캐릭터 최종 prefab 내부 구조를 VFX 작업이 선점하지 않는다.

## 이벤트 타입 후보

MVP 1차 구현에서 모든 타입을 동시에 구현할 필요는 없다. 아래 목록은 계약 후보이며, 실제 구현 이슈에서는 우선순위를 다시 고른다.

| 이벤트 | 의미 | 1차 우선순위 |
|---|---|---|
| `CardEffectApplied` | 카드 효과가 player runtime state에 적용됨 | 필수 |
| `CardEffectToastRequested` | 카드 적용 결과를 짧게 설명하는 toast 요청 | 필수 |
| `PlayerSpawn` | 플레이어 NetworkObject 최초 spawn이 확정됨. `player_spawn` VFX가 소비하는 presentation 사건 | 필수 후보 |
| `PlayerJumpStarted` | StateAuthority가 실제 점프 속도 적용을 확정함. `player_jump_started` 발 위치 VFX가 소비 | 필수 후보 |
| `PlayerLanded` | 공중 상태에서 지상 상태로 전이됨. 계약 event는 유지하되 현재 profile에서는 표시하지 않음 | 후보 |
| `ProjectileSpawnedWithModifier` | 변형된 투사체가 생성됨 | 후보 |
| `ProjectileImpact` | 투사체가 피격/충돌함 | 필수 후보 |
| `SegmentBoundaryAdvanced` | StateAuthority가 구간 돌파/전선 이동을 확정했으며, `segment_advance` world-space sweep VFX와 SFX가 소비하는 presentation 사건 | 필수 후보 |
| `VictoryZoneReached` | 마지막 전투 구간 돌파로 `VictoryRun` 구간에 진입함. `victory_zone_confetti`가 상단 끝을 카메라 상단 끝에 맞추고 카메라 높이를 덮는 크기로 좌우 타일링됨 | 필수 후보 |
| `EmojiRequested` | 로컬 플레이어가 숫자키 1~6으로 머리 위 emoji 표현을 요청함 | 후보 |
| `PlayerInvulnerabilityStarted` | 스폰/리스폰 직후 무적 시간이 시작됨. `player_invulnerable` aura VFX가 무적 시간 동안 소비 | 필수 후보 |
| `ProjectileReflected` | 반사탄이 반사됨 | 후보 |
| `ProjectilePierced` | 관통탄이 벽/지형을 관통함. 캐릭터 직격은 관통하지 않고 직격 피해/상태이상/폭발 후 소멸로 해결 | 후보 |
| `ExplosionTriggered` | 폭발탄이 폭발함 | 필수 후보 |
| `PitFallDeath` | pit 낙하/사망 순간이 확정됨 | 필수 후보 |
| `StatusApplied` | 상태이상이 적용됨 | 필수 후보 |
| `StatusTick` | 상태이상 주기 효과가 발생함 | 1차 제외 |
| `StatusExpired` | 상태이상이 종료됨 | 후보 |
| `ReloadStarted` | 재장전 시작 | 후보 |
| `ReloadCompleted` | 재장전 완료 | 후보 |
| `OverchargeReady` | 오버차지 발사 준비 완료 | 필수 후보 |
| `OverchargeShotFired` | 오버차지 탄 발사 | 후보 |

## payload pseudo-code sample

아래 코드는 compile-ready API가 아니라 필드 책임을 설명하기 위한 pseudo-code sample이다. 실제 타입명, namespace, 필드명은 후속 구현 이슈에서 `Contracts` 구조와 asmdef 경계에 맞춰 확정한다.

`Contracts`에 둘 payload 후보는 Unity/Fusion object를 직접 들고 있지 않는 plain data여야 한다. `UnityEngine.Vector2` 의존을 피해야 하는 경우 `VfxVector2` 같은 plain struct를 사용할 수 있다.

```csharp
public readonly struct VfxEventPayload
{
    public readonly VfxEventType EventType;
    public readonly string CardKey;
    public readonly string EffectKey;
    public readonly int SourceTick;
    public readonly int SourceObjectId;
    public readonly int SourceEventSequence;
    public readonly VfxPredictionState PredictionState;
    public readonly int SourcePlayerSlot;
    public readonly int TargetPlayerSlot;
    public readonly VfxVector2 WorldPosition;
    public readonly VfxVector2 Direction;
    public readonly float RadiusHint;
    public readonly float DurationHint;
    public readonly float MagnitudeHint;
    public readonly VfxAttachPoint AttachPoint;
}
```

| 필드 | 기준 |
|---|---|
| `CardKey` | 카드 catalog/data와 연결되는 식별자. 카드 수치 자체를 복사하지 않음 |
| `EffectKey` | VFX preset lookup용 효과 식별자 |
| `SourceTick` | Fusion tick 또는 동등한 사건 발생 순서 hint |
| `SourceObjectId` | NetworkObject 또는 gameplay object의 안정 식별자. Contracts에는 engine object 참조 대신 id만 둠 |
| `SourceEventSequence` | 같은 object에서 발생한 단발 사건 순번 |
| `PredictionState` | predicted/confirmed/corrected/rejected 구분 |
| `SourcePlayerSlot`, `TargetPlayerSlot` | VFX가 source/target 캐릭터를 따라가야 할 때 쓰는 player slot hint. Transform 참조를 payload에 직접 넣지 않음 |
| `RadiusHint`, `DurationHint`, `MagnitudeHint` | 표현 hint. 판정 수치의 정본이 아님 |
| `AttachPoint` | `World`, `CharacterHead`, `CharacterBody`, `CharacterFeet`, `WeaponMuzzle`, `HudOverlay`, `CardSlot` 같은 부착 위치 계약 |

## rollback/resimulation 중복 재생 방지

Fusion rollback/resimulation 환경에서는 같은 전투 사건이 presentation에 여러 번 관측될 수 있다. VFX는 게임 결과가 아니므로 같은 사건을 반복 재생하면 안 된다.

- 기본 dedupe key는 `(EventType, SourceObjectId, SourceEventSequence)`로 둔다.
- object id가 없으면 `(EventType, SourcePlayerSlot, TargetPlayerSlot, SourceTick, EffectKey)`를 fallback key로 사용한다.
- predicted 이벤트는 즉시 재생할 수 있지만, confirmed 이벤트가 같은 key로 오면 재생하지 않고 위치/강도만 보정한다.
- rejected/corrected 이벤트가 오면 같은 key의 pending VFX를 중단하거나 fade-out한다.
- dedupe cache는 라운드 종료, player despawn, scene transition 시 clear한다.
- `PlayerSpawn/player_spawn`은 `VfxEventType` tail에 추가해 기존 eventType 숫자를 흔들지 않고, `PlayerHealth.Spawned()`에서 StateAuthority가 최초 등장 위치를 기준으로 발행한다.
- `PlayerInvulnerabilityStarted/player_invulnerable`은 스폰/리스폰에서 `StartRespawnInvulnerability()`가 적용된 뒤 StateAuthority가 발행한다. presentation은 `MagicAuraYellow` prefab을 target player body에 follow시키며, duration hint는 `baseline.respawnInvulnerableSeconds`를 사용한다.
- `PlayerJumpStarted/player_jump_started`와 `PlayerLanded/player_landed`는 `PlayerController`/`PlayerMovement`의 StateAuthority 확정 경로에서 발행한다. 둘 다 `CharacterFeet` attach point를 사용하며, 입력 시점이 아니라 실제 점프 속도 적용/grounded 전이를 기준으로 한다. 점프 VFX는 `MagicMuzzleFire` prefab을 아래 방향으로 정렬해 발밑 추진/먼지처럼 보이게 하고, 시각 검수 기준으로 직전 크기의 3/4 scale을 사용한다. 착지 VFX는 현재 profile `ToastOnly` 처리해 표시하지 않는다.
- `PitFallDeath` VFX/SFX는 실제 낙하/사망 순간의 pit world position만 소비한다. 낙하 전 사전경고 VFX/SFX를 만들지 않는다.
- `ProjectileImpact`, `ExplosionTriggered`, `ProjectileShot`, `PlayerDeath`, `PlayerRespawn`, `PitFallDeath` 같은 일회성 burst는 payload의 world position을 기준으로 재생한다.
- `ExplosionTriggered/projectile_explosion`은 실제 splash 판정에 쓰는 `ExplosionRadius`와 presentation radius hint를 분리한다. 시각 검수 기준으로 폭발탄 gameplay 반경은 기존 대비 1.7배로 키우되, 폭발 VFX는 기존 체감 크기를 유지하기 위해 `ExplosionVfxRadiusScale=0.5882353`이 적용된 radius hint를 사용한다.
- `ProjectileImpact/projectile_impact`의 캐릭터 피격 burst는 `WaterExplosion` prefab을 사용한다. prefab 원본 회전을 보존하고 hierarchy particle scale을 사용해 profile scale이 파티클 크기에 반영되게 한다. 지형 충돌은 별도 `projectile_terrain_impact` preset을 유지한다. 탄환 충돌 burst는 시각 검수 기준으로 이전 대비 1/2 scale을 사용한다.
- `ProjectileReflected/projectile_reflect`는 벽/지형 또는 카메라 상/좌/우 edge에서 실제 반사가 발생한 world position을 기준으로 재생하되, VFX 크기는 탄환 크기 modifier가 아니라 owner 캐릭터 collider 기준 25% radius hint를 사용한다. 대형탄과 반사탄을 함께 적용해도 반사 이펙트가 탄환 크기만큼 커지면 안 된다. 하단 edge, 일반탄, 반사 횟수를 다 쓴 탄환은 카메라 edge를 통과하므로 이 이벤트를 발행하지 않는다.
- 상태형/지속형 VFX와 카드 overhead 표현은 `SourcePlayerSlot` 또는 `TargetPlayerSlot`으로 follow target을 찾고, `AttachPoint`와 preset offset을 더해 위치를 갱신한다. target을 찾지 못하면 payload world position fallback을 쓴다.
- `CardEffectApplied`는 선택된 카드의 `CardData.cardIcon` 일러스트를 로드된 카드 데이터에서 찾아 선택자 머리 위에 2초간 띄운다. 전체 카드 프레임이나 카드명 텍스트가 아니라 일러스트 Sprite만 world-space overhead로 보여준다. 시각 검수 기준으로 card illustration은 emoji overhead와 같은 높이와 체감 크기를 사용하며, 표시 중 alpha는 1.0 불투명으로 유지한다. 별도 `CardEffectToastRequested` HUD toast는 기존처럼 독립 이벤트다.
- `EmojiRequested`는 `NetworkInputData.EmojiIndex`를 통해 StateAuthority가 확정한 뒤 `emoji_1`~`emoji_6` effect key로 fan-out한다. 숫자키 1~6과 keypad 1~6을 지원하고, 각 prefab은 source player `CharacterHead`를 따라 2초간 표시한다. 얼굴 앞에 겹치지 않도록 profile `followOffset.y`를 추가하고, 캐릭터 중앙 기준 기존 상대 높이의 2배가 되도록 offset을 둔다. 시각 검수 기준으로 2.5배 scale을 사용한다. 매핑은 1=`EmojiDerp`, 2=`EmojiCool`, 3=`EmojiDeadTired`, 4=`EmojiPuke`, 5=`EmojiSinister`, 6=`EmojiCry`다.
- `ProjectileSpawnedWithModifier`는 projectile runtime 값이 초기화된 뒤 발행하며, 위치 hint는 muzzle 원점이 아니라 생성된 projectile의 world position을 사용한다. 다중 발사에서는 projectile별 `SourceEventSequence`를 구분해 marker/SFX가 한 발로 collapse되지 않게 한다.
- 원격 클라이언트에서 상대 탄환이 총구보다 앞에서 처음 보이는 문제는 projectile physics root나 sprite/trail child를 되감아 보정하지 않는다. 짧은 visual catch-up도 반사탄 궤적을 어긋나게 보일 수 있으므로 탄환 visual child는 항상 authoritative projectile root에 붙여 둔다. 총구보다 조금 앞에서 등장하는 시각 오차는 허용하고, 판정 위치와 VFX/SFX payload의 source position은 authoritative projectile root가 정본이다.
- 상태탄 카드를 여러 종류 획득한 경우 gameplay projectile은 `AppliedStatusMask`로 여러 상태를 함께 보유할 수 있다. 발사 순간 modifier 분류 key는 호환용 최신 상태 key를 사용할 수 있지만, 실제 피격 처리와 `StatusApplied` VFX 발행은 mask에 포함된 상태별로 각각 수행된다.
- `ProjectileSpawnedWithModifier`의 `EffectKey`는 gameplay 결과값을 다시 계산하지 않는 presentation 분류값이다. 중량탄처럼 속도 하락, 크기 증가, 조건부 폭발 반경 곱연산 modifier가 같이 온 경우에도 `projectile_explosion`/`projectile_size_up`보다 `projectile_heavy`를 우선 노출한다. 중량탄 단독은 폭발 사건을 만들지 않고, 폭발탄이 있어 실제 `ExplosionTriggered`가 발생할 때만 폭발 VFX를 낸다. 벅샷처럼 `extraBullets > 0`인 발사는 별도 수치 modifier가 없어도 `projectile_multishot` runtime key를 낸다. 안티그래비티는 `gravityModifier` 기준으로 `projectile_gravity_zero`를 유지한다.
- `ProjectileSpawnedWithModifier` world marker는 `projectile_modifier_trail` 채널로 짧은 색/트레일 cue를 띄운다. 단, `hit_apply_burn`, `hit_apply_poison`, `hit_apply_frost`, `hit_apply_electric_stack`, `projectile_gravity_zero`, `projectile_reflect`, `projectile_pierce`, `projectile_explosion`, `hit_knockback_up`, `projectile_size_up`, `projectile_speed_up`, `projectile_heavy`처럼 전용 시각 표현이 따로 있거나 발사 순간 표현이 혼선을 만드는 key는 발사 순간 legacy placeholder를 만들지 않는다. `projectile_multishot`처럼 별도 전용 VFX가 없는 modifier만 짧은 관측 trail을 유지한다. 판정 크기, 속도, 상태 적용 여부는 gameplay state가 정본이다.
- `ProjectileSpawnedWithModifier` SFX는 전용 runtime SFX handler가 modifier 관측음만 담당한다. 카드 SFX profile의 일반 playback과 중복 재생하지 않으며, modifier 관측음은 projectile world position 기준으로 로그와 재생 위치를 맞춘다. heavy/size/multishot/gravity/status는 서로 다른 cue channel을 써서 카드 차이를 들을 수 있게 하고, 실제 청감은 2D-biased mix로 유지해 카메라/리스너 거리 때문에 소리가 빠지지 않게 한다.
- `ProjectileShot/projectile_shot` SFX는 `Gun.Rpc_PublishShotSfx`가 muzzle 기준으로 발행하고 `CardSfxPresentationManager`가 `SfxFeedbackProfile` preset으로 재생한다. 이 발사음은 `ProjectileSpawnedWithModifier` runtime SFX handler의 처리 대상이 아니다.
- `ProjectileImpact/projectile_impact`, `ProjectileReflected/projectile_reflect`, `ProjectilePierced/projectile_pierce`, `OverchargeReady/reload_overcharge` SFX는 dedicated runtime SFX handler가 단독 처리한다. `CardSfxPresentationManager`는 해당 event/effect 조합을 profile playback에서 제외해 같은 사건이 profile route와 runtime route 양쪽에서 동시에 재생되지 않게 한다.
- `OverchargeReady/reload_overcharge` VFX는 ready 상태형 `MagicChargeGreen` prefab을 source player의 weapon muzzle 기준으로 따라가게 만들고, `OverchargeShotFired/reload_overcharge`가 오면 제거한다. 원본 크기가 커서 profile에서 작은 scale을 사용하며, 총기 sprite보다 앞에 읽히도록 `overrideSortingOrder: 1`, `sortingOrder: 7`을 사용한다. 별도 상태 해제 event가 추가되면 같은 key로 제거해야 한다.
- `OverchargeShotFired/reload_overcharge` 발사 burst는 `EnergyNovaMuzzleGreen` prefab을 유지하되, 시각 검수 기준으로 직전 1/3 scale 튜닝값에서 2배 키운 값을 사용한다.
- `PlayerSpawn/player_spawn`과 `PlayerRespawn/player_respawn`은 동일한 `MagicBuffYellow` prefab을 사용한다. prefab 원본 회전을 보존하고 hierarchy particle scale을 사용해 root x/y/z scale이 실제 파티클 크기에 적용되게 한다.
- `VictoryZoneReached/victory_zone_confetti`는 `RoomManager`가 마지막 구간 진입으로 `VictoryRun`을 시작한 뒤 한 번 발행한다. payload 위치는 빅토리존 카메라 중앙, radius hint는 해당 room width, magnitude hint는 카메라 전체 높이이며, presentation은 `ConfettiShowerRainbow` prefab을 카메라 높이를 채울 만큼 균일 scale한 뒤 prefab 상단 끝이 카메라 상단 끝에 맞도록 배치하고 X축으로만 빈틈없이 타일링한다. 빅토리런 중에는 duration으로 제거하지 않고 `ROOM_RESET`, `GAME_START`, disconnect 같은 방/세션 전환 신호가 올 때 제거한다.
- Prefab의 로컬 성장축이 payload direction과 다르면 profile에서 축 정렬, rotation offset, 또는 prefab 원본 rotation 보존으로 보정한다. 예: `EnergyNovaMuzzleGreen`은 로컬 forward(Z) 성장축이 화면 깊이 방향으로 나와 `OverchargeShotFired/reload_overcharge`에서 local forward를 탄환 direction의 XY 평면으로 눕힌다. `hit_apply_burn`은 local forward를 상태 payload direction의 XY 평면으로 눕혀 z축 방향 grow가 화면 정면으로 읽히지 않게 한다. `hit_apply_frost`의 `Eff_Water_Uncommon_Ice breathskill_02`는 캐릭터 우측 방향으로 grow되지 않도록 prefab 원본 rotation을 보존하고 `prefabScale: 0.1833335`로 조정한다. 이 prefab은 ParticleSystem이 부모 스케일을 무시할 수 있으므로 `useHierarchyParticleScale`을 켜 root x/y/z scale이 실제 파티클 크기에 적용되게 한다.
- 외부 VFX asset은 `_CandidateImports` 전체를 제품 branch에 반입하지 않는다. 실제 포함 대상은 선택된 root prefab과 Unity가 요구하는 transitive dependency 및 `.meta`로 제한한다.
- `StatusTick`은 tick마다 새 VFX를 만들지 않고 상태 시작/종료 또는 낮은 빈도의 UI 갱신으로 제한한다. `StatusApplied` prefab은 캐릭터에 붙는 표현이므로 기본적으로 캐릭터 sprite 뒤에 묻히지 않게 foreground sorting을 강제한다. `hit_apply_burn`, `hit_apply_poison`, `hit_apply_frost`, `hit_apply_electric_stack`은 상태 지속시간 동안 유지되는 상태형 VFX로 취급한다. 재생 중간에 `Clear/Play`로 다시 시작하지 않고, 상태 종료 타이밍에 자연스럽게 끝나도록 특정 시점까지만 ParticleSystem loop를 유지한 뒤 loop를 끈다. loop cutoff는 burn/poison/electric 2.0초, frost 1.0초다. 같은 대상에게 같은 상태가 다시 적용되면 기존 VFX를 제거하고 새 지속시간/스택 크기로 다시 붙여 refresh한다. `hit_apply_burn`과 `hit_apply_poison`은 몸통 뒤에 묻히지 않도록 target player head 쪽으로 `followOffset.y: 0.45`를 주고, 시각 검수 기준으로 기존 대비 2배 scale을 사용한다. 단, `hit_apply_burn`은 z축 grow가 화면 정면에서 하얗게 읽히는 문제가 있어 `followZOffset: 0.05`, `sortingOrder: 3`으로 캐릭터 후경에 살짝 배치한다. burn prefab은 ParticleSystem이 root scale 변화를 놓치지 않도록 `useHierarchyParticleScale`을 켜서 상태 stack scale이 1/2/3스택에서 실제 particle 크기에 반영되게 한다.
- 상태 VFX의 스택 배율은 카드를 몇 장 먹었는지가 아니라 피격 대상의 `StatusEffectInstance.Stacks`를 기준으로 한다. 카드 runtime은 상태탄 카드 획득 횟수를 상태별 gameplay magnitude 슬롯으로 전달할 수 있지만, VFX `MagnitudeHint`에는 카드 장수가 아니라 `PlayerStatusController`가 같은 상태 재적중 후 계산한 실제 runtime stack만 발행한다. 한 발에 여러 상태가 함께 실린 경우에도 상태별 `ApplyByEnum`이 따로 실행되므로 `hit_apply_poison`, `hit_apply_burn`, `hit_apply_frost`, `hit_apply_electric_stack` VFX가 각각 발행될 수 있다. MVP 4종 상태 SO는 모두 `maxStack=3`으로 두어 재적중 시 VFX scale이 갱신된다. `StatusApplied` VFX 중 `hit_apply_poison`, `hit_apply_burn`, `hit_apply_frost`, `hit_apply_electric_stack`은 `MagnitudeHint`를 스택 수로 해석해 1스택 1.0배, 2스택 1.6배, 3스택 2.2배처럼 스택당 0.6배씩 prefab root scale을 키운다. 단, 감전은 3스택 도달 frame에서 3스택 VFX hint를 발행한 뒤 gameplay stack을 0으로 소비하고 별도 stun timer로 경직을 유지한다. 이 배율은 표현 전용이며 데미지/감속/경직 판정은 status runtime state가 정본이다.
- `SegmentBoundaryAdvanced`의 `segment_sweep` VFX와 SFX one-shot은 같은 `(EventType, SourceObjectId, SourceEventSequence)` 기준으로 중복 재생을 막는다. VFX는 payload의 world position과 `VfxFeedbackProfile.asset`의 sweep duration/radius 값을 표현값으로 쓰고, SFX는 `SegmentAdvanceSfxPresentationManager`가 event/effect key와 world position을 받아 별도 one-shot으로 처리한다. fallback key는 `EffectKey=segment_advance`를 포함해 같은 구간 돌파 사건을 클라이언트 resimulation마다 새 소리나 sweep으로 만들지 않게 한다.

## VFX 한정 UI 범위

| 포함 | 제외 |
|---|---|
| 카드 적용 직후 짧은 toast | 체력, 탄약, 라운드, 구간 진행도 같은 기본 HUD |
| 상태이상 적용/종료 아이콘 또는 짧은 marker | 상시 버프 목록처럼 HUD 정보구조에 편입되는 UI |
| 오버차지 준비/소비 indicator | 메뉴, 로비, 설정 UI |
| 폭발 범위 또는 착탄 순간의 짧은 world-space indicator | 캐릭터 최종 sprite/animation/material |
| 반사/관통/분열 등 projectile modifier trail/marker | 전투 결과를 결정하는 상태 저장 |
| `segment_advance` 구간 돌파 순간의 `segment_sweep` world-space marker와 local SFX one-shot | 구간 진행도 HUD 또는 라운드 결과 truth |
| `victory_zone_confetti` 빅토리존 도달 축하 confetti | 승패 truth 또는 최종 UI 판정 |

VFX 한정 UI는 짧게 나타났다 사라지는 feedback이다. 플레이어가 계속 읽어야 하는 정보는 `02_UI_HUD_정보구조.md`의 HUD 범위로 넘긴다.

## MVP16 카드별 VFX 분류

| cardKey | 1차 VFX 타입 | UI 필요 | 캐릭터 디자인 의존 |
|---|---|---|---|
| `projectile_size_up` | projectile scale/trail 강조 | 낮음 | 낮음 |
| `projectile_speed_up` | 빠른 trail/속도선 | 낮음 | 낮음 |
| `hit_knockback_up` | hit impact 방향/밀림 강조 | 낮음 | 중간 |
| `reload_time_down` | reload completion flash | 중간 | 낮음 |
| `ammo_capacity_up` | 카드 적용 toast 중심 | 중간 | 낮음 |
| `projectile_heavy` | 무거운 trail/착탄 dust + 조건부 폭발 반경 multiplier hint | 낮음 | 낮음 |
| `hit_apply_poison` | poison hit + 상태 icon | 높음 | 높음 |
| `hit_apply_burn` | burn hit + 상태 icon | 높음 | 높음 |
| `hit_apply_frost` | frost hit + 상태 icon | 높음 | 높음 |
| `hit_apply_electric_stack` | electric stack + stun flash | 높음 | 높음 |
| `reload_overcharge` | ready glow + fired burst | 높음 | 중간 |
| `projectile_reflect` | reflect spark/trail color change, owner 캐릭터 기준 25% 크기 | 낮음 | 낮음 |
| `projectile_explosion` | explosion impact + VFX용 보정 radius hint | 중간 | 낮음 |
| `projectile_pierce` | pierce streak/afterimage | 낮음 | 낮음 |
| `projectile_multishot` | multi-lane muzzle burst | 낮음 | 낮음 |
| `projectile_gravity_zero` | straight long beam-like trail | 낮음 | 낮음 |

## placeholder 구현 순서

### 1차: 계약과 수신 경로

- `CardEffectApplied`
- `CardEffectToastRequested`
- `EmojiRequested`
- `PlayerSpawn`
- `PlayerInvulnerabilityStarted`
- `ProjectileImpact`
- `ExplosionTriggered`
- `StatusApplied`
- `OverchargeReady`

성공 기준은 시각 품질이 아니라 전투 코드가 presentation prefab을 모르고도 event가 VFX manager까지 도착하는 것이다.

### 2차: 데모 가독성 placeholder

- 폭발탄: 착탄 폭발 + 반경 hint.
- 상태이상 4종: 상태 icon + 색상/particle placeholder.
- 오버차지: 준비/소비 표시.

### 3차: 캐릭터 디자인 이후 polish

- `CharacterHead`, `CharacterBody`, `CharacterFeet`, `WeaponMuzzle` attach point에 붙는 상태이상 VFX.
- palette swap, outline, hit flash, overlay.
- 캐릭터 animation과 sync되는 burn/frost/electric/poison 표현.

## preset 배치 기준

후보 경로 예시:

```text
Assets/_Project/Data/Presentation/VfxFeedbackProfile.asset
Assets/_Project/Data/Presentation/CardVfxPreset.asset
Assets/_Project/Data/Presentation/StatusVfxPreset.asset
```

위 경로는 실제 asset 생성 또는 파일 이동을 의미하지 않는 예시다. preset은 presentation 설정이며 전투 수치의 정본이 아니다. lookup key 후보는 `CardKey`, `EffectKey`, `EventType` 조합을 기본으로 검토한다.
profile lookup이 실패해 fallback preset을 쓰더라도 `segment_advance`처럼 시연 핵심 피드백은 profile의 대표 색상 계열과 어긋나지 않게 맞춘다.

## 후속 이슈 분리 기준

A303-298은 계약/docs 정리 범위다. 실제 구현은 후속 이슈로 나눈다.

| 후속 범위 | 권장 DoD |
|---|---|
| VFX manager + preset lookup + dedupe cache | event payload 수신, dedupe key 적용, placeholder 1~2개 smoke |
| MVP 주요 placeholder asset 연결 | 폭발탄, 상태이상, 오버차지 중 demo 가독성 높은 효과 연결 |
| 상태이상 UI/indicator 확장 | `StatusApplied`/`StatusExpired` 중심 표현, `StatusTick` 반복 toast 금지 |
| 캐릭터 부착형 polish | 캐릭터 attach point 확정 후 body/head/feet 기반 VFX 적용 |

## 검증 질문

| no | 질문 |
|---|---|
| 1 | VFX가 전투 결과를 바꾸지 않는가? |
| 2 | event payload가 card/effect id, 위치, 강도 hint만 담고 판정 수치를 소유하지 않는가? |
| 3 | rollback/resimulation 상황에서 같은 사건이 반복 재생되지 않는가? |
| 4 | VFX 한정 UI가 기본 HUD 정보구조를 침범하지 않는가? |
| 5 | 캐릭터 최종 prefab 구조를 가정하지 않고 attach point 계약과 fallback으로 버티는가? |
| 6 | placeholder VFX 실패가 spawn, 발사, 피격, 라운드 전환, 카드 선택 후 전투 복귀를 막지 않는가? |

## 관련 문서

- [01_타격감_피드백_기준.md](01_타격감_피드백_기준.md)
- [02_UI_HUD_정보구조.md](02_UI_HUD_정보구조.md)
- [40_카드_무기_설계/02_카드_효과_분류_기준.md](../40_카드_무기_설계/02_카드_효과_분류_기준.md)
- [50_기술_구조_기준/02_네트워크_truth_표.md](../50_기술_구조_기준/02_네트워크_truth_표.md)
- [50_기술_구조_기준/05_파일경로별_DRI.md](../50_기술_구조_기준/05_파일경로별_DRI.md)
